using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
// Hides only our bars on the foreground game's monitor; never changes focus, resolution or game state.
static class GamePresentation {
 static DispatcherTimer timer;static readonly HashSet<IntPtr> hidden=new HashSet<IntPtr>();
 public static void Start(){if(timer!=null)return;timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};timer.Tick+=delegate{Refresh();};timer.Start();Refresh();}
 public static void Stop(){if(timer!=null){timer.Stop();timer=null;}foreach(var h in hidden)if(IsWindow(h))ShowWindowAsync(h,4);hidden.Clear();ScreenFrame.GameMonitor=null;}
 static void Refresh(){try{var game=GameWindows.ForegroundGame;System.Drawing.Rectangle? monitor=game==IntPtr.Zero?(System.Drawing.Rectangle?)null:System.Windows.Forms.Screen.FromHandle(game).Bounds;ScreenFrame.GameMonitor=monitor;
  foreach(var h in new List<IntPtr>(hidden))if(!IsWindow(h)||monitor==null||System.Windows.Forms.Screen.FromHandle(h).Bounds!=monitor.Value){if(IsWindow(h))ShowWindowAsync(h,4);hidden.Remove(h);}
  if(monitor==null)return;var ids=new HashSet<uint>();foreach(var process in Process.GetProcessesByName("zebar"))using(process)ids.Add((uint)process.Id);
  EnumWindows((h,p)=>{uint pid;GetWindowThreadProcessId(h,out pid);RECT rect;if(ids.Contains(pid)&&IsWindowVisible(h)&&GetWindowRect(h,out rect)&&rect.Bottom-rect.Top>30&&rect.Bottom-rect.Top<200&&System.Windows.Forms.Screen.FromHandle(h).Bounds==monitor.Value){ShowWindowAsync(h,0);hidden.Add(h);}return true;},IntPtr.Zero);
 }catch{/* A game closing during lookup must not affect focus. */}}
 [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
 delegate bool Callback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumWindows(Callback callback,IntPtr p);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out RECT rect);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr h,int mode);
}
