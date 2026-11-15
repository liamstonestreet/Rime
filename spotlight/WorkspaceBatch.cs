using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
static class WorkspaceBatch {
 internal sealed class Window {public string Id,Process,Title,Workspace;public long Handle;public uint Pid;public long Started;}
 static readonly string[] excluded={"zebar","NordRuntime","NordControlCenter","NordSpotlight","NordWorkspaceIndicator","Lightshot","dwm","ShellExperienceHost","StartMenuExperienceHost","SearchHost"};
 static readonly string[] protectedProcesses={"explorer","glazewm","glazewm-watcher","windhawk","System","Registry","smss","csrss","wininit","winlogon","services","lsass","svchost","ApplicationFrameHost"};
 static bool Protected(string name){return excluded.Contains(name,StringComparer.OrdinalIgnoreCase)||protectedProcesses.Contains(name,StringComparer.OrdinalIgnoreCase);}
 internal static Func<string,string> Query=args=>SpotlightWindows.Run(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"glzr.io\GlazeWM\cli\glazewm.exe"),args,5000);
 internal static Action<AppEntry> Launch=AppCatalog.Launch;
 internal static Func<Window,bool> Close=CloseNative;
 internal static Func<BatchAppSettings> Settings=BatchAppSettings.Load;
 internal static Func<AppEntry[]> Catalog=AppCatalog.Load;
 internal static Func<WorkspaceApp[]> Rules=AppCatalog.Rules;
 static bool busy;
 static string Text(Dictionary<string,object> data,string key){object value;return data.TryGetValue(key,out value)?Convert.ToString(value):"";}
 internal static Window[] Parse(string json){var root=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);object data;if(root.ContainsKey("success")&&!(bool)root["success"]||!root.TryGetValue("data",out data))throw new Exception("GlazeWM query failed");var found=new List<Window>();var seen=new HashSet<long>();Action<Dictionary<string,object>,string> visit=null;visit=(node,workspace)=>{
  if(Text(node,"type")=="workspace")workspace=Text(node,"name");if(Text(node,"type")=="window"){long handle;Guid id;var process=Regex.Replace(Text(node,"processName"),@"\.exe$","",RegexOptions.IgnoreCase);if(!String.IsNullOrEmpty(workspace)&&Guid.TryParse(Text(node,"id"),out id)&&long.TryParse(Text(node,"handle"),out handle)&&handle!=0&&!excluded.Contains(process,StringComparer.OrdinalIgnoreCase)&&seen.Add(handle)){uint pid;GetWindowThreadProcessId(new IntPtr(handle),out pid);found.Add(new Window{Id=id.ToString(),Handle=handle,Pid=pid,Process=process,Title=Text(node,"title"),Workspace=workspace,Started=Started(pid)});}}
  object children;if(node.TryGetValue("children",out children)&&children is IEnumerable)foreach(var child in (IEnumerable)children){var map=child as Dictionary<string,object>;if(map!=null)visit(map,workspace);}
 };foreach(Dictionary<string,object> workspace in (IEnumerable)((Dictionary<string,object>)data)["workspaces"])visit(workspace,null);return found.ToArray();}
 static Window[] Windows(){return Parse(Query("query workspaces"));}
 static long Started(uint pid){try{using(var process=Process.GetProcessById((int)pid))return process.StartTime.ToUniversalTime().Ticks;}catch{return 0;}}
 static bool CloseNative(Window window){
  uint pid;var hwnd=new IntPtr(window.Handle);if(!IsWindow(hwnd)||GetWindowThreadProcessId(hwnd,out pid)==0||pid==0||pid!=window.Pid||window.Started==0)return false;
  try{using(var self=Process.GetCurrentProcess())using(var process=Process.GetProcessById((int)pid)){
   if(process.Id==self.Id||process.SessionId!=self.SessionId||process.StartTime.ToUniversalTime().Ticks!=window.Started||Protected(process.ProcessName)||!String.Equals(process.ProcessName,window.Process,StringComparison.OrdinalIgnoreCase))return false;
   process.Kill();return true;
  }}catch{return false;}
 }
 internal static string RequestCloses(Window[] windows){int requested=0;var failed=new List<string>();var targets=windows.Where(w=>!Protected(w.Process)).GroupBy(w=>w.Pid>0?"pid:"+w.Pid:"hwnd:"+w.Handle).Select(g=>g.First()).ToArray();foreach(var window in targets){try{if(Close(window)){requested++;continue;}}catch{}failed.Add(window.Process);}
  return "Force-close sent to "+requested+" app processes across "+windows.Select(w=>w.Workspace).Distinct().Count()+" workspaces. Unsaved changes may be lost."+(failed.Count>0?" Could not terminate: "+String.Join(", ",failed.Distinct())+" (exited, inaccessible or changed).":"");
 }
 public static async Task<string> Run(bool close){if(busy)return "An app batch is already running.";busy=true;try{return await Task.Run(()=>{
  var windows=Windows();if(close)return RequestCloses(windows);var settings=Settings();var catalog=Catalog();var selected=catalog.Where(settings.Includes).GroupBy(a=>a.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();if(selected.Length==0)return "No installed apps selected. Configure /openall apps in CC > Configure.";
  int opened=0,running=0;var errors=new List<string>();foreach(var app in selected){try{var rule=Rules().FirstOrDefault(r=>!String.IsNullOrEmpty(app.AppID)&&r.appId==app.AppID||WorkspaceApp.Match(app.Name,r.catalogPattern));string process=null;if(settings.processNames!=null)settings.processNames.TryGetValue(app.Name,out process);if(String.IsNullOrEmpty(process)&&!String.IsNullOrEmpty(app.Executable))process=Path.GetFileNameWithoutExtension(app.Executable);
   if(windows.Any(w=>rule!=null?rule.Matches(w.Process,w.Title):!String.IsNullOrEmpty(process)&&String.Equals(w.Process,process,StringComparison.OrdinalIgnoreCase))){running++;continue;}Launch(app);opened++;
  }catch(Exception ex){errors.Add(app.Name+": "+ex.Message);}}return "Opened "+opened+" apps; "+running+" already running."+(errors.Count>0?" "+String.Join("; ",errors):"");
 });}finally{busy=false;}}
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
}
