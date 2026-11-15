using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

sealed class MonitorContext {
 public readonly Rectangle Bounds;
 public MonitorContext(Rectangle bounds){Bounds=bounds;}
}
interface IPanelMonitorSource {MonitorContext At(int x,int y);MonitorContext Primary {get;}}
interface IPanelPositionTarget {
 double Width {get;set;}
 bool TryOrigin(out System.Drawing.Point origin);
 void Position(int x,int y);
}
sealed class WindowsPanelMonitorSource:IPanelMonitorSource {
 public MonitorContext At(int x,int y){return new MonitorContext(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x,y)).Bounds);}
 public MonitorContext Primary {get{return new MonitorContext(System.Windows.Forms.Screen.PrimaryScreen.Bounds);}}
}
sealed class WindowsPanelPositionTarget:IPanelPositionTarget {
 readonly Window window;
 public WindowsPanelPositionTarget(Window window){this.window=window;}
 public double Width {get{return window.Width;}set{window.Width=value;}}
 [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int left,top,right,bottom;}
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
 public bool TryOrigin(out System.Drawing.Point origin){NativeRect rect=new NativeRect();var h=new WindowInteropHelper(window).Handle;bool found=h!=IntPtr.Zero&&GetWindowRect(h,out rect);origin=found?new System.Drawing.Point(rect.left,rect.top):new System.Drawing.Point();return found;}
 public void Position(int x,int y){var h=new WindowInteropHelper(window).EnsureHandle();SetWindowPos(h,IntPtr.Zero,x,y,0,0,0x0015);}// NOSIZE | NOZORDER | NOACTIVATE
}

// Coordinates are physical pixels. Width/height returned to WPF are DIPs. The
// monitor lookup and native positioning are replaceable in deterministic tests.
sealed class PanelPlacementController {
 readonly IPanelMonitorSource monitors;readonly IPanelPositionTarget target;readonly double edge;bool keeping;
 public int X {get;private set;}public int Y {get;private set;}
 public double Scale {get;private set;}public bool IsPlaced {get;private set;}
 public PanelPlacementController(IPanelMonitorSource monitors,IPanelPositionTarget target,double edge){this.monitors=monitors;this.target=target;this.edge=edge;Scale=1;}
 public MonitorContext Monitor {get{return IsPlaced?monitors.At(X,Y):monitors.Primary;}}
 public static double ConstrainWidth(int requested,int monitorWidth,double scale){return Math.Min(Math.Max(480,Math.Min(1200,requested)),Math.Max(1,monitorWidth/Math.Max(.1,scale)-24));}
 public static double HeightLimit(int pixelHeight,double scale,int percent,double edge){return Math.Max(1,Math.Floor(pixelHeight*Math.Max(25,Math.Min(90,percent))/(100*Math.Max(.1,scale))-edge));}
 public double MaxHeight(int percent){return HeightLimit(Monitor.Bounds.Height,Scale,percent,edge);}
 public void Place(double centerX,double bottom,double scale,int requested){
  Scale=Math.Max(.1,scale);var monitor=monitors.At((int)centerX,(int)bottom);
  target.Width=ConstrainWidth(requested,monitor.Bounds.Width,Scale);X=(int)Math.Round(centerX-target.Width*Scale/2);Y=(int)Math.Round(bottom-edge*Scale);IsPlaced=true;
  // WPF may apply Windows' suggested rect during a mixed-DPI monitor move.
  for(int i=0;i<2;i++)target.Position(X,Y);
 }
 public bool ApplyWidth(int requested){if(Math.Abs(target.Width-ConstrainWidth(requested,Monitor.Bounds.Width,Scale))<.1)return false;
  if(IsPlaced)Place(X+target.Width*Scale/2,Y+edge*Scale,Scale,requested);else target.Width=ConstrainWidth(requested,Monitor.Bounds.Width,Scale);return true;
 }
 public void KeepPosition(){if(!IsPlaced||keeping)return;System.Drawing.Point origin;if(!target.TryOrigin(out origin)||(origin.X==X&&origin.Y==Y))return;keeping=true;try{target.Position(X,Y);}finally{keeping=false;}}
}
