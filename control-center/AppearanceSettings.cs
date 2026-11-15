using System;

sealed class AppearanceSettings {
 public string centerAnimation {get;set;}
 public string notificationAnimation {get;set;}
 public static readonly string[] PanelAnimations={"slide","fade"};
 public string fontFamily {get;set;}
 public int fontRevision {get;set;}
 public bool frost {get;set;}           // frosted glass: the blurred wallpaper behind every surface
 public int blur {get;set;}             // 0-100, frost blur radius
 public int brightness {get;set;}       // 0-100, frost brightness lift (50 = neutral)
 public int surfaceContrast {get;set;}  // 0-100, separation of controls from their containing surface
 public int transparency {get;set;}     // 0 = opaque tint, 100 = no tint (plain frost or desktop)
 public bool appWindowTint {get;set;}
 public bool appWindowTintRich {get;set;}
 public string appWindowTintExcluded {get;set;}
 public string notificationShortcut {get;set;}
 public bool wallpaperColors {get;set;}
 public int grain {get;set;}            // 0-100, film grain over the bar and control center
 public int radius {get;set;}           // 0-200, corner radius as a % of each element's designed radius
 public string edgeStyle {get;set;}     // the bar's bottom line: flat, gradient, glow or none
 public int motion {get;set;}           // 0-200, animation speed (100 = designed timing, 0 = off)
 public string motionStyle {get;set;}   // smooth or springy
 public string indicatorStyle {get;set;} // classic, gallery or ribbon
 public string wallpaperIndicatorStyle {get;set;} // classic, showcase or filmstrip
 public int borderWidth {get;set;}      // 1-8 px focus border (1 = Windows' own border only)
 public int mediaPlayerHeight {get;set;}
 public bool lockScreenSync {get;set;}
 public int lockScreenBlur {get;set;}
 public int ribbonSensitivity {get;set;}
 public bool calendarCompact {get;set;}
 public int centerWidth {get;set;}
 public int centerMaxHeight {get;set;}
 public int frame {get;set;}            // 0-24 px screen frame around each monitor (0 = off)
 public bool syncCorners {get;set;}     // GlazeWM window corners follow the corner radius
 public AppearanceSettings(){notificationShortcut="win+shift+n";fontFamily="Rubik";blur=50;brightness=50;radius=100;edgeStyle="flat";motion=100;motionStyle="smooth";indicatorStyle="classic";wallpaperIndicatorStyle="classic";borderWidth=1;centerWidth=759;centerMaxHeight=50;ribbonSensitivity=200;mediaPlayerHeight=100;lockScreenSync=true;lockScreenBlur=45;}
 public static readonly string[] WallpaperIndicatorStyles={"classic","showcase","filmstrip"};
 public static readonly string[] IndicatorStyles={"classic","gallery","ribbon","zebar"};
 public static readonly string[] EdgeStyles={"flat","gradient","glow","none"},MotionStyles={"smooth","springy"};
 internal AppearanceSettings Copy(){return (AppearanceSettings)MemberwiseClone();}
 internal void Normalize(){
  if(Array.IndexOf(PanelAnimations,centerAnimation)<0)centerAnimation="slide";
  if(Array.IndexOf(PanelAnimations,notificationAnimation)<0)notificationAnimation="slide";
  notificationShortcut=notificationShortcut==null?"win+shift+n":notificationShortcut.Trim().ToLowerInvariant();
  fontFamily=String.IsNullOrWhiteSpace(fontFamily)?"Rubik":fontFamily.Trim();
  if(Array.IndexOf(IndicatorStyles,indicatorStyle)<0)indicatorStyle="classic";
  if(Array.IndexOf(WallpaperIndicatorStyles,wallpaperIndicatorStyle)<0)wallpaperIndicatorStyle="classic";
  Func<int,int> pct=v=>Math.Max(0,Math.Min(100,v));
  surfaceContrast=pct(surfaceContrast);lockScreenBlur=pct(lockScreenBlur);mediaPlayerHeight=Math.Max(75,Math.Min(175,mediaPlayerHeight));ribbonSensitivity=Math.Max(25,Math.Min(400,ribbonSensitivity));centerMaxHeight=Math.Max(25,Math.Min(90,centerMaxHeight));centerWidth=Math.Max(480,Math.Min(1200,centerWidth));
  transparency=pct(transparency);grain=pct(grain);blur=pct(blur);brightness=pct(brightness);radius=Math.Max(0,Math.Min(200,radius));if(Array.IndexOf(EdgeStyles,edgeStyle)<0)edgeStyle="flat";motion=Math.Max(0,Math.Min(200,motion));borderWidth=Math.Max(1,Math.Min(8,borderWidth));frame=Math.Max(0,Math.Min(24,frame));if(Array.IndexOf(MotionStyles,motionStyle)<0)motionStyle="smooth";
 }
 public static AppearanceSettings Load(){return new AppearanceSettingsStore(System.IO.Path.Combine(AppCatalog.Folder,"bar-appearance.json")).Load();}
 public void Save(){new AppearanceSettingsStore(System.IO.Path.Combine(AppCatalog.Folder,"bar-appearance.json")).Save(this);}
}
