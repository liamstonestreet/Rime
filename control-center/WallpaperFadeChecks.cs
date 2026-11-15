using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;

static class WallpaperFadeChecks {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Until(Func<bool> condition){if(condition())return;var frame=new DispatcherFrame();var clock=Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(5)};timer.Tick+=delegate{if(condition()||clock.ElapsedMilliseconds>5000){timer.Stop();frame.Continue=false;}};timer.Start();Dispatcher.PushFrame(frame);Check(condition(),"Wallpaper fade check timed out");}
 static void Await(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
 static bool Disposed(System.Drawing.Bitmap picture){try{var width=picture.Width;return false;}catch(ArgumentException){return true;}catch(InvalidOperationException){return false;}}
 sealed class FakeCover : IWallpaperFadeCover {
  public byte Value;public bool Closed,Released;public readonly System.Drawing.Bitmap Picture;
  public FakeCover(System.Drawing.Bitmap picture){Picture=picture;}
  public byte Alpha {set{Check(!Closed,"Closed cover received an animation frame");Check(!Disposed(Picture),"Fade bitmap disposed while still visible");Value=value;}}
  public void Close(){Check(!Disposed(Picture),"Fade bitmap must survive until its window closes");Closed=true;}
  public void Dispose(){Released=true;}
 }
 sealed class FakePlatform:IWallpaperPlatform {
  public Func<string,System.Drawing.Rectangle[],string,WallpaperImage[]> BuildImages;
  public Func<System.Drawing.Rectangle,System.Drawing.Bitmap,IWallpaperFadeCover> CreateCover;
  public Func<IList<IWallpaperFadeCover>,ThemeTransitionData,Task> AnimateCovers;
  public Func<Task> Present;public Func<int,Task> Wait;public Func<string,Task> Commit;public Func<string,Task<ThemeTransitionData>> BeginTheme;public Action EndTheme;
  public System.Drawing.Rectangle[] Screens {get{return new[]{new System.Drawing.Rectangle(0,0,1920,1080),new System.Drawing.Rectangle(-2560,0,2560,1440)};}}public string Style {get{return "10";}}
  WallpaperImage[] IWallpaperPlatform.Build(string path,System.Drawing.Rectangle[] screens,string style){return BuildImages(path,screens,style);}
  IWallpaperFadeCover IWallpaperPlatform.CreateCover(System.Drawing.Rectangle bounds,System.Drawing.Bitmap picture){return CreateCover(bounds,picture);}
  Task IWallpaperPlatform.Animate(IList<IWallpaperFadeCover> covers,ThemeTransitionData data,System.Threading.CancellationToken token){return AnimateCovers(covers,data);}
  Task IWallpaperPlatform.Present(){return Present();}Task IWallpaperPlatform.Wait(int ms){return Wait(ms);}Task IWallpaperPlatform.Commit(string path){return Commit(path);}Task<ThemeTransitionData> IWallpaperPlatform.BeginTheme(string path){return BeginTheme(path);}void IWallpaperPlatform.EndTheme(){EndTheme();}
  public void Warm(string path){}public void Log(string text){}
 }
 internal static void Run(){
  var fake=new FakePlatform();var coordinator=new WallpaperCoordinator(fake);
  var pictures=new System.Collections.Concurrent.ConcurrentDictionary<string,WallpaperImage[]>();var windows=new List<FakeCover>();var committed=new List<string>();var events=new List<string>();var hold=new TaskCompletionSource<bool>();var tasks=new List<Task>();int fades=0;
  var buildStarted=new System.Threading.ManualResetEventSlim();var releaseBuild=new System.Threading.ManualResetEventSlim();var built=new System.Collections.Concurrent.ConcurrentQueue<string>();
  try{
   fake.BuildImages=(path,screens,style)=>{built.Enqueue(path);if(path=="build-hold"){buildStarted.Set();releaseBuild.Wait();}var result=screens.Select(screen=>new WallpaperImage{Bounds=screen,Picture=new System.Drawing.Bitmap(2,2)}).ToArray();pictures[path]=result;return result;};
   fake.CreateCover=(bounds,picture)=>{var cover=new FakeCover(picture);windows.Add(cover);events.Add("create-zero");return cover;};
   fake.Present=()=>{events.Add("present");return Task.FromResult(0);};fake.Wait=ms=>Task.FromResult(0);
   fake.AnimateCovers=(covers,timeline)=>{Check(covers.Count>0&&covers.Cast<FakeCover>().All(c=>c.Value==0),"Every monitor must enter the fade at alpha zero");Check(events.Last().StartsWith("theme:"),"Theme must be prepared before the wallpaper fade starts");Check(timeline!=null&&timeline.duration==700,"Wallpaper must receive the same theme clock");fades++;events.Add("fade");foreach(var cover in covers)cover.Alpha=255;return fades==1?hold.Task:Task.FromResult(true);};
   fake.Commit=path=>{Check(events.Last()=="present","Opaque covers must be presented before wallpaper commit");Check(windows.Where(w=>!w.Closed).All(w=>w.Value==255),"Wallpaper must not change before every cover is opaque");committed.Add(path);events.Add("commit:"+path);return Task.FromResult(0);};
   fake.BeginTheme=path=>{Check(events.Last()=="present"&&windows.Where(w=>!w.Closed).All(w=>w.Value==0),"Theme preparation must happen with every cover painted and transparent");events.Add("theme:"+path);return Task.FromResult(new ThemeTransitionData{revision=path,startsAt=ThemeTransition.Now+180,duration=700});};
   fake.EndTheme=()=>events.Add("theme-end");
   var preparationTasks=new List<Task>{coordinator.Prepare("build-hold")};Until(()=>buildStarted.IsSet);for(int i=0;i<10;i++)preparationTasks.Add(coordinator.Prepare("build-stale-"+i));preparationTasks.Add(coordinator.Prepare("build-latest"));releaseBuild.Set();Await(Task.WhenAll(preparationTasks));Check(built.Count==2&&built.Contains("build-latest"),"Obsolete queued preparations must be skipped rather than decoding every browsed image");Await(coordinator.ClearPrepared());Until(()=>pictures.Values.SelectMany(c=>c).All(c=>Disposed(c.Picture)));
   Await(coordinator.Prepare("cancelled-preview"));coordinator.DiscardUnused();Until(()=>pictures["cancelled-preview"].All(c=>Disposed(c.Picture)));
   Await(coordinator.Prepare("fade-A"));var first=coordinator.Apply("fade-A");tasks.Add(first);Until(()=>fades==1);coordinator.DiscardUnused();Check(pictures["fade-A"].All(c=>!Disposed(c.Picture)),"Closing the picker must never discard an active fade's buffers");
   Await(coordinator.Prepare("fade-B"));Check(pictures["fade-A"].All(c=>!Disposed(c.Picture)),"Browsing another image disposed the active fade buffers");var second=coordinator.Apply("fade-B");tasks.Add(second);var third=coordinator.Apply("fade-C");tasks.Add(third);hold.SetResult(true);Await(Task.WhenAll(tasks));
   Check(committed.SequenceEqual(new[]{"fade-A","fade-C"})&&fades==2,"Overlapping changes must serialize and skip superseded queued choices");Check(windows.All(w=>w.Closed&&w.Released),"Fade windows must always close and dispose");Until(()=>pictures.Values.SelectMany(c=>c).All(c=>Disposed(c.Picture)));
   fake.Commit=path=>{throw new InvalidOperationException("synthetic commit failure");};bool rejected=false;try{Await(coordinator.Apply("fade-failure"));}catch(InvalidOperationException){rejected=true;}Check(rejected&&windows.All(w=>w.Closed&&w.Released),"Commit failure must still close fade windows");
   fake.Commit=path=>{committed.Add(path);events.Add("commit:"+path);return Task.FromResult(0);};Await(coordinator.Apply("fade-recovery"));Check(committed.Last()=="fade-recovery","A failed transition must release the gate for subsequent changes");
   var shutdownHold=new TaskCompletionSource<bool>();fake.AnimateCovers=(covers,timeline)=>{foreach(var cover in covers)cover.Alpha=255;return shutdownHold.Task;};var shutdown=coordinator.Apply("fade-shutdown");tasks.Add(shutdown);Until(()=>windows.Any(w=>!w.Closed));coordinator.Dispose();Check(windows.All(w=>w.Closed&&w.Released),"Shutdown must close covers immediately");shutdownHold.SetResult(true);bool cancelled=false;try{Await(shutdown);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&!committed.Contains("fade-shutdown"),"Disposed coordinator committed wallpaper");Until(()=>pictures.Values.SelectMany(c=>c).All(c=>Disposed(c.Picture)));bool closed=false;try{coordinator.Prepare("after-disposal");}catch(ObjectDisposedException){closed=true;}Check(closed,"Disposed coordinator accepted preparation");

  }finally{
   releaseBuild.Set();hold.TrySetResult(true);if(tasks.Any(t=>!t.IsCompleted))Until(()=>tasks.All(t=>t.IsCompleted));Await(coordinator.ClearPrepared());coordinator.Dispose();buildStarted.Dispose();releaseBuild.Dispose();
  }
  // A tiny, off-screen native cover checks the real WinForms first-show lifecycle.
  using(var picture=new System.Drawing.Bitmap(2,2))using(var cover=new CoverWindow(new System.Drawing.Rectangle(-32000,-32000,2,2),picture,false)){
   Check(cover.Opacity==0&&!cover.Visible&&!cover.IsHandleCreated,"Cover opacity must be zero before native handle creation");bool shown=false;byte firstAlpha=255;double managed=1;cover.Shown+=delegate{uint key,flags;managed=cover.Opacity;Check(GetLayeredWindowAttributes(cover.Handle,out key,out firstAlpha,out flags),"Cannot inspect native cover alpha");shown=true;};
   cover.ShowPrepared(new System.Drawing.Rectangle(-32000,-32000,2,2));Until(()=>shown);Check(managed==0&&firstAlpha==0,"WinForms exposed an opaque first frame before the fade: managed="+managed+", native="+firstAlpha);cover.Alpha=128;Check(Math.Abs(cover.Opacity-128/255.0)<.00001,"Fade must update managed opacity");cover.Close();
  }
  Console.WriteLine("PASS: zero-alpha native first show, all-monitor preparation and shared wallpaper/theme clock before fading, commit behind opaque covers, active-buffer lifetime, coalesced preparations, serialized/latest queued changes, failure cleanup and active-fade shutdown. No real wallpaper changed.");
 }
 [DllImport("user32.dll",SetLastError=true)] static extern bool GetLayeredWindowAttributes(IntPtr window,out uint key,out byte alpha,out uint flags);
}
