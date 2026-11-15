using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Generates GlazeWM's workspace list and app assignment rules from workspaces.json, the single
// workspace definition file. Used by the bar's rules button (over the bridge) and by the watcher
// below whenever the file is saved. The bar's pins and the Alt indicator read the file directly.
static class WorkspaceConfig {
 const string RulesBegin="  # BEGIN LIAM APP ASSIGNMENTS",RulesEnd="  # END LIAM APP ASSIGNMENTS";
 static string ConfigPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@".glzr\glazewm\config.yaml");}}
 static readonly object gate=new object();

 /// <summary>Bridge entry point: "status", "on", "off" or "sync". Replies {"workspaceRules":{...}}.</summary>
 public static string Receive(string action){
  try{return Reply(new{action,enabled=Apply(action)});}
  catch(Exception ex){return Reply(new{action,error=ex.Message,enabled=Enabled(SafeRead())});}
 }
 static string Reply(object state){return new JavaScriptSerializer().Serialize(new{workspaceRules=state});}
 static string SafeRead(){try{return File.ReadAllText(ConfigPath);}catch{return "";}}
 internal static bool RoutingEnabled(){return Enabled(SafeRead());}
 static bool Enabled(string config){return config.Contains(RulesBegin);}

 /// <summary>Applies workspaces.json to GlazeWM. Returns whether app rules are on afterwards.</summary>
 public static bool Apply(string action){
  if(action!="status"&&action!="on"&&action!="off"&&action!="sync")throw new ArgumentException("Unknown action");
  lock(gate)using(var mutex=new Mutex(false,"Local\\LiamNordWorkspaceRules")){
   bool held=false;
   try{
    try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}
    if(!held)throw new Exception("Workspace rules are being updated. Try again.");
    string original=File.ReadAllText(ConfigPath);
    bool enabled=Enabled(original);
    if(WorkspaceSync.Stopping&&action!="status")return enabled;
    if(action=="status")return enabled;
    // Lock-in marks the rule lines it rewrote; regenerating them now would break its restore.
    if(original.Contains("# nord-focus-owned-"))throw new LockInActive();
    bool wantRules=action=="sync"?enabled:action=="on";
    string next=Generate(original,Load(),wantRules);
    if(next!=original&&!WorkspaceSync.Stopping){
     File.WriteAllText(ConfigPath+".before-app-rules.bak",original);
     var utf8=new UTF8Encoding(false);
     File.WriteAllText(ConfigPath,next,utf8);
     try{GlazeCommand.ReloadConfig();}
     catch{File.WriteAllText(ConfigPath,original,utf8);try{GlazeCommand.Command("wm-reload-config");}catch{}throw new Exception("GlazeWM rejected the generated config; the previous one was restored.");}
    }
    return wantRules;
   }finally{if(held)mutex.ReleaseMutex();}
  }
 }
 public class LockInActive:Exception{public LockInActive():base("Lock-in is active. Workspace changes apply when it ends."){}}

 /// <summary>Reads and validates workspaces.json. Throws with a readable message on any problem.</summary>
 public static WorkspaceDefinition[] Load(){
  WorkspaceFile file;
  try{file=new JavaScriptSerializer().Deserialize<WorkspaceFile>(File.ReadAllText(AppCatalog.WorkspacesPath));}
  catch(Exception ex){throw new Exception("workspaces.json is not valid JSON: "+ex.Message);}
  var list=(file==null?null:file.workspaces)??new WorkspaceDefinition[0];
  if(list.Length==0)throw new Exception("workspaces.json has no workspaces.");
  var names=new HashSet<string>();var ids=new HashSet<string>();
  foreach(var ws in list){
   if(ws==null||!Regex.IsMatch(ws.name??"",@"^[A-Za-z0-9_-]+$"))throw new Exception("Invalid workspace name '"+(ws==null?"":ws.name)+"'.");
   if(!names.Add(ws.name))throw new Exception("Workspace '"+ws.name+"' is listed twice.");
   if(ws.monitor.HasValue&&ws.monitor.Value<1)throw new Exception("Workspace '"+ws.name+"': monitor starts at 1.");
   foreach(var app in ws.apps??new WorkspaceApp[0]){
    if(app==null||String.IsNullOrEmpty(app.id)||!ids.Add(app.id))throw new Exception("Workspace '"+ws.name+"': app ids must be present and unique.");
    if(String.IsNullOrEmpty(app.processPattern))throw new Exception("App '"+app.id+"' needs a processPattern.");
    foreach(var pattern in new[]{app.processPattern,app.titlePattern,app.catalogPattern}.Where(p=>!String.IsNullOrEmpty(p))){
     if(pattern.IndexOfAny(new[]{'\r','\n'})>=0)throw new Exception("App '"+app.id+"': patterns must be one line.");
     try{new Regex(pattern);}catch{throw new Exception("App '"+app.id+"': invalid pattern '"+pattern+"'.");}
    }
   }
  }
  return list;
 }

 static string Quote(string text){return "'"+(text??"").Replace("'","''")+"'";}

 /// <summary>Returns the config with its workspaces section and app rules block regenerated.</summary>
 public static string Generate(string config,WorkspaceDefinition[] workspaces,bool rules){
  string nl=config.Contains("\r\n")?"\r\n":"\n";
  // Workspaces section: owned entirely by workspaces.json, in its order.
  var ws=new List<string>{"workspaces:","  # Generated from nord-rice\\workspaces.json. Edit that file instead; changes here are replaced."};
  foreach(var w in workspaces){
   ws.Add("  - name: "+Quote(w.name));
   if(w.monitor.HasValue)ws.Add("    bind_to_monitor: "+(w.monitor.Value-1));
   bool hasApps=w.apps!=null&&w.apps.Any(a=>a!=null);
   if(w.keepAlive??hasApps)ws.Add("    keep_alive: true");
  }
  var section=new Regex(@"^workspaces:[ \t]*\r?\n(?:(?:[ \t]+.*)?\r?\n)*",RegexOptions.Multiline);
  if(!section.IsMatch(config))throw new Exception("No workspaces section in GlazeWM's config.");
  string generated=String.Join(nl,ws)+nl+nl;
  config=section.Replace(config,m=>generated,1);

  // App assignment rules, regenerated as one marked block at the top of window_rules.
  config=new Regex("^"+Regex.Escape(RulesBegin)+@"\r?\n.*?^"+Regex.Escape(RulesEnd)+@"\r?\n",RegexOptions.Multiline|RegexOptions.Singleline).Replace(config,"");
  if(rules){
   var lines=new List<string>{RulesBegin,"  # Startup-only app routing is handled by NordControlCenter.","  # Existing instances are never moved; no unconditional native assignment rules."};
   lines.Add(RulesEnd);
   var anchor=new Regex(@"^window_rules:[ \t]*\r?\n",RegexOptions.Multiline);
   if(!anchor.IsMatch(config))throw new Exception("No window_rules section in GlazeWM's config.");
   string block="window_rules:"+nl+String.Join(nl,lines)+nl;
   config=anchor.Replace(config,m=>block,1);
  }
  return config;
 }
}

// Applies workspaces.json whenever it is saved, and once at startup in case it changed while the
// control center wasn't running. Errors go to workspace-sync.log; GlazeWM keeps its last config.
static class WorkspaceSync {
 internal static volatile bool Stopping;static FileSystemWatcher watcher;
 static Timer debounce;
 public static void Start(){
  if(watcher!=null)return;Stopping=false;debounce=new Timer(delegate{Run();},null,1500,Timeout.Infinite);
  watcher=new FileSystemWatcher(AppCatalog.Folder,"workspaces.json"){NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|NotifyFilters.Size};
  watcher.Changed+=delegate{Schedule(700);};watcher.Created+=delegate{Schedule(700);};watcher.Renamed+=delegate{Schedule(700);};
  watcher.EnableRaisingEvents=true;
 }
 // Editors save in bursts (truncate, write, rename); wait for the file to settle.
 public static void Stop(){Stopping=true;var file=watcher;watcher=null;if(file!=null){file.EnableRaisingEvents=false;file.Dispose();}var pending=Interlocked.Exchange(ref debounce,null);if(pending!=null)pending.Dispose();}
 static void Schedule(int ms){var pending=debounce;if(!Stopping&&pending!=null)try{pending.Change(ms,Timeout.Infinite);}catch(ObjectDisposedException){}}
 static void Run(){
  try{if(!Stopping&&File.Exists(AppCatalog.WorkspacesPath))WorkspaceConfig.Apply("sync");}
  catch(WorkspaceConfig.LockInActive){Schedule(30000);} // Retry until lock-in ends.
  catch(Exception ex){Log(ex.Message);}
 }
 static void Log(string message){
  try{
   var path=Path.Combine(AppCatalog.Folder,"workspace-sync.log");
   if(File.Exists(path)&&new FileInfo(path).Length>65536)File.Delete(path);
   File.AppendAllText(path,DateTime.Now.ToString("s")+" "+message+Environment.NewLine);
  }catch{}
 }
}
