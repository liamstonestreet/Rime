using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

// A single fade owns both its render subscription and hidden-surface completion
// wake-up. Cancellation removes them on their dispatcher before covers are freed.
static class WallpaperFadeAnimator {
 public static async Task Run(IList<IWallpaperFadeCover> covers,ThemeTransitionData timeline,CancellationToken cancellation){
  if(covers.Count==0)return;timeline=timeline??new ThemeTransitionData{startsAt=ThemeTransition.Now,duration=700};var dispatcher=Dispatcher.CurrentDispatcher;var done=new TaskCompletionSource<bool>();var timer=new DispatcherTimer(DispatcherPriority.Render);EventHandler frame=null;
  Action tick=()=>{try{cancellation.ThrowIfCancellationRequested();double t=ThemeTransition.Progress(timeline,ThemeTransition.Now);foreach(var cover in covers)cover.Alpha=(byte)Math.Round(255*t);if(t>=1)done.TrySetResult(true);}catch(OperationCanceledException){done.TrySetCanceled();}catch(Exception ex){done.TrySetException(ex);}};
  frame=delegate{tick();};timer.Tick+=frame;timer.Interval=TimeSpan.FromMilliseconds(Math.Max(1,timeline.startsAt+timeline.duration-ThemeTransition.Now+1));
  CompositionTarget.Rendering+=frame;timer.Start();var registration=cancellation.Register(()=>{if(dispatcher.CheckAccess())done.TrySetCanceled();else if(!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(new Action(()=>done.TrySetCanceled()));});
  try{cancellation.ThrowIfCancellationRequested();tick();await done.Task;}finally{registration.Dispose();timer.Stop();timer.Tick-=frame;CompositionTarget.Rendering-=frame;}
 }
}
