using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;

/// <summary>
/// GlazeWM's window borders in the bar's edge color: the focused window gets the edge itself, other
/// windows the edge blended halfway into the background. Applies final colors once; GlazeWM caches the same final colors via one preserved-focus reload. Rewrites only the two border color lines under
/// window_effects, only when they change. Lock-in owns those lines while it runs
/// (it restores them by exact text), so syncing waits until it ends.
/// </summary>
static class GlazeBorders {
 const string NordEdge="#6b86a3",NordBase="#2e3440"; // the bar's defaults (src/index.css)
 static readonly string Config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@".glzr\glazewm\config.yaml");
 internal static Func<string> ReadConfig=()=>File.ReadAllText(Config);
 internal static Action<string> WriteConfig=text=>File.WriteAllText(Config,text);
 static string applied;
 static IntPtr[] animated=new IntPtr[0];static bool listening;
 static bool focusedEnabled,otherEnabled;
 static IntPtr focusedTarget;
 static ThemeTransitionData latest;
 static volatile bool stopped;static int lifetime;static readonly List<IntPtr> hooks=new List<IntPtr>();
 static WinEventProc events;static DispatcherTimer settle,discover;static bool discovering;
 public static void Start(){stopped=false;}
 public static void Stop(){stopped=true;++lifetime;listening=false;if(settle!=null){settle.Stop();settle=null;}if(discover!=null){discover.Stop();discover=null;}foreach(var hook in hooks)if(hook!=IntPtr.Zero)UnhookWinEvent(hook);hooks.Clear();events=null;latest=null;animated=new IntPtr[0];}
 internal static IntPtr[] PrepareWindows(){
  if(stopped)return new IntPtr[0];
  if(FocusMode.State.ThemeActive||FocusMode.Transitioning)return new IntPtr[0];
  try{
   focusedEnabled=GlazeConfig.Get("window_effects/focused_window/border/enabled")!="false";
   otherEnabled=GlazeConfig.Get("window_effects/other_windows/border/enabled")!="false";
   var result=new List<IntPtr>();var root=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(GlazeCommand.Run("query windows"));
   focusedTarget=IntPtr.Zero;
   foreach(Dictionary<string,object> w in (System.Collections.IEnumerable)((Dictionary<string,object>)root["data"])["windows"]){var h=new IntPtr(Convert.ToInt64(w["handle"]));if(GameWindows.IsGame(h))continue;result.Add(h);object focus;if(w.TryGetValue("hasFocus",out focus)&&Convert.ToBoolean(focus))focusedTarget=h;}
   return result.ToArray();
  }catch{return new IntPtr[0];}
 }
 internal static void Begin(ThemeTransitionData data,IntPtr[] windows){
  if(stopped)return;latest=data;if(windows.Length>0)animated=windows;if(!listening){listening=true;Watch();}PaintColors(data,1);ThreadPool.QueueUserWorkItem(delegate{SyncNative(data);});
 }
 static void Watch(){
  settle=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};settle.Tick+=delegate{settle.Stop();PaintFinal();};
  discover=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};discover.Tick+=async delegate{
   discover.Stop();if(discovering)return;discovering=true;
   try{int version=lifetime;var windows=await System.Threading.Tasks.Task.Run(()=>PrepareWindows());if(stopped||version!=lifetime)return;animated=windows;PaintFinal();}finally{discovering=false;}
  };
  events=(hook,e,window,obj,child,thread,time)=>{
   if(stopped||obj!=0||window==IntPtr.Zero)return;var app=Application.Current;if(app==null||app.Dispatcher.HasShutdownStarted)return;
   app.Dispatcher.BeginInvoke(new Action(()=>{if(stopped||settle==null||discover==null)return;if(e==0x800B&&Array.IndexOf(animated,window)<0)return;if(e!=0x800B)PaintFinal();settle.Stop();settle.Start();if(e!=0x800B&&Array.IndexOf(animated,window)<0){discover.Stop();discover.Start();}}));
  };
  hooks.Add(SetWinEventHook(3,3,IntPtr.Zero,events,0,0,0));
  hooks.Add(SetWinEventHook(0x8002,0x8002,IntPtr.Zero,events,0,0,0));
  hooks.Add(SetWinEventHook(0x800B,0x800B,IntPtr.Zero,events,0,0,0)); // one settled repaint after moving/resizing managed windows
 }
 static void PaintFinal(){if(stopped||latest==null)return;PaintColors(latest,1);}
 static void PaintColors(ThemeTransitionData data,double t){
  if(FocusMode.State.ThemeActive||FocusMode.Transitioning)return;
  var edge=ThemeTransition.Mix(WallpaperPalette.Get(data.from,"--edge",NordEdge),WallpaperPalette.Get(data.to,"--edge",NordEdge),t);
  var bg=ThemeTransition.Mix(WallpaperPalette.Get(data.from,"--nord-0",NordBase),WallpaperPalette.Get(data.to,"--nord-0",NordBase),t);
  var other=ThemeTransition.Mix(edge,bg,.5);var foreground=GetForegroundWindow();
  if(Array.IndexOf(animated,foreground)<0)foreground=focusedTarget; // CC/other ignored overlays do not change GlazeWM's managed focus
  foreach(var h in animated){bool focused=h==foreground;if(!(focused?focusedEnabled:otherEnabled)||!IsWindow(h))continue;var c=focused?edge:other;int rgb=c.R|(c.G<<8)|(c.B<<16);DwmSetWindowAttribute(h,34,ref rgb,4);}
 }
 delegate void WinEventProc(IntPtr hook,uint e,IntPtr window,int obj,int child,uint thread,uint time);
 [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern bool IsWindow(IntPtr h);
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);

 internal static Action ReloadNative=GlazeCommand.ReloadConfig;
 static readonly object nativeGate=new object();
 static void SyncNative(ThemeTransitionData data){lock(nativeGate){if(stopped||latest!=data||FocusMode.State.ThemeActive||FocusMode.Transitioning)return;Sync(data.to,true);}}
 public static void Sync(Dictionary<string,string> palette){Sync(palette,false);}
 internal static void Sync(Dictionary<string,string> palette,bool refreshNative){
  if(stopped)return;
  if(FocusMode.State.ThemeActive||FocusMode.Transitioning)return;
  string edge=Pick(palette,"--edge",NordEdge),other=Blend(edge,Pick(palette,"--nord-0",NordBase),.5);
  if(edge+other==applied)return;
  try{
   using(var mutex=new Mutex(false,"Local\\LiamNordWorkspaceRules")){ // shared with the other GlazeWM config editors
    bool held=false;
    try{
     try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}
     if(!held)return;
     // A previous fade's persistence job may have waited behind another config editor.
     // Never persist stale colors over a newer transition.
     var current=ThemeTransition.Current;if(ThemeTransition.Active&&!refreshNative)return;
     if(current!=null){var desired=Pick(current.to,"--edge",NordEdge);if(edge!=desired||other!=Blend(desired,Pick(current.to,"--nord-0",NordBase),.5))return;}
     var text=ReadConfig();var next=Patch(text,edge,other);
     if(next!=text&&!stopped){WriteConfig(next);if(refreshNative)ReloadNative();} // One final native color, never per-frame reloads.
     applied=edge+other;
    }finally{if(held)mutex.ReleaseMutex();}
   }
  }catch(Exception e){WallpaperSwitcher.Log("glazewm borders: "+e.Message);}
 }

 /// <summary>Sets window_effects' focused_window and other_windows border colors.</summary>
 static string Patch(string text,string focused,string other){
  var lines=text.Replace("\r\n","\n").Split('\n');string section=null;bool effects=false,border=false;
  for(int i=0;i<lines.Length;i++){
   var line=lines[i];
   if(Regex.IsMatch(line,@"^\w")){effects=line.StartsWith("window_effects:");section=null;continue;}
   if(!effects)continue;
   var m=Regex.Match(line,@"^  (\w+):");if(m.Success){section=m.Groups[1].Value;border=false;continue;}
   if(Regex.IsMatch(line,@"^    border:"))border=true;else if(Regex.IsMatch(line,@"^    \w"))border=false;
   if(border&&Regex.IsMatch(line,@"^      color:")){
    var color=section=="focused_window"?focused:section=="other_windows"?other:null;
    if(color!=null)lines[i]=Regex.Replace(line,@"color:.*$","color: '"+color+"'");
   }
  }
  return String.Join(text.Contains("\r\n")?"\r\n":"\n",lines);
 }

 static string Pick(Dictionary<string,string> palette,string name,string fallback){string v;return palette!=null&&palette.TryGetValue(name,out v)&&Regex.IsMatch(v??"","^#[0-9a-fA-F]{6}$")?v.ToLowerInvariant():fallback;}
 static string Blend(string a,string b,double t){
  var x=(Color)ColorConverter.ConvertFromString(a);var y=(Color)ColorConverter.ConvertFromString(b);
  Func<byte,byte,byte> mix=(p,q)=>(byte)Math.Round(p+(q-p)*t);
  return String.Format("#{0:x2}{1:x2}{2:x2}",mix(x.R,y.R),mix(x.G,y.G),mix(x.B,y.B));
 }
}
