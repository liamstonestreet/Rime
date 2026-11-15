using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

sealed class AppServiceStep {
 public readonly string Name;public readonly Action Start,Stop;
 public AppServiceStep(string name,Action start,Action stop){Name=name;Start=start;Stop=stop;}
}
// Used on the application dispatcher. Arm cleanup before starting each step so
// partial initialization is unwound too. Cleanup failures cannot skip later owners.
sealed class AppServices:IDisposable {
 readonly AppServiceStep[] steps;readonly List<AppServiceStep> armed=new List<AppServiceStep>();readonly Action<string> log;bool starting,started,disposed;
 public NordControlCenter Window {get;private set;}
 public AppServices(IEnumerable<AppServiceStep> steps,Action<string> log){this.steps=steps.ToArray();this.log=log;}
 public void Start(){if(disposed)throw new ObjectDisposedException("AppServices");if(started)return;if(starting)throw new InvalidOperationException("Application startup is already in progress");starting=true;
  try{foreach(var step in steps){armed.Add(step);step.Start();if(disposed)throw new ObjectDisposedException("AppServices");}started=true;}catch{Dispose();throw;}finally{starting=false;}
 }
 // Production yields between stages so the frame can actually paint before slower UI construction.
 public async System.Threading.Tasks.Task StartAsync(Func<Action,System.Threading.Tasks.Task> dispatch){
  if(disposed)throw new ObjectDisposedException("AppServices");if(started)return;if(starting)throw new InvalidOperationException("Application startup is already in progress");starting=true;
  try{foreach(var step in steps){await dispatch(()=>{if(disposed)throw new ObjectDisposedException("AppServices");armed.Add(step);var watch=System.Diagnostics.Stopwatch.StartNew();step.Start();if(watch.ElapsedMilliseconds>=250||step.Name=="screen frame")log("startup "+step.Name+": "+watch.ElapsedMilliseconds+" ms");if(disposed)throw new ObjectDisposedException("AppServices");});}started=true;}catch{Dispose();throw;}finally{starting=false;}
 }
 public void Dispose(){if(disposed)return;disposed=true;for(int i=armed.Count-1;i>=0;i--)try{armed[i].Stop();}catch(Exception ex){try{log("shutdown "+armed[i].Name+": "+ex.GetType().Name+" "+ex.Message);}catch{}}armed.Clear();}
 public static AppServices CreateProduction(Func<NordControlCenter> createWindow,EventWaitHandle signal){AppServices services=null;var router=new WorkspaceLaunchRouter();MediaBridge bridge=null;
  var steps=new[]{
   new AppServiceStep("theme clock",()=>{},ThemeTransition.Stop),
   new AppServiceStep("theme brushes",()=>ControlCenterTheme.Current.Start(),()=>ControlCenterTheme.Current.Stop()),
   new AppServiceStep("frost staging",()=>{},FrostMaterial.Stop),
   new AppServiceStep("screen frame",ScreenFrame.Start,ScreenFrame.Stop),
   new AppServiceStep("CC window",()=>services.Window=createWindow(),()=>{if(services.Window!=null){services.Window.Dispose();if(!services.Window.HasClosed)services.Window.Close();}}),
   new AppServiceStep("focus recovery",FocusMode.Initialize,FocusMode.Stop),
   new AppServiceStep("workspace watcher",WorkspaceSync.Start,WorkspaceSync.Stop),
   new AppServiceStep("startup routing",router.Start,router.Dispose),
   new AppServiceStep("native borders",GlazeBorders.Start,GlazeBorders.Stop),
   new AppServiceStep("appearance",BarAppearance.Start,BarAppearance.Stop),
   new AppServiceStep("app window tint",AppWindowTint.Start,AppWindowTint.Stop),
   new AppServiceStep("notifications",NotificationCenter.Start,NotificationCenter.Stop),
   new AppServiceStep("notification popup",NotificationPopup.Start,NotificationPopup.Stop),
   new AppServiceStep("bar state",BarState.Start,BarState.Stop),
   new AppServiceStep("usage history",()=>{},AppUsage.Stop),
   new AppServiceStep("wallpaper pipeline",()=>{},DesktopWallpaper.Stop),
   new AppServiceStep("lock-screen sync",()=>{},LockScreenWallpaper.Stop),
   new AppServiceStep("power overlays",()=>{},PowerMenu.Stop),
   new AppServiceStep("wallpaper indicator",WallpaperSwitcher.Start,WallpaperSwitcher.Stop),
   new AppServiceStep("focus overlay",FocusBorder.Start,FocusBorder.Stop),
   new AppServiceStep("game presentation",GamePresentation.Start,GamePresentation.Stop),
   new AppServiceStep("window corners",GlazeCorners.Start,GlazeCorners.Stop),
   new AppServiceStep("menu popup",()=>{},NordMenu.Stop),
   new AppServiceStep("tray popup",()=>{},TrayPopup.Stop),
   new AppServiceStep("quick actions",()=>{},QuickActions.Stop),
   new AppServiceStep("CC interaction",()=>services.Window.StartInteraction(signal),()=>services.Window.StopInteraction()),
   new AppServiceStep("bar bridge",()=>{bridge=new MediaBridge(MediaBridge.Receive);bridge.Start();},()=>{if(bridge!=null)bridge.Dispose();})
  };services=new AppServices(steps,WallpaperSwitcher.Log);return services;
 }
}
