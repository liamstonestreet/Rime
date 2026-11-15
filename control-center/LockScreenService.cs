using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

sealed class LockScreenPreferences {public bool Enabled;public int Blur;}
interface ILockScreenPlatform {string CacheFolder {get;}void Build(string path,string target,int blur);Task SetImage(string path);void Dispatch(Action changed);void Log(string message);}
sealed class WindowsLockScreenPlatform:ILockScreenPlatform {
 public string CacheFolder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NordRice","LockScreen");}}
 public void Build(string path,string target,int blur){LockScreenImageBuilder.Build(path,target,blur);}
 public async Task SetImage(string path){var file=await WinRT.Await(Windows.Storage.StorageFile.GetFileFromPathAsync(path)).ConfigureAwait(false);await WinRT.Await(Windows.System.UserProfile.LockScreen.SetImageFileAsync(file)).ConfigureAwait(false);}
 public void Dispatch(Action action){var app=System.Windows.Application.Current;if(app!=null&&!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke(action);}
 public void Log(string message){WallpaperSwitcher.Log(message);}
}
sealed class LockScreenService:IDisposable {
 readonly SemaphoreSlim gate=new SemaphoreSlim(1);readonly Func<LockScreenPreferences> preferences;readonly ILockScreenPlatform platform;int version,slot;volatile bool disposed;
 public string Status {get;private set;}public event Action Changed;public Task Pending {get;private set;}
 public LockScreenService(Func<LockScreenPreferences> preferences,ILockScreenPlatform platform){this.preferences=preferences;this.platform=platform;Pending=Task.FromResult(0);Status="Updates after a wallpaper change. Uses a separate blurred copy.";}
 void Report(string text){if(disposed)return;Status=text;platform.Dispatch(()=>{if(disposed)return;var changed=Changed;if(changed!=null)changed();});}
 public void Queue(string path){int revision=Interlocked.Increment(ref version);var settings=preferences();if(disposed||!settings.Enabled||String.IsNullOrEmpty(path))return;Pending=Apply(path,settings.Blur,revision);}
 async Task Apply(string path,int blur,int revision){await gate.WaitAsync().ConfigureAwait(false);try{
  if(disposed||revision!=Volatile.Read(ref version)||!preferences().Enabled)return;
  var folder=platform.CacheFolder;Directory.CreateDirectory(folder);var target=Path.Combine(folder,"wallpaper-"+(slot++%2)+".jpg");
  Report("Preparing blurred lock-screen wallpaper...");await Task.Run(()=>platform.Build(path,target,blur)).ConfigureAwait(false);
  if(disposed||revision!=Volatile.Read(ref version)||!preferences().Enabled)return;
  await platform.SetImage(target).ConfigureAwait(false);Report("Lock screen updated from your wallpaper.");
 }catch(Exception ex){if(!disposed){platform.Log("lock screen: "+ex.Message);Report("Windows could not update the lock screen. Check Personalization > Lock screen, then try another wallpaper.");}}finally{gate.Release();}}
 public void Dispose(){disposed=true;Interlocked.Increment(ref version);Changed=null;}
}
