using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Owns the Audio tab and its UI coordination. The host supplies visibility/poll timing.
sealed class AudioPage:StackPanel,IDisposable {
 readonly AudioController audioController;
 bool audioBusy,audioRefreshPending,updating,disposed;DateTime lastAudio;
 readonly TextBlock error=new TextBlock(),masterValue=new TextBlock();
 readonly Slider volume=new Slider{Name="AudioVolume",Minimum=0,Maximum=100};
 readonly Slider balance=new Slider{Name="AudioBalance",Minimum=-1,Maximum=1,TickFrequency=.1};
 readonly ComboBox outputs=new ComboBox(),inputs=new ComboBox();
 readonly Button masterMute=new Button{Name="AudioMasterMute"};
 readonly MediaCard media=new MediaCard();
 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
 static TextBlock Label(string text){return new TextBlock{Text=text,Margin=new Thickness(0,8,0,5),Foreground=B("Muted")};}
 public bool DevicePickerOpen {get{return outputs.IsDropDownOpen||inputs.IsDropDownOpen;}}
 public AudioPage(IAudioEndpointService endpoints):this(new AudioController(endpoints)){}
 internal AudioPage(AudioController controller){audioController=controller;var audio=this;
  audio.Margin=new Thickness(0,10,0,0);media.Margin=new Thickness(0,0,0,12);audio.Children.Add(media);audio.Children.Add(RiceLayout.RibbonControl());var volumeSection=new StackPanel();var balanceSection=new StackPanel();audio.Children.Add(RiceLayout.Pair(volumeSection,balanceSection));
  var volumeHeading=new Grid{Margin=new Thickness(0,2,0,2)};volumeHeading.ColumnDefinitions.Add(new ColumnDefinition());volumeHeading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});volumeHeading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  volumeHeading.Children.Add(new TextBlock{Text="Master volume",FontSize=12,VerticalAlignment=VerticalAlignment.Center});masterValue.FontSize=11;masterValue.Foreground=B("Muted");masterValue.VerticalAlignment=VerticalAlignment.Center;masterValue.Margin=new Thickness(0,0,8,0);Grid.SetColumn(masterValue,1);volumeHeading.Children.Add(masterValue);
  masterMute.FontSize=11;masterMute.Content="Mute";masterMute.Padding=new Thickness(8,4,8,4);masterMute.BorderThickness=new Thickness(0);masterMute.Click+=delegate{Attempt(()=>{if(audioController.HasOutput){audioController.ToggleMute();PaintMaster();}});};Grid.SetColumn(masterMute,2);volumeHeading.Children.Add(masterMute);volumeSection.Children.Add(volumeHeading);
  var volumeRow=new Grid{Margin=new Thickness(0,0,0,6)};volumeRow.Children.Add(volume);volumeSection.Children.Add(volumeRow);volumeSection.Children.Add(RiceLayout.Hint("Overall output loudness. Mute silences playback without changing the level."));
  var balanceHeading=new Grid {Margin=new Thickness(0,2,0,2)};
  balanceHeading.Children.Add(new TextBlock {Text="Stereo balance",FontSize=12,Foreground=B("Muted"),VerticalAlignment=VerticalAlignment.Center});
  var reset=new Button {Content="Reset",FontSize=11,Padding=new Thickness(8,3,8,3),BorderThickness=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Right,ToolTip="Center stereo balance"};reset.Name="AudioResetBalance";reset.Click+=delegate{ResetBalance();};balanceHeading.Children.Add(reset);balanceSection.Children.Add(balanceHeading);
  var balanceRow=new Grid();balanceRow.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(20)});balanceRow.ColumnDefinitions.Add(new ColumnDefinition());balanceRow.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(20)});
  var leftLabel=new TextBlock {Text="L",VerticalAlignment=VerticalAlignment.Center,Foreground=B("Muted")};var rightLabel=new TextBlock {Text="R",VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,Foreground=B("Muted")};Grid.SetColumn(balance,1);Grid.SetColumn(rightLabel,2);balanceRow.Children.Add(leftLabel);balanceRow.Children.Add(balance);balanceRow.Children.Add(rightLabel);balanceSection.Children.Add(balanceRow);balanceSection.Children.Add(RiceLayout.Hint("Shifts sound between left and right channels. Reset centers it."));
  audio.Children.Add(Label("Output device"));audio.Children.Add(outputs);audio.Children.Add(RiceLayout.Hint("Choose which speakers or headphones play audio."));
  audio.Children.Add(Label("Input device"));audio.Children.Add(inputs);audio.Children.Add(RiceLayout.Hint("Choose the microphone used as your default recording device."));
  var microphoneRow=new Grid{Margin=new Thickness(0,12,0,6)};microphoneRow.ColumnDefinitions.Add(new ColumnDefinition());microphoneRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(142)});
  microphoneRow.Children.Add(new TextBlock{Text="Microphone",FontSize=12,Foreground=B("Muted"),VerticalAlignment=VerticalAlignment.Center});var microphone=new MicControl();Grid.SetColumn(microphone,1);microphoneRow.Children.Add(microphone);audio.Children.Add(microphoneRow);audio.Children.Add(RiceLayout.Hint("Turns the selected microphone on or mutes its input."));
  audio.Children.Add(new VolumeMixer());audio.Children.Add(RiceLayout.Hint("Adjust each audio app independently. Its mute button keeps the saved volume level."));
  foreach(var box in new[]{outputs,inputs}) {box.Padding=new Thickness(4);box.DisplayMemberPath="FriendlyName";box.Foreground=B("Text");}
  error.Foreground=B("Error");error.FontSize=11;error.TextWrapping=TextWrapping.Wrap;audio.Children.Add(error);
  volume.ValueChanged+=delegate {masterValue.Text=((int)Math.Round(volume.Value))+"%";if(!updating && audioController.HasOutput) Attempt(delegate{audioController.SetVolume((float)(volume.Value/100),(float)balance.Value);PaintAudioValues();});};
  balance.ValueChanged+=delegate {if(!updating) Attempt(ApplyBalance);};
  outputs.SelectionChanged+=delegate {if(!updating) SelectDevice(outputs);}; inputs.SelectionChanged+=delegate {if(!updating) SelectDevice(inputs);};
  PaintAudioValues();PaintMaster();
 }
 public void RefreshIfDue(double seconds){if(!disposed&&DateTime.UtcNow-lastAudio>TimeSpan.FromSeconds(seconds)){var pending=RefreshDevicesAsync();}}
 public void RefreshMedia(){if(!disposed){var pending=media.Refresh();}}
 public void Tick(){if(disposed)return;RefreshLevels();RefreshMedia();}
 public void Dispose(){if(disposed)return;disposed=true;audioRefreshPending=false;media.Dispose();foreach(var item in Children){var microphone=item as MicControl;if(microphone!=null)microphone.Dispose();var mixer=item as VolumeMixer;if(mixer!=null)mixer.Dispose();}DisposeMicrophone(this);}
 static void DisposeMicrophone(System.Windows.DependencyObject node){var microphone=node as MicControl;if(microphone!=null)microphone.Dispose();foreach(var child in System.Windows.LogicalTreeHelper.GetChildren(node).OfType<System.Windows.DependencyObject>())DisposeMicrophone(child);}
 void PaintMaster(){masterValue.Text=((int)Math.Round(volume.Value))+"%";bool muted=audioController.HasOutput&&audioController.Current.Muted;masterMute.IsEnabled=audioController.HasOutput;masterMute.Content=muted?"Unmute":"Mute";masterMute.Foreground=B(muted?"Accent":"Muted");masterMute.ToolTip=muted?"Unmute speakers":"Mute speakers";}
 void PaintAudioValues(){var state=audioController.Current;if(state==null)return;bool before=updating;updating=true;try{volume.Value=state.Volume*100;balance.Value=state.Balance;balance.IsEnabled=state.Stereo;PaintMaster();}finally{updating=before;}}
 void RefreshLevels(){if(!audioController.HasOutput||updating||volume.IsMouseCaptureWithin||volume.IsKeyboardFocusWithin||balance.IsMouseCaptureWithin||balance.IsKeyboardFocusWithin)return;try{audioController.RefreshLevels();PaintAudioValues();}catch{}}
 void Attempt(Action action){try{action();error.Text="";}catch(Exception ex){if(!disposed)error.Text=ex.Message;}}
 void ResetBalance(){bool before=updating;updating=true;try{Attempt(()=>{ApplyBalanceValue(0);});}finally{updating=before;}}
 void ApplyBalance(){ApplyBalanceValue((float)balance.Value);}
 void ApplyBalanceValue(float pan){audioController.SetBalance(pan);PaintAudioValues();if(audioController.HasOutput&&Math.Abs(pan)<.00001&&Math.Abs(audioController.Current.Balance)>.02)throw new InvalidOperationException("Windows did not accept equal left/right levels. The slider shows the device's actual balance.");}
 async void SelectDevice(ComboBox box){var device=box.SelectedItem as AudioDeviceChoice;if(device==null)return;try{await audioController.SelectDefaultAsync(device.ID);if(disposed)return;media.ResetMeter();await RefreshDevicesAsync();}catch(Exception ex){if(!disposed)error.Text=ex.Message;}}
 public async System.Threading.Tasks.Task RefreshDevicesAsync(){if(disposed)return;if(audioBusy){audioRefreshPending=true;return;}audioBusy=true;lastAudio=DateTime.UtcNow;long startedEdit=audioController.Revision;try{
  var result=await audioController.DiscoverAsync();if(disposed)return;bool interacting=balance.IsMouseCaptureWithin||balance.IsKeyboardFocusWithin||volume.IsMouseCaptureWithin||volume.IsKeyboardFocusWithin;
  audioController.AcceptDiscovery(result,startedEdit,interacting);updating=true;
  if(!outputs.IsDropDownOpen&&!inputs.IsDropDownOpen){outputs.ItemsSource=result.Outputs;inputs.ItemsSource=result.Inputs;outputs.SelectedItem=result.Outputs.FirstOrDefault(d=>d.ID==result.OutputId);inputs.SelectedItem=result.Inputs.FirstOrDefault(d=>d.ID==result.InputId);}
  PaintAudioValues();
 }catch(Exception ex){if(!disposed)error.Text=ex.Message;}finally{updating=false;audioBusy=false;}if(audioRefreshPending&&!disposed){audioRefreshPending=false;await RefreshDevicesAsync();}}
}
