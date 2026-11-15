using System.Collections.Generic;

sealed class PreparedWallpaperLook {
 public readonly string Path,Stamp,Key;public readonly Dictionary<string,string> Palette;public readonly int FrostBefore;public readonly bool ClearFrost;
 public PreparedWallpaperLook(string path,string stamp,Dictionary<string,string> palette,int frostBefore,bool clearFrost=false){ClearFrost=clearFrost;Path=path;Stamp=stamp;Palette=palette;Key=palette==null?null:stamp;FrostBefore=frostBefore;}
}
// Access is serialized by the coordinator's async pipeline. Warm data has one owner
// and the same stamp key as committed data; no independent timer or publication.
sealed class WallpaperLookPreparer {
 readonly IThemeHost host;string warmKey;Dictionary<string,string> warmPalette;bool cleared;
 public WallpaperLookPreparer(IThemeHost host){this.host=host;}
 public void Warm(string path,AppearanceSettings settings){string stamp=host.Stamp(path);if(settings.wallpaperColors&&warmKey!=stamp){warmPalette=host.ExtractPalette(path);warmKey=stamp;}if(settings.frost)host.RenderFrost(path,settings.blur,settings.brightness);}
 public void Commit(PreparedWallpaperLook look){if(look.ClearFrost){host.ClearFrost();cleared=true;}else host.CommitFrost();}
 public PreparedWallpaperLook Prepare(string path,AppearanceSettings settings,string previousStamp,Dictionary<string,string> previous){
  string stamp=host.Stamp(path);var palette=settings.wallpaperColors?(stamp==previousStamp&&previous!=null?previous:stamp==warmKey&&warmPalette!=null?warmPalette:path==null?null:host.ExtractPalette(path)):null;
  int revision=host.FrostRevision;
  // A missing color source is not permission to clear either the palette or frost.
  if(settings.wallpaperColors&&palette==null)return new PreparedWallpaperLook(path,stamp,null,revision);
  if(settings.frost){host.RenderFrost(path,settings.blur,settings.brightness);cleared=false;}
  return new PreparedWallpaperLook(path,stamp,palette,revision,!settings.frost&&!cleared);
 }
}
