using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Shows the bar's tray icons in a box under the tray button. Zebar owns the icons and their
// click actions; this window only displays them and reports which one was clicked. Like the
// power menu it never takes focus, so full-screen apps and the window manager are untouched.
static class TrayPopup {
 public sealed class Icon {public string id {get;set;}public string tooltip {get;set;}public string png {get;set;}}
 // x/y: bottom centre of the tray button; left/top/right/bottom: the button, in screen pixels.
 public sealed class Request {public int x {get;set;}public int y {get;set;}public int left {get;set;}public int top {get;set;}public int right {get;set;}public int bottom {get;set;}public Icon[] icons {get;set;}}
 // Clicks queue up so the box can stay open across several icon interactions.
 sealed class Session {public string id=Guid.NewGuid().ToString("N");public Request request;public readonly ManualResetEvent signal=new ManualResetEvent(false);public readonly System.Collections.Generic.Queue<object> actions=new System.Collections.Generic.Queue<object>();public volatile bool closed;public int waiters;public System.Collections.Generic.HashSet<IntPtr> menusBefore=Menus();public Window window;public long seen=DateTime.UtcNow.Ticks;}
 static Session current;static readonly object gate=new object();

 public static string Open(string json){
  var request=new JavaScriptSerializer{MaxJsonLength=524288}.Deserialize<Request>(json);
  if(request==null||request.icons==null||request.icons.Length>64)throw new Exception("Invalid tray catalog");
  var session=new Session{request=request};
  Application.Current.Dispatcher.Invoke(new Action(()=>{Session old;lock(gate){old=current;current=session;}if(old!=null){Retire(old);if(old.window!=null)old.window.Close();}Show(session);}));
  return new JavaScriptSerializer().Serialize(new{traySession=session.id});
 }
 // Long poll: returns as soon as an icon is clicked or the box closes.
 public static string Wait(string id){Session s;lock(gate){s=current;if(s==null||s.id!=id||s.closed)return "{\"trayClosed\":true}";s.waiters++;}Interlocked.Exchange(ref s.seen,DateTime.UtcNow.Ticks);var deadline=DateTime.UtcNow.AddSeconds(20);
  try{while(true){lock(gate){if(s.closed)return "{\"trayClosed\":true}";if(s.actions.Count>0){var action=s.actions.Dequeue();if(s.actions.Count==0)s.signal.Reset();return new JavaScriptSerializer().Serialize(action);}}var left=deadline-DateTime.UtcNow;if(left<=TimeSpan.Zero||!s.signal.WaitOne(left))return "{\"trayWaiting\":true}";}}
  finally{lock(gate){s.waiters--;if(s.closed&&s.waiters==0)s.signal.Dispose();}}
 }
 static void Retire(Session session){lock(gate){if(session.closed)return;session.closed=true;session.signal.Set();if(session.waiters==0)session.signal.Dispose();}}
 public static void Stop(){Session session;lock(gate){session=current;current=null;}if(session!=null){Retire(session);if(session.window!=null)session.window.Close();}}
 public static string Close(string id){Application.Current.Dispatcher.BeginInvoke(new Action(()=>{Session s;lock(gate)s=current;if(s!=null&&s.id==id&&s.window!=null)s.window.Close();}));return "{\"ok\":true}";}
 static void Complete(Session s,string icon,string kind){
  // This click is the user's latest input, so this process may hand foreground rights on. Without
  // them the tray app's menu cannot take focus, and clicking elsewhere would never dismiss it.
  AllowSetForegroundWindow(-1);
 lock(gate){if(s.closed)return;s.actions.Enqueue(new{trayAction=new{id=icon,kind=kind}});s.signal.Set();}}

 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
 static FrameworkElement Content(Icon[] icons,Action<string,string> action){
  var panel=new StackPanel();
  if(icons.Length==0)panel.Children.Add(new TextBlock{Text="No tray icons",FontSize=12,Foreground=B("Muted"),Margin=new Thickness(6,4,6,4)});
  var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=Math.Max(1,Math.Min(6,icons.Length))};
  foreach(var item in icons){
   var face=new Border{Width=34,Height=34,CornerRadius=new CornerRadius(9),Background=Brushes.Transparent,Cursor=Cursors.Hand,ToolTip=String.IsNullOrWhiteSpace(item.tooltip)?"Tray app":item.tooltip};
   try{var bytes=Convert.FromBase64String(item.png??"");using(var stream=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.DecodePixelWidth=40;image.EndInit();image.Freeze();face.Child=new Image{Source=image,Width=18,Height=18};}}
   catch{face.Child=new TextBlock{Text="•",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};}
   string id=item.id;
   face.MouseEnter+=delegate{face.Background=B("Hover");};face.MouseLeave+=delegate{face.Background=Brushes.Transparent;};
   face.MouseUp+=delegate(object sender,MouseButtonEventArgs e){e.Handled=true;action(id,e.ChangedButton==MouseButton.Right?"right":e.ChangedButton==MouseButton.Middle?"middle":"left");};
   grid.Children.Add(face);
  }
  if(icons.Length>0)panel.Children.Add(new ScrollViewer{Content=grid,MaxHeight=320,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
  return new Border{Child=panel,Padding=new Thickness(6),CornerRadius=new CornerRadius(14),Background=B("Surface"),BorderBrush=B("Border"),BorderThickness=new Thickness(1)};
 }

 static void Show(Session session){
  var r=session.request;
  var w=new Window{Title="Nord Tray",SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowInTaskbar=false,ShowActivated=false,Opacity=0,
   FontFamily=RiceFont.Current,Foreground=B("Text")};
  session.window=w;
  if(Application.Current.MainWindow!=null)w.Resources=Application.Current.MainWindow.Resources;
  w.Content=Content(r.icons,(id,kind)=>Complete(session,id,kind));Roundness.Apply(w.Content as DependencyObject,BarAppearance.Settings.radius/100.0);
  w.SourceInitialized+=delegate{var hwnd=new WindowInteropHelper(w).Handle;SetWindowLongPtr(hwnd,-20,new IntPtr(GetWindowLongPtr(hwnd,-20).ToInt64()|0x08000000L|0x80L));};
  // Close on a click outside the box (except on the tray button, which toggles it itself) or Escape.
  bool wasDown=true;
  var watch=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
  watch.Tick+=delegate{
   if((GetAsyncKeyState(0x1B)&0x8000)!=0){w.Close();return;}
   if(DateTime.UtcNow.Ticks-Interlocked.Read(ref session.seen)>TimeSpan.FromSeconds(45).Ticks){w.Close();return;}
   bool down=(GetAsyncKeyState(1)&0x8000)!=0||(GetAsyncKeyState(2)&0x8000)!=0||(GetAsyncKeyState(4)&0x8000)!=0;
   if(down&&!wasDown){POINT p;GetCursorPos(out p);RECT box;GetWindowRect(new WindowInteropHelper(w).Handle,out box);
    bool inBox=p.x>=box.left&&p.x<box.right&&p.y>=box.top&&p.y<box.bottom,onButton=p.x>=r.left&&p.x<r.right&&p.y>=r.top&&p.y<r.bottom;
    if(!inBox&&!onButton&&!IsMenu(p))w.Close();}
   wasDown=down;
  };
  w.Closed+=delegate{watch.Stop();Retire(session);CloseMenus(session.menusBefore);};
  w.Show();
  // Move onto the target monitor first so WPF settles that monitor's DPI, then centre under the button.
  var handle=new WindowInteropHelper(w).Handle;
  SetWindowPos(handle,new IntPtr(-1),r.x,r.y+6,0,0,0x0001|0x0010);
  w.UpdateLayout();
  RECT now;GetWindowRect(handle,out now);int width=now.right-now.left,height=now.bottom-now.top;
  var area=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(r.x,r.y)).Bounds;
  int x=Math.Max(area.Left+4,Math.Min(r.x-width/2,area.Right-width-4));
  SetWindowPos(handle,new IntPtr(-1),x,r.y+6,0,0,0x0001|0x0010);
  w.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,Motion.Time(110)));
  watch.Start();
 }
 // A tray app's right-click menu is part of using the tray, so clicks in it keep the box open.
 static bool IsMenu(POINT p){var root=GetAncestor(WindowFromPoint(p),2);var name=new System.Text.StringBuilder(64);GetClassName(root,name,64);return name.ToString()=="#32768";}
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(POINT point);
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,System.Text.StringBuilder name,int count);
 // Menus opened from the box close with it. Escape pops one menu level, and each level is its own window.
 static System.Collections.Generic.HashSet<IntPtr> Menus(){var found=new System.Collections.Generic.HashSet<IntPtr>();EnumWindows((h,l)=>{if(IsWindowVisible(h)){var name=new System.Text.StringBuilder(16);GetClassName(h,name,16);if(name.ToString()=="#32768")found.Add(h);}return true;},IntPtr.Zero);return found;}
 static void CloseMenus(System.Collections.Generic.HashSet<IntPtr> before){foreach(var menu in Menus())if(!before.Contains(menu)){PostMessage(menu,0x100,new IntPtr(0x1B),new IntPtr(0x00010001));PostMessage(menu,0x101,new IntPtr(0x1B),new IntPtr(unchecked((int)0xC0010001)));}}
 delegate bool EnumProc(IntPtr window,IntPtr parameter);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(int process);
 struct POINT{public int x,y;} struct RECT{public int left,top,right,bottom;}
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out RECT rect);
 [DllImport("user32.dll")]static extern bool GetCursorPos(out POINT point);
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")]static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
 [DllImport("user32.dll")]static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
}
