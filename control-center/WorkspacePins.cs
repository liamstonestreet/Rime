using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

static class WorkspacePins {
 static readonly object gate=new object();static DateTime rulesStamp,catalogStamp;static string snapshot;
 static readonly Dictionary<string,DateTime> launching=new Dictionary<string,DateTime>();
 public static string Snapshot(){lock(gate){var r=File.GetLastWriteTimeUtc(AppCatalog.WorkspacesPath);var c=File.GetLastWriteTimeUtc(Path.Combine(AppCatalog.Folder,"spotlight-apps.json"));if(snapshot!=null&&r==rulesStamp&&c==catalogStamp)return snapshot;
  var apps=AppCatalog.Load();var pins=new List<object>();
  foreach(var rule in AppCatalog.Rules()){
   if(rule.pinned==false)continue;
   var app=Resolve(rule,apps);if(app==null)continue;
   string icon=null;var bitmap=app.Icon as BitmapSource;if(bitmap!=null){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()){png.Save(stream);icon="data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}}
   pins.Add(new {id=rule.id,workspace=rule.workspace,name=app.Name,processPattern=rule.processPattern,titlePattern=rule.titlePattern,icon=icon});
  }
  // Every workspace with default apps in workspaces.json, even if they aren't installed: the bar always shows these.
  var ruled=AppCatalog.Rules().Select(rule=>rule.workspace).Where(name=>!String.IsNullOrEmpty(name)).Distinct().ToArray();
  snapshot=new JavaScriptSerializer().Serialize(new{workspacePins=pins,ruledWorkspaces=ruled,taskOrders=TaskPins.Orders()});rulesStamp=r;catalogStamp=c;return snapshot;
 }}
 public static string Launch(string id){try{
  var rule=AppCatalog.Rules().FirstOrDefault(r=>r.id==id);if(rule==null)throw new Exception("Unknown workspace app");
  lock(gate){DateTime last;if(launching.TryGetValue(id,out last)&&DateTime.UtcNow-last<TimeSpan.FromSeconds(3))return "{\"ok\":true}";launching[id]=DateTime.UtcNow;}
  // Recheck at click time: rapid clicks and apps already on another workspace reuse a window.
  var root=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(GlazeCommand.Run("query windows"));
  var data=(Dictionary<string,object>)root["data"];
  foreach(var item in (System.Collections.IEnumerable)data["windows"]){var w=(Dictionary<string,object>)item;
   if(WorkspaceLaunchPolicy.Matches(rule,new WorkspaceLaunchWindow{Process=Convert.ToString(w["processName"]),Title=Convert.ToString(w["title"]),AppId=String.IsNullOrEmpty(rule.windowAppId)?null:AppShortcuts.WindowIdentity(new IntPtr(Convert.ToInt64(w["handle"])))})){TaskActions.Focus(Convert.ToString(w["id"]),Convert.ToInt64(w["handle"]));return "{\"ok\":true}";}
  }
  var app=Resolve(rule,AppCatalog.Load());if(app==null)throw new Exception("This app is no longer installed");
  AppCatalog.ValidateLaunch(app);
  // No matching managed window exists, so this is the app's first open window.
  // Tray/background processes do not prevent applying its startup workspace.
  GlazeCommand.Command("focus --workspace "+rule.workspace);
  AppCatalog.Launch(app);return "{\"ok\":true}";
 }catch(Exception ex){lock(gate)launching.Remove(id);return new JavaScriptSerializer().Serialize(new{pinError=ex.Message,pinId=id});}}
 internal static AppEntry Resolve(WorkspaceApp rule,AppEntry[] apps){return apps.FirstOrDefault(a=>!String.IsNullOrEmpty(rule.appId)&&a.AppID==rule.appId)??apps.FirstOrDefault(a=>WorkspaceApp.Match(a.Name,rule.catalogPattern))??(!String.IsNullOrEmpty(rule.executable)&&File.Exists(rule.executable)?new AppEntry{Name=rule.name??rule.id,Executable=rule.executable}:null);}
 public static void Refresh(){lock(gate)snapshot=null;MediaBridge.Broadcast(Snapshot());}
}
