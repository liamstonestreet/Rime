using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

/// <summary>Reads the Rice tab's settings (bar-appearance.json beside the helpers), re-read only when the file changes.</summary>
static class AppearanceFile {
 static readonly string File=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"bar-appearance.json");
 static DateTime stamp;static readonly object gate=new object();
 static System.Collections.Generic.Dictionary<string,object> values=new System.Collections.Generic.Dictionary<string,object>();
 static System.Collections.Generic.Dictionary<string,object> Data(){lock(gate){
  try{var now=System.IO.File.GetLastWriteTimeUtc(File);if(now!=stamp){var next=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(System.IO.File.ReadAllText(File));if(next!=null){values=next;stamp=now;}}}catch{}
  return values;
 }}
 public static string StringValue(string name,string fallback){object value;return Data().TryGetValue(name,out value)&&value is string&&!String.IsNullOrWhiteSpace((string)value)?(string)value:fallback;}
 public static int Get(string name,int fallback){object value;return Data().TryGetValue(name,out value)&&value is int&&(int)value>=0?(int)value:fallback;}
 public static string Get(string name,string fallback){return StringValue(name,fallback);}
}

/// <summary>
/// The Rice tab's motion settings for WPF surfaces: one speed (100% is the designed timing, 0% turns
/// animation off) and one style. Springy adds a slight overshoot to things that slide or grow into
/// place; leaving motions always ease in plainly.
/// </summary>
static class Motion {
 public static double Speed {get{return AppearanceFile.Get("motion",100)/100.0;}}
 public static bool Springy {get{return AppearanceFile.Get("motionStyle","smooth")=="springy";}}
 public static double Ms(double designed){double s=Speed;return s<=0?0:designed/s;}
 public static TimeSpan Time(double designed){return TimeSpan.FromMilliseconds(Ms(designed));}
 /// <summary>For something arriving: sliding or growing into place.</summary>
 public static IEasingFunction Arrive(){return Springy?(IEasingFunction)new BackEase{Amplitude=.35,EasingMode=EasingMode.EaseOut}:new CubicEase{EasingMode=EasingMode.EaseOut};}
 /// <summary>For something moving between two resting places (tabs, the indicator's marker).</summary>
 public static IEasingFunction Shift(){return Springy?(IEasingFunction)new BackEase{Amplitude=.25,EasingMode=EasingMode.EaseOut}:new CubicEase{EasingMode=EasingMode.EaseInOut};}
}

/// <summary>
/// The Rice tab's corner radius for WPF surfaces: <see cref="Apply"/> walks a window and sets every
/// border's corners (and rounded clip) to its designed radius times the scale, remembering the designed
/// value, so the same call works again after the slider moves. 100% is the original look, 0% square.
/// </summary>
static class Roundness {
 static readonly ConditionalWeakTable<DependencyObject,object> designed=new ConditionalWeakTable<DependencyObject,object>();

 /// <summary>The current scale (corner radius / 100), read from the settings file.</summary>
 public static double Scale {get{return AppearanceFile.Get("radius",100)/100.0;}}

 public static void Apply(DependencyObject root){Apply(root,Scale);}
 public static void Apply(DependencyObject root,double scale){
  if(root==null)return;
  var border=root as Border;
  if(border!=null){var c=(CornerRadius)designed.GetValue(border,b=>((Border)b).CornerRadius);border.CornerRadius=new CornerRadius(c.TopLeft*scale,c.TopRight*scale,c.BottomRight*scale,c.BottomLeft*scale);}
  var element=root as UIElement;var clip=element==null?null:element.Clip as RectangleGeometry;
  if(clip!=null&&!clip.IsFrozen){var r=(Size)designed.GetValue(clip,g=>new Size(((RectangleGeometry)g).RadiusX,((RectangleGeometry)g).RadiusY));clip.RadiusX=r.Width*scale;clip.RadiusY=r.Height*scale;}
  int count=root is Visual?VisualTreeHelper.GetChildrenCount(root):0;
  for(int i=0;i<count;i++)Apply(VisualTreeHelper.GetChild(root,i),scale);
  // Collapsed content has no visual children yet; its logical tree still holds the borders.
  if(count==0)foreach(var child in LogicalTreeHelper.GetChildren(root))if(child is DependencyObject)Apply((DependencyObject)child,scale);
 }
}

/// <summary>Portable archive fonts, shared by all native rice helpers.</summary>
static class RiceFont {
 public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"99 - Archive","Fonts");
 static System.Collections.Generic.Dictionary<string,FontFamily> families=new System.Collections.Generic.Dictionary<string,FontFamily>(StringComparer.OrdinalIgnoreCase);
 static readonly System.Windows.Threading.DispatcherTimer timer=new System.Windows.Threading.DispatcherTimer();
 static string lastName;static FontFamily current;static int revision;
 static RiceFont(){
  Refresh();
  timer.Interval=TimeSpan.FromMilliseconds(500);timer.Tick+=delegate{var font=Current;var app=Application.Current;if(app!=null)foreach(Window window in app.Windows)if(window.FontFamily!=font)window.FontFamily=font;};timer.Start();
 }
 public static void Refresh(){
  var found=new System.Collections.Generic.Dictionary<string,FontFamily>(StringComparer.OrdinalIgnoreCase);
  try{var bundled=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fonts");if(Directory.Exists(bundled))foreach(var family in Fonts.GetFontFamilies(new Uri(bundled+Path.DirectorySeparatorChar)))foreach(var name in family.FamilyNames.Values)found[name]=family;}catch{}
  try{if(Directory.Exists(Folder))foreach(var dir in new[]{Folder}.Concat(Directory.GetDirectories(Folder,"*",SearchOption.AllDirectories)))try{foreach(var f in Fonts.GetFontFamilies(new Uri(dir+Path.DirectorySeparatorChar)))foreach(var name in f.FamilyNames.Values)if(!found.ContainsKey(name))found[name]=f;}catch{}}catch{}
  revision=AppearanceFile.Get("fontRevision",0);families=found;lastName=null;current=null;webName=null;webData=null;
 }
 public static string[] Names {get{return families.Keys.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).Take(7).ToArray();}}
 public static string Name {get{return AppearanceFile.StringValue("fontFamily","Rubik");}}
 public static FontFamily Current {get{if(revision!=AppearanceFile.Get("fontRevision",0))Refresh();var name=Name;if(current==null||name!=lastName){lastName=name;if(!families.TryGetValue(name,out current))try{current=name=="Rubik"?new FontFamily(new Uri(AppDomain.CurrentDomain.BaseDirectory),"./Fonts/#Rubik"):new FontFamily(name);}catch{current=new FontFamily("Segoe UI");}}return current;}}
 static string webName,webData;
 public static string WebData(string name){
  if(name==webName)return webData;
  webName=name;webData=null;FontFamily family;
  if(families.TryGetValue(name,out family))try{GlyphTypeface glyph;if(new Typeface(family,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal).TryGetGlyphTypeface(out glyph)&&glyph.FontUri.IsFile)webData="data:font/ttf;base64,"+Convert.ToBase64String(System.IO.File.ReadAllBytes(glyph.FontUri.LocalPath));}catch{}
  return webData;
 }
}
