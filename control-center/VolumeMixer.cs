using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

sealed class MixerApp {
 public string Key,Name,Device;public string[] Sessions;public double Volume;public bool Muted,Active;
}
sealed class MixerChange {public MixerApp App;public double? Volume;public bool? Muted;}

// Endpoint/session objects never escape a sample. Commands target the exact device and sessions
// displayed by the row, so an output switch cannot accidentally change a different device.
static class MixerAudio {
 static readonly Dictionary<string,DateTime> seen=new Dictionary<string,DateTime>();
 static string Key(AudioSessionControl session){if(session.IsSystemSoundsSession)return "system";try{using(var p=Process.GetProcessById((int)session.GetProcessID))return p.ProcessName.ToLowerInvariant();}catch{return "pid:"+session.GetProcessID;}}
 static string Name(string key,AudioSessionControl session){if(key=="system")return "System sounds";try{using(var p=Process.GetProcessById((int)session.GetProcessID)){var name=p.MainModule.FileVersionInfo.FileDescription;if(!String.IsNullOrWhiteSpace(name))return name;return p.ProcessName;}}catch{return key;}}
 internal static bool Retain(bool active,DateTime? last,DateTime now){return active||(last.HasValue&&now-last.Value<TimeSpan.FromMinutes(2));}
 public static MixerApp[] Read(){
  var result=new Dictionary<string,MixerApp>();var now=DateTime.UtcNow;var present=new HashSet<string>();
  using(var enumerator=new MMDeviceEnumerator())using(var device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia)){
   var sessions=device.AudioSessionManager.Sessions;
   for(int i=0;i<sessions.Count;i++)using(var session=sessions[i])try{
    if(session.State==AudioSessionState.AudioSessionStateExpired)continue;
    var id=session.GetSessionInstanceIdentifier;var token=device.ID+"|"+id;present.Add(token);
    bool active=session.State==AudioSessionState.AudioSessionStateActive;DateTime last;bool known=seen.TryGetValue(token,out last);if(active)seen[token]=now;
    if(!Retain(active,known?(DateTime?)last:null,now))continue;
    var key=Key(session);MixerApp app;var value=session.SimpleAudioVolume;
    if(!result.TryGetValue(key,out app)){app=new MixerApp{Key=key,Name=Name(key,session),Device=device.ID,Sessions=new string[0],Muted=true};result[key]=app;}
    app.Sessions=app.Sessions.Concat(new[]{id}).ToArray();app.Volume=Math.Max(app.Volume,value.Volume*100);app.Muted&=value.Mute;app.Active|=active;
   }catch{} // A process can exit during enumeration.
  }
  foreach(var token in seen.Keys.Where(k=>!present.Contains(k)).ToArray())seen.Remove(token);
  return result.Values.OrderBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
 }
 public static void Apply(MixerChange change){
  using(var enumerator=new MMDeviceEnumerator())using(var device=enumerator.GetDevice(change.App.Device)){
   var sessions=device.AudioSessionManager.Sessions;bool found=false;
   for(int i=0;i<sessions.Count;i++)using(var session=sessions[i]){
    if(!change.App.Sessions.Contains(session.GetSessionInstanceIdentifier))continue;
    found=true;var volume=session.SimpleAudioVolume;
    if(change.Volume.HasValue)volume.Volume=(float)(Math.Max(0,Math.Min(100,change.Volume.Value))/100);
    if(change.Muted.HasValue)volume.Mute=change.Muted.Value;
   }
   if(!found)throw new InvalidOperationException("This audio session ended. Try again when the app plays audio.");
  }
 }
}

sealed class VolumeMixer : StackPanel,IDisposable {
 bool disposed;
 sealed class Row {
  public MixerApp App;public Grid Root;public Slider Slider;public Button Mute;public TextBlock Value,State;public bool Updating;
 }
 readonly StackPanel rows=new StackPanel();readonly TextBlock status=new TextBlock();
 readonly Dictionary<string,Row> controls=new Dictionary<string,Row>();
 readonly Dictionary<string,MixerChange> pending=new Dictionary<string,MixerChange>();
 readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
 readonly Func<MixerApp[]> read;readonly Action<MixerChange> write;bool busy;DateTime sampled;
 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
 public void Dispose(){disposed=true;timer.Stop();pending.Clear();}
 public VolumeMixer():this(MixerAudio.Read,MixerAudio.Apply){}
 internal VolumeMixer(Func<MixerApp[]> read,Action<MixerChange> write){
  this.read=read;this.write=write;Margin=new Thickness(0,12,0,4);
  var heading=new Grid();heading.Children.Add(new TextBlock{Text="App volume",FontSize=12,FontWeight=FontWeights.Medium,Foreground=B("Text")});
  heading.Children.Add(new TextBlock{Text="Audio apps",FontSize=10,Foreground=B("Muted"),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center});Children.Add(heading);
  Children.Add(rows);status.FontSize=11;status.Foreground=B("Muted");status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,8,0,2);status.Text="Apps appear when they play audio.";Children.Add(status);
  timer.Tick+=async delegate{await Refresh();};IsVisibleChanged+=async delegate{if(!disposed&&IsVisible){timer.Start();sampled=DateTime.MinValue;await Refresh();}else {timer.Stop();if(pending.Count>0)await Refresh();}};Unloaded+=delegate{timer.Stop();};
 }
 internal MixerChange[] TakeChanges(){var changes=pending.Values.ToArray();pending.Clear();return changes;}
 internal async Task Refresh(){
  if(disposed||busy||(!IsVisible&&pending.Count==0)||(pending.Count==0&&DateTime.UtcNow-sampled<TimeSpan.FromSeconds(1)))return;
  busy=true;var changes=TakeChanges();
  try{string failure=null;var apps=await Task.Run(()=>{foreach(var change in changes)try{write(change);}catch(Exception e){failure=e.Message;}return read();});if(disposed)return;sampled=DateTime.UtcNow;Render(apps);if(failure!=null){status.Text=failure;status.Visibility=Visibility.Visible;}}
  catch{if(disposed)return;status.Text="Audio is unavailable. Check your output device.";status.Visibility=Visibility.Visible;}
  finally{busy=false;}
  if(pending.Count>0&&!IsVisible)await Refresh();
 }
 void Queue(Row row,double? volume,bool? muted){if(disposed)return;MixerChange change;if(!pending.TryGetValue(row.App.Key,out change)){change=new MixerChange{App=row.App};pending[row.App.Key]=change;}if(volume.HasValue)change.Volume=volume;if(muted.HasValue)change.Muted=muted;}
 internal void Render(MixerApp[] apps){if(disposed)return;
  var keys=new HashSet<string>(apps.Select(a=>a.Key));foreach(var key in controls.Keys.Where(k=>!keys.Contains(k)).ToArray()){var row=controls[key];if(row.Slider.IsMouseCaptureWithin)continue;rows.Children.Remove(row.Root);controls.Remove(key);}
  foreach(var app in apps){Row row;if(!controls.TryGetValue(app.Key,out row)){row=CreateRow(app);controls[app.Key]=row;rows.Children.Add(row.Root);}row.App=app;
   if(row.Slider.IsMouseCaptureWithin||row.Slider.IsKeyboardFocusWithin||pending.ContainsKey(app.Key))continue;
   row.Updating=true;row.Slider.Value=app.Volume;Paint(row,app.Muted,app.Active);row.Updating=false;
  }
  status.Text="Apps appear when they play audio.";status.Visibility=apps.Length==0?Visibility.Visible:Visibility.Collapsed;
 }
 void Paint(Row row,bool muted,bool active){row.Value.Text=((int)Math.Round(row.Slider.Value)).ToString();row.Mute.Content=muted?"Unmute":"Mute";row.Mute.ToolTip=(muted?"Unmute ":"Mute ")+row.App.Name;row.Mute.Foreground=B(muted?"Accent":"Muted");row.State.Text=muted?"Muted":active?"Playing":"Paused";row.State.Foreground=B(muted?"Accent":"Muted");row.Slider.Opacity=muted?.45:1;}
 Row CreateRow(MixerApp app){
  var row=new Row{App=app,Root=new Grid{Margin=new Thickness(0,10,0,2)}};
  row.Root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});row.Root.ColumnDefinitions.Add(new ColumnDefinition());row.Root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});row.Root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(58)});
  row.Root.RowDefinitions.Add(new RowDefinition());row.Root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(24)});
  var badge=new Border{Width=23,Height=23,CornerRadius=new CornerRadius(7),Background=B("Raised"),VerticalAlignment=VerticalAlignment.Center,Child=new TextBlock{Text=app.Name.Substring(0,1).ToUpperInvariant(),FontSize=11,FontWeight=FontWeights.Medium,Foreground=B("Accent"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};Grid.SetRowSpan(badge,2);row.Root.Children.Add(badge);
  var title=new TextBlock{Text=app.Name,FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,ToolTip=app.Name};Grid.SetColumn(title,1);row.Root.Children.Add(title);
  row.State=new TextBlock{FontSize=9,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(row.State,2);Grid.SetColumnSpan(row.State,2);row.Root.Children.Add(row.State);
  row.Slider=new Slider{Minimum=0,Maximum=100,SmallChange=1,LargeChange=5,Value=app.Volume,ToolTip=app.Name+" volume",Margin=new Thickness(0,0,7,0)};System.Windows.Automation.AutomationProperties.SetName(row.Slider,app.Name+" volume");Grid.SetRow(row.Slider,1);Grid.SetColumn(row.Slider,1);row.Root.Children.Add(row.Slider);
  row.Value=new TextBlock{FontSize=10,Foreground=B("Muted"),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,9,0)};Grid.SetRow(row.Value,1);Grid.SetColumn(row.Value,2);row.Root.Children.Add(row.Value);
  row.Mute=new Button{FontSize=10,Padding=new Thickness(5,2,5,2),BorderThickness=new Thickness(0),Background=Brushes.Transparent};Grid.SetRow(row.Mute,1);Grid.SetColumn(row.Mute,3);row.Root.Children.Add(row.Mute);
  row.Slider.ValueChanged+=delegate{row.Value.Text=((int)Math.Round(row.Slider.Value)).ToString();if(!row.Updating)Queue(row,row.Slider.Value,null);};
  row.Mute.Click+=delegate{MixerChange queued;bool muted=pending.TryGetValue(row.App.Key,out queued)&&queued.Muted.HasValue?queued.Muted.Value:row.App.Muted;row.App.Muted=!muted;Queue(row,null,!muted);Paint(row,!muted,row.App.Active);};Paint(row,app.Muted,app.Active);return row;
 }
}
