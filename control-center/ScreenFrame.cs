using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

/// <summary>
/// Caelestia-style screen frame: along each monitor's left, right and bottom edges, joining the bar, so the
/// desktop sits in a rounded inset. Same frosted glass and tint as the bar, with the edge line curving down
/// from the bar's line around the inside. Lives just above the desktop, behind every window (full-screen
/// apps cover it; it never takes clicks). GlazeWM's outer gaps grow by the frame thickness so tiled
/// windows stay clear of it.
/// </summary>
static class ScreenFrame {
 const double Radius=16; // inner corner at 100% corner radius
 const int Overlap=8;    // physical rows the frame reaches up behind the bar (more than its line at any scale)
 static readonly List<Pane> panes=new List<Pane>();
 static readonly List<IntPtr> wallpaperCovers=new List<IntPtr>();
 internal static void WallpaperCover(IntPtr handle,bool visible){if(visible){if(!wallpaperCovers.Contains(handle))wallpaperCovers.Add(handle);}else wallpaperCovers.Remove(handle);foreach(var pane in panes)pane.EnsurePlacement();}
 static IntPtr Backdrop(System.Drawing.Rectangle area){foreach(var cover in wallpaperCovers){RECT r;if(IsWindowVisible(cover)&&GetWindowRect(cover,out r)&&new System.Drawing.Rectangle(r.left,r.top,r.right-r.left,r.bottom-r.top).IntersectsWith(area))return cover;}return DesktopSurface();}
 static volatile System.Drawing.Rectangle[] barBounds=new System.Drawing.Rectangle[0];
 internal static System.Drawing.Rectangle[] BarBounds {get{return barBounds;}}
 internal static int BarBottom(System.Drawing.Rectangle monitor,double scale){var bars=barBounds.Where(b=>b.Left<monitor.Right&&b.Right>monitor.Left&&b.Top>=monitor.Top&&b.Top<monitor.Bottom).ToArray();return bars.Length>0?bars.Max(b=>b.Bottom):StartupBarBounds(monitor,scale).Bottom;}
 internal static System.Drawing.Rectangle? GameMonitor;static System.Drawing.Rectangle? notificationGap;
 internal static void SetNotificationGap(System.Drawing.Rectangle? gap){if(notificationGap==gap)return;notificationGap=gap;foreach(var pane in panes)pane.UpdateGap();}

 static readonly HashSet<string> observedMonitors=new HashSet<string>();
 static int appliedThickness=-1;static string layoutKey,lookKey;
 static DispatcherTimer poll,gaps;static bool started;static Action repaint;

 public static void Start(){
  StartBarIndicator();
  if(started)return;started=true;observedMonitors.Clear();appliedThickness=BarAppearance.Settings.frame; // the gaps already include it
  BarAppearance.Changed+=Changed;
  repaint=()=>Rebuild(false);ControlCenterTheme.Current.Changed+=repaint;FrostMaterial.Rendered+=repaint; // repaint with everything else
  poll=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};poll.Tick+=delegate{Rebuild(false);};poll.Start();
  gaps=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};gaps.Tick+=delegate{gaps.Stop();SyncGaps();};
  Rebuild(true);
 }
 static System.IO.FileSystemWatcher indicatorWatcher;
 static void StartBarIndicator(){if(indicatorWatcher!=null)return;
  var folder=AppDomain.CurrentDomain.BaseDirectory;indicatorWatcher=new System.IO.FileSystemWatcher(folder,"bar-indicator-state.txt"){NotifyFilter=System.IO.NotifyFilters.LastWrite};
  System.IO.FileSystemEventHandler update=(s,e)=>{try{bool active=System.IO.File.ReadAllText(e.FullPath)=="1";MediaBridge.Broadcast("{\"barIndicator\":"+(active?"true":"false")+"}");}catch{}};
  indicatorWatcher.Changed+=update;indicatorWatcher.EnableRaisingEvents=true;
 }

 public static void Stop(){if(indicatorWatcher!=null){indicatorWatcher.Dispose();indicatorWatcher=null;}started=false;BarAppearance.Changed-=Changed;if(repaint!=null){ControlCenterTheme.Current.Changed-=repaint;FrostMaterial.Rendered-=repaint;repaint=null;}if(poll!=null){poll.Stop();poll=null;}if(gaps!=null){gaps.Stop();gaps=null;}foreach(var pane in panes)pane.Close();panes.Clear();barBounds=new System.Drawing.Rectangle[0];layoutKey=lookKey=null;observedMonitors.Clear();}
 static void Changed(){if(!started)return;Rebuild(false);gaps.Stop();gaps.Start();} // GlazeWM reloads once the slider settles

 /// <summary>Grows (or shrinks) GlazeWM's left, right and bottom outer gaps by the change in frame thickness.</summary>
 static void SyncGaps(){
  int next=BarAppearance.Settings.frame,delta=next-appliedThickness;if(delta==0)return;
  try{
   var changes=new Dictionary<string,string>();
   foreach(var side in new[]{"left","right","bottom"}){var path="gaps/outer_gap/"+side;changes[path]="'"+Math.Max(0,Px(GlazeConfig.Get(path))+delta)+"px'";}
   GlazeConfig.Set(changes);appliedThickness=next;
  }catch(Exception e){WallpaperSwitcher.Log("screen frame gaps: "+e.Message);}
 }

 /// <summary>Rebuilds when the settings, theme, monitors, bars or frost changed (or when forced).</summary>
 static void Rebuild(bool force){
  if(!started)return;
  var s=BarAppearance.Settings;var bars=Bars();
  // Windows follow the layout (thickness, monitors, bars); everything else repaints the existing windows.
  var layout=s.frame+"|"+String.Join(";",bars.Select(b=>b.ToString()));
  if(force||layout!=layoutKey){
   layoutKey=layout;lookKey=null;
   foreach(var pane in panes)pane.Close();panes.Clear();
   if(s.frame>0)foreach(var bar in bars)panes.Add(new Pane(bar,s.frame));
  }
  var look=s.radius+"|"+s.edgeStyle+"|"+s.grain+"|"+ControlCenterTheme.Current.Revision+"|"+FrostStamp();
  foreach(var pane in panes)pane.EnsurePlacement();
  if(look==lookKey)return;lookKey=look;
  foreach(var pane in panes)pane.Draw(Radius*s.radius/100.0,s.edgeStyle);
 }

 /// <summary>Each monitor with a bar: the area below the bar, in physical pixels.</summary>
 static List<System.Drawing.Rectangle> Bars(){
  var result=new List<System.Drawing.Rectangle>();
  var bounds=new List<System.Drawing.Rectangle>();
  var ids=new HashSet<uint>();
  foreach(var process in Process.GetProcessesByName("zebar"))using(process)ids.Add((uint)process.Id);
  EnumWindows((h,l)=>{uint pid;GetWindowThreadProcessId(h,out pid);RECT r;
   if(ids.Contains(pid)&&IsWindowVisible(h)&&GetWindowRect(h,out r)&&r.bottom-r.top>30&&r.bottom-r.top<200){
    var screen=System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(r.left,r.top,r.right-r.left,r.bottom-r.top));var monitor=screen.Bounds;observedMonitors.Add(screen.DeviceName);
    var drawable=DrawableBarBounds(new System.Drawing.Rectangle(r.left,r.top,r.right-r.left,r.bottom-r.top),GetDpiForWindow(h)/96.0);
    result.Add(new System.Drawing.Rectangle(monitor.Left,drawable.Bottom,monitor.Width,monitor.Bottom-drawable.Bottom));
    bounds.Add(drawable);
   }return true;},IntPtr.Zero);
  // All-monitor Zebar preset has a known 50-DIP host. Do not wait on WebView2 discovery at logon.
  // Once a monitor's real bar has appeared, normal visibility detection owns it (including games).
  foreach(var screen in System.Windows.Forms.Screen.AllScreens)if(!observedMonitors.Contains(screen.DeviceName)){
   var drawable=StartupBarBounds(screen.Bounds,NotificationShortcut.ScaleAt(screen.Bounds));bounds.Add(drawable);result.Add(new System.Drawing.Rectangle(screen.Bounds.Left,drawable.Bottom,screen.Bounds.Width,screen.Bounds.Bottom-drawable.Bottom));
  }
  barBounds=bounds.OrderBy(r=>r.Left).ThenBy(r=>r.Top).ToArray();return result.OrderBy(r=>r.Left).ToList();
 }
 internal static System.Drawing.Rectangle StartupBarBounds(System.Drawing.Rectangle monitor,double scale){return new System.Drawing.Rectangle(monitor.Left,monitor.Top,monitor.Width,(int)Math.Floor(50*scale+1e-6));}
 internal static System.Drawing.Rectangle DrawableBarBounds(System.Drawing.Rectangle host,double scale){
  return new System.Drawing.Rectangle(host.X,host.Y,host.Width,Math.Min(host.Height,(int)Math.Floor(50*scale+1e-6)));
 }
 static string FrostStamp(){return FrostMaterial.Revision.ToString();}
 // WPF first lays out the hidden 1x1 staging HWND. Its height can be less
 // than the overlap above the bar; wait for the real SizeChanged layout.
 internal static bool PlacementMatches(System.Drawing.Rectangle area,System.Drawing.Rectangle actual){
  return actual==new System.Drawing.Rectangle(area.Left,area.Top-Overlap,area.Width,area.Height+Overlap);
 }
 internal static Rect FrameSurface(double width,double height,double scale){
  if(scale<=0||Double.IsNaN(scale)||Double.IsInfinity(scale)||width<=0||height<=Overlap/scale||Double.IsNaN(width)||Double.IsNaN(height)||Double.IsInfinity(width)||Double.IsInfinity(height))return Rect.Empty;
  return new Rect(0,Overlap/scale,width,height-Overlap/scale);
 }
 static int Px(string text){var m=System.Text.RegularExpressions.Regex.Match(text??"",@"\d+");return m.Success?Int32.Parse(m.Value):0;}

 internal static PathGeometry Outline(double w,double h,double top,double t,double r,double line,bool closed){
   double o=line/2,L=t-o,R=w-t+o,T=top-o,B=h-t+o,rr=r+o;var size=new Size(rr,rr);
   var figure=new PathFigure{StartPoint=new Point(L+rr,T),IsClosed=closed};
   figure.Segments.Add(new ArcSegment(new Point(L,T+rr),size,0,false,SweepDirection.Counterclockwise,true));
   figure.Segments.Add(new LineSegment(new Point(L,B-rr),true));
   figure.Segments.Add(new ArcSegment(new Point(L+rr,B),size,0,false,SweepDirection.Counterclockwise,true));
   figure.Segments.Add(new LineSegment(new Point(R-rr,B),true));
   figure.Segments.Add(new ArcSegment(new Point(R,B-rr),size,0,false,SweepDirection.Counterclockwise,true));
   figure.Segments.Add(new LineSegment(new Point(R,T+rr),true));
   figure.Segments.Add(new ArcSegment(new Point(R-rr,T),size,0,false,SweepDirection.Counterclockwise,true));
   var outline=new PathGeometry(new[]{figure});outline.Freeze();return outline;
 }

 /// <summary>One monitor's frame window.</summary>
 sealed class Pane {
  readonly Window window;readonly Grid root=new Grid();readonly System.Drawing.Rectangle area;
  readonly double thickness;double radius;string edge;
  Geometry clipShape,clipBounds;double clipDpi;Rectangle fillLayer,grainLayer;Path borderLine;FrameworkElement glowLayer;
  Brush lastFrost;int frostRevision=-1;bool closed;

  public Pane(System.Drawing.Rectangle area,double thickness){
   this.area=area;this.thickness=thickness;
   window=new Window{Title="Nord Screen Frame",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,
    ShowInTaskbar=false,ShowActivated=false,Focusable=false,IsHitTestVisible=false,Width=1,Height=1,Left=-32000,Top=-32000,Content=root};
   window.SourceInitialized+=delegate{
    var h=new WindowInteropHelper(window).Handle;SetWindowLong(h,-20,GetWindowLong(h,-20)|0x08000000|0x80|0x20); // no-activate, tool window, click-through
    // WPF must process WM_DPICHANGED so geometry uses this monitor's scale. Reapply only
    // our physical bounds after WPF handles its suggested rectangle, without suppressing DPI.
    HwndSource.FromHwnd(h).AddHook((IntPtr hw,int msg,IntPtr w,IntPtr l,ref bool handled)=>{if(msg==0x02E0)window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{Place();Draw();}));return IntPtr.Zero;});
   };
   root.SizeChanged+=delegate{Draw();};
   window.Loaded+=delegate{window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(closed)return;Place();Draw();}));};
   window.Show();
   Place();
  }

  void Place(){if(closed)return;
   var handle=new WindowInteropHelper(window).Handle;
   // Just above the desktop window: behind every app window.
   var desktop=Backdrop(area);var above=desktop==IntPtr.Zero?IntPtr.Zero:GetWindow(desktop,3);
   SetWindowPos(handle,above,area.Left,area.Top-Overlap,area.Width,area.Height+Overlap,0x0010u|(above==handle||desktop==IntPtr.Zero?0x0004u:0u));
  }
  public void EnsurePlacement(){if(closed)return;window.Opacity=GameMonitor.HasValue&&System.Windows.Forms.Screen.FromRectangle(area).Bounds==GameMonitor.Value?0:1;
   var handle=new WindowInteropHelper(window).Handle;RECT actual;
   // WPF can apply its initial 1x1 staging bounds after our first native placement.
   if(!GetWindowRect(handle,out actual)||!PlacementMatches(area,new System.Drawing.Rectangle(actual.left,actual.top,actual.right-actual.left,actual.bottom-actual.top)))Place();
   // A deferred first layout must not be cached as a completed paint.
   if(root.Children.Count==0)Draw();
   var desktop=Backdrop(area);if(desktop==IntPtr.Zero)return;
   var above=GetWindow(desktop,3);
   if(above!=handle)SetWindowPos(handle,above,0,0,0,0,0x0013);
  }

  /// <summary>The frame shape (monitor minus a rounded inset), frosted and tinted like the bar, plus the inner edge line.</summary>
  public void Redraw(){Draw();}
  public void Draw(double radius,string edge){this.radius=radius;this.edge=edge;Draw();}
  void Draw(){if(closed)return;
   double w=root.ActualWidth,h=root.ActualHeight;if(w<=0||h<=0||edge==null)return;
   var dpi=VisualTreeHelper.GetDpi(root).DpiScaleX;
   var surface=FrameSurface(w,h,dpi);if(surface.IsEmpty)return;
   // The window starts Overlap rows up, behind the bar, so the edge line can begin on the bar's own line.
   double top=Overlap/dpi,t=thickness,r=Math.Max(0,Math.Min(radius,Math.Min(w-2*t,h-top-t)/2));
   var hole=new RectangleGeometry(new Rect(t,top,Math.Max(0,w-2*t),Math.Max(0,h-top-t)),r,r);
   var shape=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(surface),hole);shape.Freeze();
   root.Children.Clear();borderLine=null;glowLayer=null;grainLayer=null;clipShape=shape;clipDpi=dpi;clipBounds=new RectangleGeometry(new Rect(0,top,w,Math.Max(0,h-top)));
   var handle=new WindowInteropHelper(window).Handle;var theme=ControlCenterTheme.Current;
   if(frostRevision!=FrostMaterial.Revision){lastFrost=ThemeTransition.Crossfade(lastFrost,FrostFile.Behind(area.Left,area.Top-Overlap,area.Width,area.Height+Overlap));frostRevision=FrostMaterial.Revision;}
   var frost=lastFrost;var tint=theme.Brush("Panel");
   // Retained brushes already render cheaply. BitmapCache here rasterizes an entire
   // monitor twice, even though only its thin frame is visible.
   fillLayer=new Rectangle{Fill=frost==null?(Brush)tint:FrostFile.Under(frost,tint),Clip=shape};root.Children.Add(fillLayer);
   if(BarAppearance.GrainOpacity>0){grainLayer=new Rectangle{Fill=Grain.Brush(dpi,0,area.Top-Overlap-System.Windows.Forms.Screen.FromRectangle(area).Bounds.Top),Opacity=BarAppearance.GrainOpacity,Clip=shape};root.Children.Add(grainLayer);}
   if(edge=="none"){UpdateGap();return;}
   // The line sits on the frame's side of the inset, like the bar's sits on the bar's side. Its corners
   // continue the bar's: same center, radius and width (src/frost-bar.css, html.framed).
   double line=Math.Floor(3*dpi+1e-6)/dpi;var outline=Outline(w,h,top,t,r,line,false);
   Brush stroke=theme.Brush("Edge");
   if(edge=="gradient")stroke=theme.Gradient("Accent","Partner",new Point(0,0),new Point(w,0)); // live colors; no per-frame tree rebuilds
   // Close only the blur source across the top: an open-ended arc loses glow energy
   // at the join. The surface clip still keeps all native output below the bar.
   if(edge=="glow"){glowLayer=EdgeGlow.Inside(Outline(w,h,top,t,r,line,true),stroke,line,shape);root.Children.Add(glowLayer);}
   borderLine=new Path{Data=outline,Stroke=stroke,StrokeThickness=line};root.Children.Add(borderLine);UpdateGap();
  }
  public void UpdateGap(){if(closed||clipShape==null)return;Geometry fillClip=clipShape,edgeClip=clipBounds;
   if(notificationGap.HasValue){var gap=notificationGap.Value;if(gap.IntersectsWith(area)){var cut=new RectangleGeometry(new Rect((gap.Left-area.Left)/clipDpi,(gap.Top-area.Top+Overlap)/clipDpi,gap.Width/clipDpi,gap.Height/clipDpi));fillClip=new CombinedGeometry(GeometryCombineMode.Exclude,clipShape,cut);edgeClip=new CombinedGeometry(GeometryCombineMode.Exclude,clipBounds,cut);}}
   if(fillLayer!=null)fillLayer.Clip=fillClip;if(grainLayer!=null)grainLayer.Clip=fillClip;if(borderLine!=null)borderLine.Clip=edgeClip;if(glowLayer!=null)glowLayer.Clip=new CombinedGeometry(GeometryCombineMode.Intersect,fillClip,edgeClip);
  }

  public void Close(){if(closed)return;closed=true;window.Close();root.Children.Clear();lastFrost=null;}
 }

 // Explorer can create its wallpaper WorkerW after our first frame is shown.
 static IntPtr DesktopSurface(){
  IntPtr desktop=IntPtr.Zero;
  EnumWindows((h,l)=>{if(!IsWindowVisible(h))return true;var name=new System.Text.StringBuilder(256);GetClassName(h,name,name.Capacity);
   if(name.ToString()=="Progman"||name.ToString()=="WorkerW"){desktop=h;return false;}return true;},IntPtr.Zero);
  return desktop;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,System.Text.StringBuilder name,int count);
 [StructLayout(LayoutKind.Sequential)] struct RECT{public int left,top,right,bottom;}
 delegate bool EnumProc(IntPtr window,IntPtr parameter);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rect);
 [DllImport("user32.dll")] static extern IntPtr FindWindow(string className,string title);
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint relation);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
}
