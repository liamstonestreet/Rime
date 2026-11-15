using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
static class GlazeCommand {
 internal static Func<string,string> Execute=RunProcess;
 public static string Run(string args){return Execute(args);}
 static string RunProcess(string args){var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"glzr.io\GlazeWM\cli\glazewm.exe");if(!File.Exists(path))path="glazewm.exe";using(var p=Process.Start(new ProcessStartInfo(path,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true})){var output=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception("GlazeWM command failed");return output;}}
 /// <summary>
 /// Reloads GlazeWM's config without the side effect of the reload refocusing a workspace of its own
 /// choosing (and moving the cursor there): the focused workspace, window and cursor are put back.
 /// </summary>
 public static void ReloadConfig(){
  string workspace=null,window=null;Point cursor;bool cursorKnown=GetCursorPos(out cursor);
  try{
   foreach(Dictionary<string,object> w in (System.Collections.IEnumerable)Query("workspaces")["workspaces"])if((bool)w["hasFocus"]){workspace=(string)w["name"];break;}
   window=(string)((Dictionary<string,object>)Query("focused")["focused"])["id"];
  }catch{}
  Command("wm-reload-config");
  try{if(workspace!=null)Command("focus --workspace "+workspace);if(window!=null)Command("focus --container-id "+window);}catch{}
  finally{if(cursorKnown)SetCursorPos(cursor.x,cursor.y);}
 }
 static Dictionary<string,object> Query(string name){return (Dictionary<string,object>)new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Run("query "+name))["data"];}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct Point{public int x,y;}
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 public static void Command(string args){var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Run("command "+args));if(result.ContainsKey("success")&&!(bool)result["success"])throw new Exception("GlazeWM rejected the command");}
}
static class TaskActions {
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 internal static Dictionary<string,object> Validate(string id,long handle){var root=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(GlazeCommand.Run("query windows"));var data=(Dictionary<string,object>)root["data"];foreach(var item in (System.Collections.IEnumerable)data["windows"]){var w=(Dictionary<string,object>)item;if((string)w["id"]==id&&Convert.ToInt64(w["handle"])==handle)return w;}throw new Exception("This window has closed");}
 public static string Receive(string text){var p=text.Split(':');Guid id;long hwnd;if(p.Length!=5||!Guid.TryParse(p[1],out id)||!long.TryParse(p[2],out hwnd))return "{\"ok\":false}";if(p[0]=="task-focus")Run(p[1],hwnd,false);else if(p[0]=="task-menu")Application.Current.Dispatcher.BeginInvoke(new Action(()=>Menu(p[1],hwnd)));return "{\"ok\":true}";}
 public static void Focus(string id,long handle){Run(id,handle,false);} public static void End(string id,long handle){Run(id,handle,true);}
 internal static void Activate(string id,long h){
  var window=Validate(id,h);object raw;var state=window.TryGetValue("state",out raw)?raw as Dictionary<string,object>:null;
  if(state!=null&&Convert.ToString(state["type"])=="minimized"){
   // Restore through the WM so its previous floating/fullscreen state stays coherent.
   GlazeCommand.Command("--id "+id+" toggle-minimized");Validate(id,h);
  }
  GlazeCommand.Command("focus --container-id "+id);
 }
 static async void Run(string id,long h,bool end){Exception failure=null;try{await Task.Run(()=>{Validate(id,h);uint pid;GetWindowThreadProcessId(new IntPtr(h),out pid);if(pid==0)throw new Exception("Window no longer exists");if(end){using(var process=Process.GetProcessById((int)pid)){if(process.Id==Process.GetCurrentProcess().Id)return;process.Kill();}}else{Activate(id,h);}});}catch(Exception ex){failure=ex;}if(failure!=null)await Application.Current.Dispatcher.InvokeAsync(new Action(()=>MessageBox.Show(failure.Message,"Taskbar")));}
 // The window lookup (for the header) runs off the UI thread; the menu then opens at the cursor.
 static async void Menu(string id,long h){
  string header="";
  try{header=await Task.Run(()=>{var w=Validate(id,h);var title=Convert.ToString(w.ContainsKey("title")?w["title"]:"");var app=Convert.ToString(w.ContainsKey("processName")?w["processName"]:"");return String.IsNullOrWhiteSpace(title)?app:title;});}
  catch(Exception ex){NordMenu.Log("task menu lookup: "+ex.Message);return;} // Usually: the window closed first.
  try{
   NordMenu.Show(header,new[]{
    new NordMenuItem("Focus window",()=>Run(id,h,false)),
    new NordMenuItem("End task",()=>Run(id,h,true),"Force closes the app. Unsaved changes can be lost.",true)});
  }catch(Exception ex){NordMenu.Log("task menu: "+ex);}
 }
}
