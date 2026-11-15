using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

static class ControlCenterRequests {
 internal static readonly string Position=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"control-center-position.txt");
 public static bool ValidAnchor(string key){return System.Text.RegularExpressions.Regex.IsMatch(key??"",@"^-?\d{1,6}_-?\d{1,6}$");}
 public static void Register(string[] args){if(args.Length==9&&args[0]=="--register"&&ValidAnchor(args[1]))File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"center-anchor-"+args[1]+".txt"),String.Join(" ",args.Skip(2)));if(args.Length==3||args.Length==7)File.WriteAllText(Position,String.Join(" ",args));}
}
// Composition root: registration and single-instance signalling precede WPF;
// AppServices owns startup/rollback/shutdown independently of the window layout.
static class ControlCenterApp {
 public static void Run(string[] args){if(args.Length>0&&args[0]=="--power"){PowerMenu.Run(args);return;}ControlCenterRequests.Register(args);bool created;
  using(var mutex=new Mutex(true,"Local\\NordControlCenter",out created))using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\NordControlCenterToggle")){
   if(!created){signal.Set();return;}var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};DisplayCadence.Initialize();
   using(var services=AppServices.CreateProduction(()=>new NordControlCenter(),signal)){
    ExitEventHandler exit=(sender,eventArgs)=>services.Dispose();app.Exit+=exit;
    try{
     app.Dispatcher.BeginInvoke(new Action(async ()=>{try{
      await services.StartAsync(action=>app.Dispatcher.InvokeAsync(action,System.Windows.Threading.DispatcherPriority.Background).Task);
      app.MainWindow=services.Window;app.ShutdownMode=ShutdownMode.OnMainWindowClose;if(args.Length==0||args[0]!="--register")services.Window.Open();
     }catch(Exception ex){WallpaperSwitcher.Log("CC startup: "+ex);Environment.ExitCode=1;if(!app.Dispatcher.HasShutdownStarted)app.Shutdown(1);}}));
     app.Run();
    }
    catch(Exception ex){WallpaperSwitcher.Log("CC startup: "+ex);Environment.ExitCode=1;}
    finally{services.Dispose();app.Exit-=exit;}
   }
  }
 }
}
