using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

sealed class WindowsDesktopWallpaperRenderer:IWallpaperPlatform {
 public System.Drawing.Rectangle[] Screens {get{return System.Windows.Forms.Screen.AllScreens.Select(screen=>screen.Bounds).ToArray();}}
 public string Style {get{return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop","WallpaperStyle","10") as string;}}
 public IWallpaperFadeCover CreateCover(System.Drawing.Rectangle bounds,System.Drawing.Bitmap picture){return new CoverWindow(bounds,picture);}
 public Task Present(){return Task.Run(()=>{DwmFlush();});}
 public Task Wait(int ms){return Task.Delay(ms);}
 public Task<ThemeTransitionData> BeginTheme(string path){return BarAppearance.BeginWallpaper(path);}
 public void EndTheme(){BarAppearance.EndWallpaper();}
 public void Warm(string path){BarAppearance.WarmWallpaperLook(path);}
 public Task Commit(string path){return Task.Run(()=>{if(!SystemParametersInfo(0x14,0,path,3))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());LockScreenWallpaper.Queue(path);});}
 public Task Animate(IList<IWallpaperFadeCover> covers,ThemeTransitionData timeline,CancellationToken cancellation){return WallpaperFadeAnimator.Run(covers,timeline,cancellation);}
 public void Log(string message){WallpaperSwitcher.Log(message);}
 public WallpaperImage[] Build(string path,System.Drawing.Rectangle[] screens,string style){
  int sourceWidth,sourceHeight;
  using(var input=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
   var header=BitmapFrame.Create(input,BitmapCreateOptions.DelayCreation|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.None);sourceWidth=header.PixelWidth;sourceHeight=header.PixelHeight;
  }
  double need=screens.Max(b=>Math.Max(b.Width/(double)sourceWidth,b.Height/(double)sourceHeight))*sourceWidth;
  var decoded=ScaledDecode.Load(path,(int)Math.Ceiling(Math.Min(sourceWidth,need)));
  var bgra=new FormatConvertedBitmap(decoded,PixelFormats.Pbgra32,null,0);
  using(var source=new System.Drawing.Bitmap(bgra.PixelWidth,bgra.PixelHeight,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)){
   var bits=source.LockBits(new System.Drawing.Rectangle(0,0,source.Width,source.Height),System.Drawing.Imaging.ImageLockMode.WriteOnly,source.PixelFormat);
   try{bgra.CopyPixels(Int32Rect.Empty,bits.Scan0,bits.Stride*bits.Height,bits.Stride);}finally{source.UnlockBits(bits);}
   var result=new List<WallpaperImage>();try{foreach(var b in screens){
    var picture=new System.Drawing.Bitmap(b.Width,b.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
    try{using(var g=System.Drawing.Graphics.FromImage(picture)){
     g.Clear(System.Drawing.Color.Black);
     g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
     g.DrawImage(source,Place(style,source.Width,source.Height,b.Width,b.Height));
    }}catch{picture.Dispose();throw;}
    result.Add(new WallpaperImage{Bounds=b,Picture=picture});
   }return result.ToArray();}catch{foreach(var cover in result)cover.Picture.Dispose();throw;}
  }
 }
 internal static System.Drawing.RectangleF Place(string style,double iw,double ih,int w,int h){
  if(style=="2")return new System.Drawing.RectangleF(0,0,w,h);
  bool fill=style!="6"&&style!="0";
  double scale=style=="6"?Math.Min(w/iw,h/ih):style=="0"?1:Math.Max(w/iw,h/ih),sw=iw*scale,sh=ih*scale;
  return new System.Drawing.RectangleF((float)((w-sw)/2),(float)((h-sh)/(fill?3:2)),(float)sw,(float)sh);
 }

 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SystemParametersInfo(int action,int param,string value,int flags);
 [DllImport("dwmapi.dll")]static extern int DwmFlush();
}
