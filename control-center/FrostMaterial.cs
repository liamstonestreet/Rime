using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

/// <summary>
/// Renders the frosted-glass material (see <see cref="FrostFile"/>): each monitor's wallpaper, laid out
/// the way Windows draws it, blurred and given acrylic's saturation and brightness lift. Written to the
/// shared frost files for the other helpers, and kept as data URLs of each monitor's top strip for the
/// bars. Renders only when the wallpaper, the settings or the monitors change.
/// </summary>
static class FrostMaterial {
 const int Downscale=4;        // the blur hides detail, so a quarter-size image is plenty
 const int StripHeight=128;    // physical pixels of each monitor's top sent to the bars
 static readonly object gate=new object();
 static string rendered;static volatile int revision;static volatile bool stopped;
 public static int Revision {get{return revision;}}
 static List<object> strips=new List<object>();
 static List<object> pending;static string pendingKey;static readonly List<string> pendingFiles=new List<string>();
 public static bool HasPending {get{lock(gate)return pending!=null;}}

 /// <summary>Each monitor's top strip for the bars: x, y, width, height (physical pixels) and a data URL.</summary>
 public static List<object> Strips {get{lock(gate)return strips;}}

 /// <summary>Blur 0-100 maps to a radius of up to 60 screen pixels; brightness 50 is neutral.</summary>
 public static void Render(string wallpaper,int blur,int brightness,bool notify=true){
  if(stopped)return;if(wallpaper==null||!File.Exists(wallpaper)){Clear();return;}
  var screens=System.Windows.Forms.Screen.AllScreens;
  var style=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop","WallpaperStyle","10") as string;
  var key=wallpaper+"|"+File.GetLastWriteTimeUtc(wallpaper).Ticks+"|"+blur+"|"+brightness+"|"+style+"|"+String.Join(";",screens.Select(s=>s.DeviceName+s.Bounds));
  bool ready;lock(gate)ready=pending!=null&&pendingKey==key;
  if(ready){if(notify){Commit();Notify();}return;}
  DiscardPending();if(key==rendered)return;
  var next=new List<object>();
  // Twice the largest frost width: enough for the Fill crop and the scale-down, a fraction of the photo.
  using(var source=SmallImage.Load(wallpaper,2*screens.Max(s=>s.Bounds.Width)/Downscale)){
   foreach(var screen in screens){
    var b=screen.Bounds;int w=Math.Max(1,b.Width/Downscale),h=Math.Max(1,b.Height/Downscale);
    using(var frame=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
     using(var g=Graphics.FromImage(frame)){
      g.Clear(Color.Black);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
      g.DrawImage(source,DesktopWallpaper.Place(style,source.Width,source.Height,w,h));
     }
     Frost(frame,blur*0.6/Downscale,brightness);
     var output=FrostFile.PathFor(screen.DeviceName);lock(gate){if(stopped)return;Save(frame,output+".pending");if(!pendingFiles.Contains(output))pendingFiles.Add(output);}
     int rows=Math.Min(h,StripHeight/Downscale);
     using(var strip=frame.Clone(new Rectangle(0,0,w,rows),frame.PixelFormat))
      next.Add(new{x=b.X,y=b.Y,width=b.Width,height=rows*Downscale,image="data:image/jpeg;base64,"+Convert.ToBase64String(Jpeg(strip))});
    }
   }
  }
  lock(gate){if(stopped)return;pending=next;pendingKey=key;}
  if(notify){Commit();Notify();}
 }
 public static void Commit(){
  lock(gate){if(stopped||pending==null)return;foreach(var output in pendingFiles){if(File.Exists(output))File.Replace(output+".pending",output,null);else File.Move(output+".pending",output);}pendingFiles.Clear();strips=pending;rendered=pendingKey;pending=null;pendingKey=null;revision++;}
 }
 static void DiscardPending(){lock(gate){pending=null;pendingKey=null;foreach(var output in pendingFiles)try{File.Delete(output+".pending");}catch{}pendingFiles.Clear();}}
 public static void Stop(){lock(gate){stopped=true;DiscardPending();strips=new List<object>();Rendered=null;}}
 public static void Notify(){var app=System.Windows.Application.Current;var done=Rendered;if(!stopped&&app!=null&&done!=null&&!app.Dispatcher.HasShutdownStarted){if(app.Dispatcher.CheckAccess())done();else app.Dispatcher.BeginInvoke(new Action(()=>{if(!stopped)done();}));}}

 /// <summary>Raised on the UI thread after new frost files were written.</summary>
 public static event Action Rendered;

 /// <summary>Frost off: removes the files so every surface falls back to its plain tint.</summary>
 public static void Clear(){
  bool changed;lock(gate){changed=rendered!=null||strips.Count>0;strips=new List<object>();rendered=null;pending=null;pendingKey=null;foreach(var output in pendingFiles)try{File.Delete(output+".pending");}catch{}pendingFiles.Clear();}
  foreach(var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory,"frost-*.jpg"))try{File.Delete(file);changed=true;}catch{}
  if(changed)revision++;
 }

 /// <summary>Three box-blur passes each way (close to a Gaussian), then saturation and brightness.</summary>
 static void Frost(Bitmap bitmap,double radius,int brightness){
  var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
  try{
   int w=bitmap.Width,h=bitmap.Height,stride=data.Stride;var px=new byte[stride*h];Marshal.Copy(data.Scan0,px,0,px.Length);
   int r=(int)Math.Round(radius);
   if(r>0)for(int pass=0;pass<3;pass++){Box(px,w,h,4,stride,r);Box(px,h,w,stride,4,r);}
   double lift=(brightness-50)/50.0*0.35;
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){
    int i=y*stride+x*4;double gray=0.0722*px[i]+0.7152*px[i+1]+0.2126*px[i+2];
    for(int c=0;c<3;c++){double v=gray+(px[i+c]-gray)*1.2;v=lift>=0?v+(255-v)*lift:v*(1+lift);px[i+c]=(byte)Math.Max(0,Math.Min(255,Math.Round(v)));}
    px[i+3]=255;
   }
   Marshal.Copy(px,0,data.Scan0,px.Length);
  }finally{bitmap.UnlockBits(data);}
 }

 /// <summary>Box blur along lines of <paramref name="length"/> pixels (step apart), for <paramref name="lines"/> lines (lineStep apart), clamping at the edges.</summary>
 internal static void Box(byte[] px,int length,int lines,int step,int lineStep,int r){
  var line=new byte[length*3];int span=2*r+1;
  for(int n=0;n<lines;n++){
   int origin=n*lineStep;
   for(int i=0;i<length;i++)for(int c=0;c<3;c++)line[i*3+c]=px[origin+i*step+c];
   for(int c=0;c<3;c++){
    int sum=0;for(int i=-r;i<=r;i++)sum+=line[Math.Max(0,Math.Min(length-1,i))*3+c];
    for(int i=0;i<length;i++){
     px[origin+i*step+c]=(byte)(sum/span);
     sum+=line[Math.Min(length-1,i+r+1)*3+c]-line[Math.Max(0,i-r)*3+c];
    }
   }
  }
 }

 static void Save(Bitmap bitmap,string path){var temp=path+".tmp";File.WriteAllBytes(temp,Jpeg(bitmap));if(File.Exists(path))File.Delete(path);File.Move(temp,path);}
 static byte[] Jpeg(Bitmap bitmap){
  var codec=ImageCodecInfo.GetImageEncoders().First(e=>e.FormatID==ImageFormat.Jpeg.Guid);
  var options=new EncoderParameters(1);options.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,92L);
  using(var stream=new MemoryStream()){bitmap.Save(stream,codec,options);return stream.ToArray();}
 }
}
