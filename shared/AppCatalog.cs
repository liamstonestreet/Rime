using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Media;

class AppEntry { public string Name {get;set;} public string AppID {get;set;} public string Executable {get;set;} public string WindowAppId {get;set;} public string Key {get{return String.IsNullOrEmpty(AppID)?Executable:AppID;}} public ImageSource Icon {get{return AppIcons.GetExecutable(AppShortcuts.LinkIcon(Executable))??AppIcons.Get(AppID)??AppIcons.GetExecutable(Executable);}} }
class WorkspaceApp {
 public string id {get;set;} public string workspace {get;set;} public string catalogPattern {get;set;} public string processPattern {get;set;} public string titlePattern {get;set;}
 public string windowAppId {get;set;} public bool? pinned {get;set;} public string appId {get;set;} public string executable {get;set;} public string name {get;set;}
 public bool Matches(string process,string title){return Match(process,processPattern)&& (String.IsNullOrEmpty(titlePattern)||Match(title,titlePattern));}
 public static bool UnsafeDuplicate(WorkspaceApp app,IEnumerable<WorkspaceApp> all){return app!=null&&(app.id??"").StartsWith("task-",StringComparison.OrdinalIgnoreCase)&&String.IsNullOrEmpty(app.titlePattern)&&!String.IsNullOrEmpty(app.catalogPattern)&&all.Any(other=>other!=app&&!String.IsNullOrEmpty(other.titlePattern)&&String.Equals(other.catalogPattern,app.catalogPattern,StringComparison.OrdinalIgnoreCase)&&String.Equals(other.processPattern,app.processPattern,StringComparison.OrdinalIgnoreCase));}
 public static bool Match(string text,string pattern){try{return !String.IsNullOrEmpty(pattern)&&Regex.IsMatch(text??"",pattern,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(30));}catch{return false;}}
}
// workspaces.json: the single source of truth for workspace names, labels, monitors and default apps.
class WorkspaceDefinition {
 public string name {get;set;} public string label {get;set;} public int? monitor {get;set;} public bool? keepAlive {get;set;}
 public WorkspaceApp[] apps {get;set;}
}
class WorkspaceFile { public WorkspaceDefinition[] workspaces {get;set;} }
static class AppCatalog {
 public static readonly string Folder=AppDomain.CurrentDomain.BaseDirectory;
 public static string WorkspacesPath {get{return Path.Combine(Folder,"workspaces.json");}}
 public static AppEntry[] Load(){try{return AppShortcuts.Merge(new JavaScriptSerializer().Deserialize<AppEntry[]>(File.ReadAllText(Path.Combine(Folder,"spotlight-apps.json")))??new AppEntry[0]);}catch{return AppShortcuts.Merge(new AppEntry[0]);}}
 static readonly object ruleGate=new object();static WorkspaceApp[] rules=new WorkspaceApp[0];static DateTime rulesStamp;
 /// <summary>Every workspace's default apps, flattened and tagged with their workspace. Reloads when
 /// workspaces.json changes and keeps the last good copy if the file is mid-edit or invalid.</summary>
 public static WorkspaceApp[] Rules(){lock(ruleGate){var path=WorkspacesPath;var stamp=File.GetLastWriteTimeUtc(path);if(stamp==rulesStamp)return rules;try{
  var file=new JavaScriptSerializer().Deserialize<WorkspaceFile>(File.ReadAllText(path));
  var flat=new List<WorkspaceApp>();
  foreach(var ws in (file==null?null:file.workspaces)??new WorkspaceDefinition[0])
   if(ws!=null&&!String.IsNullOrEmpty(ws.name))foreach(var app in ws.apps??new WorkspaceApp[0])if(app!=null){app.workspace=ws.name;flat.Add(app);}
  rules=flat.Where(app=>!WorkspaceApp.UnsafeDuplicate(app,flat)).ToArray();rulesStamp=stamp;
 }catch{}return rules;}}
 public static void ValidateLaunch(AppEntry app){
  if(app==null || (!String.IsNullOrEmpty(app.AppID) ? app.AppID.IndexOfAny(new[]{'"','\r','\n'})>=0 : String.IsNullOrEmpty(app.Executable)||!Path.IsPathRooted(app.Executable)||!File.Exists(app.Executable)))throw new Exception("Invalid app identifier");
  var focus=FocusState.Read();
  if(focus.Active && (focus.settings.blockedProcesses.Any(n=>String.Equals(n,app.Name,StringComparison.OrdinalIgnoreCase)||(app.AppID??"").StartsWith(n+":",StringComparison.OrdinalIgnoreCase)||String.Equals(n,Path.GetFileNameWithoutExtension(app.Executable??""),StringComparison.OrdinalIgnoreCase)) || Rules().Any(r=>WorkspaceApp.Match(app.Name,r.catalogPattern)&&focus.settings.disabledWorkspaces.Contains(r.workspace))))throw new Exception("Unavailable while locked in");
 }
 public static void Launch(AppEntry app){
  ValidateLaunch(app);
  if(String.IsNullOrEmpty(app.AppID))Process.Start(new ProcessStartInfo(app.Executable){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(app.Executable)});
  else Process.Start(new ProcessStartInfo("explorer.exe","\"shell:AppsFolder\\"+app.AppID+"\""){UseShellExecute=true});
  AppUsage.Record(app.Key);
 }
}
// Successful launches and foreground activations share one bounded, local history.
// Queries use memory only; writes are debounced and merged atomically between helpers.
static class AppUsage {
 static readonly object gate=new object();
 static Dictionary<string,long> entries=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
 static readonly string path=Path.Combine(AppCatalog.Folder,"spotlight-history.json");
 static readonly Timer writer=new Timer(_=>Flush(),null,Timeout.Infinite,Timeout.Infinite);
 static bool loaded,stopping;
 static Dictionary<string,long> Read(){try{return new JavaScriptSerializer().Deserialize<Dictionary<string,long>>(File.ReadAllText(path))??new Dictionary<string,long>();}catch{return new Dictionary<string,long>();}}
 public static void Refresh(){var saved=Read();lock(gate){foreach(var pair in saved){long old;if(!entries.TryGetValue(pair.Key,out old)||pair.Value>old)entries[pair.Key]=pair.Value;}loaded=true;}}
 public static long Last(string id){lock(gate){long value;return entries.TryGetValue(id,out value)?value:0;}}
 public static void Record(string id){if(String.IsNullOrEmpty(id))return;if(!loaded)Refresh();lock(gate){if(stopping)return;entries[id]=DateTime.UtcNow.Ticks;writer.Change(750,Timeout.Infinite);}}
 static void Flush(){try{using(var mutex=new Mutex(false,"Local\\NordSpotlightHistory")){bool acquired=false;try{try{acquired=mutex.WaitOne(2000);}catch(AbandonedMutexException){acquired=true;}if(!acquired)return;
  Refresh();Dictionary<string,long> snapshot;
  lock(gate){entries=entries.OrderByDescending(p=>p.Value).Take(2048).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);snapshot=new Dictionary<string,long>(entries,StringComparer.OrdinalIgnoreCase);}
  // Ranking reads this dictionary on every keystroke: never hold its lock across disk IO.
  var temp=path+"."+Process.GetCurrentProcess().Id+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(snapshot));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
 }finally{if(acquired)mutex.ReleaseMutex();}}}catch{/* History must never prevent launching or typing. */}}
 public static void Stop(){lock(gate){if(stopping)return;stopping=true;writer.Change(Timeout.Infinite,Timeout.Infinite);writer.Dispose();}Flush();}
 internal static void Check(){var id="test-"+Guid.NewGuid();Record(id);if(Last(id)==0)throw new Exception("Recent app missing");}
}
