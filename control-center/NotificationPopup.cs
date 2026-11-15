using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

sealed class NotificationAnchor {
 public bool Pin,FromEdge;public int Left,Top,Right,Bottom;public double Scale;public int BarBottom;
 internal static bool Parse(string text,out NotificationAnchor anchor){anchor=null;var parts=(text??"").Split(',');if((parts.Length!=6&&parts.Length!=7)||(parts[0]!="hover"&&parts[0]!="pin"))return false;var values=new int[5];for(int i=0;i<5;i++)if(!Int32.TryParse(parts[i+1],NumberStyles.Integer,CultureInfo.InvariantCulture,out values[i])||Math.Abs((long)values[i])>100000)return false;
  if(values[2]<=values[0]||values[3]<=values[1]||values[2]-values[0]>500||values[3]-values[1]>300||values[4]<500||values[4]>5000)return false;
  int barBottom=values[3];if(parts.Length==7&&(!Int32.TryParse(parts[6],out barBottom)||barBottom<values[3]||barBottom-values[1]>300))return false;
  anchor=new NotificationAnchor{BarBottom=barBottom,Pin=parts[0]=="pin",Left=values[0],Top=values[1],Right=values[2],Bottom=values[3],Scale=values[4]/1000.0};return true;
 }
 internal static System.Drawing.Point Place(NotificationAnchor anchor,System.Drawing.Rectangle monitor,double width,double height){int w=(int)Math.Ceiling(width*anchor.Scale),h=(int)Math.Ceiling(height*anchor.Scale),gap=(int)Math.Round(8*anchor.Scale);return new System.Drawing.Point(Math.Max(monitor.Left+gap,Math.Min((anchor.Left+anchor.Right-w)/2,monitor.Right-gap-w)),Math.Max(monitor.Top+gap,Math.Min(Math.Max(anchor.Bottom,anchor.BarBottom)+gap,monitor.Bottom-gap-h)));}
 internal bool Over(int x,int y){return x>=Left&&x<=Right&&y>=Top&&y<=Bottom;}
}
static class NotificationPopup {
 static NotificationPopupWindow window;static string gapSnapshot="{\"notificationGap\":null}";
 internal static string GapSnapshot(){return gapSnapshot;}
 internal static void SetBarGap(int left,int right,int top,bool enabled){var value=enabled?String.Format(CultureInfo.InvariantCulture,"{{\"notificationGap\":{{\"left\":{0},\"right\":{1},\"top\":{2}}}}}",left,right,top):"{\"notificationGap\":null}";if(value==gapSnapshot)return;gapSnapshot=value;MediaBridge.Broadcast(value);}
 internal static void ClearJoin(){SetBarGap(0,0,0,false);ScreenFrame.SetNotificationGap(null);}
 public static bool IsVisible {get{return window!=null&&window.IsVisible;}}
 internal static void CloseForLayout(){if(window!=null)window.Dismiss();}
 public static void Start(){MediaBridge.OpenNotifications=Open;NotificationShortcut.Start();}
 internal static void OpenEdge(NotificationAnchor anchor){if(GameWindows.ForegroundGame!=IntPtr.Zero)return;ShowDrawer(anchor);}
 static void ShowDrawer(NotificationAnchor anchor){try{if(window==null)window=new NotificationPopupWindow();window.OpenAt(anchor);}catch(Exception e){Failed(window,e);}}
 internal static void Failed(NotificationPopupWindow failed,Exception error){WallpaperSwitcher.Log("notification drawer: "+error);if(failed==null)return;if(window==failed)window=null;try{failed.Dispose();}catch{}try{failed.Close();}catch{}}

 public static void Open(string message){NotificationAnchor anchor;if(GameWindows.ForegroundGame!=IntPtr.Zero||!NotificationAnchor.Parse(message,out anchor))return;Toggle(anchor);}
 internal static void Toggle(NotificationAnchor anchor){if(GameWindows.ForegroundGame!=IntPtr.Zero)return;if(window!=null&&window.IsVisible&&window.OnMonitor(anchor)){window.Dismiss();return;}anchor.Pin=true;ShowDrawer(anchor);}
 public static void Stop(){NotificationShortcut.Stop();if(MediaBridge.OpenNotifications==Open)MediaBridge.OpenNotifications=null;if(window!=null){window.Dispose();window.Close();window=null;}}
}
static class NotificationStripGeometry {
 internal static System.Drawing.Rectangle Bounds(System.Drawing.Rectangle screen,int barBottom){int top=Math.Max(screen.Top,Math.Min(barBottom,screen.Bottom-1));return new System.Drawing.Rectangle(screen.Left,top,screen.Width,screen.Bottom-top);}
 internal const double Join=4,Wing=24;
 internal static System.Drawing.Rectangle AttachedBounds(System.Drawing.Rectangle screen,int barBottom,double scale,int frame){var bounds=Bounds(screen,barBottom);if(frame>0){int overlap=(int)Math.Round(Join*scale);bounds.Y-=overlap;bounds.Height+=overlap;}return bounds;}
 internal static Geometry AttachedOutline(double height,double depth,double line,double radius,int frame){
  if(depth<=line||height<=line)return Geometry.Empty;
  if(frame==0){var plain=new PathFigure{StartPoint=new Point(0,0),IsClosed=true};plain.Segments.Add(new LineSegment(new Point(depth-line/2,0),false));plain.Segments.Add(new LineSegment(new Point(depth-line/2,height),true));plain.Segments.Add(new LineSegment(new Point(0,height),false));return new PathGeometry(new[]{plain});}
  // Offset a circular hole's edge, not just its straight sides: the stroke
  // radius grows by half its width, keeping the fill and outline concentric.
  double insetRadius=Math.Max(0,Math.Min(Math.Min(radius,Wing),Math.Min(depth/2,(height-frame-Join)/2)));
  double top=Join-line/2,bottom=height-frame+line/2,right=depth-line/2,end=depth+Wing,r=insetRadius>0?insetRadius+line/2:0;
  var f=new PathFigure{StartPoint=new Point(end,top),IsClosed=false,IsFilled=true};
  f.Segments.Add(new LineSegment(new Point(right+r,top),true));f.Segments.Add(new ArcSegment(new Point(right,top+r),new Size(r,r),0,false,SweepDirection.Counterclockwise,true));
  f.Segments.Add(new LineSegment(new Point(right,bottom-r),true));f.Segments.Add(new ArcSegment(new Point(right+r,bottom),new Size(r,r),0,false,SweepDirection.Counterclockwise,true));f.Segments.Add(new LineSegment(new Point(end,bottom),true));
  f.Segments.Add(new LineSegment(new Point(end,height),false));f.Segments.Add(new LineSegment(new Point(0,height),false));f.Segments.Add(new LineSegment(new Point(0,Join),false));f.Segments.Add(new LineSegment(new Point(end,Join),false));
  var shape=new PathGeometry(new[]{f});shape.Freeze();return shape;
 }
 internal static Geometry Outline(double height,double depth,double line,double radius){if(depth<=line||height<=line)return Geometry.Empty;double left=line/2,top=line/2,right=depth-line/2,bottom=height-line/2,r=Math.Max(0,Math.Min(radius,Math.Min((right-left)/2,(bottom-top)/2)));var figure=new PathFigure{StartPoint=new Point(left,top),IsClosed=true};figure.Segments.Add(new LineSegment(new Point(right-r,top),true));figure.Segments.Add(new ArcSegment(new Point(right,top+r),new Size(r,r),0,false,SweepDirection.Clockwise,true));figure.Segments.Add(new LineSegment(new Point(right,bottom-r),true));figure.Segments.Add(new ArcSegment(new Point(right-r,bottom),new Size(r,r),0,false,SweepDirection.Clockwise,true));figure.Segments.Add(new LineSegment(new Point(left,bottom),true));var shape=new PathGeometry(new[]{figure});shape.Freeze();return shape;}
}
// Transpose CC's outline: its bar attachment becomes the drawer's left-frame attachment.
static class NotificationDrawerGeometry {
 public const double Edge=4,Wing=24;
 static Point Turn(Point p){return new Point(p.Y,p.X);}
 internal static PathGeometry Outline(double height,double depth,double line,double radius,bool transparent){
  var f=PanelFrameGeometry.Calculate(height,depth,Edge,line,Wing,radius,transparent);var figure=new PathFigure{StartPoint=Turn(f.Start),IsClosed=false,IsFilled=true};
  figure.Segments.Add(new LineSegment(Turn(f.FlatLeft),true));figure.Segments.Add(new ArcSegment(Turn(f.WingLeft),f.WingSize,90,false,SweepDirection.Counterclockwise,true));
  figure.Segments.Add(new LineSegment(Turn(f.SideLeft),true));figure.Segments.Add(new ArcSegment(Turn(f.CornerLeft),f.CornerSize,90,false,SweepDirection.Clockwise,true));
  figure.Segments.Add(new LineSegment(Turn(f.Floor),true));figure.Segments.Add(new ArcSegment(Turn(f.CornerRight),f.CornerSize,90,false,SweepDirection.Clockwise,true));
  figure.Segments.Add(new LineSegment(Turn(f.SideRight),true));figure.Segments.Add(new ArcSegment(Turn(f.WingRight),f.WingSize,90,false,SweepDirection.Counterclockwise,true));
  figure.Segments.Add(new LineSegment(Turn(f.FlatRight),true));figure.Segments.Add(new LineSegment(Turn(f.CoverRight),false));figure.Segments.Add(new LineSegment(Turn(f.CoverLeft),false));
  var shape=new PathGeometry(new[]{figure});shape.Freeze();return shape;
 }
 internal static System.Drawing.Point Place(System.Drawing.Rectangle monitor,int barBottom,double scale,int frame,double height){
  double top=Math.Max(monitor.Top,barBottom)+8*scale,bottom=monitor.Bottom-Math.Max(8,frame)*scale;
  return new System.Drawing.Point((int)Math.Round(monitor.Left+(frame-Edge)*scale),(int)Math.Round(Math.Max(top,top+(bottom-top-height*scale)/2)));
 }
}
sealed class NotificationPopupWindow:Window,IDisposable {
 readonly NotificationInbox inbox;readonly NotificationsView view;readonly Border surface=new Border();readonly Grid root=new Grid(),contentClip=new Grid();readonly TranslateTransform slide=new TranslateTransform();
 readonly System.Windows.Shapes.Path shell=new System.Windows.Shapes.Path{HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};
 readonly System.Windows.Shapes.Rectangle frost=new System.Windows.Shapes.Rectangle{IsHitTestVisible=false},grain=new System.Windows.Shapes.Rectangle{IsHitTestVisible=false};
 Brush material;bool backingAvailable;readonly FrameworkElement glow;readonly Button close=RiceLayout.Button("Close","NotificationsClose");readonly DispatcherTimer hover=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
 NotificationAnchor anchor;System.Drawing.Rectangle monitor;bool pinned,disposed,placing,closing,tracking,entered,refitQueued,mouseDown;DateTime outside,opened;int revision;double contentWidth,drawerHeight,drawnDepth=-1;int hostX,hostY;
 public NotificationPopupWindow():this(NotificationCenter.Current){}
 internal NotificationPopupWindow(NotificationInbox inbox){this.inbox=inbox;view=new NotificationsView(inbox);Title="Nord Notifications";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Width=600;Height=500;Left=Top=-32000;FontFamily=RiceFont.Current;
  Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Load(System.Xml.XmlReader.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"control-center-styles.xaml")));ScrollChrome.Install(Resources);ControlCenterTheme.Current.Install(Resources);Foreground=B("Text");
  var content=new StackPanel();var tools=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};close.Margin=new Thickness(0);tools.Children.Add(close);content.Children.Add(tools);view.Margin=new Thickness(0,4,0,0);content.Children.Add(view);
  surface.Child=content;surface.Padding=new Thickness(20,14,20,16);surface.HorizontalAlignment=HorizontalAlignment.Left;surface.VerticalAlignment=VerticalAlignment.Top;surface.Margin=new Thickness(0);surface.RenderTransform=slide;
  glow=EdgeGlow.Inside(Geometry.Empty,B("Edge"),3,Geometry.Empty);root.Children.Add(frost);root.Children.Add(shell);root.Children.Add(grain);root.Children.Add(glow);contentClip.Children.Add(surface);root.Children.Add(contentClip);Content=root;
  close.Click+=delegate{Dismiss();};PreviewKeyDown+=(sender,e)=>{if(e.Key==Key.Escape){Dismiss();e.Handled=true;}};
  SourceInitialized+=delegate{var h=new WindowInteropHelper(this).Handle;SetWindowLong(h,-20,GetWindowLong(h,-20)|0x80);HwndSource.FromHwnd(h).AddHook((IntPtr hw,int msg,IntPtr w,IntPtr l,ref bool handled)=>{if(msg==0x02E0)QueuePlace();return IntPtr.Zero;});};
  Loaded+=delegate{QueuePlace();};hover.Tick+=delegate{CheckHover();};BarAppearance.Changed+=Appearance;ControlCenterTheme.Current.Changed+=Appearance;FrostMaterial.Rendered+=Appearance;inbox.Changed+=InboxChanged;
 }
 Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
 public void OpenAt(NotificationAnchor value){if(disposed)return;bool hidden=!IsVisible;anchor=value;monitor=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((anchor.Left+anchor.Right)/2,anchor.Bottom)).Bounds;pinned=value.Pin||pinned&&!hidden;outside=DateTime.MinValue;closing=false;entered=false;opened=DateTime.UtcNow;view.Render();mouseDown=(GetAsyncKeyState(0x01)&0x8000)!=0;
  new WindowInteropHelper(this).EnsureHandle();Place();if(hidden){slide.BeginAnimation(TranslateTransform.XProperty,null);slide.X=-contentWidth;UpdateShape(true);Show();Place();}LogSurface();AnimateTo(0,260,false);hover.Start();
 }
 void LogSurface(){try{
  var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())drawing.DrawRectangle(material,null,new Rect(0,0,8,8));
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(8,8,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var pixels=new byte[256];bitmap.CopyPixels(pixels,32,0);int alphaMin=255;for(int i=3;i<pixels.Length;i+=4)alphaMin=Math.Min(alphaMin,pixels[i]);
  uint color,flags;byte alpha;bool layered=GetLayeredWindowAttributes(new WindowInteropHelper(this).Handle,out color,out alpha,out flags);
  uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);string foreground="unknown";try{using(var process=System.Diagnostics.Process.GetProcessById((int)pid))foreground=process.ProcessName;}catch{}
  WallpaperSwitcher.Log("notification material: frost="+backingAvailable+" materialAlphaMin="+alphaMin+" windowOpacity="+Opacity.ToString(CultureInfo.InvariantCulture)+" nativeAlpha="+(layered?alpha.ToString():"per-pixel")+" nativeFlags="+flags+" foreground="+foreground+" scale="+anchor.Scale.ToString(CultureInfo.InvariantCulture));
 }catch(Exception error){WallpaperSwitcher.Log("notification material diagnostic: "+error.Message);}}
 [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr h,out uint color,out byte alpha,out uint flags);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid); internal FrameworkElement Preview(System.Drawing.Rectangle bounds,double scale){anchor=new NotificationAnchor{Left=bounds.Left,Right=bounds.Left+4,Top=bounds.Top+50,Bottom=bounds.Top+80,BarBottom=bounds.Top+50,Scale=scale,FromEdge=true};monitor=bounds;Place();slide.BeginAnimation(TranslateTransform.XProperty,null);slide.X=0;UpdateShape(true);root.Measure(new Size(Width,Height));root.Arrange(new Rect(0,0,Width,Height));root.UpdateLayout();return root;}
 internal bool OnMonitor(NotificationAnchor value){return monitor==System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((value.Left+value.Right)/2,value.Bottom)).Bounds;}

 void Appearance(){if(disposed)return;QueuePlace();}
 void InboxChanged(){var app=Application.Current;if(disposed||app==null||app.Dispatcher.HasShutdownStarted)return;app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(QueuePlace));}
 void QueuePlace(){if(disposed||refitQueued||!IsVisible)return;refitQueued=true;Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{refitQueued=false;if(!disposed&&IsVisible)try{Place();}catch(Exception error){NotificationPopup.Failed(this,error);}}));}

 void Place(){if(disposed||anchor==null||placing)return;placing=true;try{
  var settings=BarAppearance.Settings;double scale=anchor.Scale,line=Math.Floor(3*scale+1e-6)/scale;
  // The native bar surface is authoritative for both bell and keyboard opens.
  int barBottom=ScreenFrame.BarBottom(monitor,scale);
  var area=NotificationStripGeometry.AttachedBounds(monitor,barBottom,scale,settings.frame);
  contentWidth=Math.Min(480,Math.Max(240,monitor.Width/scale-24));Width=contentWidth+(settings.frame>0?NotificationStripGeometry.Wing:0);MaxHeight=Double.PositiveInfinity;
  var content=(StackPanel)surface.Child;var tools=content.Children[0];surface.Width=contentWidth;double innerWidth=Math.Max(1,contentWidth-surface.Padding.Left-surface.Padding.Right);tools.Measure(new Size(innerWidth,Double.PositiveInfinity));
  drawerHeight=Height=area.Height/scale;surface.Margin=new Thickness(0,settings.frame>0?NotificationStripGeometry.Join:0,0,0);view.FitList(Math.Max(0,Height-surface.Margin.Top-settings.frame-surface.Padding.Top-surface.Padding.Bottom-tools.DesiredSize.Height),innerWidth,true);
  var point=new System.Drawing.Point(area.Left,area.Top);hostX=point.X;hostY=point.Y;root.Width=Width;root.Height=Height;root.Clip=new RectangleGeometry(new Rect(0,0,Width,Height));
  var handle=new WindowInteropHelper(this).Handle;if(handle!=IntPtr.Zero)SetWindowPos(handle,IntPtr.Zero,point.X,point.Y,(int)Math.Ceiling(Width*scale),(int)Math.Ceiling(Height*scale),0x0014);
  var backing=FrostFile.Behind(point.X,point.Y,Width*scale,Height*scale);backingAvailable=backing!=null;material=backing==null?B("Panel"):FrostFile.Under(backing,B("Panel"));shell.Stroke=settings.edgeStyle=="none"?Brushes.Transparent:settings.edgeStyle=="gradient"?ControlCenterTheme.Current.Gradient("Accent","Partner",new Point(0,0),new Point(monitor.Width/scale,0)):B("Edge");shell.StrokeThickness=line;
  grain.Fill=Grain.Brush(scale,point.X-monitor.Left,point.Y-monitor.Top);grain.Opacity=BarAppearance.GrainOpacity;glow.Visibility=settings.edgeStyle=="glow"?Visibility.Visible:Visibility.Collapsed;EdgeGlow.SetStroke(glow,shell.Stroke,line);UpdateShape(true);

 }finally{placing=false;}}
 void UpdateShape(bool force){if(disposed||anchor==null)return;double depth=Math.Max(0,Math.Min(contentWidth,Math.Round((contentWidth+slide.X)*anchor.Scale)/anchor.Scale));if(!force&&Math.Abs(depth-drawnDepth)<.05)return;drawnDepth=depth;
  double radius=16*BarAppearance.Settings.radius/100.0;int frame=BarAppearance.Settings.frame;
  var shape=NotificationStripGeometry.AttachedOutline(drawerHeight,depth,shell.StrokeThickness,radius,frame);
  var fillShape=NotificationStripGeometry.AttachedOutline(drawerHeight,depth,0,radius,frame);
  // Paint the material in a full-host rectangle, just like ScreenFrame. A Path
  // fill uses its geometry bounds, which changes texture mapping at the wings.
  shell.Fill=null;frost.Fill=material;frost.Clip=fillShape;shell.Data=shape;grain.Clip=fillShape;contentClip.Clip=fillShape;glow.Clip=fillShape;((System.Windows.Shapes.Path)((Grid)glow).Children[0]).Data=shape;
  if(IsVisible){bool joined=BarAppearance.Settings.frame>0&&depth>shell.StrokeThickness;int right=hostX+(int)Math.Ceiling((depth+NotificationStripGeometry.Wing)*anchor.Scale);NotificationPopup.SetBarGap(hostX,right,hostY,joined);ScreenFrame.SetNotificationGap(joined?(System.Drawing.Rectangle?)new System.Drawing.Rectangle(monitor.Left,hostY,right-monitor.Left,(int)Math.Ceiling(Height*anchor.Scale)):null);}

 }
 TimeSpan lastSlideFrame=TimeSpan.MinValue;
 void Frame(object sender,EventArgs e){var time=((RenderingEventArgs)e).RenderingTime;if(time==lastSlideFrame)return;lastSlideFrame=time;try{UpdateShape(false);}catch(Exception error){NotificationPopup.Failed(this,error);}}
 void StopTracking(){if(tracking){CompositionTarget.Rendering-=Frame;tracking=false;}}
 void AnimateTo(double x,double ms,bool hide){int version=++revision;double from=slide.X;slide.BeginAnimation(TranslateTransform.XProperty,null);slide.X=from;StopTracking();double duration=SystemParameters.ClientAreaAnimation?Motion.Ms(ms):0;
  BeginAnimation(OpacityProperty,null);Opacity=1;
  if(BarAppearance.Settings.notificationAnimation=="fade"){
   slide.X=0;surface.BeginAnimation(OpacityProperty,null);surface.Opacity=1;UpdateShape(true);
   var fade=DisplayCadence.At(this,new DoubleAnimation(hide?1:0,hide?0:1,TimeSpan.FromMilliseconds(duration)));
   fade.Completed+=delegate{Finish(version,hide);};BeginAnimation(OpacityProperty,fade);return;
  }
  if(duration<=0){slide.X=x;Finish(version,hide);return;}
  lastSlideFrame=TimeSpan.MinValue;surface.CacheMode=new BitmapCache{RenderAtScale=anchor.Scale,EnableClearType=true};
  tracking=true;CompositionTarget.Rendering+=Frame;var animation=DisplayCadence.At(this,new DoubleAnimation(from,x,TimeSpan.FromMilliseconds(duration)){EasingFunction=hide?(IEasingFunction)new CubicEase{EasingMode=EasingMode.EaseIn}:Motion.Arrive()});
  animation.Completed+=delegate{Finish(version,hide);};slide.BeginAnimation(TranslateTransform.XProperty,animation);surface.BeginAnimation(OpacityProperty,DisplayCadence.At(this,new DoubleAnimation(surface.Opacity,hide?.65:1,TimeSpan.FromMilliseconds(duration))));
 }
 void Finish(int version,bool hide){if(disposed||version!=revision)return;StopTracking();surface.CacheMode=null;UpdateShape(true);if(hide){Hide();closing=false;NotificationPopup.ClearJoin();}}
 void CheckHover(){if(!IsVisible||disposed){hover.Stop();return;}if(GameWindows.ForegroundGame!=IntPtr.Zero){Dismiss();return;}if((GetAsyncKeyState(0x1B)&0x8000)!=0){Dismiss();return;}POINT p;RECT r;if(!GetCursorPos(out p)||!GetWindowRect(new WindowInteropHelper(this).Handle,out r))return;
  bool down=(GetAsyncKeyState(0x01)&0x8000)!=0;bool clicked=down&&!mouseDown;mouseDown=down;
  bool inDrawer=p.X>=r.Left&&p.X<=r.Right&&p.Y>=r.Top&&p.Y<=r.Bottom;if(clicked&&!inDrawer&&!anchor.Over(p.X,p.Y)){Dismiss();return;}if(pinned||IsKeyboardFocusWithin){outside=DateTime.MinValue;return;}
if(inDrawer)entered=true;
  if(anchor.Over(p.X,p.Y)||inDrawer||!anchor.FromEdge&&!entered&&DateTime.UtcNow-opened<TimeSpan.FromMilliseconds(1800))outside=DateTime.MinValue;else if(outside==DateTime.MinValue)outside=DateTime.UtcNow;else if(DateTime.UtcNow-outside>TimeSpan.FromMilliseconds(300))Dismiss();
 }
 internal void Dismiss(){if(disposed||closing||!IsVisible)return;closing=true;pinned=false;hover.Stop();AnimateTo(-contentWidth,210,true);}
 public void Dispose(){if(disposed)return;disposed=true;++revision;StopTracking();NotificationPopup.ClearJoin();hover.Stop();BarAppearance.Changed-=Appearance;ControlCenterTheme.Current.Changed-=Appearance;FrostMaterial.Rendered-=Appearance;inbox.Changed-=InboxChanged;view.Dispose();slide.BeginAnimation(TranslateTransform.XProperty,null);surface.BeginAnimation(OpacityProperty,null);}
 [StructLayout(LayoutKind.Sequential)]struct POINT{public int X,Y;}[StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out RECT rect);
 [DllImport("user32.dll")]static extern bool GetCursorPos(out POINT point);
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int index);
 [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr h,int index,int value);
}
