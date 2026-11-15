using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

sealed class ThemeTransitionData {
 public string revision {get;set;}
 public double startsAt {get;set;}
 public int duration {get;set;}
 public Dictionary<string,string> from {get;set;}
 public Dictionary<string,string> to {get;set;}
}

// One wall-clock timeline across processes and monitors. No per-frame disk IO or IPC.
static class ThemeTransition {
 static readonly DateTime Epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
 static string PathName {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"theme-transition.json");}}
 public static double Now {get{return (DateTime.UtcNow-Epoch).TotalMilliseconds;}}
 public static ThemeTransitionData Current {get;private set;}
 static DispatcherTimer timer,debounce;static FileSystemWatcher watcher;static bool rendering;
 static TimeSpan lastRenderTime=TimeSpan.MinValue;
 public static event Action Began,Frame;
 public static double Progress(ThemeTransitionData data,double now){if(data==null||data.duration<=0)return 1;double t=Math.Max(0,Math.Min(1,(now-data.startsAt)/data.duration));return .5-.5*Math.Cos(Math.PI*t);}
 public static double Position {get{return Progress(Current,Now);}}
 public static bool Active {get{return Current!=null&&Current.duration>0&&Now<Current.startsAt+Current.duration;}}
 public static void Publish(ThemeTransitionData data){
  string temp=PathName+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(data));
  if(File.Exists(PathName))File.Replace(temp,PathName,null);else File.Move(temp,PathName);
 }
 public static void Use(ThemeTransitionData data){
  if(data==null||data.duration<0||data.duration>5000||Double.IsNaN(data.startsAt)||Double.IsInfinity(data.startsAt)||data.startsAt>Now+5000||Current!=null&&(Current.revision==data.revision||data.startsAt<Current.startsAt))return;
  Current=data;lastRenderTime=TimeSpan.MinValue;
  if(timer==null){timer=new DispatcherTimer(DispatcherPriority.Render);timer.Tick+=delegate{Tick();};}
  timer.Stop();if(Began!=null)Began();Tick();if(Active){
   if(!rendering){rendering=true;CompositionTarget.Rendering+=RenderFrame;}
   // A single completion wake-up also settles hidden/minimized surfaces without a rendering event.
   timer.Interval=TimeSpan.FromMilliseconds(Math.Max(1,Current.startsAt+Current.duration-Now+1));timer.Start();
  }
 }
 static void RenderFrame(object sender,EventArgs args){var time=((RenderingEventArgs)args).RenderingTime;if(time==lastRenderTime)return;lastRenderTime=time;Tick();}
 static void Tick(){if(Frame!=null)Frame();if(!Active){if(timer!=null)timer.Stop();if(rendering){CompositionTarget.Rendering-=RenderFrame;rendering=false;}}}
 public static void Listen(){
  if(watcher!=null)return;var dispatcher=Application.Current==null?Dispatcher.CurrentDispatcher:Application.Current.Dispatcher;
  Action read=()=>{try{Use(new JavaScriptSerializer().Deserialize<ThemeTransitionData>(File.ReadAllText(PathName)));}catch{}};
  debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(15)};debounce.Tick+=delegate{debounce.Stop();read();};
  watcher=new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory,"theme-transition.json"){NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite};
  FileSystemEventHandler changed=(s,e)=>{if(dispatcher.HasShutdownStarted)return;dispatcher.BeginInvoke(new Action(()=>{if(debounce!=null){debounce.Stop();debounce.Start();}}));};
  watcher.Changed+=changed;watcher.Created+=changed;watcher.Renamed+=(s,e)=>changed(s,e);watcher.EnableRaisingEvents=true;read();
 }
 public static void Stop(){if(watcher!=null){watcher.EnableRaisingEvents=false;watcher.Dispose();watcher=null;}if(debounce!=null){debounce.Stop();debounce=null;}if(timer!=null){timer.Stop();timer=null;}if(rendering){CompositionTarget.Rendering-=RenderFrame;rendering=false;}Current=null;Frame=null;Began=null;}
 public static Color Mix(Color a,Color b,double t){return Color.FromArgb((byte)Math.Round(a.A+(b.A-a.A)*t),(byte)Math.Round(a.R+(b.R-a.R)*t),(byte)Math.Round(a.G+(b.G-a.G)*t),(byte)Math.Round(a.B+(b.B-a.B)*t));}
 public static Brush Crossfade(Brush previous,Brush next){
  if(!Active||previous==null||next==null)return next;
  var unit=new RectangleGeometry(new Rect(0,0,1,1));var layers=new DrawingGroup();
  layers.Children.Add(new GeometryDrawing(previous.CloneCurrentValue(),null,unit));
  var incoming=new DrawingGroup{Opacity=Position};incoming.Children.Add(new GeometryDrawing(next,null,unit));layers.Children.Add(incoming);
  Action update=null;update=()=>{incoming.Opacity=Position;if(!Active){Frame-=update;layers.Children.Clear();layers.Children.Add(new GeometryDrawing(next,null,unit));}};Frame+=update;
  return new DrawingBrush(layers){Stretch=Stretch.Fill};
 }
 public static void BlendBrush(Brush destination,Brush from,Brush to,double t){
  var d=destination as SolidColorBrush;var a=from as SolidColorBrush;var b=to as SolidColorBrush;
  if(d!=null&&a!=null&&b!=null){d.Color=Mix(a.Color,b.Color,t);return;}
  var dg=destination as GradientBrush;var ag=from as GradientBrush;var bg=to as GradientBrush;
  if(dg!=null&&ag!=null&&bg!=null)for(int i=0;i<Math.Min(dg.GradientStops.Count,Math.Min(ag.GradientStops.Count,bg.GradientStops.Count));i++)dg.GradientStops[i].Color=Mix(ag.GradientStops[i].Color,bg.GradientStops[i].Color,t);
 }
 public static Brush Tinted(Brush source,double opacity){var drawing=new DrawingGroup{Opacity=opacity};drawing.Children.Add(new GeometryDrawing(source,null,new RectangleGeometry(new Rect(0,0,1,1))));return new DrawingBrush(drawing){Stretch=Stretch.Fill};}
}
