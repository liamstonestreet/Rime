using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

/// <summary>Finds the image behind the desktop: Wallpaper Engine's preview when it is running, else Windows' wallpaper.</summary>
static class WallpaperSource {
 public static string Current(){return WallpaperEngine()??Windows();}

 static string WallpaperEngine(){
  try{
   var process=Process.GetProcessesByName("wallpaper64").Concat(Process.GetProcessesByName("wallpaper32")).FirstOrDefault();
   if(process==null)return null;
   string folder;using(process)folder=Path.GetDirectoryName(process.MainModule.FileName);
   var config=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(folder,"config.json")));
   // config.json: { <user>: { general: { wallpaperconfig: { selectedwallpapers: { Monitor0: { file } } } } } }
   foreach(var user in config.Values){
    var selected=Walk(user,"general","wallpaperconfig","selectedwallpapers") as Dictionary<string,object>;if(selected==null)continue;
    foreach(var monitor in selected.OrderBy(m=>m.Key).Select(m=>m.Value as Dictionary<string,object>).Where(m=>m!=null&&m.ContainsKey("file"))){
     var project=Path.GetDirectoryName(Convert.ToString(monitor["file"]));
     var preview=Walk(new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(project,"project.json"))),"preview") as string;
     if(preview!=null&&File.Exists(Path.Combine(project,preview)))return Path.Combine(project,preview);
    }
   }
  }catch{}
  return null;
 }
 static object Walk(object node,params string[] keys){foreach(var key in keys){var map=node as Dictionary<string,object>;if(map==null||!map.TryGetValue(key,out node))return null;}return node;}

 /// <summary>The Windows (static) wallpaper image, or null.</summary>
 public static string Windows(){
  var path=new StringBuilder(520);
  if(SystemParametersInfo(0x73,path.Capacity,path,0)&&File.Exists(path.ToString()))return path.ToString();
  // Slideshows and Spotlight leave the path empty; Windows keeps the image it is showing here.
  var transcoded=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Microsoft\Windows\Themes\TranscodedWallpaper");
  return File.Exists(transcoded)?transcoded:null;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool SystemParametersInfo(int action,int size,StringBuilder value,int flags);
}

/// <summary>
/// Loads an image already scaled to a width (JPEGs decode at reduced size directly), as a GDI+ bitmap. A
/// 24-megapixel wallpaper loaded whole costs ~100 MB that Windows keeps reserved for the process afterwards;
/// the palette needs a 96 px sample and the frost a quarter-screen image.
/// </summary>
static class SmallImage {
 public static Bitmap Load(string path,int width){
  var bgra=new System.Windows.Media.Imaging.FormatConvertedBitmap(ScaledDecode.Load(path,width),System.Windows.Media.PixelFormats.Bgra32,null,0);
  var bitmap=new Bitmap(bgra.PixelWidth,bgra.PixelHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
  var bits=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.WriteOnly,bitmap.PixelFormat);
  try{bgra.CopyPixels(System.Windows.Int32Rect.Empty,bits.Scan0,bits.Stride*bits.Height,bits.Stride);}finally{bitmap.UnlockBits(bits);}
  return bitmap;
 }
}

/// <summary>A color in OKLab, the perceptual space used for clustering and for building theme tones.</summary>
struct Oklab {
 public double L,A,B;
 public Oklab(double l,double a,double b){L=l;A=a;B=b;}
 public double Chroma {get{return Math.Sqrt(A*A+B*B);}}
 public double Hue {get{return Math.Atan2(B,A);}}
 public double DistanceSq(Oklab o){double l=L-o.L,a=A-o.A,b=B-o.B;return l*l+a*a+b*b;}
 public static Oklab Lch(double l,double c,double h){return new Oklab(l,c*Math.Cos(h),c*Math.Sin(h));}

 static double Lin(double c){c/=255;return c<=0.04045?c/12.92:Math.Pow((c+0.055)/1.055,2.4);}
 static double Gam(double c){return c<=0.0031308?12.92*c:1.055*Math.Pow(c,1/2.4)-0.055;}
 public static Oklab From(Color c){
  double r=Lin(c.R),g=Lin(c.G),b=Lin(c.B);
  double l=Math.Pow(0.4122214708*r+0.5363325363*g+0.0514459929*b,1/3.0),m=Math.Pow(0.2119034982*r+0.6806995451*g+0.1073969566*b,1/3.0),s=Math.Pow(0.0883024619*r+0.2817188376*g+0.6299787005*b,1/3.0);
  return new Oklab(0.2104542553*l+0.7936177850*m-0.0040720468*s,1.9779984951*l-2.4285922050*m+0.4505937099*s,0.0259040371*l+0.7827717662*m-0.8086757660*s);
 }
 public bool InGamut {get{double r,g,b;return Rgb(out r,out g,out b);}}
 /// <summary>WCAG relative luminance (0 black .. 1 white).</summary>
 public double Luminance {get{double r,g,b;Rgb(out r,out g,out b);Func<double,double> c=v=>Math.Max(0,Math.Min(1,v));return 0.2126*c(r)+0.7152*c(g)+0.0722*c(b);}}
 bool Rgb(out double r,out double g,out double b){
  double l=Math.Pow(L+0.3963377774*A+0.2158037573*B,3),m=Math.Pow(L-0.1055613458*A-0.0638541728*B,3),s=Math.Pow(L-0.0894841775*A-1.2914855480*B,3);
  r=4.0767416621*l-3.3077115913*m+0.2309699292*s;g=-1.2684380046*l+2.6097574011*m-0.3413193965*s;b=-0.0041960863*l-0.7034186147*m+1.7076147010*s;
  return r>=-1e-4&&r<=1+1e-4&&g>=-1e-4&&g<=1+1e-4&&b>=-1e-4&&b<=1+1e-4;
 }
 /// <summary>#rrggbb, reducing chroma (keeping lightness and hue) until the color fits in sRGB.</summary>
 public string Hex(){
  double r,g,b,c=Chroma,h=Hue;var color=this;
  while(!color.Rgb(out r,out g,out b)&&c>0){c=Math.Max(0,c-0.005);color=Lch(L,c,h);}
  Func<double,int> to=v=>(int)Math.Round(Math.Max(0,Math.Min(1,Gam(Math.Max(0,v))))*255);
  return "#"+to(r).ToString("x2")+to(g).ToString("x2")+to(b).ToString("x2");
 }
}

/// <summary>Dominant colors of an image: k-means in OKLab over a downscaled copy, with weights.</summary>
static class PaletteExtractor {
 public struct Swatch {public Oklab Color;public double Weight;}

 public static Swatch[] Extract(string path,int k=6){
  var pixels=Sample(path,96);
  if(pixels.Count==0)return new Swatch[0];
  var random=new Random(1); // Deterministic, so the same wallpaper always gives the same theme.
  // k-means++ seeding: spread the starting centres across the image's colors.
  var centres=new List<Oklab>{pixels[random.Next(pixels.Count)]};
  while(centres.Count<k){
   var distance=pixels.Select(p=>centres.Min(c=>p.DistanceSq(c))).ToArray();double total=distance.Sum();if(total<=0)break;
   double pick=random.NextDouble()*total;int i=0;for(;i<distance.Length-1&&(pick-=distance[i])>0;i++){}
   centres.Add(pixels[i]);
  }
  var owner=new int[pixels.Count];
  for(int pass=0;pass<15;pass++){
   for(int i=0;i<pixels.Count;i++){int best=0;double bestD=double.MaxValue;for(int c=0;c<centres.Count;c++){double d=pixels[i].DistanceSq(centres[c]);if(d<bestD){bestD=d;best=c;}}owner[i]=best;}
   for(int c=0;c<centres.Count;c++){double l=0,a=0,b=0;int n=0;for(int i=0;i<pixels.Count;i++)if(owner[i]==c){l+=pixels[i].L;a+=pixels[i].A;b+=pixels[i].B;n++;}if(n>0)centres[c]=new Oklab(l/n,a/n,b/n);}
  }
  return centres.Select((c,index)=>new Swatch{Color=c,Weight=owner.Count(o=>o==index)/(double)pixels.Count}).Where(s=>s.Weight>0).OrderByDescending(s=>s.Weight).ToArray();
 }

 static List<Oklab> Sample(string path,int size){
  var result=new List<Oklab>();
  using(var source=SmallImage.Load(path,size*2)){
   double scale=Math.Min(1.0,size/(double)Math.Max(source.Width,source.Height));
   int w=Math.Max(1,(int)(source.Width*scale)),h=Math.Max(1,(int)(source.Height*scale));
   using(var small=new Bitmap(w,h))using(var g=Graphics.FromImage(small)){
    g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(source,0,0,w,h);
    for(int y=0;y<h;y++)for(int x=0;x<w;x++){var c=small.GetPixel(x,y);if(c.A>128)result.Add(Oklab.From(c));}
   }
  }
  return result;
 }
}

/// <summary>
/// Turns a wallpaper's colors into the bar's CSS variables with a 60-30-10 scheme:
/// 60% a dark background tinted with the hue of the image's largest colorful area, 30% an accent
/// family built on its signature color (vivid and with real presence), 10% a second color that
/// sits in harmony with the accent (analogous, triadic, split-complementary or complementary).
/// Each accent takes the deepest lightness that still reads on the background, so saturated hues
/// stay saturated instead of washing out to pastel. Status colors keep their Nord meanings.
/// </summary>
static class WallpaperTheme {
 static readonly double[] Harmonies={30,120,150,180}; // degrees from the accent
 const double Deg=Math.PI/180;

 public static Dictionary<string,string> From(string imagePath){
  var swatches=PaletteExtractor.Extract(imagePath,8);
  return swatches.Length==0?null:Build(swatches);
 }

 public static Dictionary<string,string> Build(PaletteExtractor.Swatch[] swatches){
  // 60: the largest colorful area sets the background hue. Clusters are pooled by hue first, since
  // one region (a sky, say) often splits into several clusters of different lightness.
  var field=swatches.Where(s=>s.Color.Chroma>0.015).GroupBy(s=>(int)Math.Floor(((s.Color.Hue/Deg)+360)%360/30))
   .Select(g=>new{weight=g.Sum(s=>s.Weight),color=g.OrderByDescending(s=>s.Weight).First().Color}).OrderByDescending(g=>g.weight).Select(g=>(Oklab?)g.color).FirstOrDefault()??swatches[0].Color;
  // 30: the signature color.
  Func<PaletteExtractor.Swatch,double> presence=s=>s.Color.Chroma*Math.Sqrt(s.Weight);
  var vivid=swatches.Where(s=>s.Color.Chroma>0.04).OrderByDescending(presence).ToArray();
  var key=vivid.Length>0?vivid[0].Color:Oklab.Lch(0.7,0.07,field.Hue);
  // 10: the strongest remaining color that fits a harmony, nudged onto the harmonic angle. With none,
  // an analogous neighbour, stepping away from the orange-brown range (dull at accent lightness).
  var partner=vivid.Skip(1).Where(s=>Gap(s.Color.Hue,key.Hue)>20*Deg).OrderByDescending(s=>presence(s)*Fit(key.Hue,s.Color.Hue)).ToArray();
  double brown=65*Deg,secondHue=partner.Length>0?Snap(key.Hue,partner[0].Color.Hue)
   :new[]{key.Hue+30*Deg,key.Hue-30*Deg}.OrderByDescending(h=>Gap(h,brown)).First();
  double secondChroma=partner.Length>0?partner[0].Color.Chroma:key.Chroma*0.7;

  // Background: dark warm tints read as mud (brown), so warm hues stay close to neutral.
  double hue=field.Hue,tint=Math.Min(field.Chroma,IsWarm(hue)?0.014:0.03);
  var bg=Oklab.Lch(0.25,tint,hue);
  Func<double,double,string> neutral=(l,c)=>Oklab.Lch(l,c,hue).Hex();
  // Cluster averages include shadows and haze, so image colors are boosted to accent strength.
  double keyChroma=Clamp(key.Chroma*1.8,0.10,0.17),otherChroma=Clamp(secondChroma*1.4,0.06,0.13);
  // Blend hue a fraction of the way towards another along the shorter arc.
  Func<double,double,double,double> toward=(from,to,t)=>from+Math.Atan2(Math.Sin(to-from),Math.Cos(to-from))*t;
  return new Dictionary<string,string>{
   {"--nord-0",bg.Hex()},{"--nord-1",neutral(0.30,tint)},{"--nord-2",neutral(0.34,tint)},{"--nord-3",neutral(0.43,tint)},
   {"--shade",neutral(0.21,tint)},
   {"--nord-4",neutral(0.90,0.016)},{"--nord-5",neutral(0.93,0.012)},{"--nord-6",neutral(0.96,0.008)},
   {"--muted",neutral(0.77,0.025)},{"--muted-2",neutral(0.71,0.025)},{"--muted-3",neutral(0.63,0.02)},
   // Accent family: primary, a calmer variant leaning toward the partner (borders, tints), a deep tone (fills).
   {"--nord-8",Readable(key.Hue,keyChroma,bg,5.5).Hex()},
   {"--nord-9",Readable(toward(key.Hue,secondHue,0.15),keyChroma*0.75,bg,3.5).Hex()},
   {"--nord-10",Deep(key.Hue,keyChroma).Hex()},
   {"--edge",Readable(key.Hue,keyChroma,bg,3).Hex()},
   // Partner: small doses only (tray, media glow, highlights).
   {"--nord-7",Readable(secondHue,otherChroma,bg,5).Hex()},
   {"--nord-15",Readable(toward(secondHue,key.Hue,-0.12),otherChroma*0.9,bg,4.5).Hex()},
  };
 }

 /// <summary>The deepest tone of a hue (up to the wanted chroma) with at least this WCAG contrast on the background.</summary>
 static Oklab Readable(double hue,double chroma,Oklab background,double ratio){
  double bgY=background.Luminance;
  for(double l=Floor(hue);l<0.95;l+=0.005){
   var c=Oklab.Lch(l,Math.Min(chroma,MaxChroma(l,hue)),hue);
   if((c.Luminance+0.05)/(bgY+0.05)>=ratio)return c;
  }
  return Oklab.Lch(0.95,Math.Min(chroma,MaxChroma(0.95,hue)),hue);
 }
 /// <summary>A darker tone of the hue for fills, no darker than the hue can go without turning muddy.</summary>
 static Oklab Deep(double hue,double chroma){double l=Math.Max(0.52,Floor(hue)-0.12);return Oklab.Lch(l,Math.Min(chroma,MaxChroma(l,hue)),hue);}
 /// <summary>
 /// Lowest lightness at which a hue still looks clean: yellows and limes turn olive and oranges turn
 /// brown when dark, so they sit higher; other hues can go deep.
 /// </summary>
 static double Floor(double hue){double d=((hue/Deg)%360+360)%360,yellow=Math.Max(0,1-Math.Abs(d-105)/70);return 0.5+0.27*yellow;}
 static double MaxChroma(double l,double hue){double lo=0,hi=0.4;for(int i=0;i<20;i++){double m=(lo+hi)/2;if(Oklab.Lch(l,m,hue).InGamut)lo=m;else hi=m;}return lo;}
 static double Gap(double a,double b){double d=Math.Abs(a-b)%(2*Math.PI);return d>Math.PI?2*Math.PI-d:d;}
 /// <summary>1 on a harmonic angle, falling off over ~15°.</summary>
 static double Fit(double key,double other){double off=Harmonies.Min(h=>Math.Abs(Gap(key,other)/Deg-h));return Math.Exp(-off*off/(2*15*15));}
 /// <summary>Moves a hue up to 15° onto the nearest harmonic angle from the key, keeping its side.</summary>
 static double Snap(double key,double other){
  double gap=Gap(key,other)/Deg,target=Harmonies.OrderBy(h=>Math.Abs(gap-h)).First(),moved=gap+Clamp(target-gap,-15,15);
  double side=Math.Sin(other-key)>=0?1:-1;return key+side*moved*Deg;
 }
 static bool IsWarm(double hue){double d=hue/Deg;if(d<0)d+=360;return d>=20&&d<=110;} // reds through yellows
 static double Clamp(double v,double lo,double hi){return Math.Max(lo,Math.Min(hi,v));}
}
