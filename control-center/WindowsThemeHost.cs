using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// External effects only. The coordinator and look preparation can be exercised
// with a fake host without touching desktop, palette files, borders or sockets.
interface IThemeHost {
 double Now {get;}bool AnimationEnabled {get;}
 string CurrentWallpaper();string Stamp(string path);
 Dictionary<string,string> LoadPalette();Dictionary<string,string> ExtractPalette(string path);Dictionary<string,string> CurrentColors(Dictionary<string,string> fallback);
 void RenderFrost(string path,int blur,int brightness);void ClearFrost();void CommitFrost();void NotifyFrost();int FrostRevision {get;}bool FrostPending {get;}
 object FrostStrips {get;}object BarBounds {get;}
 IntPtr[] PrepareWindows();void SavePalette(Dictionary<string,string> palette);void PublishTransition(ThemeTransitionData data);void ApplyTransition(ThemeTransitionData data,IntPtr[] windows);
 Task OnUiAsync(Action action);string FontData(string family);void Broadcast(string payload);void Log(string message);
}
sealed class WindowsThemeHost:IThemeHost {
 public double Now {get{return ThemeTransition.Now;}}public bool AnimationEnabled {get{return System.Windows.SystemParameters.ClientAreaAnimation;}}
 public string CurrentWallpaper(){return WallpaperSource.Current();}
 public string Stamp(string path){return path==null?null:path+"|"+File.GetLastWriteTimeUtc(path).Ticks;}
 public Dictionary<string,string> LoadPalette(){return WallpaperPalette.Load();}
 public Dictionary<string,string> ExtractPalette(string path){return WallpaperTheme.From(path);}
 public Dictionary<string,string> CurrentColors(Dictionary<string,string> fallback){return ThemeTransition.Active?WallpaperPalette.Interpolate(ThemeTransition.Current.from,ThemeTransition.Current.to,ThemeTransition.Position):fallback;}
 public void RenderFrost(string path,int blur,int brightness){FrostMaterial.Render(path,blur,brightness,false);}
 public void ClearFrost(){FrostMaterial.Clear();}public void CommitFrost(){FrostMaterial.Commit();}public void NotifyFrost(){FrostMaterial.Notify();}
 public int FrostRevision {get{return FrostMaterial.Revision;}}public bool FrostPending {get{return FrostMaterial.HasPending;}}
 public object FrostStrips {get{return FrostMaterial.Strips;}}
 public object BarBounds {get{return ScreenFrame.BarBounds.Select(r=>new{x=r.X,y=r.Y,width=r.Width,height=r.Height}).ToArray();}}
 public IntPtr[] PrepareWindows(){return GlazeBorders.PrepareWindows();}
 public void SavePalette(Dictionary<string,string> palette){WallpaperPalette.Save(palette);}
 public void PublishTransition(ThemeTransitionData data){ThemeTransition.Publish(data);}
 public void ApplyTransition(ThemeTransitionData data,IntPtr[] windows){ThemeTransition.Use(data);ControlCenterTheme.Current.Apply(FocusMode.State);GlazeBorders.Begin(data,windows);}
 public Task OnUiAsync(Action action){var app=System.Windows.Application.Current;if(app==null||app.Dispatcher.CheckAccess()){action();return Task.FromResult(0);}return app.Dispatcher.InvokeAsync(action).Task;}
 public string FontData(string family){return RiceFont.WebData(family);}
 public void Broadcast(string payload){MediaBridge.Broadcast(payload);}public void Log(string message){WallpaperSwitcher.Log(message);}
}
