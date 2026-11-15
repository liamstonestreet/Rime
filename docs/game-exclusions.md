using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
static class TaskPinChecks {
 public static void Run(){
  if(!GameWindows.Matches("Overwatch.exe","",new string[0])||!GameWindows.Matches("game.exe",@"D:\SteamLibrary\steamapps\common\Example\game.exe",new string[0])||!GameWindows.Matches("game.exe","",new[]{"launcher.exe","steam.exe"})||!GameWindows.Matches("javaw.exe","",new[]{"prismlauncher.exe"}))throw new Exception("Steam/Prism game detection failed");
  if(GameWindows.Matches("steamwebhelper.exe","",new[]{"steam.exe"})||GameWindows.Matches("prismlauncher.exe","",new string[0])||GameWindows.Matches("chrome.exe","",new[]{"steam.exe"})||GameWindows.Matches("javaw.exe","",new string[0])||GameWindows.Matches("editor.exe","",new string[0]))throw new Exception("Game exclusions must preserve launchers, browsers and unrelated Java apps");
  Console.WriteLine("PASS: game exclusions recognize Steam library/ancestry, Prism Java and Overwatch; launchers, browsers and unrelated apps remain managed. Synthetic identities only.");

  if(new AppearanceSettings().centerWidth!=759||NordControlCenter.CenterWidth(759,1920,1.25)!=759||NordControlCenter.CenterWidth(1200,1280,1.5)>1280/1.5-24)throw new Exception("Control Center default width or monitor fitting failed");
  var widthRoundTrip=new JavaScriptSerializer().Deserialize<AppearanceSettings>(new JavaScriptSerializer().Serialize(new AppearanceSettings{centerWidth=888}));if(widthRoundTrip.centerWidth!=888)throw new Exception("CC width persistence failed");
  var heightRoundTrip=new JavaScriptSerializer().Deserialize<AppearanceSettings>(new JavaScriptSerializer().Serialize(new AppearanceSettings{centerMaxHeight=75}));
  if(new AppearanceSettings().centerMaxHeight!=50||heightRoundTrip.centerMaxHeight!=75||NordControlCenter.MonitorPanelLimit(1440,1.25,75)!=860||NordControlCenter.MonitorPanelLimit(1080,1,25)!=266)throw new Exception("CC max-height persistence or per-monitor scaling failed");
  var orderPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"nord-task-order-"+Guid.NewGuid().ToString("N")+".json");try{
   TaskPins.SaveOrder(orderPath,"1",new[]{"pin:helium","app:discord:0"});TaskPins.SaveOrder(orderPath,"2",new[]{"pin:spotify"});TaskPins.SaveOrder(orderPath,"1",new[]{"app:discord:0","pin:helium"});
   var orders=TaskPins.ReadOrders(orderPath);if(orders["1"][0]!="app:discord:0"||orders["2"].Single()!="pin:spotify"||!System.IO.File.Exists(orderPath+".previous"))throw new Exception("Taskbar order must persist separately per workspace with backup");
  }finally{foreach(var path in new[]{orderPath,orderPath+".previous",orderPath+".tmp"})if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
  Console.WriteLine("PASS: persistent per-workspace task order with atomic replacement/backup, 1.3x CC default and width persistence/monitor fit.");
  var json="{\"custom\":42,\"workspaces\":[{\"name\":\"1\",\"monitor\":2,\"extra\":true,\"apps\":[{\"id\":\"helium\",\"catalogPattern\":\"^Helium$\",\"processPattern\":\"^chrome$\",\"titlePattern\":\" - Helium$\",\"extraRule\":7}]},{\"name\":\"2\",\"apps\":[]}]}";
  var moved=TaskPins.Edit(json,"2","helium",null,null,false);
  var serializer=new JavaScriptSerializer();var data=serializer.Deserialize<WorkspaceFile>(moved);
  if(data.workspaces[0].apps.Length!=0||data.workspaces[1].apps.Length!=1||data.workspaces[1].apps[0].titlePattern!=" - Helium$"||!moved.Contains("\"extraRule\":7")||!moved.Contains("\"custom\":42")||!moved.Contains("\"extra\":true"))throw new Exception("Moving a pin lost existing rule details or custom settings");
  var unpinned=TaskPins.Edit(moved,"2","helium",null,null,true);
  data=serializer.Deserialize<WorkspaceFile>(unpinned);if(data.workspaces[1].apps[0].pinned!=false||data.workspaces[1].apps[0].processPattern!="^chrome$")throw new Exception("Unpin must retain app placement");
  var repinned=TaskPins.Edit(unpinned,"2","helium",null,null,false);data=serializer.Deserialize<WorkspaceFile>(repinned);if(data.workspaces[1].apps.Length!=1||data.workspaces[1].apps[0].pinned!=true)throw new Exception("Repin duplicated app rule");
  var added=TaskPins.Edit(repinned,"1",null,new AppEntry{Name="A+ App",AppID="test!App",Executable=@"C:\Apps\App.exe"},"App.exe",false);
  data=serializer.Deserialize<WorkspaceFile>(added);var app=data.workspaces[0].apps.Single();if(!app.Matches("App","any document")||app.Matches("AppExtra","")||!WorkspaceApp.Match("A+ App",app.catalogPattern)||app.appId!="test!App"||app.pinned!=true)throw new Exception("New pin identity or escaped process rule is invalid");
  bool rejected=false;try{TaskPins.Edit(json,"missing",null,null,null,false);}catch{rejected=true;}if(!rejected)throw new Exception("Unknown workspace accepted");
  rejected=false;try{TaskPins.Edit(json,"1","stale",null,null,true);}catch{rejected=true;}if(!rejected)throw new Exception("Stale app rule accepted");
  var duplicate=TaskPins.Edit(json,"2",null,new AppEntry{Name="Helium",AppID="helium"},"chrome",false,"Work - Helium");var duplicateData=serializer.Deserialize<WorkspaceFile>(duplicate);if(duplicateData.workspaces.SelectMany(w=>w.apps??new WorkspaceApp[0]).Count(a=>a.catalogPattern=="^Helium$")!=1||duplicateData.workspaces[1].apps.Single().titlePattern!=" - Helium$")throw new Exception("Browser repin duplicated/broadened canonical placement");
  var youtube=TaskPins.Edit(json,"2",null,new AppEntry{Name="YouTube",Executable=@"C:\Menu\YouTube.lnk",WindowAppId="browser.youtube"},"chrome",false,"Video title - YouTube");var yt=serializer.Deserialize<WorkspaceFile>(youtube).workspaces[1].apps.Single();if(yt.windowAppId!="browser.youtube"||!yt.Matches("chrome","Other video - YouTube")||yt.Matches("chrome","Work - Helium"))throw new Exception("Web app pin must exclude regular browser windows");
  rejected=false;try{TaskPins.Edit(json,"2",null,new AppEntry{Name="Unknown browser app"},"chrome",false,"Other page - Helium");}catch{rejected=true;}if(!rejected)throw new Exception("Ambiguous browser pin accepted");
  var conflict=serializer.Deserialize<WorkspaceFile>(json).workspaces;conflict[1].apps=new[]{new WorkspaceApp{id="task-bad",catalogPattern="^Helium$",processPattern="^chrome$",pinned=false}};
  var generated=WorkspaceConfig.Generate("workspaces:\n  - name: '1'\n\nwindow_rules:\n  - commands: ['ignore']\n    match: []\n\nkeybindings: []\n",conflict,true);
  if(generated.Contains("focus --workspace")||generated.Contains("move --workspace 2")||generated.Contains("move --workspace 1")||!generated.Contains("commands: ['ignore']"))throw new Exception("Generated app assignments retained focus loop/broad duplicate or changed unrelated rules");
  Console.WriteLine("PASS: overlapping browser pin reproduction, canonical pin reuse, web-app title scoping, ambiguous-pin rejection and startup-only routing generation without native move rules.");
  Console.WriteLine("PASS: workspace pin create/move/unpin/repin, exact process matching, preserved browser title rules/custom metadata, stale target guards (isolated JSON).");
 }
}
