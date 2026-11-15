using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
static class LockScreenChecks {
 sealed class FakePlatform:ILockScreenPlatform {
  public Func<string,Task> Setter;public Action<string,string,int> Builder;public string Folder;
  public string CacheFolder {get{return Folder;}}
  public void Build(string source,string target,int blur){if(Builder!=null)Builder(source,target,blur);else LockScreenImageBuilder.Build(source,target,blur);}
  public Task SetImage(string path){return Setter(path);}public void Dispatch(Action action){action();}public void Log(string message){}
 }

 public static void Run(){
  var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lock-screen-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var preferences=new LockScreenPreferences{Enabled=true,Blur=35};var fake=new FakePlatform{Folder=folder};var service=new LockScreenService(()=>preferences,fake);
  try{
   var source=Path.Combine(folder,"source.png");using(var image=new System.Drawing.Bitmap(64,64,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){using(var g=System.Drawing.Graphics.FromImage(image)){g.Clear(System.Drawing.Color.Black);g.FillRectangle(System.Drawing.Brushes.White,32,0,32,64);}image.Save(source,System.Drawing.Imaging.ImageFormat.Png);}
   byte[] original=File.ReadAllBytes(source);var sharp=Path.Combine(folder,"sharp.jpg");var soft=Path.Combine(folder,"soft.jpg");LockScreenWallpaper.Build(source,sharp,0);LockScreenWallpaper.Build(source,soft,100);
   using(var a=new System.Drawing.Bitmap(sharp))using(var b=new System.Drawing.Bitmap(soft)){if(a.Width!=64||b.Width!=64||Math.Abs(a.GetPixel(31,32).R-a.GetPixel(32,32).R)<=Math.Abs(b.GetPixel(31,32).R-b.GetPixel(32,32).R))throw new Exception("Lock-screen blur must soften edges without changing dimensions");}
   if(!original.SequenceEqual(File.ReadAllBytes(source)))throw new Exception("Original wallpaper was modified");
   int calls=0;fake.Setter=path=>{if(!File.Exists(path))throw new Exception("Missing lock-screen copy");calls++;return Task.FromResult(0);};preferences.Enabled=true;preferences.Blur=35;service.Queue(source);service.Pending.GetAwaiter().GetResult();if(calls!=1)throw new Exception("Committed wallpaper did not update lock-screen copy");
   preferences.Enabled=false;service.Queue(source);if(calls!=1)throw new Exception("Disabled lock-screen sync still applied");
   preferences.Enabled=true;var entered=new System.Threading.ManualResetEventSlim();var hold=new TaskCompletionSource<bool>();calls=0;fake.Setter=path=>{calls++;entered.Set();return calls==1?hold.Task:Task.FromResult(true);};service.Queue(source);var first=service.Pending;if(!entered.Wait(5000)){hold.TrySetResult(true);throw new Exception("Lock-screen worker did not reach setter");}service.Queue(source);var stale=service.Pending;service.Queue(source);var latest=service.Pending;hold.SetResult(true);Task.WhenAll(first,stale,latest).GetAwaiter().GetResult();entered.Dispose();if(calls!=2)throw new Exception("Rapid wallpaper changes must skip obsolete queued lock-screen copies");
   fake.Setter=path=>{throw new Exception("Simulated Windows policy rejection");};service.Queue(source);service.Pending.GetAwaiter().GetResult();if(!service.Status.Contains("could not"))throw new Exception("Windows rejection must be visible without breaking wallpaper switching");
   using(var started=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()){
    int writes=0;var effects=new FakePlatform{Folder=folder,Builder=(sourcePath,target,amount)=>{started.Set();release.Wait();},Setter=path=>{writes++;return Task.FromResult(0);}};var owner=new LockScreenService(()=>new LockScreenPreferences{Enabled=true,Blur=45},effects);owner.Queue(source);if(!started.Wait(3000)){release.Set();throw new Exception("Shutdown fixture did not build");}owner.Dispose();release.Set();owner.Pending.GetAwaiter().GetResult();if(writes!=0)throw new Exception("Disposed lock-screen owner called Windows setter");
   }
   var model=new AppearanceSettings();if(!model.lockScreenSync||model.lockScreenBlur!=45)throw new Exception("Lock-screen defaults incorrect");
   Console.WriteLine("PASS: independent blurred lock-screen copy, original preservation, disabled sync and visible Windows rejection. Mock setter only; no real lock screen changed.");
  }finally{service.Dispose();foreach(var path in Directory.GetFiles(folder))File.Delete(path);Directory.Delete(folder);}
 }
}
