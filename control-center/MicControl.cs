using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.CoreAudioApi;

sealed class MicState {public string Id,Name;public bool Muted;}
static class MicrophoneAudio {
 public static MicState Read(){using(var enumerator=new MMDeviceEnumerator())using(var device=enumerator.GetDefaultAudioEndpoint(DataFlow.Capture,Role.Multimedia))return new MicState{Id=device.ID,Name=device.FriendlyName,Muted=device.AudioEndpointVolume.Mute};}
 public static void Mute(string id,bool muted){using(var enumerator=new MMDeviceEnumerator())using(var device=enumerator.GetDevice(id))device.AudioEndpointVolume.Mute=muted;}
}

// A distinct microphone control: never confused with speaker mute. Idle while the tab is hidden.
sealed class MicControl : Button,IDisposable {
 bool disposed;
 readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
 MicState state;bool busy;
 public MicControl(){
  SetResourceReference(StyleProperty,typeof(Button));
  Name="AudioMicrophone";Background=ControlCenterTheme.Current.Brush("Raised");Content="Microphone";FontSize=12;MinHeight=34;Padding=new Thickness(10,6,10,6);BorderThickness=new Thickness(0);Margin=new Thickness(12,0,0,0);
  timer.Tick+=async delegate{await Refresh();};IsVisibleChanged+=async delegate{if(!disposed&&IsVisible){timer.Start();await Refresh();}else timer.Stop();};Unloaded+=delegate{timer.Stop();};
  Click+=async delegate{if(disposed||busy)return;busy=true;IsEnabled=false;try{var current=state??await Task.Run(()=>MicrophoneAudio.Read());await Task.Run(()=>MicrophoneAudio.Mute(current.Id,!current.Muted));Show(await Task.Run(()=>MicrophoneAudio.Read()));}catch{Unavailable();}finally{busy=false;if(!disposed)IsEnabled=true;}};
 }
 async Task Refresh(){if(disposed||busy)return;busy=true;try{Show(await Task.Run(()=>MicrophoneAudio.Read()));}catch{Unavailable();}finally{busy=false;}}
 public void Dispose(){disposed=true;timer.Stop();state=null;}
 internal void Show(MicState value){if(disposed)return;state=value;Content=value.Muted?"Mic muted":"Mic on";ToolTip=(value.Muted?"Unmute ":"Mute ")+value.Name;Foreground=ControlCenterTheme.Current.Brush(value.Muted?"Error":"Text");Background=ControlCenterTheme.Current.Brush(value.Muted?"Selected":"Raised");System.Windows.Automation.AutomationProperties.SetName(this,(value.Muted?"Unmute":"Mute")+" microphone");}
 void Unavailable(){if(disposed)return;state=null;Content="No microphone";ToolTip="Connect a microphone or choose an input in Audio settings.";Foreground=ControlCenterTheme.Current.Brush("Muted");Background=ControlCenterTheme.Current.Brush("Raised");}
}
