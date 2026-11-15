using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// The frosted-glass material shared by every nord-rice surface: the control center renders the blurred
/// wallpaper of each monitor to frost-&lt;DISPLAYn&gt;.jpg beside the helpers (and deletes the files when the
/// option is off); a surface paints the part of its monitor's image that lies under it, so neighbouring
/// surfaces line up like one pane of glass. Works in any DPI mode: the window and monitor rectangles
/// come from the same process, and only their ratio is used.
/// </summary>
static class FrostFile {
 static readonly string Folder=AppDomain.CurrentDomain.BaseDirectory;
 static readonly System.Collections.Generic.Dictionary<string,Tuple<DateTime,WeakReference<BitmapSource>>> cache=new System.Collections.Generic.Dictionary<string,Tuple<DateTime,WeakReference<BitmapSource>>>();

 public static string PathFor(string device){return System.IO.Path.Combine(Folder,"frost-"+device.TrimStart('\\','.')+".jpg");}

 /// <summary>A brush showing the frost behind a window's screen rectangle, or null when frost is off.</summary>
 public static Brush Behind(IntPtr window){RECT r;return GetWindowRect(window,out r)?Behind(r.left,r.top,r.right-r.left,r.bottom-r.top):null;}

 /// <summary>A brush showing the frost behind a screen rectangle (in this process's coordinates), or null.</summary>
 public static Brush Behind(double left,double top,double width,double height){
  if(width<=0||height<=0)return null;
  var area=new RECT{left=(int)left,top=(int)top,right=(int)(left+width),bottom=(int)(top+height)};
  var monitor=MonitorFromRect(ref area,2); // MONITOR_DEFAULTTONEAREST
  var info=new MONITORINFOEX{size=Marshal.SizeOf(typeof(MONITORINFOEX))};
  if(!GetMonitorInfo(monitor,ref info))return null;
  var image=Load(PathFor(info.device));if(image==null)return null;
  double mw=info.monitor.right-info.monitor.left,mh=info.monitor.bottom-info.monitor.top;
  var brush=new ImageBrush(image){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.RelativeToBoundingBox,
   Viewbox=new Rect((left-info.monitor.left)/mw,(top-info.monitor.top)/mh,width/mw,height/mh)};
  brush.Freeze();return brush;
 }

 /// <summary>Frost under a tint, as one brush that can stand in for a surface's plain background.</summary>
 public static Brush Under(Brush frost,Brush tint){
  var unit=new RectangleGeometry(new Rect(0,0,1,1));var layers=new DrawingGroup();
  layers.Children.Add(new GeometryDrawing(frost,null,unit));layers.Children.Add(new GeometryDrawing(tint,null,unit));
  var brush=new DrawingBrush(layers){Stretch=Stretch.Fill};if(frost.IsFrozen&&tint.IsFrozen)brush.Freeze();return brush;
 }

 /// <summary>How opaque a surface's tint is over the frost: 1 minus the Rice tab's transparency.</summary>
 public static double TintOpacity(){
  try{var m=System.Text.RegularExpressions.Regex.Match(File.ReadAllText(System.IO.Path.Combine(Folder,"bar-appearance.json")),"\"transparency\":(\\d+)");return m.Success?1-Int32.Parse(m.Groups[1].Value)/100.0:1;}catch{return 1;}
 }

 static BitmapSource Load(string path){
  try{
   if(!File.Exists(path))return null;
   var stamp=File.GetLastWriteTimeUtc(path);
   Tuple<DateTime,WeakReference<BitmapSource>> hit;BitmapSource retained;if(cache.TryGetValue(path,out hit)&&hit.Item1==stamp&&hit.Item2.TryGetTarget(out retained))return retained;
   var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad; // (IgnoreImageCache throws with a StreamSource)
   using(var stream=File.OpenRead(path)){image.StreamSource=stream;image.EndInit();}
   image.Freeze();cache[path]=Tuple.Create(stamp,new WeakReference<BitmapSource>(image));return image;
  }catch{return null;}
 }

 [StructLayout(LayoutKind.Sequential)] struct RECT{public int left,top,right,bottom;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MONITORINFOEX{public int size;public RECT monitor,work;public uint flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string device;}
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rect);
 [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref RECT rect,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFOEX info);
}
