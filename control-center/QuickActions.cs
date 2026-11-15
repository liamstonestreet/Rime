using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

static class QuickActions {
 public static string Voice(string action){if(action!="mute"&&action!="status")return "{}";Application.Current.Dispatcher.BeginInvoke(new Action(()=>VoiceAsync(action)));return "{}";}
 static readonly System.Threading.SemaphoreSlim voiceGate=new System.Threading.SemaphoreSlim(1);
 static DateTime lastVoiceCheck;
 static async void VoiceAsync(string action){if(action=="status"){if(DateTime.UtcNow-lastVoiceCheck<TimeSpan.FromMilliseconds(1500)||!voiceGate.Wait(0))return;lastVoiceCheck=DateTime.UtcNow;}else await voiceGate.WaitAsync();try{var state=await DiscordVoice.Run(action);if(action=="mute"&&state.available){await Task.Delay(200);var confirmed=await DiscordVoice.Run("status");if(confirmed.available)state=confirmed;}lastVoiceCheck=DateTime.UtcNow;MediaBridge.Broadcast(Json(new{discordVoice=state}));if(action=="mute")MediaBridge.Broadcast(Json(new{quickAction=new{action="discord",ok=state.available,error=state.error}}));}finally{voiceGate.Release();}}
 internal static Action<string> Launch=value=>Process.Start(new ProcessStartInfo(value){UseShellExecute=true});
 internal static string ReflectionFolder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@"Repos\ObsidianBackup\Reflection");}}
 internal static string Reflection(string folder,DateTime date){
  Directory.CreateDirectory(folder);string path=Path.Combine(folder,date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+".md");
  try{using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){} }
  catch(IOException){if(!File.Exists(path))throw;}
  return path;
 }
 internal static string NoteUri(string path){return "obsidian://open?path="+Uri.EscapeDataString(Path.GetFullPath(path));}
 public static string Receive(string action){
  try{
   var menu=popup;if(menu!=null)Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(popup==menu)menu.Close();}));
   if(action=="reflection")Launch(NoteUri(Reflection(ReflectionFolder,DateTime.Now)));
   else if(action=="github")Launch("https://github.com/");
   else throw new ArgumentException("Unknown quick action.");
   return Json(new{quickAction=new{action,ok=true}});
  }catch(Exception e){return Json(new{quickAction=new{action,ok=false,error=action=="reflection"?"Could not create/open the reflection note: "+e.Message:"Could not open GitHub: "+e.Message}});}
 }
 public sealed class Anchor {public int left {get;set;}public int top {get;set;}public int right {get;set;}public int bottom {get;set;}}
 static Window popup;static Anchor anchor;static TextBlock status;static Button mute,deafen;
 static Task<DiscordVoiceState> reading;static bool updating;
 const string MicShape="M9,4 C9,0 15,0 15,4 L15,11 C15,15 9,15 9,11 Z M5,10 L5,11 C5,20 19,20 19,11 L19,10 M12,18 L12,22 M8,22 L16,22";
 const string DeafShape="M3,14 L3,11 C3,-1 21,-1 21,11 L21,14 M3,12 L7,12 L7,21 L3,21 Z M17,12 L21,12 L21,21 L17,21 Z";
 internal static string VoiceLabel(bool microphone,bool disabled){return microphone?(disabled?"Unmute microphone":"Mute microphone"):(disabled?"Undeafen audio":"Deafen audio");}
 internal static string VoiceShape(bool microphone,bool disabled){return (microphone?MicShape:DeafShape)+(disabled?" M2,2 L22,22":"");}
 static long menuRevision;
 static void MenuState(bool open,Anchor at){MediaBridge.Broadcast(Json(new{discordMenu=new{open,left=at.left,top=at.top,sequence=System.Threading.Interlocked.Increment(ref menuRevision)}}));}
 public static void Stop(){var window=popup;popup=null;if(window!=null)window.Close();reading=null;mute=deafen=null;status=null;}
 public static string Close(){Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(popup!=null)popup.Close();}));return "{}";}
 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
 static string Json(object value){return new JavaScriptSerializer().Serialize(value);}
 public static string Open(string json){
  try{var r=new JavaScriptSerializer().Deserialize<Anchor>(json);if(r==null||r.right<=r.left||r.bottom<=r.top||r.right-r.left>200||r.bottom-r.top>200)throw new ArgumentException();
   Application.Current.Dispatcher.BeginInvoke(new Action(()=>Show(r)));return "{}";
  }catch{return Json(new{quickAction=new{action="discord",ok=false,error="Could not place the Discord menu."}});}
 }
 static async void Show(Anchor r){
  if(popup!=null){if(anchor.left==r.left&&anchor.top==r.top&&anchor.right==r.right&&anchor.bottom==r.bottom){MenuState(true,r);return;}popup.Close();}anchor=r;
  var w=new Window{Title="Nord Discord Controls",SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowInTaskbar=false,ShowActivated=false,Opacity=0,FontFamily=RiceFont.Current,Foreground=B("Text"),FontSize=12};popup=w;
  if(Application.Current.MainWindow!=null)w.Resources=Application.Current.MainWindow.Resources;
  var stack=new StackPanel{MinWidth=188,MaxWidth=248};
  stack.Children.Add(new TextBlock{Text="DISCORD",Foreground=B("Muted"),FontSize=10,Margin=new Thickness(9,5,9,7)});
  mute=Row("Mute microphone",MicShape,()=>Toggle(w,"mute"));deafen=Row("Deafen audio",DeafShape,()=>Toggle(w,"deafen"));stack.Children.Add(mute);stack.Children.Add(deafen);
  status=new TextBlock{Text="Checking Discord…",Foreground=B("Muted"),FontSize=10,Margin=new Thickness(9,7,9,5),TextWrapping=TextWrapping.Wrap};stack.Children.Add(status);
  w.Content=new Border{Child=stack,Padding=new Thickness(5),CornerRadius=new CornerRadius(14),Background=B("Surface"),BorderBrush=B("Border"),BorderThickness=new Thickness(1)};
  Roundness.Apply(w.Content as DependencyObject,BarAppearance.Settings.radius/100.0);
  w.SourceInitialized+=delegate{var h=new WindowInteropHelper(w).Handle;SetWindowLongPtr(h,-20,new IntPtr(GetWindowLongPtr(h,-20).ToInt64()|0x08000000L|0x80L));};
  var watch=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(35)};DateTime outside=DateTime.MinValue;bool wasDown=false;
  watch.Tick+=delegate{
   if((GetAsyncKeyState(0x1B)&0x8000)!=0){w.Close();return;}POINT p;GetCursorPos(out p);RECT box;GetWindowRect(new WindowInteropHelper(w).Handle,out box);
   bool over=Inside(p,box.left,box.top,box.right,box.bottom)||Inside(p,anchor.left,anchor.top,anchor.right,anchor.bottom)||Inside(p,anchor.left-4,anchor.bottom,anchor.right+4,box.top+4);
   bool down=(GetAsyncKeyState(1)&0x8000)!=0||(GetAsyncKeyState(2)&0x8000)!=0;
   if(over)outside=DateTime.MinValue;else if(outside==DateTime.MinValue)outside=DateTime.UtcNow;
   if(!over&&(down&&!wasDown||outside!=DateTime.MinValue&&(DateTime.UtcNow-outside).TotalMilliseconds>260))w.Close();wasDown=down;
  };
  var refresh=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(750)};refresh.Tick+=async delegate{await RefreshVoice(w);};
  w.Closed+=delegate{watch.Stop();refresh.Stop();if(popup==w)popup=null;MenuState(false,r);};
  w.Show();var handle=new WindowInteropHelper(w).Handle;SetWindowPos(handle,new IntPtr(-1),r.left,r.bottom+6,0,0,0x0011);w.UpdateLayout();RECT size;GetWindowRect(handle,out size);var area=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(r.left,r.top)).Bounds;
  int x=Math.Max(area.Left+4,Math.Min((r.left+r.right-(size.right-size.left))/2,area.Right-(size.right-size.left)-4));int y=Math.Max(area.Top+4,Math.Min(r.bottom+6,area.Bottom-(size.bottom-size.top)-4));SetWindowPos(handle,new IntPtr(-1),x,y,0,0,0x0011);
  w.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,Motion.Time(110)));watch.Start();
  MenuState(true,r);
  refresh.Start();await RefreshVoice(w);
 }
 static Button Row(string label,string icon,Action action){
  var panel=new StackPanel{Orientation=Orientation.Horizontal};panel.Children.Add(new Border{Width=23,Child=new System.Windows.Shapes.Path{Data=Geometry.Parse(icon),Stroke=B("Accent"),StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=14,Height=17,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center}});panel.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});
  var button=new Button{Content=panel,Background=Brushes.Transparent,Foreground=B("Text"),BorderThickness=new Thickness(0),Padding=new Thickness(9,9,9,9),HorizontalContentAlignment=HorizontalAlignment.Stretch,Cursor=Cursors.Hand,IsEnabled=false};
  button.MouseEnter+=delegate{button.Background=B("Hover");};button.MouseLeave+=delegate{button.Background=Brushes.Transparent;};button.Click+=delegate{action();};return button;
 }
 static async Task RefreshVoice(Window window){if(updating||reading!=null&&!reading.IsCompleted)return;var task=DiscordVoice.Run("status");reading=task;var state=await task;if(popup==window&&!updating)Paint(state);}
 static async void Toggle(Window window,string action){
  if(updating)return;updating=true;mute.IsEnabled=deafen.IsEnabled=false;status.Text="Updating Discord…";
  try{var pending=reading;if(pending!=null)await pending;if(popup!=window)return;var state=await DiscordVoice.Run(action);if(popup==window)Paint(state);}finally{updating=false;}
 }
 static void Paint(DiscordVoiceState state){
  mute.IsEnabled=deafen.IsEnabled=state.available;
  PaintVoiceRow(mute,true,state.muted==true);PaintVoiceRow(deafen,false,state.deafened==true);
  status.Text=state.error??"Discord's own voice controls";MediaBridge.Broadcast(Json(new{discordVoice=state}));
 }
 internal static void PaintVoiceRow(Button button,bool microphone,bool disabled){
  var row=(StackPanel)button.Content;((TextBlock)row.Children[1]).Text=VoiceLabel(microphone,disabled);
  var icon=(System.Windows.Shapes.Path)((Border)row.Children[0]).Child;icon.Data=Geometry.Parse(VoiceShape(microphone,disabled));icon.Stroke=B(disabled?"Error":"Accent");
 }
 static bool Inside(POINT p,int l,int t,int r,int b){return p.x>=l&&p.x<r&&p.y>=t&&p.y<b;}
 struct POINT{public int x,y;}struct RECT{public int left,top,right,bottom;}
 [DllImport("user32.dll")]static extern bool GetCursorPos(out POINT p);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll")]static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
}
