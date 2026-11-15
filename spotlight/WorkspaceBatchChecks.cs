using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
static class WorkspaceBatchChecks {
 public static void Run(){
  var query=WorkspaceBatch.Query;var launch=WorkspaceBatch.Launch;var close=WorkspaceBatch.Close;var settings=WorkspaceBatch.Settings;var catalog=WorkspaceBatch.Catalog;var rules=WorkspaceBatch.Rules;
  try{
   var json="{\"success\":true,\"data\":{\"workspaces\":[{\"type\":\"workspace\",\"name\":\"1\",\"children\":[{\"type\":\"split\",\"children\":[{\"type\":\"window\",\"id\":\"00000000-0000-0000-0000-000000000001\",\"handle\":101,\"processName\":\"Code\",\"title\":\"Document\"}]}]},{\"type\":\"workspace\",\"name\":\"2\",\"children\":[{\"type\":\"window\",\"id\":\"00000000-0000-0000-0000-000000000002\",\"handle\":202,\"processName\":\"UnpinnedApp\"},{\"type\":\"window\",\"id\":\"00000000-0000-0000-0000-000000000003\",\"handle\":303,\"processName\":\"zebar\"}]}]}}";
   var windows=WorkspaceBatch.Parse(json);if(windows.Length!=2||windows[0].Workspace!="1"||windows[1].Workspace!="2")throw new Exception("Close-all must enumerate live taskbar trees, including unpinned apps, and exclude helpers");
   var closed=new List<long>();WorkspaceBatch.Close=w=>{closed.Add(w.Handle);return true;};WorkspaceBatch.Query=args=>{if(args!="query workspaces")throw new Exception("Batch commands must not rewrite/reload Glaze config");return json;};var result=WorkspaceBatch.Run(true).GetAwaiter().GetResult();if(!closed.SequenceEqual(new long[]{101,202})||!result.Contains("Force-close"))throw new Exception("Close-all targeted the wrong windows");
   windows[0].Pid=1001;windows[1].Pid=1001;closed.Clear();result=WorkspaceBatch.RequestCloses(windows);if(closed.Count!=1||!result.Contains("1 app processes"))throw new Exception("Shared-process apps must be terminated only once");
   var shell=new WorkspaceBatch.Window{Handle=400,Pid=400,Process="explorer",Workspace="1"};closed.Clear();WorkspaceBatch.RequestCloses(new[]{shell,windows[0]});if(closed.Count!=1||closed[0]!=101)throw new Exception("Shell must be protected from force-close");
   WorkspaceBatch.Close=w=>{if(w.Handle==101)throw new Exception("Denied");closed.Add(w.Handle);return true;};closed.Clear();windows[1].Pid=1002;result=WorkspaceBatch.RequestCloses(windows);if(closed.Count!=1||closed[0]!=202||!result.Contains("Could not terminate: Code"))throw new Exception("One denied force-close must not skip other apps");
   WorkspaceBatch.Catalog=()=>new[]{new AppEntry{Name="Editor",AppID="editor"},new AppEntry{Name="Music",AppID="music"},new AppEntry{Name="Other",AppID="other"}};WorkspaceBatch.Settings=()=>new BatchAppSettings{apps=new[]{"Editor"},appIds=new[]{"music"}};WorkspaceBatch.Rules=()=>new[]{new WorkspaceApp{catalogPattern="^Editor$",processPattern="^Code$"}};var opened=new List<string>();WorkspaceBatch.Launch=a=>opened.Add(a.AppID);result=WorkspaceBatch.Run(false).GetAwaiter().GetResult();if(!opened.SequenceEqual(new[]{"music"})||!result.Contains("1 already running"))throw new Exception("Open-all must honor configured IDs/legacy names and skip matching live apps");
   WorkspaceBatch.Launch=a=>{throw new Exception("Unavailable test app");};result=WorkspaceBatch.Run(false).GetAwaiter().GetResult();if(!result.Contains("Unavailable test app"))throw new Exception("Failed app launches need a visible summary");
   Console.WriteLine("PASS: /openall selection/legacy names/running-rule matching and force /closeall workspace-tree scope; no apps launched, closed or killed; no config reloads.");
  }finally{WorkspaceBatch.Query=query;WorkspaceBatch.Launch=launch;WorkspaceBatch.Close=close;WorkspaceBatch.Settings=settings;WorkspaceBatch.Catalog=catalog;WorkspaceBatch.Rules=rules;}
 }
}
