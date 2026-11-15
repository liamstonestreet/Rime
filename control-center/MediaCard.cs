using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using Windows.Media.Control;

sealed class MediaCard : Grid,IDisposable {
 bool disposed;readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(25)},noticeTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
 GlobalSystemMediaTransportControlsSessionManager manager;
 GlobalSystemMediaTransportControlsSession session;
 readonly Image art=new Image {Width=80,Height=80};
 readonly TextBlock title=new TextBlock {Text="No active media",FontSize=22,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Left,HorizontalAlignment=HorizontalAlignment.Stretch,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,0,0,5)};
 readonly Button play=new Button {Content="\uE768",Width=44,ToolTip="Play / pause"};
 bool busy; string lastKey;
 readonly TextBlock albumText=new TextBlock {FontSize=13,LineHeight=18,TextAlignment=TextAlignment.Left,HorizontalAlignment=HorizontalAlignment.Stretch,Foreground=ControlCenterTheme.Current.Brush("Muted"),TextTrimming=TextTrimming.CharacterEllipsis};
 readonly TextBlock playlistText=new TextBlock {FontSize=13,LineHeight=18,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=ControlCenterTheme.Current.Brush("Muted")};
 readonly TextBlock artist=new TextBlock {FontSize=13,LineHeight=19,TextAlignment=TextAlignment.Left,HorizontalAlignment=HorizontalAlignment.Stretch,Foreground=ControlCenterTheme.Current.Brush("Muted"),TextTrimming=TextTrimming.CharacterEllipsis};
 readonly Button copy=new Button {Content="↗",ToolTip="Copy link unavailable: Windows media does not expose the source URL",IsEnabled=false,Width=28};
 readonly Button[] linkButtons;readonly Button artistButton,shuffleButton=new Button{Name="MediaShuffle",Content="\uE8B1",ToolTip="Shuffle"};bool copyBusy,shuffleBusy;string artistUrl;
 readonly MediaAtmosphere pulse=new MediaAtmosphere();
 AudioPeakSampler meter;bool meterBusy;
 internal static double PlayerHeight(bool active,int percent){int p=Math.Max(75,Math.Min(175,percent));return active&&p<100?140+64*(p-75)/25.0:Math.Max(140,(active?204:140)*p/100.0);}
 void ApplyPlayerHeight(){Height=PlayerHeight(session!=null,BarAppearance.Settings.mediaPlayerHeight);}
 public MediaCard(){Margin=new Thickness(0,10,0,2);ApplyPlayerHeight();Loaded+=delegate{if(!disposed){BarAppearance.Changed-=ApplyPlayerHeight;BarAppearance.Changed+=ApplyPlayerHeight;ApplyPlayerHeight();}};Unloaded+=delegate{BarAppearance.Changed-=ApplyPlayerHeight;timer.Stop();ResetMeter();};
  ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(96)});ColumnDefinitions.Add(new ColumnDefinition());ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
  art.Stretch=Stretch.UniformToFill;art.Clip=new RectangleGeometry(new Rect(0,0,80,80),14,14);
  SetColumnSpan(pulse,2);Children.Add(pulse);
  var artSlot=new Grid{Width=80,Height=80,Margin=new Thickness(0,0,16,0),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};artSlot.Children.Add(new Border{CornerRadius=new CornerRadius(14),Background=ControlCenterTheme.Current.Brush("Raised"),Opacity=.45});artSlot.Children.Add(new TextBlock{Text="\uE8D6",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=26,Foreground=ControlCenterTheme.Current.Brush("Muted"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});artSlot.Children.Add(art);var artColumn=new StackPanel{Name="MediaArtworkColumn",Width=80,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};artSlot.Margin=new Thickness(0);artColumn.Children.Add(artSlot);Children.Add(artColumn);var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};text.HorizontalAlignment=HorizontalAlignment.Stretch;var songCopy=TextCopy(title,1,"MediaCopySong");var albumCopy=TextCopy(albumText,3,"MediaCopyAlbum");var playlistCopy=TextCopy(playlistText,2,"MediaCopyPlaylist");artistButton=new Button{Name="MediaArtist",Content=artist,Template=textCopyTemplate,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Stretch};artistButton.Click+=async delegate{await CopyComponent(4);};text.Children.Add(songCopy);text.Children.Add(artistButton);text.Children.Add(albumCopy);text.Children.Add(playlistCopy);SetColumn(text,1);Children.Add(text);
  timer.Tick+=async delegate{if(disposed||!IsVisible||meterBusy)return;if(!pulse.Playing||BarAppearance.Settings.motion==0){pulse.Target=0;return;}meterBusy=true;try{var reader=meter??(meter=new AudioPeakSampler());float peak=await reader.Read();if(!disposed&&IsVisible)pulse.Target=peak;}catch{if(!disposed)pulse.Target=0;}finally{meterBusy=false;}};IsVisibleChanged+=delegate{if(!disposed&&IsVisible)timer.Start();else{timer.Stop();ResetMeter();}};
  var buttons=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};SetColumn(buttons,2);Children.Add(buttons);
  var prev=new Button{Content="\uE892",Width=36,ToolTip="Previous"};var next=new Button{Content="\uE893",Width=36,ToolTip="Next"};foreach(var control in new[]{prev,play,next,shuffleButton}){control.BorderThickness=new Thickness(0);control.Margin=new Thickness(3,0,3,0);control.Width=44;control.Height=48;control.VerticalContentAlignment=VerticalAlignment.Center;control.HorizontalContentAlignment=HorizontalAlignment.Center;control.FontSize=27;control.Padding=new Thickness(4);}
  prev.FontFamily=play.FontFamily=next.FontFamily=shuffleButton.FontFamily=new FontFamily("Segoe Fluent Icons");
  buttons.Children.Add(prev);buttons.Children.Add(play);buttons.Children.Add(next);buttons.Children.Add(shuffleButton);shuffleButton.Click+=async delegate{await ToggleShuffle();};
  linkButtons=new[]{copy,songCopy,playlistCopy,albumCopy};
  var copyHost=new StackPanel{Width=80,Margin=new Thickness(0,4,0,0),HorizontalAlignment=HorizontalAlignment.Center};
  copy.Name="MediaCopyAll";copy.Content="Copy all";copy.Width=70;copy.Height=28;copy.FontSize=13;copy.Padding=new Thickness(3,0,3,0);copy.Margin=new Thickness(0);copy.Background=Brushes.Transparent;copy.Foreground=ControlCenterTheme.Current.Brush("Muted");copy.ToolTip="Copy song | artist | playlist | album links (NA for missing links)";copy.Click+=async delegate{await CopyComponent(0);};
  copyHost.Children.Add(copy);artColumn.Children.Add(copyHost);
  noticeTimer.Tick+=delegate{CopyNotice("");noticeTimer.Stop();};
  prev.Click+=async delegate {try{if(session!=null)await WinRT.Await(session.TrySkipPreviousAsync());}catch{}};
  next.Click+=async delegate {try{if(session!=null)await WinRT.Await(session.TrySkipNextAsync());}catch{}};
  play.Click+=async delegate {try{if(session!=null)await WinRT.Await(session.TryTogglePlayPauseAsync());await Refresh();}catch{}};
  // Keep the animated surface separate from foreground layout and hit testing.
  Children.Remove(pulse);var foreground=new Grid {Height=112,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(16,14,12,14)};
  var columns=ColumnDefinitions.ToArray();ColumnDefinitions.Clear();foreach(var column in columns)foreground.ColumnDefinitions.Add(column);
  var elements=Children.Cast<UIElement>().ToArray();Children.Clear();foreach(var element in elements)foreground.Children.Add(element);
  SetColumnSpan(pulse,1);Children.Add(pulse);Children.Add(foreground);
 }
 static readonly ControlTemplate textCopyTemplate=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='Face' Background='{TemplateBinding Background}' CornerRadius='4'><ContentPresenter HorizontalAlignment='Stretch'/></Border><ControlTemplate.Triggers><MultiTrigger><MultiTrigger.Conditions><Condition Property='IsMouseOver' Value='True'/><Condition Property='IsEnabled' Value='True'/></MultiTrigger.Conditions><Setter TargetName='Face' Property='Background' Value='{DynamicResource ThemeHover}'/></MultiTrigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource ThemeSelected}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
 Button TextCopy(TextBlock label,int component,string name){var button=new Button{Name=name,Content=label,Template=textCopyTemplate,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Stretch};button.Click+=async delegate{await CopyComponent(component);};return button;}
 internal void UpdateLinkButtons(bool spotify,MediaLinks links){var names=new[]{"all links (song | artist | playlist | album)","song link","playlist link","album link"};for(int i=0;i<linkButtons.Length;i++){var button=linkButtons[i];bool available=spotify&&(i==0?links.Song!=null:links.Component(i)!=null);button.IsEnabled=!copyBusy&&available;button.Cursor=available?System.Windows.Input.Cursors.Hand:System.Windows.Input.Cursors.Arrow;button.ToolTip=(i>0?((TextBlock)button.Content).Text+"\n":"")+(available?"Click to copy "+names[i]+(i==0?" (NA for missing links)":""):"No "+names[i]+" available"+(spotify&&links.Song==null?". Spotify metadata is disconnected. After a Spotify update, run spicetify backup apply and reopen Spotify.":""));System.Windows.Automation.AutomationProperties.SetName(button,button.ToolTip.ToString());}
  artistUrl=spotify?links.Artist:null;artistButton.IsEnabled=!copyBusy&&ArtistPage(artistUrl)!=null;artistButton.Cursor=artistButton.IsEnabled?System.Windows.Input.Cursors.Hand:System.Windows.Input.Cursors.Arrow;artistButton.ToolTip=artist.Text+(artistButton.IsEnabled?"\nClick to copy artist link":"");
  copy.Visibility=spotify?Visibility.Visible:Visibility.Hidden;linkButtons[3].Visibility=String.IsNullOrWhiteSpace(albumText.Text)?Visibility.Collapsed:Visibility.Visible;linkButtons[2].Visibility=String.IsNullOrWhiteSpace(playlistText.Text)?Visibility.Collapsed:Visibility.Visible;
 }
 internal static string ArtistPage(string url){return MediaMetadataStore.ContextUrl(url,"artist");}
 void CopyNotice(string message){copy.Content=String.IsNullOrEmpty(message)?"Copy all":message;}
 void PaintShuffle(){if(session==null){shuffleButton.IsEnabled=false;return;}var state=session.GetPlaybackInfo();bool? reported=IsSpotify(session.SourceAppUserModelId)?MediaBridge.ShuffleState(title.Text):null;bool active=reported??state.IsShuffleActive??false;shuffleButton.IsEnabled=!shuffleBusy&&(state.Controls.IsShuffleEnabled||reported.HasValue);shuffleButton.Foreground=ControlCenterTheme.Current.Brush(active?"Accent":"Text");shuffleButton.Background=active?ControlCenterTheme.Current.Brush("Selected"):Brushes.Transparent;shuffleButton.ToolTip=shuffleButton.IsEnabled?(active?"Turn shuffle off":"Turn shuffle on"):"Shuffle is unavailable for this player";System.Windows.Automation.AutomationProperties.SetName(shuffleButton,shuffleButton.ToolTip.ToString());}
 async Task ToggleShuffle(){if(disposed||shuffleBusy||session==null)return;var target=session;var info=target.GetPlaybackInfo();bool? reported=IsSpotify(target.SourceAppUserModelId)?MediaBridge.ShuffleState(title.Text):null;bool value=!(reported??info.IsShuffleActive??false);shuffleBusy=true;PaintShuffle();try{if(reported.HasValue&&MediaBridge.Shuffle(title.Text,value))await Task.Delay(950);else if(info.Controls.IsShuffleEnabled){bool changed=await WinRT.Await(target.TryChangeShuffleActiveAsync(value));if(!changed&&!disposed){CopyNotice("Unavailable");noticeTimer.Start();}}}catch{if(!disposed){CopyNotice("Shuffle failed");noticeTimer.Start();}}finally{shuffleBusy=false;}if(!disposed)await Refresh();}
 internal static string CopyText(MediaLinks links,int component){return links.Component(component)??"NA";}
 async Task CopyComponent(int component){if(disposed||copyBusy||session==null||!IsSpotify(session.SourceAppUserModelId))return;var links=MediaBridge.Links(title.Text);if(links.Song==null){CopyNotice("Unavailable");return;}string value=CopyText(links,component);copyBusy=true;UpdateLinkButtons(true,links);noticeTimer.Stop();CopyNotice("");
  try{bool success=false;for(int attempt=0;attempt<4&&!disposed;attempt++){try{Clipboard.SetText(value);success=true;break;}catch(System.Runtime.InteropServices.COMException){}await Task.Delay(50);}if(!disposed)CopyNotice(success?"Copied":"Try again");}
  catch{if(!disposed)CopyNotice("Copy failed");}finally{copyBusy=false;if(!disposed){UpdateLinkButtons(session!=null&&IsSpotify(session.SourceAppUserModelId),MediaBridge.Links(title.Text));noticeTimer.Start();}}
 }
 // Track links come only from Spotify (the Spicetify extension reports them to MediaBridge).
 static bool IsSpotify(string source){return (source??"").IndexOf("spotify",StringComparison.OrdinalIgnoreCase)>=0;}
 public async System.Threading.Tasks.Task Refresh(){if(disposed||busy)return;busy=true;try{
  if(manager==null){var available=await WinRT.Await(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());if(disposed)return;manager=available;}
  var current=manager.GetCurrentSession();
  session=current!=null&&current.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing?current:manager.GetSessions().FirstOrDefault(s=>s.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)??current;
  ApplyPlayerHeight();IsEnabled=session!=null;pulse.Playing=session!=null&&session.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;if(session==null){title.Text="No active media";artist.Text="";art.Source=null;lastKey=null;albumText.Text=playlistText.Text="";UpdateLinkButtons(false,new MediaLinks(null,null,null));PaintShuffle();return;}
  var props=await WinRT.Await(session.TryGetMediaPropertiesAsync());if(disposed)return;var key=session.SourceAppUserModelId+props.Title+props.Artist;
  bool spotify=IsSpotify(session.SourceAppUserModelId);
  title.Text=props.Title;artist.Text=String.IsNullOrWhiteSpace(props.Artist)?"":"Artist: "+props.Artist;artist.ToolTip=null;title.ToolTip=null;
  var details=spotify?MediaBridge.Context(props.Title):new[]{"",""};var album=String.IsNullOrWhiteSpace(details[0])?props.AlbumTitle:details[0];albumText.Text=String.IsNullOrWhiteSpace(album)?"":"Album: "+album;albumText.Visibility=Visibility.Visible;playlistText.Text=String.IsNullOrWhiteSpace(details[1])?"":"Playlist: "+details[1];UpdateLinkButtons(spotify,MediaBridge.Links(props.Title));PaintShuffle();
  play.Content=session.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing?"\uE769":"\uE768";
  if(key!=lastKey){lastKey=key;art.Source=null;if(props.Thumbnail!=null){using(var stream=await WinRT.Await(props.Thumbnail.OpenReadAsync())){var size=stream.Size;if(size>0&&size<8388608){var bytes=new byte[size];using(var reader=new Windows.Storage.Streams.DataReader(stream)){await WinRT.Await(reader.LoadAsync((uint)size));reader.ReadBytes(bytes);}using(var source=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=160;image.StreamSource=source;image.EndInit();image.Freeze();if(!disposed)art.Source=image;}}}}}
 }catch{if(!disposed)title.Text="Media unavailable";}finally{busy=false;}}
 internal void ResetMeter(){var previous=meter;meter=null;if(previous!=null)previous.Dispose();}
 public void Dispose(){if(disposed)return;disposed=true;BarAppearance.Changed-=ApplyPlayerHeight;timer.Stop();noticeTimer.Stop();ResetMeter();pulse.Dispose();art.Source=null;session=null;manager=null;}

}

// Flowing aurora ribbons. Real output peak drives energy;
// the decorative strands are deliberately not presented as a frequency spectrum.
sealed class MediaAtmosphere : FrameworkElement,IDisposable {
 bool disposed;
 public double Target;public bool Playing;
 double level,reference=.08,phase;DateTime last;int motion=-1;

 readonly SolidColorBrush backdrop=new SolidColorBrush();
 readonly RadialGradientBrush[] halos={Glow(),Glow(),Glow()};
 readonly SolidColorBrush[] strands={new SolidColorBrush(),new SolidColorBrush(),new SolidColorBrush()};
 readonly Pen[] lines={new Pen(null,1.2),new Pen(null,1.1),new Pen(null,.8)};
 readonly Pen edge=new Pen(new SolidColorBrush(),1);
 readonly LinearGradientBrush veil=new LinearGradientBrush();
 RectangleGeometry clip;int themeRevision=-1;bool themeWasActive,hooked;
 static RadialGradientBrush Glow(){return new RadialGradientBrush(new GradientStopCollection{new GradientStop(Colors.Transparent,0),new GradientStop(Colors.Transparent,1)}){RadiusX=.65,RadiusY=1};}
 public MediaAtmosphere(){IsHitTestVisible=false;
  for(int i=0;i<3;i++)lines[i].Brush=strands[i];
  veil.StartPoint=new Point(0,0);veil.EndPoint=new Point(0,1);veil.GradientStops.Add(new GradientStop(Colors.Transparent,0));veil.GradientStops.Add(new GradientStop(Colors.Transparent,.5));veil.GradientStops.Add(new GradientStop(Colors.Transparent,1));
  SizeChanged+=delegate{clip=new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight),16,16);clip.Freeze();};
  IsVisibleChanged+=delegate{if(!disposed&&IsVisible&&!hooked){last=DateTime.UtcNow;hooked=true;CompositionTarget.Rendering+=Frame;InvalidateVisual();}else if(!IsVisible&&hooked){hooked=false;CompositionTarget.Rendering-=Frame;}};
 }
 void UpdateTheme(){var theme=ControlCenterTheme.Current;if(themeRevision==theme.Revision&&!ThemeTransition.Active&&!themeWasActive)return;themeWasActive=ThemeTransition.Active;themeRevision=theme.Revision;
  backdrop.Color=theme.Color("MediaBase");
  for(int i=0;i<3;i++){var c=theme.Color("Glow"+(i+1));halos[i].GradientStops[0].Color=c;halos[i].GradientStops[1].Color=Color.FromArgb(0,c.R,c.G,c.B);strands[i].Color=theme.Color(i==0?"Accent":i==1?"Bright":"Edge");}
  var bg=backdrop.Color;veil.GradientStops[0].Color=Color.FromArgb(220,bg.R,bg.G,bg.B);veil.GradientStops[1].Color=Color.FromArgb(125,bg.R,bg.G,bg.B);veil.GradientStops[2].Color=Color.FromArgb(15,bg.R,bg.G,bg.B);
  var e=theme.Color("Accent");((SolidColorBrush)edge.Brush).Color=Color.FromArgb(55,e.R,e.G,e.B);
 }
 public void Dispose(){disposed=true;if(hooked){CompositionTarget.Rendering-=Frame;hooked=false;}Target=0;Playing=false;}
 void Frame(object sender,EventArgs args){if(disposed)return;var now=DateTime.UtcNow;double dt=Math.Min(.1,(now-last).TotalSeconds);last=now;
  int next=BarAppearance.Settings.motion;bool changed=motion!=next;motion=next;
  if(motion==0){if(changed||ThemeTransition.Active||themeWasActive||themeRevision!=ControlCenterTheme.Current.Revision){level=0;InvalidateVisual();}return;}
  if(!Playing&&level<.002&&!ThemeTransition.Active&&!themeWasActive&&themeRevision==ControlCenterTheme.Current.Revision&&!changed)return;
  double peak=Playing?Math.Max(0,Math.Min(1,Target)):0;reference=Math.Max(.025,Math.Max(peak,reference*Math.Exp(-dt*.6)));double target=Response(peak,reference,BarAppearance.Settings.ribbonSensitivity);
  level+=(target-level)*(1-Math.Exp(-dt*(target>level?18:4)));
  phase+=dt*(.55+1.65*level)*Math.Min(1.5,motion/100.0);InvalidateVisual();
 }
 internal static double Response(double peak,double reference,int sensitivity){if(peak<.00002)return 0;double gain=Math.Max(25,Math.Min(400,sensitivity))/100.0;return Math.Min(1,Math.Pow(Math.Max(0,peak)/Math.Max(.01,reference)*gain,.48));}
 internal static double Ribbon(double x,double phase,int layer,double energy){return Math.Sin(x*8+phase*(1+layer*.13)+layer*1.7)*(.30+.26*energy)+Math.Sin(x*17-phase*.8+layer)*.19+Math.Cos(x*4-phase*.4)*.21;}
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);UpdateTheme();double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;var bounds=new Rect(0,0,w,h);if(clip==null){clip=new RectangleGeometry(bounds,16,16);clip.Freeze();}dc.PushClip(clip);dc.DrawRectangle(backdrop,null,bounds);
  for(int i=0;i<3;i++){halos[i].Center=halos[i].GradientOrigin=new Point(.2+i*.3+.12*Math.Sin(phase*.4+i),.75+.15*Math.Cos(phase*.6+i));halos[i].Opacity=.18+level*.22;dc.DrawRectangle(halos[i],null,bounds);}
  // Three translucent, interwoven aurora sheets, kept in the lower landscape behind the controls.
  for(int layer=2;layer>=0;layer--){var shape=new StreamGeometry();using(var g=shape.Open()){
   for(int i=0;i<=64;i++){double x=i/64.0,y=h*.76+Ribbon(x,phase,layer,level)*(15+level*48)+layer*4;var p=new Point(x*w,y);if(i==0)g.BeginFigure(p,true,true);else g.LineTo(p,true,false);}
   g.LineTo(new Point(w,h+10),false,false);g.LineTo(new Point(0,h+10),false,false);
  }shape.Freeze();dc.PushOpacity(.09+level*.07);dc.DrawGeometry(strands[layer],null,shape);dc.Pop();dc.PushOpacity(.38+level*.28);dc.DrawGeometry(null,lines[layer],shape);dc.Pop();}
  dc.DrawRectangle(veil,null,bounds);dc.Pop();dc.DrawRoundedRectangle(null,edge,new Rect(.5,.5,Math.Max(0,w-1),Math.Max(0,h-1)),16,16);
 }
}
// Awaits a WinRT async operation without System.Runtime.WindowsRuntime's GetAwaiter, whose types only
// match the Windows SDK's Windows.winmd. This lets the helper build against the WinRT metadata that
// ships with Windows (System32\WinMetadata), so no Windows SDK install is needed.
static class WinRT {
 public static System.Threading.Tasks.Task Await(Windows.Foundation.IAsyncAction operation){var done=new System.Threading.Tasks.TaskCompletionSource<bool>();operation.Completed=(op,status)=>{try{if(status==Windows.Foundation.AsyncStatus.Completed){op.GetResults();done.TrySetResult(true);}else if(status==Windows.Foundation.AsyncStatus.Canceled)done.TrySetCanceled();else done.TrySetException(op.ErrorCode??new Exception("Windows operation failed"));}catch(Exception ex){done.TrySetException(ex);}};return done.Task;}
 public static System.Threading.Tasks.Task<T> Await<T>(Windows.Foundation.IAsyncOperation<T> operation){
  var done=new System.Threading.Tasks.TaskCompletionSource<T>();
  operation.Completed=(op,status)=>{try{
   if(status==Windows.Foundation.AsyncStatus.Completed)done.TrySetResult(op.GetResults());
   else if(status==Windows.Foundation.AsyncStatus.Canceled)done.TrySetCanceled();
   else done.TrySetException(op.ErrorCode??new Exception("WinRT operation failed"));
  }catch(Exception e){done.TrySetException(e);}};
  return done.Task;
 }
}
