using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// Instance owner of appearance state and its preparation/commit pipeline. No
// state lock is held across dispatcher calls, disk palette writes or broadcasts.
sealed class ThemeCoordinator:IDisposable {
 readonly object gate=new object();readonly SemaphoreSlim pipeline=new SemaphoreSlim(1,1);
 readonly IAppearanceSettingsStore store;readonly IThemeHost host;readonly WallpaperLookPreparer preparer;readonly AppearancePublisher publisher;
 AppearanceSettings settings;Dictionary<string,string> palette;string wallpaper,wallpaperStamp,published="unpublished",changingWallpaper;
 ThemeTransitionData transition;long requested,appearanceRevision;Timer timer;bool disposed,sourceUnavailable;
 public bool AutoSync=true;public event Action Changed;
 public ThemeCoordinator(IAppearanceSettingsStore store,IThemeHost host){this.store=store;this.host=host;settings=store.Load().Copy();settings.Normalize();palette=Copy(host.LoadPalette());preparer=new WallpaperLookPreparer(host);publisher=new AppearancePublisher(host);appearanceRevision=(long)(host.Now*1000);}
 static Dictionary<string,string> Copy(Dictionary<string,string> value){return value==null?null:new Dictionary<string,string>(value);}
 static ThemeTransitionData Copy(ThemeTransitionData data){return data==null?null:new ThemeTransitionData{revision=data.revision,startsAt=data.startsAt,duration=data.duration,from=Copy(data.from),to=Copy(data.to)};}
 void BumpRevision(){appearanceRevision=Math.Max(appearanceRevision+1,(long)(host.Now*1000));}
 public AppearanceSettings Settings {get{lock(gate)return settings.Copy();}}
 public string Wallpaper {get{lock(gate)return wallpaper;}}
 public Dictionary<string,string> Palette {get{lock(gate)return settings.wallpaperColors?Copy(palette):null;}}
 public string PaletteKey {get{lock(gate)return settings.wallpaperColors&&palette!=null?(wallpaperStamp??"cached-wallpaper"):null;}}
 public void Start(){lock(gate){if(disposed||timer!=null)return;timer=new Timer(delegate{var pending=SynchronizeAsync();},null,0,3000);}}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;Interlocked.Increment(ref requested);if(timer!=null){timer.Dispose();timer=null;}}}
 public void Update(Action<AppearanceSettings> change){lock(gate){if(disposed)return;var next=settings.Copy();change(next);next.Normalize();store.Save(next);settings=next.Copy();Interlocked.Increment(ref requested);BumpRevision();}Refresh();var changed=Changed;if(changed!=null)changed();}
 public void Refresh(string upcoming=null){if(!AutoSync)return;lock(gate)if(disposed||changingWallpaper!=null)return;long request=Interlocked.Increment(ref requested);var pending=SynchronizeAsync(true,upcoming,request);}
 public async Task<ThemeTransitionData> BeginWallpaper(string path){if(!AutoSync)return null;long request;lock(gate){if(disposed)throw new ObjectDisposedException("ThemeCoordinator");changingWallpaper=path;request=Interlocked.Increment(ref requested);}
  while(true){var data=await SynchronizeAsync(true,path,request,700).ConfigureAwait(false);if(data!=null)return data;lock(gate){if(!disposed&&changingWallpaper==path&&request!=Interlocked.Read(ref requested)){request=Interlocked.Read(ref requested);continue;}}throw new InvalidOperationException("Wallpaper theme could not be prepared; keeping the current wallpaper.");}
 }
 public void EndWallpaper(){lock(gate)changingWallpaper=null;Refresh();}
 public void WarmWallpaperLook(string path){if(!AutoSync||!pipeline.Wait(0))return;try{lock(gate)if(disposed||changingWallpaper!=null)return;preparer.Warm(path,Settings);}catch(Exception e){host.Log("wallpaper prewarm: "+e.Message);}finally{pipeline.Release();}}
 public string FontSnapshot(){return publisher.FontSnapshot(Settings);}
 public string Snapshot(){AppearanceSettings now;Dictionary<string,string> colors;ThemeTransitionData fade;long revision;
  lock(gate){now=settings.Copy();colors=now.wallpaperColors?Copy(palette):null;fade=Copy(transition);revision=appearanceRevision;}
  return new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Serialize(new{appearance=new{revision,now.fontFamily,now.transparency,now.surfaceContrast,now.wallpaperColors,now.radius,now.edgeStyle,now.frame,now.motion,now.motionStyle,grainOpacity=now.grain/100.0*.3,bars=host.BarBounds,frost=now.frost?host.FrostStrips:null,palette=colors,transition=fade}});
 }
 void PublishState(){publisher.Publish(()=>Settings,Snapshot);}
 bool Current(long generation){lock(gate)return !disposed&&generation==Interlocked.Read(ref requested);}
 // Async serialization keeps requested refreshes ordered without blocking the UI
 // on a worker that is waiting for that same UI. Poll ticks skip a busy pipeline.
 internal async Task<ThemeTransitionData> SynchronizeAsync(bool wait=false,string upcoming=null,long request=0,int duration=450){
  if(!AutoSync)return null;if(wait)await pipeline.WaitAsync().ConfigureAwait(false);else if(!pipeline.Wait(0))return null;
  try{
   AppearanceSettings now;Dictionary<string,string> previous;string previousStamp,previousPublished;long generation;
   lock(gate){if(disposed||(changingWallpaper!=null&&upcoming==null))return null;generation=Interlocked.Read(ref requested);if(request!=0&&request!=generation)return null;now=settings.Copy();previous=Copy(palette);previousStamp=wallpaperStamp;previousPublished=published;}
   var look=await Task.Run(()=>preparer.Prepare(upcoming??host.CurrentWallpaper(),now,previousStamp,previous)).ConfigureAwait(false);
   if(!Current(generation))return null;
   if(now.wallpaperColors&&look.Palette==null){if(upcoming!=null)throw new InvalidOperationException("No usable wallpaper colors were extracted");if(!sourceUnavailable)host.Log("appearance: retaining the last wallpaper palette while the image source is unavailable");sourceUnavailable=true;PublishState();lock(gate)return Copy(transition);}sourceUnavailable=false;
   if(look.Key!=previousPublished||host.FrostPending||look.ClearFrost||upcoming!=null){
    var windows=await Task.Run(()=>look.Key!=previousPublished?host.PrepareWindows():new IntPtr[0]).ConfigureAwait(false);
    var data=new ThemeTransitionData{revision=Guid.NewGuid().ToString("N"),duration=(previousPublished=="unpublished"&&upcoming==null)||now.motion==0?0:(int)Math.Min(1500,Math.Round(duration*100.0/now.motion)),from=Copy(host.CurrentColors(previous)),to=Copy(look.Palette)};bool committed=false;
    await host.OnUiAsync(()=>{
     if(!Current(generation))return;if(!host.AnimationEnabled)data.duration=0;
     preparer.Commit(look);host.SavePalette(Copy(look.Palette));data.startsAt=host.Now+180;host.PublishTransition(Copy(data));
     lock(gate){palette=Copy(look.Palette);wallpaper=look.Path;wallpaperStamp=look.Stamp;transition=Copy(data);published=look.Key;BumpRevision();}
     host.ApplyTransition(Copy(data),windows);if(host.FrostRevision!=look.FrostBefore)host.NotifyFrost();committed=true;
    }).ConfigureAwait(false);
    if(!committed)return null;
   }else{preparer.Commit(look);if(host.FrostRevision!=look.FrostBefore)host.NotifyFrost();}
   PublishState();lock(gate)return Copy(transition);
  }catch(Exception e){host.Log("appearance sync: "+e.Message);return null;}finally{pipeline.Release();}
 }
}
