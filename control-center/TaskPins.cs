using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;

// Pin edits share the app-rule source, lock and generator; no separate launcher configuration.
static class TaskPins {
 static readonly object orderGate=new object();
 static string OrderPath {get{return Path.Combine(AppCatalog.Folder,"taskbar-order.json");}}
 public static Dictionary<string,string[]> Orders(){return ReadOrders(OrderPath);}
 internal static Dictionary<string,string[]> ReadOrders(string path){lock(orderGate){try{return new JavaScriptSerializer().Deserialize<Dictionary<string,string[]>>(File.ReadAllText(path))??new Dictionary<string,string[]>();}catch{return new Dictionary<string,string[]>();}}}
 internal static void SaveOrder(string path,string workspace,string[] keys){lock(orderGate){var orders=ReadOrders(path);orders[workspace]=keys.Distinct().ToArray();var temp=path+".tmp";try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(orders));if(File.Exists(path))File.Replace(temp,path,path+".previous");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}}
 public static string Order(string payload){try{var request=new JavaScriptSerializer().Deserialize<OrderRequest>(payload);
  if(request==null||!Regex.IsMatch(request.workspace??"","^[A-Za-z0-9_-]+$")||request.keys==null||request.keys.Length>256||request.keys.Any(k=>String.IsNullOrEmpty(k)||k.Length>512))throw new Exception("Invalid taskbar order");
  if(!new JavaScriptSerializer().Deserialize<WorkspaceFile>(File.ReadAllText(AppCatalog.WorkspacesPath)).workspaces.Any(w=>w.name==request.workspace))throw new Exception("Unknown workspace");
  SaveOrder(OrderPath,request.workspace,request.keys);
  WorkspacePins.Refresh();return "{\"ok\":true}";
 }catch(Exception ex){return new JavaScriptSerializer().Serialize(new{taskOrderError=ex.Message});}}
 class OrderRequest {public string workspace {get;set;} public string[] keys {get;set;}}
 internal class Request {public string id {get;set;} public long handle {get;set;} public string workspace {get;set;} public string pinId {get;set;} public int x {get;set;} public int y {get;set;}}
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process,ref uint length,System.Text.StringBuilder value);
 [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr process);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,System.Text.StringBuilder path,ref uint length);
 public static string Receive(string payload){try{var request=new JavaScriptSerializer().Deserialize<Request>(payload);if(request==null||!Regex.IsMatch(request.workspace??"","^[A-Za-z0-9_-]+$"))return "{\"ok\":false}";Application.Current.Dispatcher.BeginInvoke(new Action(()=>Menu(request)));return "{\"ok\":true}";}catch{return "{\"ok\":false}";}}
 static async void Menu(Request request){try{
  Dictionary<string,object> window=null;AppEntry app=null;WorkspaceApp rule=null;
  await Task.Run(()=>{
   if(!String.IsNullOrEmpty(request.id))window=TaskActions.Validate(request.id,request.handle);
   var identity=window==null?null:AppShortcuts.WindowApp(new IntPtr(request.handle));
   rule=AppCatalog.Rules().FirstOrDefault(r=>r.id==request.pinId);
   if(rule==null&&window!=null)rule=AppCatalog.Rules().FirstOrDefault(r=>(identity==null||WorkspaceApp.Match(identity.Name,r.catalogPattern))&&r.Matches(Value(window,"processName"),Value(window,"title")));
   if(rule==null&&window==null)throw new Exception("This app is no longer available");
   app=rule==null?(identity??Identify(window,request.handle)):WorkspacePins.Resolve(rule,AppCatalog.Load());
  });
  var rows=new List<NordMenuItem>();
  if(window!=null)rows.Add(new NordMenuItem("Focus window",()=>TaskActions.Focus(request.id,request.handle)));
  else rows.Add(new NordMenuItem("Open app",()=>Launch(request.pinId)));
  bool unpin=rule!=null&&rule.workspace==request.workspace&&rule.pinned!=false;
  if(rule!=null||app!=null){var existing=rule;var launch=app;
   string label=unpin?"Unpin from this workspace":rule!=null&&rule.workspace!=request.workspace?"Move pin here (from workspace "+rule.workspace+")":"Pin to this workspace";
   rows.Add(new NordMenuItem(label,()=>Save(request,existing,launch,unpin),unpin?"Keeps startup placement. Running windows stay where they are.":"Stays in this taskbar when closed. Opens here only when the app has no other open window."));
  }else rows.Add(new NordMenuItem("App launcher unavailable",null,"This app needs an installed launcher before it can be pinned."));
  if(window!=null)rows.Add(new NordMenuItem("End task",()=>TaskActions.End(request.id,request.handle),"Force closes the app. Unsaved changes can be lost.",true));
  NordMenu.Show(app==null?(window==null?"Workspace app":Value(window,"processName")):app.Name,rows,request.x,request.y);
 }catch(Exception ex){NordMenu.Log("workspace task menu: "+ex.Message);}}
 static async void Launch(string id){var result=await Task.Run(()=>WorkspacePins.Launch(id));var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(result);if(data.ContainsKey("pinError"))MessageBox.Show(Convert.ToString(data["pinError"]),"Workspace app");}
 static async void Save(Request request,WorkspaceApp rule,AppEntry app,bool unpin){try{await Task.Run(()=>{
   // Revalidate running targets, rather than persisting a reused or stale window handle.
   Dictionary<string,object> window=null;if(!String.IsNullOrEmpty(request.id))window=TaskActions.Validate(request.id,request.handle);
   using(var mutex=new Mutex(false,"Local\\LiamNordWorkspaceRules")){bool acquired=false;try{
    try{acquired=mutex.WaitOne(5000);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new Exception("App rules are busy. Try again.");
    var path=AppCatalog.WorkspacesPath;var original=File.ReadAllText(path);
    var updated=Edit(original,request.workspace,rule==null?null:rule.id,app,window==null?null:Value(window,"processName"),unpin,window==null?null:Value(window,"title"));
    var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,updated);File.Replace(temp,path,path+".before-taskbar-pins.bak");}finally{if(File.Exists(temp))File.Delete(temp);}
   }finally{if(acquired)mutex.ReleaseMutex();}}
   WorkspaceConfig.Apply("sync");WorkspacePins.Refresh();
  });}catch(Exception ex){WorkspacePins.Refresh();MessageBox.Show(ex.Message,"Workspace pin");}}
 internal static string Edit(string json,string workspace,string ruleId,AppEntry app,string process,bool unpin,string title=null){
  var serializer=new JavaScriptSerializer();var root=serializer.Deserialize<Dictionary<string,object>>(json);
  var workspaces=((IEnumerable)root["workspaces"]).Cast<Dictionary<string,object>>().ToArray();
  var target=workspaces.FirstOrDefault(w=>Value(w,"name")==workspace);if(target==null)throw new Exception("Workspace no longer exists");
  // A launcher's existing rule is its identity; moving a pin must not create a
  // second, broader assignment for the same browser/application.
  if(ruleId==null&&!unpin&&app!=null){
   var candidates=workspaces.SelectMany(ws=>{object raw;return ws.TryGetValue("apps",out raw)&&raw!=null?((IEnumerable)raw).Cast<Dictionary<string,object>>():Enumerable.Empty<Dictionary<string,object>>();});
   var existing=candidates.Where(a=>String.Equals(Value(a,"catalogPattern"),"^"+Regex.Escape(app.Name)+"$",StringComparison.OrdinalIgnoreCase)&&WorkspaceApp.Match(Regex.Replace(process??"",@"\.exe$","",RegexOptions.IgnoreCase),Value(a,"processPattern"))).OrderByDescending(a=>!String.IsNullOrEmpty(Value(a,"titlePattern"))).FirstOrDefault();
   if(existing!=null)ruleId=Value(existing,"id");
  }
  Dictionary<string,object> selected=null;
  foreach(var ws in workspaces){object raw;var entries=ws.TryGetValue("apps",out raw)&&raw!=null?((IEnumerable)raw).Cast<Dictionary<string,object>>().ToList():new List<Dictionary<string,object>>();
   var found=entries.FirstOrDefault(a=>Value(a,"id")==ruleId&&ruleId!=null);if(found!=null){selected=found;if(Value(ws,"name")!=workspace&&!unpin){entries.Remove(found);ws["apps"]=entries;}}
  }
  if(ruleId!=null&&selected==null)throw new Exception("App rule changed. Reopen the menu and try again.");
  if(unpin){if(selected==null)throw new Exception("Pin no longer exists");selected["pinned"]=false;}
  else{
   if(selected==null){if(app==null||String.IsNullOrWhiteSpace(process))throw new Exception("Cannot identify this app");
    selected=new Dictionary<string,object>{{"id","task-"+Guid.NewGuid().ToString("N")},{"catalogPattern","^"+Regex.Escape(app.Name)+"$"},{"processPattern","^"+Regex.Escape(Regex.Replace(process,@"\.exe$","",RegexOptions.IgnoreCase))+"$"},{"name",app.Name}};
    if(Regex.IsMatch(process??"",@"^(chrome|msedge|brave|vivaldi|opera|firefox)(\.exe)?$",RegexOptions.IgnoreCase)){
     string pattern=@"(?:^| - )"+Regex.Escape(app.Name)+@"(?: - (?:Helium|Google Chrome|Microsoft Edge|Brave|Vivaldi|Opera))?$";
     if(String.IsNullOrEmpty(title)||!WorkspaceApp.Match(title,pattern))throw new Exception("Cannot safely distinguish this browser app from its other windows. No pin or placement rule was saved.");
     selected["titlePattern"]=pattern;
    }
    if(!String.IsNullOrEmpty(app.WindowAppId))selected["windowAppId"]=app.WindowAppId;
    if(!String.IsNullOrEmpty(app.AppID))selected["appId"]=app.AppID;
    if(!String.IsNullOrEmpty(app.Executable))selected["executable"]=app.Executable;
   }
   selected["pinned"]=true;
   object raw;var entries=target.TryGetValue("apps",out raw)&&raw!=null?((IEnumerable)raw).Cast<Dictionary<string,object>>().ToList():new List<Dictionary<string,object>>();
   if(!entries.Contains(selected))entries.Add(selected);target["apps"]=entries;
  }
  return serializer.Serialize(root);
 }
 internal static AppEntry Identify(Dictionary<string,object> window,long handle){
  var webApp=AppShortcuts.WindowApp(new IntPtr(handle));if(webApp!=null)return webApp;
  string executable=null,appId=null;uint pid;GetWindowThreadProcessId(new IntPtr(handle),out pid);
  var process=OpenProcess(0x1000,false,pid);try{if(process!=IntPtr.Zero){
   uint capacity=32768;var path=new System.Text.StringBuilder((int)capacity);if(QueryFullProcessImageName(process,0,path,ref capacity))executable=path.ToString();
   try{uint length=0;GetApplicationUserModelId(process,ref length,null);if(length>0&&length<32768){var value=new System.Text.StringBuilder((int)length);if(GetApplicationUserModelId(process,ref length,value)==0)appId=value.ToString();}}catch{}
  }}finally{if(process!=IntPtr.Zero)CloseHandle(process);}
  var apps=AppCatalog.Load();var exact=apps.FirstOrDefault(a=>appId!=null&&a.AppID==appId)??apps.FirstOrDefault(a=>!String.IsNullOrEmpty(executable)&&String.Equals(a.Executable,executable,StringComparison.OrdinalIgnoreCase));if(exact!=null)return exact;
  string name=Regex.Replace(Value(window,"processName"),@"\.exe$","",RegexOptions.IgnoreCase);
  // Shared runtime hosts are not launchers for the app whose window they display.
  if(Regex.IsMatch(name,"^(ApplicationFrameHost|RuntimeBroker|rundll32|dllhost|msedgewebview2)$",RegexOptions.IgnoreCase))return null;
  var candidates=apps.Where(a=>String.Equals(Path.GetFileNameWithoutExtension(a.Executable??""),name,StringComparison.OrdinalIgnoreCase)).ToArray();if(candidates.Length==1)return candidates[0];
  return !String.IsNullOrEmpty(executable)&&File.Exists(executable)?new AppEntry{Name=name,Executable=executable}:null;
 }
 static string Value(Dictionary<string,object> data,string key){object value;return data.TryGetValue(key,out value)?Convert.ToString(value):"";}
}
