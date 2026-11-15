using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

/// <summary>wallpapers.json beside the helper: the image folder, the next/previous hotkeys and the hold key.</summary>
sealed class WallpaperSettings {
 public string folder {get;set;}
 public string next {get;set;}
 public string previous {get;set;}
 public string hold {get;set;}     // legacy field; indicator-keys.json now owns the hold key
 // Not a property: the JSON reader matches names case-insensitively and would bind "folder" to it.
 public string ImageFolder(){var path=Environment.ExpandEnvironmentVariables(folder??"");return Path.IsPathRooted(path)?path:Path.GetFullPath(Path.Combine(AppCatalog.Folder,path));}
 public static void SetFolder(string folder){
  var full=Path.GetFullPath(Environment.ExpandEnvironmentVariables(folder));if(!Directory.Exists(full))throw new DirectoryNotFoundException("Choose an existing wallpaper folder.");
  var path=Path.Combine(AppCatalog.Folder,"wallpapers.json");var serializer=new JavaScriptSerializer();var value=File.Exists(path)?serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path)):new Dictionary<string,object>();
  value["folder"]=full;var temp=path+".tmp";File.WriteAllText(temp,serializer.Serialize(value));if(File.Exists(path))File.Replace(temp,path,path+".previous");else File.Move(temp,path);
 }

 public static WallpaperSettings Load(){
  WallpaperSettings s=null;
  try{s=new JavaScriptSerializer().Deserialize<WallpaperSettings>(File.ReadAllText(Path.Combine(AppCatalog.Folder,"wallpapers.json")));}catch{}
  s=s??new WallpaperSettings();
  if(String.IsNullOrWhiteSpace(s.folder))s.folder=@"%USERPROFILE%\Pictures\Wallpapers";
  if(String.IsNullOrWhiteSpace(s.next))s.next="win+]";
  if(String.IsNullOrWhiteSpace(s.previous))s.previous="win+[";
  if(String.IsNullOrWhiteSpace(s.hold))s.hold="rightctrl";
  return s;
 }
}

/// <summary>
/// Cycles the Windows wallpaper through a folder of images. Tap the hotkeys to move the
/// <see cref="WallpaperPicker"/> (the choice applies once the presses stop), or hold the hold key and
/// use [ ] (the choice applies on release).
/// </summary>
static class WallpaperSwitcher {
 static bool started;static WallpaperPicker picker;static HoldKey hold;static IDisposable keyWatcher;
 static readonly string[] Types={".jpg",".jpeg",".png",".bmp"};

 public static void Start(){
  if(started)return;started=true;var s=WallpaperSettings.Load();
  if(!GlobalHotkeys.Register(s.next,()=>Step(1)))Log("hotkey unavailable: "+s.next);
  if(!GlobalHotkeys.Register(s.previous,()=>Step(-1)))Log("hotkey unavailable: "+s.previous);
  // Build the strip once the UI is running, so the first open is as instant as the rest. A failure here
  // must never take the control center down with it.
  Application.Current.Dispatcher.BeginInvoke(new Action(()=>{
   try{if(!started)return;var library=Images(s.ImageFolder());if(library.Count>0){if(picker==null)picker=new WallpaperPicker();picker.Warm(library,WallpaperSource.Windows());}}
   catch(Exception e){Log("warm-up failed: "+e.Message);}
  }),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
  hold=new HoldKey(IndicatorKeys.Code("rightctrl"));
  hold.CopySelected=async ()=>{if(picker!=null)await picker.CopySelected();};
  hold.CategoryStep=delta=>{if(picker!=null)picker.ChangeCategory(delta);};
  hold.Held=()=>Open((p,images,current)=>p.Peek(images,current));hold.Step=Step;
  hold.Released=()=>{if(picker!=null)picker.Release();};hold.Cancelled=()=>{if(picker!=null)picker.Cancel();};hold.Start();
  keyWatcher=IndicatorKeys.Watch(ApplyHoldKey);
 }
 public static void Stop(){started=false;if(keyWatcher!=null){keyWatcher.Dispose();keyWatcher=null;}if(hold!=null){hold.Dispose();hold=null;}GlobalHotkeys.UnregisterAll();if(picker!=null){try{picker.Cancel();picker.Close();}catch{}picker=null;}}
 public static void ApplyHoldKey(IndicatorKeySettings settings){if(hold!=null)hold.Rebind(IndicatorKeys.Code(settings.wallpaper));}

 static void Step(int delta){if(picker!=null&&picker.IsBrowsing){picker.Step(null,null,delta);return;}Open((p,images,current)=>p.Step(images,current,delta));}
 static void Open(Action<WallpaperPicker,List<string>,string> use){
  var folder=WallpaperSettings.Load().ImageFolder();var images=Images(folder);
  if(images.Count==0){Log("no images in "+folder);return;}
  if(picker==null)picker=new WallpaperPicker();
  use(picker,images,WallpaperSource.Windows());
 }
 /// <summary>The folder's images: numbered files in numeric order (1, 2, … 10), then the rest by name.</summary>
 public static List<string> Images(string folder){return WallpaperLibrary.Scan(folder)[0].Images;}
 public static void Log(string message){
  try{var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper.log");
   if(File.Exists(path)&&new FileInfo(path).Length>65536)File.Delete(path);
   File.AppendAllText(path,DateTime.Now.ToString("s")+" "+message+Environment.NewLine);}catch{}
 }
}

/// <summary>
/// Sets the wallpaper with a fade. A picture of the new wallpaper, laid out exactly as Windows draws it,
/// fades in over the desktop (below every window); only once it is fully opaque does Windows apply the
/// real wallpaper underneath (about a second for a large photo, and setting it mid-fade would cut the
/// fade short), and the picture is then removed, which is invisible. Pictures are prepared while the
/// user browses, so the fade starts as soon as they stop.
/// </summary>
static class DesktopWallpaper {
 static readonly WallpaperCoordinator coordinator=new WallpaperCoordinator(new WindowsDesktopWallpaperRenderer());
 public static Task Prepare(string path){return coordinator.Prepare(path);}
 public static Task Apply(string path){return coordinator.Apply(path);}
 internal static Task ClearPrepared(){return coordinator.ClearPrepared();}
 internal static void DiscardUnused(){coordinator.DiscardUnused();}
 public static void Stop(){coordinator.Dispose();}
 internal static System.Drawing.RectangleF Place(string style,double iw,double ih,int w,int h){return WindowsDesktopWallpaperRenderer.Place(style,iw,ih,w,h);}
}

interface IWallpaperFadeCover : IDisposable {byte Alpha {set;}void Close();}

/// <summary>
/// A plain window showing one monitor's picture, just above the desktop. Windows fades it (layered
/// window alpha), so each frame of the fade costs nothing to draw. Click-through, never activated.
/// </summary>
sealed class CoverWindow : System.Windows.Forms.Form,IWallpaperFadeCover {
 readonly System.Drawing.Bitmap picture;
 public CoverWindow(System.Drawing.Rectangle bounds,System.Drawing.Bitmap picture,bool show=true){
  // Set the managed value before handle creation/Show. A raw native alpha alone can
  // be replaced by WinForms' default Opacity=1 during its first visibility update.
  Opacity=0;AutoScaleMode=System.Windows.Forms.AutoScaleMode.None;
  this.picture=picture;FormBorderStyle=System.Windows.Forms.FormBorderStyle.None;ShowInTaskbar=false;StartPosition=System.Windows.Forms.FormStartPosition.Manual;
  Bounds=bounds;BackColor=System.Drawing.Color.Black;Text="Nord Wallpaper Fade";
  SetStyle(System.Windows.Forms.ControlStyles.AllPaintingInWmPaint|System.Windows.Forms.ControlStyles.UserPaint|System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer,true);
  if(show)ShowPrepared(bounds);
 }
 internal void ShowPrepared(System.Drawing.Rectangle bounds){
  Show();
  // Directly above the desktop window: below every app window, and never beneath the desktop itself.
  var desktop=FindWindow("Progman",null);var above=desktop==IntPtr.Zero?IntPtr.Zero:GetWindow(desktop,3); // GW_HWNDPREV
  SetWindowPos(Handle,above==IntPtr.Zero||above==Handle?new IntPtr(1):above,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0010); // SWP_NOACTIVATE
  Update(); // paint now, before the fade's clock starts
  ScreenFrame.WallpaperCover(Handle,true);
 }
 protected override void OnFormClosed(System.Windows.Forms.FormClosedEventArgs e){ScreenFrame.WallpaperCover(Handle,false);base.OnFormClosed(e);}
 public byte Alpha {set{Opacity=value/255.0;}}
 protected override bool ShowWithoutActivation {get{return true;}}
 // Layered, no-activate, tool window, click-through.
 protected override System.Windows.Forms.CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x08000000|0x80|0x20;return p;}}
 protected override void OnPaint(System.Windows.Forms.PaintEventArgs e){e.Graphics.DrawImage(picture,ClientRectangle);}

 [DllImport("user32.dll")] static extern IntPtr FindWindow(string className,string title);
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint relation);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}

/// <summary>System-wide hotkeys owned by the control center ("win+]", "ctrl+alt+w", …).</summary>
static class GlobalHotkeys {
 static HwndSource sink;
 static readonly Dictionary<int,Action> actions=new Dictionary<int,Action>();

 public static bool Register(string combo,Action action){
  uint modifiers,key;if(!Parse(combo,out modifiers,out key))return false;
  if(sink==null){sink=new HwndSource(new HwndSourceParameters("Nord hotkeys"){ParentWindow=new IntPtr(-3)});sink.AddHook(Hook);} // message-only window
  int id=actions.Count+1;
  if(!RegisterHotKey(sink.Handle,id,modifiers,key))return false;
  actions[id]=action;return true;
 }

 static IntPtr Hook(IntPtr window,int message,IntPtr w,IntPtr l,ref bool handled){
  Action action;if(message==0x0312&&actions.TryGetValue(w.ToInt32(),out action)){handled=true;action();}
  return IntPtr.Zero;
 }

 static bool Parse(string combo,out uint modifiers,out uint key){
  modifiers=0;key=0;
  foreach(var part in (combo??"").ToLowerInvariant().Split('+').Select(p=>p.Trim()).Where(p=>p.Length>0)){
   switch(part){
    case "alt":modifiers|=1;break;
    case "ctrl":case "control":modifiers|=2;break;
    case "shift":modifiers|=4;break;
    case "win":modifiers|=8;break;
    default:key=Key(part);break;
   }
  }
  return key!=0;
 }

 public static void UnregisterAll(){if(sink==null)return;foreach(var id in actions.Keys)UnregisterHotKey(sink.Handle,id);actions.Clear();sink.RemoveHook(Hook);sink.Dispose();sink=null;}
 static uint Key(string name){
  var named=new Dictionary<string,uint>{{"]",0xDD},{"[",0xDB},{".",0xBE},{",",0xBC},{"/",0xBF},{";",0xBA},{"'",0xDE},{"-",0xBD},{"=",0xBB},{"`",0xC0}};
  uint vk;if(named.TryGetValue(name,out vk))return vk;
  if(name.Length==1&&Char.IsLetterOrDigit(name[0]))return Char.ToUpperInvariant(name[0]);
  System.Windows.Forms.Keys key;return Enum.TryParse(name,true,out key)?(uint)key:0;
 }

 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
}
