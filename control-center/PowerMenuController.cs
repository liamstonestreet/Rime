using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Screen=System.Windows.Forms.Screen;

sealed class PowerMenuController:IDisposable {
 readonly PowerActionDispatcher power;volatile bool disposed;System.Windows.Threading.Dispatcher workerDispatcher;
 System.Windows.Threading.DispatcherTimer dismissTimer,checkTimer;
 public PowerMenuController(IPowerActions actions){power=new PowerActionDispatcher(actions);}

 readonly List<Window> windows=new List<Window>();
 readonly Dictionary<BitmapSource,BitmapSource> blurred=new Dictionary<BitmapSource,BitmapSource>();
 public volatile bool IsOpen;bool standalone;
 readonly List<UIElement> fades=new List<UIElement>();
 bool dismissing,preview;
 const double Duration=220;
 [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
 [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 System.Windows.Threading.DispatcherTimer escapeTimer;
 IntPtr previousForeground;
 PowerColors themeColors;
 sealed class PowerColors:IDisposable {
  volatile bool disposed;
  public bool CheckAccess {get{return dispatcher.CheckAccess();}}
  readonly Dictionary<string,SolidColorBrush> brushes=new Dictionary<string,SolidColorBrush>();
  readonly System.Windows.Threading.Dispatcher dispatcher=System.Windows.Threading.Dispatcher.CurrentDispatcher;
  readonly Action frame;
  public PowerColors(){frame=()=>{if(disposed)return;if(!dispatcher.Thread.IsAlive){Dispose();return;}if(!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(new Action(Paint));};ThemeTransition.Frame+=frame;}
  public Brush Get(string hex){SolidColorBrush brush;if(!brushes.TryGetValue(hex,out brush)){brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));brushes[hex]=brush;}Paint();return brush;}
  void Paint(){
   if(disposed)return;
   var data=ThemeTransition.Current;double t=ThemeTransition.Position;
   foreach(var pair in brushes){var original=(Color)ColorConverter.ConvertFromString(pair.Key);string rgb=pair.Key.Substring(pair.Key.Length-6).ToLowerInvariant(),role=null;
    switch(rgb){case "eceff4":role="--nord-6";break;case "88c0d0":role="--nord-8";break;case "1b202b":role="--shade";break;case "2e3440":role="--nord-0";break;case "7b8c9f":role="--edge";break;case "434c5e":role="--nord-2";break;}
    if(role==null)continue;var fallback=original.ToString();var c=data==null?WallpaperPalette.Get(BarAppearance.Palette,role,fallback):ThemeTransition.Mix(WallpaperPalette.Get(data.from,role,fallback),WallpaperPalette.Get(data.to,role,fallback),t);
    pair.Value.Color=Color.FromArgb(original.A,c.R,c.G,c.B);
   }
  }
  public void Dispose(){disposed=true;ThemeTransition.Frame-=frame;}
 }

 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int left,top,right,bottom;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MonitorInfo {public int size;public NativeRect monitor,work;public uint flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string device;}
 delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,IntPtr rect,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
 public sealed class Display {public System.Drawing.Rectangle Bounds;public string DeviceName;}
 public Display[] Displays(){var result=new List<Display>();var old=SetThreadDpiAwarenessContext(new IntPtr(-4));try{EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr h,IntPtr dc,IntPtr r,IntPtr data){var info=new MonitorInfo{size=Marshal.SizeOf(typeof(MonitorInfo))};if(GetMonitorInfo(h,ref info))result.Add(new Display{DeviceName=info.device,Bounds=System.Drawing.Rectangle.FromLTRB(info.monitor.left,info.monitor.top,info.monitor.right,info.monitor.bottom)});return true;},IntPtr.Zero);}finally{SetThreadDpiAwarenessContext(old);}return result.ToArray();}
 readonly Dictionary<Window,Display> displaysByWindow=new Dictionary<Window,Display>();
 // All overlay HWNDs are created and positioned on the same PMv2 STA thread.
 // SetWindowPos owns geometry; WPF sizes its content from the native client size.
 void Place(Window window,Display display){var r=display.Bounds;var hwnd=new WindowInteropHelper(window).Handle;SetWindowPos(hwnd,new IntPtr(-1),r.X,r.Y,r.Width,r.Height,0x0010);}
 Brush B(string value){if(themeColors!=null&&!themeColors.CheckAccess){themeColors.Dispose();themeColors=null;}if(themeColors==null)themeColors=new PowerColors();return themeColors.Get(value);}

 BitmapSource Capture(Display screen){var r=screen.Bounds;using(var full=new System.Drawing.Bitmap(r.Width,r.Height)){using(var g=System.Drawing.Graphics.FromImage(full))g.CopyFromScreen(r.Left,r.Top,0,0,r.Size);using(var small=new System.Drawing.Bitmap(full,new System.Drawing.Size(Math.Max(1,r.Width/2),Math.Max(1,r.Height/2)))){var h=small.GetHbitmap();try{var image=Imaging.CreateBitmapSourceFromHBitmap(h,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;}finally{DeleteObject(h);}}}}
 void Animate(UIElement target,DependencyProperty property,double from,double to,double ms){var a=new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(ms)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};target.BeginAnimation(property,a);}
 BitmapSource Preblur(BitmapSource source){
  // Render blur once at one-eighth screen resolution, then animate only cached image opacity.
  int width=Math.Max(1,source.PixelWidth/4),height=Math.Max(1,source.PixelHeight/4);
  var image=new Image{Source=source,Width=width,Height=height,Stretch=Stretch.Fill,Effect=new BlurEffect{Radius=4,RenderingBias=RenderingBias.Performance}};
  image.Measure(new Size(width,height));image.Arrange(new Rect(0,0,width,height));image.UpdateLayout();
  var result=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);result.Render(image);result.Freeze();return result;
 }
 public void Open(int x,int y,bool test=false){
  if(disposed)return;
  if(IsOpen)return;IsOpen=true;dismissing=false;preview=test;windows.Clear();displaysByWindow.Clear();fades.Clear();blurred.Clear();
  previousForeground=GetForegroundWindow();
  var dispatcher=Application.Current.Dispatcher;
  var worker=new Thread(delegate(){workerDispatcher=System.Windows.Threading.Dispatcher.CurrentDispatcher;try{if(disposed){Cleanup();return;}SetThreadDpiAwarenessContext(new IntPtr(-4));var watch=Stopwatch.StartNew();var screens=Displays();var chosen=Array.Find(screens,d=>d.Bounds.Contains(x,y))??screens[0];var captures=new Dictionary<string,BitmapSource>();var soft=new Dictionary<BitmapSource,BitmapSource>();
   foreach(var screen in screens){if(test)File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"power-timing.txt"),"capture start\n");var captured=Capture(screen);if(test)File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"power-timing.txt"),"capture done\n");captures[screen.DeviceName]=captured;soft[captured]=Preblur(captured);if(test)File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"power-timing.txt"),"blur done\n");}
   long preparation=watch.ElapsedMilliseconds;if(disposed){Cleanup();return;}
   try{
    foreach(var pair in soft)blurred[pair.Key]=pair.Value;
    foreach(var screen in screens)windows.Add(Create(screen,captures[screen.DeviceName],screen.DeviceName==chosen.DeviceName));
    foreach(var window in windows){if(disposed){Cleanup();return;}window.Show();Place(window,displaysByWindow[window]);}
    // Never activate these overlays: activation can minimize fullscreen apps
    // and makes the shell/window manager react on the other monitor.
    escapeTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};
    escapeTimer.Tick+=delegate{if((GetAsyncKeyState(0x1B)&0x8000)!=0)Dismiss(null);};escapeTimer.Start();
    foreach(var fade in fades)Animate(fade,UIElement.OpacityProperty,0,1,Duration);
    if(test){var check=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(800)};checkTimer=check;check.Tick+=delegate{check.Stop();File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"power-smoke-test.txt"),"Opened "+windows.Count+" overlays; backdrop preparation "+preparation+" ms. "+VerifyBounds()+" Preview only.");Dismiss(null);};check.Start();}
   }catch(Exception ex){Cleanup();File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"power-error.txt"),ex.ToString());if(!test&&!disposed)MessageBox.Show(ex.Message,"Power menu");if(standalone)dispatcher.BeginInvoke(new Action(delegate{Application.Current.Shutdown();}));return;}
   System.Windows.Threading.Dispatcher.Run();
  }catch(Exception ex){dispatcher.BeginInvoke(new Action(delegate{IsOpen=false;File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"power-error.txt"),ex.ToString());if(!test&&!disposed)MessageBox.Show(ex.Message,"Power menu");if(standalone)Application.Current.Shutdown();}));}});
  worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
 }
 public void Run(string[] args){bool created;using(var mutex=new Mutex(true,"Local\\NordPowerMenu",out created)){if(!created)return;standalone=true;SetProcessDPIAware();var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Exit+=delegate{Dispose();};int x=0,y=0;if(args.Length>=3){int.TryParse(args[1],out x);int.TryParse(args[2],out y);}Open(x,y,Array.IndexOf(args,"--preview")>=0||Array.IndexOf(args,"--smoke-test")>=0);app.Run();}}
 internal Window Create(Display screen,BitmapSource backdrop,bool controls){
  if(disposed)throw new ObjectDisposedException("PowerMenuController");
  var window=new Window{Title="Nord Power Menu",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Background=Brushes.Black,Foreground=B("#ECEFF4"),FontFamily=RiceFont.Current,Tag=controls};
  displaysByWindow[window]=screen;
  window.SourceInitialized+=delegate{
   var hwnd=new WindowInteropHelper(window).Handle;
   SetWindowLongPtr(hwnd,-20,new IntPtr(GetWindowLongPtr(hwnd,-20).ToInt64()|0x08000000L|0x80L));
   int disabled=1;DwmSetWindowAttribute(hwnd,3,ref disabled,4);
   // Position while still hidden, so the first visible frame is full-screen.
   Place(window,screen);
  };
  var root=new Grid();window.Content=root;
  root.Children.Add(new Image{Source=backdrop,Stretch=Stretch.Fill,IsHitTestVisible=false});
  BitmapSource soft;if(!blurred.TryGetValue(backdrop,out soft))soft=Preblur(backdrop);
  var blurLayer=new Image{Source=soft,Stretch=Stretch.Fill,IsHitTestVisible=false,Opacity=0};RenderOptions.SetBitmapScalingMode(blurLayer,BitmapScalingMode.Linear);root.Children.Add(blurLayer);fades.Add(blurLayer);
  var dim=new Border{Background=B("#B51B202B"),Opacity=0};root.Children.Add(dim);fades.Add(dim);
  root.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.OriginalSource==dim||e.OriginalSource==root)Dismiss(null);};
  window.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;Dismiss(null);}};
  if(controls){var row=new StackPanel{Orientation=Orientation.Vertical};var primary=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};var secondary=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};row.Children.Add(primary);row.Children.Add(secondary);var fit=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(40),Child=row,Opacity=0};root.Children.Add(fit);fades.Add(fit);
   string[] shapes={"M12,2 L12,11 M6,5 A9,9 0 1 0 18,5","M20,8 A9,9 0 1 0 21,15 M20,2 L20,8 L14,8","M20,15 A9,9 0 0 1 9,3 A9,9 0 1 0 20,15","M3,4 L21,4 L21,18 L3,18 Z M8,22 L16,22 M12,18 L12,22","M6,11 L18,11 L18,22 L6,22 Z M8,11 L8,7 A4,4 0 0 1 16,7 L16,11 M12,15 L12,18"};
   for(int i=0;i<PowerMenu.Actions.Length;i++){string action=PowerMenu.Actions[i];var content=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};content.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(shapes[i]),Stroke=B(i==0?"#BF8A91":"#88C0D0"),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=46,Height=46,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center});content.Children.Add(new TextBlock{Text=action,FontSize=17,Margin=new Thickness(0,22,0,0),HorizontalAlignment=HorizontalAlignment.Center});
    var button=new Button{Content=content,Width=166,Height=166,Margin=new Thickness(9),Background=B("#E62E3440"),BorderBrush=B("#607B8C9F"),BorderThickness=new Thickness(1),Cursor=Cursors.Hand,Foreground=window.Foreground,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(1,1)};
    var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(22));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});border.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding("BorderBrush"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});border.SetValue(Border.BorderThicknessProperty,new Thickness(1));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);button.Template=new ControlTemplate(typeof(Button)){VisualTree=border};
    button.MouseEnter+=delegate{button.Background=B("#F0434C5E");Scale(button,1.035);};button.MouseLeave+=delegate{button.Background=B("#E62E3440");Scale(button,1);};button.Click+=delegate{Dismiss(action);};System.Windows.Automation.AutomationProperties.SetName(button,action);(i<3?primary:secondary).Children.Add(button);
   }
  }return window;
 }
 string VerifyBounds(){var report=new System.Text.StringBuilder();foreach(var window in windows){NativeRect actual;GetWindowRect(new WindowInteropHelper(window).Handle,out actual);var expected=displaysByWindow[window].Bounds;bool match=actual.left==expected.X&&actual.top==expected.Y&&actual.right-actual.left==expected.Width&&actual.bottom-actual.top==expected.Height;report.Append(displaysByWindow[window].DeviceName+": "+(match?"PASS":"FAIL")+" at "+actual.left+","+actual.top+" "+(actual.right-actual.left)+"x"+(actual.bottom-actual.top)+" expected "+expected.Width+"x"+expected.Height+"; rendered "+window.ActualWidth+"x"+window.ActualHeight+" DIPs, scale "+PresentationSource.FromVisual(window).CompositionTarget.TransformToDevice.M11+"; ");}report.Append("Foreground preserved: "+(GetForegroundWindow()==previousForeground)+"; ");return report.ToString();}
 void Scale(Button button,double target){var transform=(ScaleTransform)button.RenderTransform;var a=new DoubleAnimation(target,TimeSpan.FromMilliseconds(110)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};transform.BeginAnimation(ScaleTransform.ScaleXProperty,a);transform.BeginAnimation(ScaleTransform.ScaleYProperty,a);}
 void Cleanup(){
  if(escapeTimer!=null){escapeTimer.Stop();escapeTimer=null;}if(dismissTimer!=null){dismissTimer.Stop();dismissTimer=null;}if(checkTimer!=null){checkTimer.Stop();checkTimer=null;}
  if(themeColors!=null){themeColors.Dispose();themeColors=null;}
  foreach(var window in new List<Window>(displaysByWindow.Keys))try{window.Close();}catch{}
  windows.Clear();displaysByWindow.Clear();fades.Clear();blurred.Clear();IsOpen=false;
 }
 void Dismiss(string action){if(dismissing||disposed)return;dismissing=true;if(escapeTimer!=null){escapeTimer.Stop();escapeTimer=null;}foreach(var fade in fades)Animate(fade,UIElement.OpacityProperty,fade.Opacity,0,160);
  dismissTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(170)};dismissTimer.Tick+=delegate{Cleanup();try{if(!disposed)power.Execute(action,preview);}catch(Exception ex){MessageBox.Show(ex.Message,"Power action failed",MessageBoxButton.OK,MessageBoxImage.Error);}System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);if(standalone)Application.Current.Dispatcher.BeginInvoke(new Action(()=>Application.Current.Shutdown()));};dismissTimer.Start();
 }
 public void Dispose(){if(disposed)return;disposed=true;IsOpen=false;var dispatcher=workerDispatcher;if(dispatcher!=null&&!dispatcher.HasShutdownStarted&&!dispatcher.CheckAccess()){dispatcher.BeginInvoke(new Action(()=>{Cleanup();dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);}));}else Cleanup();}
 internal void RunAction(string action,bool test){if(!disposed)power.Execute(action,test);}

}
