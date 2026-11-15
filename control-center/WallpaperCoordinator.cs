using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

sealed class WallpaperImage {public System.Drawing.Rectangle Bounds;public System.Drawing.Bitmap Picture;}
interface IWallpaperPlatform {
 System.Drawing.Rectangle[] Screens {get;}string Style {get;}
 WallpaperImage[] Build(string path,System.Drawing.Rectangle[] screens,string style);
 IWallpaperFadeCover CreateCover(System.Drawing.Rectangle bounds,System.Drawing.Bitmap picture);
 Task Present();Task Wait(int milliseconds);Task<ThemeTransitionData> BeginTheme(string path);void EndTheme();void Warm(string path);
 Task Animate(IList<IWallpaperFadeCover> covers,ThemeTransitionData timeline,CancellationToken cancellation);Task Commit(string path);void Log(string message);
}

// One owner for prepared bitmaps and all visible fade covers. Apply/dispose run
// on the UI dispatcher; decoding is serialized on workers. Visible transitions
// finish before newer choices, while queued obsolete choices are skipped.
sealed class WallpaperCoordinator:IDisposable {
 sealed class Preparation {public string Path;public Task<WallpaperImage[]> Task;public bool Retired,InUse,Disposed;}
 readonly IWallpaperPlatform platform;readonly object gate=new object();readonly SemaphoreSlim buildGate=new SemaphoreSlim(1),applyGate=new SemaphoreSlim(1);readonly CancellationTokenSource stop=new CancellationTokenSource();
 Preparation prepared,activePreparation;List<IWallpaperFadeCover> activeCovers;int requested;bool disposed;
 public WallpaperCoordinator(IWallpaperPlatform platform){this.platform=platform;}
 public Task Prepare(string path){return Covers(path,false).Task;}
 Preparation Covers(string path,bool pin){lock(gate){if(disposed)throw new ObjectDisposedException("WallpaperCoordinator");
  if(prepared==null||!String.Equals(path,prepared.Path,StringComparison.OrdinalIgnoreCase)||prepared.Task.IsFaulted){
   var screens=platform.Screens;var style=platform.Style;if(prepared!=null){prepared.Retired=true;Observe(prepared);}
   var next=new Preparation{Path=path};next.Task=Task.Run(async()=>{await buildGate.WaitAsync().ConfigureAwait(false);try{lock(gate)if(next.Retired&&!next.InUse)return new WallpaperImage[0];var pictures=platform.Build(path,screens,style);try{bool warm;lock(gate)warm=!disposed&&(!next.Retired||next.InUse);if(warm)platform.Warm(path);return pictures;}catch{foreach(var picture in pictures)picture.Picture.Dispose();throw;}}finally{buildGate.Release();}});
   prepared=next;Observe(next);
  }if(pin)prepared.InUse=true;return prepared;
 }}
 void Observe(Preparation preparation){preparation.Task.ContinueWith(task=>{if(task.IsFaulted){var observed=task.Exception;}DisposeReady(preparation);});}
 void DisposeReady(Preparation preparation){WallpaperImage[] pictures=null;lock(gate){if(preparation.Disposed||!preparation.Retired||preparation.InUse||!preparation.Task.IsCompleted)return;preparation.Disposed=true;if(preparation.Task.Status==TaskStatus.RanToCompletion)pictures=preparation.Task.Result;}if(pictures!=null)foreach(var picture in pictures)picture.Picture.Dispose();}
 void Release(Preparation preparation){lock(gate){preparation.InUse=false;preparation.Retired=true;if(ReferenceEquals(prepared,preparation))prepared=null;}DisposeReady(preparation);}
 public async Task ClearPrepared(){Preparation old;lock(gate){old=prepared;prepared=null;if(old!=null)old.Retired=true;}if(old!=null){try{await old.Task;}catch{}DisposeReady(old);}}
 public void DiscardUnused(){Preparation old;lock(gate){old=prepared;if(old==null||old.InUse)return;prepared=null;old.Retired=true;}Observe(old);}
 void Close(List<IWallpaperFadeCover> covers){if(covers==null)return;foreach(var cover in covers){try{cover.Close();}catch(Exception ex){platform.Log("fade close: "+ex.Message);}finally{try{cover.Dispose();}catch(Exception ex){platform.Log("fade dispose: "+ex.Message);}}}covers.Clear();}
 public async Task Apply(string path){int version=Interlocked.Increment(ref requested);await applyGate.WaitAsync(stop.Token);Preparation preparation=null;var covers=new List<IWallpaperFadeCover>();bool theme=false;
  try{stop.Token.ThrowIfCancellationRequested();if(version!=Volatile.Read(ref requested))return;preparation=Covers(path,true);activePreparation=preparation;activeCovers=covers;WallpaperImage[] pictures;
   try{pictures=await preparation.Task;}catch(Exception ex){throw new InvalidOperationException("Wallpaper preparation failed; keeping the current wallpaper.",ex);}
   stop.Token.ThrowIfCancellationRequested();if(version!=Volatile.Read(ref requested))return;
   foreach(var picture in pictures)covers.Add(platform.CreateCover(picture.Bounds,picture.Picture));await platform.Present();stop.Token.ThrowIfCancellationRequested();
   theme=true;var timeline=await platform.BeginTheme(path);stop.Token.ThrowIfCancellationRequested();await platform.Animate(covers,timeline,stop.Token);stop.Token.ThrowIfCancellationRequested();
   await platform.Present();stop.Token.ThrowIfCancellationRequested();await platform.Commit(path);await platform.Wait(250);stop.Token.ThrowIfCancellationRequested();await platform.Present();
  }finally{try{Close(covers);}finally{activeCovers=null;activePreparation=null;try{if(preparation!=null)Release(preparation);if(theme)platform.EndTheme();}finally{applyGate.Release();}}}
 }
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;Interlocked.Increment(ref requested);if(prepared!=null)prepared.Retired=true;}
  stop.Cancel();Close(activeCovers);if(activePreparation!=null)Release(activePreparation);DiscardUnused();
 }
}
