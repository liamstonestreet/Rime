using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

/// <summary>
/// A thicker focus border than Windows draws (GlazeWM's border is Windows' own 1 px line): a click-through
/// frame in the gap just outside the focused window, in the edge color and the window corner style. Follows
/// focus and window moves through WinEvent hooks; hidden for full-screen, minimized, shell and rice windows.
/// </summary>
static class FocusBorder {
 static readonly System.Collections.Generic.List<IntPtr> hooks=new System.Collections.Generic.List<IntPtr>();
 static Window frame;static Border outline;
 static IntPtr target;static WinEventProc proc;static int width;static bool enabled;static double radius;
 static readonly string[] Ignored={"NordControlCenter","NordSpotlight","NordWorkspaceIndicator","zebar","Lightshot"};
 static readonly string[] Shell={"Progman","WorkerW","Shell_TrayWnd","Shell_SecondaryTrayWnd"};
 static bool eligible; // the focused window wants a border (checked once per focus change)

 // Temporarily disabled: workspace/cloaking events can leave a stale target.
 static readonly bool Available=false;
 public static void Start(){if(!Available)return;
  proc=OnEvent;
  // Foreground changes, window moves/resizes, minimize/restore, hide/show and destroy.
  foreach(var range in new[]{new[]{0x0003,0x0003},new[]{0x0016,0x0017},new[]{0x8001,0x8004},new[]{0x800B,0x800B}})
   hooks.Add(SetWinEventHook((uint)range[0],(uint)range[1],IntPtr.Zero,proc,0,0,0)); // WINEVENT_OUTOFCONTEXT
  BarAppearance.Changed+=Refresh;Refresh();
 }

 /// <summary>Re-reads the thickness (Glaze tab), whether borders are on and the corner style, then redraws.</summary>
 public static void Stop(){BarAppearance.Changed-=Refresh;foreach(var hook in hooks)if(hook!=IntPtr.Zero)UnhookWinEvent(hook);hooks.Clear();if(frame!=null)frame.Close();frame=null;outline=null;target=IntPtr.Zero;proc=null;}
 [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
 public static void Refresh(){if(!Available){enabled=false;if(frame!=null)frame.Hide();return;}
  width=BarAppearance.Settings.borderWidth;
  string on=null,corner=null,style=null;
  try{on=GlazeConfig.Get("window_effects/focused_window/border/enabled");corner=GlazeConfig.Get("window_effects/focused_window/corner_style/enabled");style=GlazeConfig.Get("window_effects/focused_window/corner_style/style");}catch{}
  enabled=width>1&&on!="false";
  // Windows 11 rounds windows 8 px by default; GlazeWM can make them square or small (4 px).
  radius=corner!="true"?8:style=="'square'"?0:style=="'small_rounded'"?4:8;
  Focus(GetForegroundWindow());
 }

 static void OnEvent(IntPtr hook,uint e,IntPtr window,int obj,int child,uint thread,uint time){
  if(obj!=0)return; // OBJID_WINDOW only
  if(frame!=null&&window==new WindowInteropHelper(frame).Handle)return; // ignore our own placement events
  if(e==0x0016)LogMinimize(window);
  if(e==0x0003)Focus(window);
  else if(window==target||e==0x8004)Track(); // moves and stacking changes, including nonactivating popups
 }

 static void Focus(IntPtr window){target=window;eligible=Eligible(window);Track();}
 static void Track(){
  var window=target;RECT r;
  if(!enabled||!eligible||!IsWindowVisible(window)||IsIconic(window)||FullScreen(window)||DwmGetWindowAttribute(window,9,out r,Marshal.SizeOf(typeof(RECT)))!=0){Hide();return;} // DWMWA_EXTENDED_FRAME_BOUNDS
  if(frame==null)Create();
  int t=(int)Math.Round(width*Dpi(window));
  outline.BorderThickness=new Thickness(width);outline.CornerRadius=new CornerRadius(radius+width);outline.BorderBrush=ControlCenterTheme.Current.Brush("Edge");
  var handle=new WindowInteropHelper(frame).Handle;
  if(!frame.IsVisible)frame.Show();
  PlaceBehind(handle,window,r.left-t,r.top-t,r.right-r.left+2*t,r.bottom-r.top+2*t);
 }

 // A decoration belongs immediately BELOW its window, never at the top of the desktop.
 // This also follows the target between the normal and topmost bands. Keeping an already
 // correct order avoids generating a stream of EVENT_OBJECT_REORDER callbacks.
 internal static void PlaceBehind(IntPtr handle,IntPtr window,int x,int y,int w,int h){
  bool topmost=(GetWindowLong(window,-20)&8)!=0;
  if(((GetWindowLong(handle,-20)&8)!=0)!=topmost)
   SetWindowPos(handle,new IntPtr(topmost?-1:-2),0,0,0,0,0x0013); // change band without moving or activating
  uint flags=0x0010|0x0040; // NOACTIVATE | SHOWWINDOW
  if(GetWindow(window,2)==handle)flags|=0x0004; // NOZORDER
  SetWindowPos(handle,window,x,y,w,h,flags);
 }

 static bool Eligible(IntPtr window){
  if(window==IntPtr.Zero)return false;
  var name=new StringBuilder(64);GetClassName(window,name,64);if(Array.IndexOf(Shell,name.ToString())>=0)return false;
  int cloaked;if(DwmGetWindowAttribute(window,14,out cloaked,4)==0&&cloaked!=0)return false; // DWMWA_CLOAKED (other workspaces)
  if((GetWindowLong(window,-20)&0x80)!=0)return false; // tool windows
  uint pid;GetWindowThreadProcessId(window,out pid);
  try{using(var p=Process.GetProcessById((int)pid))return !Array.Exists(Ignored,ignoredName=>String.Equals(ignoredName,p.ProcessName,StringComparison.OrdinalIgnoreCase));}catch{return false;}
 }
 /// <summary>Full-screen windows cover their monitor exactly: no border there.</summary>
 static bool FullScreen(IntPtr window){
  RECT r;GetWindowRect(window,out r);var monitor=System.Windows.Forms.Screen.FromHandle(window).Bounds;
  return r.left<=monitor.Left&&r.top<=monitor.Top&&r.right>=monitor.Right&&r.bottom>=monitor.Bottom;
 }

 static void Create(){
  outline=new Border{SnapsToDevicePixels=true};
  frame=new Window{Title="Nord Focus Border",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,
   ShowInTaskbar=false,ShowActivated=false,Focusable=false,Topmost=false,Width=1,Height=1,Left=-32000,Top=-32000,Content=outline,IsHitTestVisible=false};
  frame.SourceInitialized+=delegate{var h=new WindowInteropHelper(frame).Handle;SetWindowLong(h,-20,GetWindowLong(h,-20)|0x08000000|0x80|0x20);}; // no-activate, tool window, click-through
  // Placement is ours in physical pixels; ignore WPF's DPI-change resize.
  frame.SourceInitialized+=delegate{HwndSource.FromHwnd(new WindowInteropHelper(frame).Handle).AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=>{if(msg==0x02E0)handled=true;return IntPtr.Zero;});};
 }

 /// <summary>minimize.log: which window minimized, when, and what had focus, to trace unexpected minimizes.</summary>
 static void LogMinimize(IntPtr window){
  try{
   uint pid;GetWindowThreadProcessId(window,out pid);string name="?";try{using(var p=Process.GetProcessById((int)pid))name=p.ProcessName;}catch{}
   var title=new StringBuilder(80);GetWindowText(window,title,80);
   var path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"minimize.log");
   if(System.IO.File.Exists(path)&&new System.IO.FileInfo(path).Length>65536)System.IO.File.Delete(path);
   System.IO.File.AppendAllText(path,DateTime.Now.ToString("s")+" "+name+" | "+title+" | foreground="+GetForegroundWindow()+Environment.NewLine);
  }catch{}
 }
 static void Hide(){if(frame!=null&&frame.IsVisible)frame.Hide();}
 static double Dpi(IntPtr window){return GetDpiForWindow(window)/96.0;}

 [StructLayout(LayoutKind.Sequential)] struct RECT{public int left,top,right,bottom;}
 delegate void WinEventProc(IntPtr hook,uint e,IntPtr window,int obj,int child,uint thread,uint time);
 [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc proc,uint pid,uint thread,uint flags);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rect);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out RECT value,int size);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
}
