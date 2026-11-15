using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

// A single foreground event hook, with no periodic process scan or UI-title history.
sealed class AppUsageTracker : IDisposable {
 delegate void WinEvent(IntPtr hook,uint evt,IntPtr window,int obj,int child,uint thread,uint time);
 readonly WinEvent callback; readonly Dispatcher dispatcher; readonly Func<AppEntry[]> catalog;
 IntPtr hook; uint previousPid;
 public AppUsageTracker(Func<AppEntry[]> entries,Dispatcher ui){catalog=entries;dispatcher=ui;callback=Changed;hook=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,2);Changed(hook,3,GetForegroundWindow(),0,0,0,0);}
 void Changed(IntPtr h,uint e,IntPtr window,int obj,int child,uint thread,uint time){if(window==IntPtr.Zero)return;uint pid;GetWindowThreadProcessId(window,out pid);if(pid==0||pid==previousPid)return;previousPid=pid;dispatcher.BeginInvoke(new Action(()=>Observe(window,pid)),DispatcherPriority.Background);}
 void Observe(IntPtr window,uint pid){try{if(window!=GetForegroundWindow())return;using(var process=Process.GetProcessById((int)pid)){
  var apps=catalog();AppEntry app=null;var handle=OpenProcess(0x1000,false,pid);
  try{if(handle!=IntPtr.Zero){uint len=0;GetApplicationUserModelId(handle,ref len,null);if(len>0&&len<1024){var name=new StringBuilder((int)len);if(GetApplicationUserModelId(handle,ref len,name)==0)app=apps.FirstOrDefault(a=>String.Equals(a.AppID,name.ToString(),StringComparison.OrdinalIgnoreCase));}}}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}
  if(app==null){var title=new StringBuilder(512);GetWindowText(window,title,title.Capacity);var rule=AppCatalog.Rules().FirstOrDefault(r=>r.Matches(process.ProcessName,title.ToString()));if(rule!=null)app=apps.FirstOrDefault(a=>WorkspaceApp.Match(a.Name,rule.catalogPattern));}
  if(app==null)app=apps.FirstOrDefault(a=>!String.IsNullOrEmpty(a.Executable)&&String.Equals(Path.GetFileNameWithoutExtension(a.Executable),process.ProcessName,StringComparison.OrdinalIgnoreCase));
  if(app!=null)AppUsage.Record(app.Key);
 }}catch{}}
 public void Dispose(){if(hook!=IntPtr.Zero)UnhookWinEvent(hook);hook=IntPtr.Zero;}
 [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEvent callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int size);
 [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process,ref uint length,StringBuilder id);
}
