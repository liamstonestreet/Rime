using System;
using System.Threading.Tasks;

static class LockScreenWallpaper {
 static readonly LockScreenService service=new LockScreenService(()=>{var settings=BarAppearance.Settings;return new LockScreenPreferences{Enabled=settings.lockScreenSync,Blur=settings.lockScreenBlur};},new WindowsLockScreenPlatform());
 public static string Status {get{return service.Status;}}
 public static event Action Changed {add{service.Changed+=value;}remove{service.Changed-=value;}}
 internal static Task Pending {get{return service.Pending;}}
 public static void Queue(string path){service.Queue(path);}
 public static void Stop(){service.Dispose();}
 internal static void Build(string source,string destination,int blur){LockScreenImageBuilder.Build(source,destination,blur);}
 internal static void Blur(System.Drawing.Bitmap bitmap,int amount){LockScreenImageBuilder.Blur(bitmap,amount);}
}
