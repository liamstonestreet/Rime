using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

// A palette owns color semantics; the resource owner updates existing brushes without rebuilding UI.
sealed class ControlCenterPalette {
 public readonly Dictionary<string,Color> Colors=new Dictionary<string,Color>();
 public static Color Parse(string s){return (Color)ColorConverter.ConvertFromString(s);}
 public static Color Mix(Color a,Color b,double t){return Color.FromRgb((byte)(a.R+(b.R-a.R)*t),(byte)(a.G+(b.G-a.G)*t),(byte)(a.B+(b.B-a.B)*t));}
 // Lock-in colors win; otherwise the wallpaper palette (bar CSS variable names) if given; else Nord.
 public ControlCenterPalette(FocusState state,Dictionary<string,string> wallpaper=null){
  Colors["Surface"]=Parse("#FF2E3440");
  Colors["Edge"]=Parse("#6B86A3");
  Colors["Text"]=Parse("#ECEFF4");
  Colors["Muted"]=Parse("#AAB6C8");
  Colors["Accent"]=Parse("#88C0D0");Colors["Partner"]=Parse("#8FBCBB"); // the bar's --nord-8 and --nord-7
  Colors["Selected"]=Parse("#4C6574");
  Colors["Raised"]=Parse("#3B4252");
  Colors["Border"]=Parse("#526174");
  Colors["Error"]=Parse("#BF616A");
  Colors["SoftEdge"]=Parse("#506D8290");
  Colors["Hover"]=Parse("#304C6574");
  Colors["Pressed"]=Parse("#605E81AC");
  Colors["Track"]=Parse("#4C566A");
  Colors["Bright"]=Parse("#D8DEE9");
  Colors["Over"]=Parse("#434C5E");
  Colors["MediaBase"]=Parse("#272E3B");Colors["Glow1"]=Parse("#64B8BE");Colors["Glow2"]=Parse("#9A84BA");Colors["Glow3"]=Parse("#5E81AC");Colors["Shade"]=Parse("#1F2531");
  if(!state.ThemeActive){if(wallpaper!=null)FromWallpaper(wallpaper);return;}
  var s=state.settings;var bg=Parse(s.background);var edge=Parse(s.edge);var accent=Parse(s.accent);
  Colors["Surface"]=bg;Colors["Edge"]=edge;Colors["Accent"]=accent;
  Colors["Raised"]=Mix(bg,edge,.28);Colors["Border"]=edge;Colors["Selected"]=Mix(bg,accent,.32);
  Colors["Track"]=Mix(bg,edge,.75);Colors["Over"]=Mix(bg,accent,.22);
  Colors["Text"]=Mix(accent,Colors["Text"],.88);Colors["Bright"]=Mix(accent,Colors["Bright"],.7);Colors["Muted"]=Mix(accent,Colors["Text"],.38);
  Colors["Hover"]=Color.FromArgb(48,accent.R,accent.G,accent.B);Colors["Pressed"]=Color.FromArgb(96,accent.R,accent.G,accent.B);Colors["SoftEdge"]=Color.FromArgb(80,edge.R,edge.G,edge.B);
  Colors["MediaBase"]=Mix(bg,edge,.12);Colors["Shade"]=bg;
  Colors["Glow1"]=accent;Colors["Glow2"]=Mix(edge,accent,.4);Colors["Glow3"]=Mix(edge,accent,.7);
 }
 // Same roles as the bar: base tones from --nord-0..3, text from --nord-4/6, accents from the frost slots.
 void FromWallpaper(Dictionary<string,string> p){
  Func<string,Color> c=name=>WallpaperPalette.Get(p,name,"#2E3440");
  Func<Color,byte,Color> alpha=(color,a)=>Color.FromArgb(a,color.R,color.G,color.B);
  var bg=c("--nord-0");var accent=c("--nord-8");var edge=c("--edge");
  Colors["Surface"]=bg;Colors["Raised"]=c("--nord-1");Colors["Over"]=c("--nord-2");Colors["Track"]=c("--nord-3");
  Colors["Border"]=Mix(c("--nord-2"),edge,.35);Colors["Edge"]=edge;Colors["Accent"]=accent;
  Colors["Text"]=c("--nord-6");Colors["Bright"]=c("--nord-4");Colors["Muted"]=c("--muted");
  Colors["Selected"]=Mix(bg,accent,.3);Colors["Hover"]=alpha(Colors["Selected"],0x30);Colors["Pressed"]=alpha(c("--nord-10"),0x60);Colors["SoftEdge"]=alpha(edge,0x50);
  Colors["Shade"]=c("--shade");Colors["MediaBase"]=Mix(bg,Colors["Shade"],.5);
  Colors["Glow1"]=c("--nord-7");Colors["Glow2"]=c("--nord-15");Colors["Glow3"]=c("--nord-10");Colors["Partner"]=c("--nord-7");
 }
}
sealed class ThemeColor : System.ComponentModel.INotifyPropertyChanged {
 Color value;public Color Value {get{return value;}set{if(this.value==value)return;this.value=value;if(PropertyChanged!=null)PropertyChanged(this,new System.ComponentModel.PropertyChangedEventArgs("Value"));}}
 public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
}
sealed class ControlCenterTheme {
 bool listening;
 public static readonly ControlCenterTheme Current=new ControlCenterTheme();
 readonly Dictionary<string,SolidColorBrush> brushes=new Dictionary<string,SolidColorBrush>();
 readonly Dictionary<string,ThemeColor> colors=new Dictionary<string,ThemeColor>();
 string signature;public int Revision {get;private set;}
 Dictionary<string,Color> origins,targets;
 public ControlCenterTheme(){Start();Apply(new FocusState());}
 public void Start(){if(listening)return;listening=true;ThemeTransition.Frame+=PaintFrame;}
 public void Stop(){if(listening)ThemeTransition.Frame-=PaintFrame;listening=false;origins=targets=null;Changed=null;}
 public SolidColorBrush Brush(string role){return brushes[role];}
 public Color Color(string role){return Brush(role).Color;}
 public void Install(ResourceDictionary resources){foreach(var entry in brushes)resources["Theme"+entry.Key]=entry.Value;}
 public LinearGradientBrush Gradient(string first,string second,Point start,Point end){
  var gradient=new LinearGradientBrush{StartPoint=start,EndPoint=end,MappingMode=BrushMappingMode.Absolute};
  foreach(var role in new[]{first,second}){var stop=new GradientStop{Offset=gradient.GradientStops.Count};System.Windows.Data.BindingOperations.SetBinding(stop,GradientStop.ColorProperty,new System.Windows.Data.Binding("Value"){Source=colors[role]});gradient.GradientStops.Add(stop);}return gradient;
 }
 void PaintFrame(){
  if(targets==null)return;double t=ThemeTransition.Position;
  foreach(var entry in targets)colors[entry.Key].Value=ThemeTransition.Mix(origins[entry.Key],entry.Value,t);
  if(t>=1){origins=null;targets=null;}
 }
 /// <summary>Raised (on the UI thread) when the palette in effect changes, so every surface repaints together.</summary>
 public event Action Changed;
 internal static void SurfaceContrast(ControlCenterPalette palette,int amount){
  double lift=Math.Max(0,Math.Min(100,amount))/100.0;
  if(lift==0)return;
  foreach(var role in new[]{"Raised","Over","Track","Border","Selected"})
   palette.Colors[role]=ControlCenterPalette.Mix(palette.Colors[role],palette.Colors["Text"],lift*.16);
  foreach(var role in new[]{"Hover","Pressed"}){
   var c=palette.Colors[role];palette.Colors[role]=System.Windows.Media.Color.FromArgb((byte)Math.Min(255,c.A+lift*55),c.R,c.G,c.B);
  }
 }
 public void Apply(FocusState state){
  var s=state.settings;string wallpaper=state.ThemeActive?null:BarAppearance.PaletteKey;
  // Panel is the shell fill: Surface at the bar's opacity, so panel and bar read as one surface.
  // Lock-in keeps an opaque bar, so the panel stays opaque too.
  byte panelAlpha=state.ThemeActive?(byte)255:(byte)Math.Round(255*(1-BarAppearance.Settings.transparency/100.0));
  string key=(state.ThemeActive?s.background+"/"+s.edge+"/"+s.accent:wallpaper??"Nord")+"/"+panelAlpha+"/"+BarAppearance.Settings.surfaceContrast+"/"+(ThemeTransition.Current==null?"":ThemeTransition.Current.revision);if(key==signature)return;
  var palette=new ControlCenterPalette(state,wallpaper==null?null:BarAppearance.Palette);
  SurfaceContrast(palette,BarAppearance.Settings.surfaceContrast);
  var surface=palette.Colors["Surface"];palette.Colors["Panel"]=System.Windows.Media.Color.FromArgb(panelAlpha,surface.R,surface.G,surface.B);
  bool animate=ThemeTransition.Active&&colors.Count>0&&!state.ThemeActive;
  origins=new Dictionary<string,Color>();targets=animate?palette.Colors:null;
  foreach(var entry in palette.Colors){ThemeColor color;if(colors.TryGetValue(entry.Key,out color)){origins[entry.Key]=color.Value;if(!animate)color.Value=entry.Value;}else {color=new ThemeColor{Value=entry.Value};origins[entry.Key]=entry.Value;colors[entry.Key]=color;var brush=new SolidColorBrush();System.Windows.Data.BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new System.Windows.Data.Binding("Value"){Source=color,Mode=System.Windows.Data.BindingMode.OneWay});brushes[entry.Key]=brush;}}
  if(animate)PaintFrame();
  signature=key;Revision++;var changed=Changed;if(changed!=null)changed();
 }
}
