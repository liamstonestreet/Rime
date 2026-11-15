using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Compatibility facade: callers keep the existing API while one owned coordinator
// controls persistence, preparation, revisions and publication.
static class BarAppearance {
 static readonly ThemeCoordinator coordinator=new ThemeCoordinator(new AppearanceSettingsStore(System.IO.Path.Combine(AppCatalog.Folder,"bar-appearance.json")),new WindowsThemeHost());
 internal static bool AutoSync {get{return coordinator.AutoSync;}set{coordinator.AutoSync=value;}}
 public static AppearanceSettings Settings {get{return coordinator.Settings;}}
 public static string Wallpaper {get{return coordinator.Wallpaper;}}
 public static Dictionary<string,string> Palette {get{return coordinator.Palette;}}
 public static string PaletteKey {get{return coordinator.PaletteKey;}}
 public static double GrainOpacity {get{return Settings.grain/100.0*.3;}}
 public static event Action Changed {add{coordinator.Changed+=value;}remove{coordinator.Changed-=value;}}
 public static void Start(){coordinator.Start();}
 public static void Stop(){coordinator.Dispose();}
 public static void Update(Action<AppearanceSettings> change){coordinator.Update(change);}
 public static void Refresh(string upcoming=null){coordinator.Refresh(upcoming);}
 public static Task<ThemeTransitionData> BeginWallpaper(string path){return coordinator.BeginWallpaper(path);}
 public static void EndWallpaper(){coordinator.EndWallpaper();}
 internal static void WarmWallpaperLook(string path){coordinator.WarmWallpaperLook(path);}
 public static string Snapshot(){return coordinator.Snapshot();}
 public static string FontSnapshot(){return coordinator.FontSnapshot();}
}
