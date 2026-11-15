using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

sealed class CalendarView : StackPanel,IDisposable {
 bool disposed,compact;
 readonly Grid layout=new Grid(),timelineHeading=new Grid{Margin=new Thickness(0,20,0,10)};readonly StackPanel monthPanel=new StackPanel(),timelinePanel=new StackPanel();
 internal bool CompactLayout {get{return compact;}}
 internal static Func<ICalendarSource> SourceFactory=()=>new CalendarConnections();
 readonly ICalendarSource source;readonly Func<DateTime> clock;
 readonly Grid days=new Grid();readonly Button[] cells=new Button[42];readonly TextBlock[] numbers=new TextBlock[42],months=new TextBlock[42];readonly StackPanel[] dots=new StackPanel[42];
 readonly Grid calendarBody=new Grid();readonly Grid dayViewport=new Grid{ClipToBounds=true};readonly TranslateTransform weekSlide=new TranslateTransform();Image outgoingWeek;int weekRevision;
 readonly TextBlock period=new TextBlock(),selectedLabel=new TextBlock(),status=new TextBlock();readonly StackPanel timeline=new StackPanel();
 readonly Button compactToggle=new Button{Name="MenuCalendarCompact",FontSize=11,Padding=new Thickness(10,6,10,6),BorderThickness=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,4)};readonly Button connect,refresh,disconnect;readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(5)};
 DateTime today,anchor,selected;CalendarSnapshot snapshot;CancellationTokenSource loading,authorization;int revision;bool connecting,busy;DateTime loadingStart;string error;
 public bool ModalActive {get;private set;}
 public DateTime SelectedDate {get{return selected;}}
 internal DateTime[] VisibleDays {get{return CalendarData.MonthDays(anchor);}}
 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
 static readonly ControlTemplate eventTemplate=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='Face' Background='{TemplateBinding Background}' CornerRadius='6'><ContentPresenter HorizontalAlignment='Stretch'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource ThemeHover}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
 static Button Small(string text,string name){return new Button{Content=text,Name=name,FontSize=11,Padding=new Thickness(8,5,8,5),BorderThickness=new Thickness(0),Foreground=B("Muted"),Background=Brushes.Transparent,VerticalAlignment=VerticalAlignment.Center};}
 public CalendarView():this(SourceFactory(),()=>DateTime.Today){}
 internal CalendarView(ICalendarSource source,Func<DateTime> clock){
  this.source=source;this.clock=clock;today=clock().Date;selected=today;anchor=CalendarData.MonthStart(today);Margin=new Thickness(0,8,0,0);
  compactToggle.Content="Compact calendar + timeline";compactToggle.Click+=delegate{BarAppearance.Update(s=>s.calendarCompact=!s.calendarCompact);ApplyLayout();};Children.Add(compactToggle);Children.Add(RiceLayout.Hint("Places the month beside the selected day's events and tasks. Narrow panels stack them automatically."));
  layout.ColumnDefinitions.Add(new ColumnDefinition());layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0)});layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.Children.Add(monthPanel);Grid.SetRow(timelinePanel,1);layout.Children.Add(timelinePanel);Children.Add(layout);
  var heading=new Grid{Margin=new Thickness(0,0,0,8)};heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  period.FontSize=16;period.FontWeight=FontWeights.Medium;period.VerticalAlignment=VerticalAlignment.Center;heading.Children.Add(period);
  var navigation=new StackPanel{Orientation=Orientation.Horizontal};var previous=Small("\u2039","CalendarPrevious");var next=Small("\u203a","CalendarNext");var current=Small("Today","CalendarToday");previous.FontSize=next.FontSize=20;previous.ToolTip="Previous month";next.ToolTip="Next month";
  previous.Click+=async delegate{await MoveMonth(-1);};next.Click+=async delegate{await MoveMonth(1);};current.Click+=async delegate{ResetWeekMotion();selected=clock().Date;anchor=CalendarData.MonthStart(selected);RenderDays();RenderTimeline();await EnsureLoaded(false);};navigation.Children.Add(previous);navigation.Children.Add(current);navigation.Children.Add(next);Grid.SetColumn(navigation,1);heading.Children.Add(navigation);monthPanel.Children.Add(heading);monthPanel.Children.Add(RiceLayout.Hint("Select a date to view its events and tasks. Arrows browse months; Today returns to the current date."));
  var weekdays=new Grid{Height=22};for(int i=0;i<7;i++){days.ColumnDefinitions.Add(new ColumnDefinition());weekdays.ColumnDefinitions.Add(new ColumnDefinition());}for(int row=0;row<6;row++)days.RowDefinitions.Add(new RowDefinition{Height=new GridLength(52)});
  for(int i=0;i<7;i++){var label=new TextBlock{Text=new DateTime(2023,12,31).AddDays(i).ToString("ddd"),FontSize=10,Foreground=B("Muted"),HorizontalAlignment=HorizontalAlignment.Center};Grid.SetColumn(label,i);weekdays.Children.Add(label);}monthPanel.Children.Add(weekdays);
  for(int i=0;i<42;i++){
   int index=i;var body=new StackPanel{VerticalAlignment=VerticalAlignment.Center};months[i]=new TextBlock{FontSize=9,Height=12,Foreground=B("Muted"),HorizontalAlignment=HorizontalAlignment.Center};numbers[i]=new TextBlock{FontSize=16,HorizontalAlignment=HorizontalAlignment.Center};dots[i]=new StackPanel{Orientation=Orientation.Horizontal,Height=5,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,3,0,0)};body.Children.Add(months[i]);body.Children.Add(numbers[i]);body.Children.Add(dots[i]);
   var cell=new Button{Name="CalendarDay"+i,Content=body,Padding=new Thickness(0),Margin=new Thickness(2),BorderThickness=new Thickness(0),BorderBrush=B("Accent"),Background=Brushes.Transparent};cells[i]=cell;Grid.SetRow(cell,i/7);Grid.SetColumn(cell,i%7);days.Children.Add(cell);
   cell.Click+=delegate{SelectDay((DateTime)cells[index].Tag);};
  }
  days.RenderTransform=weekSlide;dayViewport.Children.Add(days);calendarBody.Children.Add(dayViewport);monthPanel.Children.Add(calendarBody);
  timelineHeading.Children.Add(new TextBlock{Text="Timeline",FontSize=17,FontWeight=FontWeights.Medium});selectedLabel.FontSize=11;selectedLabel.Foreground=B("Muted");selectedLabel.HorizontalAlignment=HorizontalAlignment.Right;selectedLabel.VerticalAlignment=VerticalAlignment.Center;timelineHeading.Children.Add(selectedLabel);timelinePanel.Children.Add(timelineHeading);timelinePanel.Children.Add(timeline);
  status.FontSize=10;status.Foreground=B("Muted");status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,10,0,2);Children.Add(status);
  var connection=new WrapPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(-8,2,0,0)};connect=Small("Google sign-in","CalendarConnect");refresh=Small("Refresh","CalendarRefresh");disconnect=Small("Disconnect","CalendarDisconnect");var help=Small("Setup steps","CalendarSetup");var feeds=Small("Private iCal","CalendarFeeds");feeds.Visibility=source is CalendarConnections?Visibility.Visible:Visibility.Collapsed;feeds.Click+=async delegate{await ManageFeeds();};connection.Children.Add(feeds);connection.Children.Add(connect);connection.Children.Add(refresh);connection.Children.Add(disconnect);connection.Children.Add(help);Children.Add(connection);Children.Add(RiceLayout.Hint("Private iCal manages calendar links. Google sign-in also supports Tasks. Refresh reloads events; Disconnect removes saved connections."));
  var steps=new StackPanel();steps.Children.Add(new TextBlock{Text="No Google Cloud needed: Google Calendar > Settings > select a calendar > Integrate calendar > Secret address in iCal format. Click Private iCal and add each calendar with a name. Events are read-only; Tasks are not included.\n\nOptional Google sign-in for Calendar + Tasks: enable both APIs in Google Cloud, configure consent/test users, and create a Desktop OAuth client. Click Google sign-in and choose its JSON.",FontSize=11,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap});var cloud=Small("Open Google Calendar","CalendarGoogleCloud");cloud.HorizontalAlignment=HorizontalAlignment.Left;cloud.Click+=delegate{Open("https://calendar.google.com/calendar/u/0/r/settings");};steps.Children.Add(cloud);var setup=new Border{Child=steps,Padding=new Thickness(12),Background=B("Raised"),CornerRadius=new CornerRadius(9),Margin=new Thickness(0,8,0,0),Visibility=Visibility.Collapsed};Children.Add(setup);
  connect.Click+=async delegate{await Connect();};refresh.Click+=async delegate{await EnsureLoaded(true);};disconnect.Click+=delegate{if(loading!=null)loading.Cancel();revision++;source.Disconnect();snapshot=null;error=null;RenderDays();RenderTimeline();};help.Click+=delegate{setup.Visibility=setup.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;};
  Loaded+=delegate{BarAppearance.Changed-=ApplyLayout;BarAppearance.Changed+=ApplyLayout;ApplyLayout();};Unloaded+=delegate{BarAppearance.Changed-=ApplyLayout;};SizeChanged+=delegate{ApplyLayout();};ApplyLayout();
  timer.Tick+=async delegate{RefreshToday();await EnsureLoaded(false);};IsVisibleChanged+=async delegate{if(!disposed&&IsVisible){timer.Start();RefreshToday();await EnsureLoaded(false);}else{ResetWeekMotion();timer.Stop();if(loading!=null)loading.Cancel();busy=false;revision++;}};Unloaded+=delegate{ResetWeekMotion();timer.Stop();if(loading!=null)loading.Cancel();};RenderDays();RenderTimeline();
 }
 internal void ApplyLayout(){if(disposed)return;compactToggle.Background=B(BarAppearance.Settings.calendarCompact?"Selected":"Raised");compactToggle.Foreground=B(BarAppearance.Settings.calendarCompact?"Text":"Muted");bool next=BarAppearance.Settings.calendarCompact&&(ActualWidth<1||ActualWidth>=560);if(next==compact)return;compact=next;ResetWeekMotion();layout.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);layout.ColumnDefinitions[1].Width=new GridLength(compact?20:0);layout.ColumnDefinitions[2].Width=compact?new GridLength(1,GridUnitType.Star):new GridLength(0);Grid.SetColumn(timelinePanel,compact?2:0);Grid.SetRow(timelinePanel,compact?0:1);timelineHeading.Margin=new Thickness(0,compact?0:20,0,10);RenderDays();RenderTimeline();}
 internal void SelectDay(DateTime day){selected=day.Date;if(CalendarData.MonthStart(selected)!=anchor){ResetWeekMotion();anchor=CalendarData.MonthStart(selected);}RenderDays();RenderTimeline();if(IsVisible){var pending=EnsureLoaded(false);}}
 internal async Task MoveMonth(int direction){var previous=CaptureWeek();ResetWeekMotion();anchor=anchor.AddMonths(direction);selected=anchor.AddDays(Math.Min(selected.Day,DateTime.DaysInMonth(anchor.Year,anchor.Month))-1);RenderDays();RenderTimeline();if(previous!=null)AnimateWeek(previous,direction);await EnsureLoaded(false);} ImageSource CaptureWeek(){
  if(!SystemParameters.ClientAreaAnimation||Motion.Speed==0||!IsVisible||dayViewport.ActualWidth<1)return null;
  var presentation=PresentationSource.FromVisual(this);double scale=presentation==null||presentation.CompositionTarget==null?1:presentation.CompositionTarget.TransformToDevice.M11;
  var bitmap=new RenderTargetBitmap((int)Math.Ceiling(dayViewport.ActualWidth*scale),(int)Math.Ceiling(dayViewport.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(dayViewport);bitmap.Freeze();return bitmap;
 }
 void ResetWeekMotion(){weekRevision++;weekSlide.BeginAnimation(TranslateTransform.XProperty,null);weekSlide.X=0;days.BeginAnimation(OpacityProperty,null);days.Opacity=1;if(outgoingWeek!=null){dayViewport.Children.Remove(outgoingWeek);outgoingWeek=null;}}
 void AnimateWeek(ImageSource previous,int direction){
  int version=weekRevision;var oldSlide=new TranslateTransform();outgoingWeek=new Image{Source=previous,Stretch=Stretch.Fill,Width=previous.Width,Height=previous.Height,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,RenderTransform=oldSlide};dayViewport.Children.Insert(0,outgoingWeek);
  double distance=Math.Max(1,dayViewport.ActualWidth);var duration=Motion.Time(280);var easing=new CubicEase{EasingMode=EasingMode.EaseOut};var incoming=new DoubleAnimation(direction*distance,0,duration){EasingFunction=easing};Timeline.SetDesiredFrameRate(incoming,DisplayCadence.Rate(this));incoming.Completed+=delegate{if(version==weekRevision)ResetWeekMotion();};
  weekSlide.BeginAnimation(TranslateTransform.XProperty,incoming);oldSlide.BeginAnimation(TranslateTransform.XProperty,DisplayCadence.At(this,new DoubleAnimation(0,-direction*distance,duration){EasingFunction=easing}));
 }
 public void RefreshToday(){var date=clock().Date;if(date==today)return;bool following=selected==today;today=date;if(following){selected=today;anchor=CalendarData.MonthStart(today);ResetWeekMotion();}RenderDays();RenderTimeline();if(IsVisible){var pending=EnsureLoaded(false);}}
 internal void ShowSnapshot(CalendarSnapshot data){snapshot=data;error=null;RenderDays();RenderTimeline();}
 void RenderDays(){
  var visible=VisibleDays;period.Text=anchor.ToString("MMMM yyyy");dayViewport.Height=calendarBody.Height=visible.Length/7*(compact?40:52);
  for(int row=0;row<6;row++)days.RowDefinitions[row].Height=new GridLength(row<visible.Length/7?(compact?40:52):0);
  for(int i=0;i<42;i++){
   bool shown=i<visible.Length;cells[i].Visibility=shown?Visibility.Visible:Visibility.Collapsed;if(!shown){cells[i].Tag=null;dots[i].Children.Clear();continue;}
   numbers[i].FontSize=compact?14:16;months[i].Height=compact?8:12;
   var day=visible[i];cells[i].Tag=day;numbers[i].Text=day.Day.ToString();months[i].Text=day.Month!=anchor.Month&&(day.Day==1||i%7==0)?day.ToString("MMM"):"";   bool chosen=day==selected,isToday=day==today;cells[i].Background=chosen?B("Selected"):Brushes.Transparent;cells[i].Foreground=B(chosen||day.Month==anchor.Month?"Text":"Muted");cells[i].BorderThickness=new Thickness(isToday?1:0);numbers[i].FontWeight=chosen||isToday?FontWeights.SemiBold:FontWeights.Normal;
   var items=snapshot==null?new CalendarItem[0]:snapshot.ForDay(day);dots[i].Children.Clear();foreach(var item in items.Take(3))dots[i].Children.Add(new Ellipse{Width=3,Height=3,Margin=new Thickness(1,0,1,0),Fill=Ink(item.Color)});
   cells[i].ToolTip=day.ToString("dddd, MMMM d, yyyy")+(isToday?" \u00b7 Today":"")+(items.Length>0?" \u00b7 "+items.Length+" items":"");System.Windows.Automation.AutomationProperties.SetName(cells[i],(string)cells[i].ToolTip);
  }
 }
 static Brush Ink(string color){try{if(!String.IsNullOrEmpty(color)&&System.Text.RegularExpressions.Regex.IsMatch(color,@"^#[a-fA-F0-9]{6}$"))return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));}catch{}return B("Accent");}
 void Empty(string title,string detail){var card=new StackPanel{Margin=new Thickness(12)};card.Children.Add(new TextBlock{Text=title,FontSize=12,FontWeight=FontWeights.Medium});card.Children.Add(new TextBlock{Text=detail,FontSize=11,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)});timeline.Children.Add(new Border{Background=B("Raised"),CornerRadius=new CornerRadius(9),Child=card});}
 void RenderTimeline(){
  selectedLabel.Text=selected.ToString("ddd, MMM d");timeline.Children.Clear();bool connected=source.Connected;
  var connections=source as CalendarConnections;bool googleConnected=connections==null?connected:connections.GoogleConnected;
  connect.Content=connecting?"Connecting...":googleConnected?"Reconnect Google":"Google sign-in";connect.IsEnabled=!connecting;refresh.IsEnabled=connected&&!busy&&!connecting;disconnect.Visibility=connected?Visibility.Visible:Visibility.Collapsed;disconnect.IsEnabled=!connecting;
  if(!connected){Empty("Your day, in one place",connecting?"Finish signing in in your browser.":"Add your calendars with Private iCal. No Google Cloud required. Google sign-in also includes dated Tasks.");status.Text=error??"Calendar connections \u00b7 Read-only access";return;}
  bool hasDay=snapshot!=null&&selected>=snapshot.Start&&selected<snapshot.End;
  var items=hasDay?snapshot.ForDay(selected):new CalendarItem[0];
  if(items.Length==0)Empty(busy?"Loading your day...":error!=null?"Could not load this day":"Nothing scheduled",busy?"Getting your calendars and tasks.":error??selected.ToString("dddd, MMMM d"));
  foreach(var item in items){
   var row=new Grid{Margin=new Thickness(0,0,0,8)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(compact?60:82)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});row.ColumnDefinitions.Add(new ColumnDefinition());
   var time=new TextBlock{Text=item.TimeLabel(selected),FontSize=11,Foreground=B("Muted"),VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,9,0,0)};row.Children.Add(time);
   var mark=new Border{Width=2,Background=Ink(item.Color),CornerRadius=new CornerRadius(1),Margin=new Thickness(0,7,8,7)};Grid.SetColumn(mark,1);row.Children.Add(mark);
   var content=new StackPanel{Margin=new Thickness(8,7,8,7)};content.Children.Add(new TextBlock{Text=item.Title,FontSize=12,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.Medium});var details=item.Details(selected);if(details.Length>0)content.Children.Add(new TextBlock{Text=details,FontSize=10,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0)});
   var entry=item;var button=new Button{Content=content,Template=eventTemplate,Padding=new Thickness(0),BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=Brushes.Transparent,ToolTip=String.IsNullOrEmpty(item.Url)?item.Title:"Open in Google"};button.Click+=delegate{if(!String.IsNullOrEmpty(entry.Url))Open(entry.Url);};Grid.SetColumn(button,2);row.Children.Add(button);timeline.Children.Add(row);
  }
  status.Text=error??(snapshot==null?"Google connected":String.IsNullOrEmpty(snapshot.Notice)?"Google Calendar + Tasks \u00b7 Updated "+snapshot.FetchedUtc.ToLocalTime().ToString("h:mm tt"):snapshot.Notice);
 }
 public void Dispose(){if(disposed)return;disposed=true;BarAppearance.Changed-=ApplyLayout;++revision;timer.Stop();ResetWeekMotion();if(loading!=null){loading.Cancel();loading.Dispose();loading=null;}if(authorization!=null)authorization.Cancel();snapshot=null;var owned=source as IDisposable;if(owned!=null)owned.Dispose();}
 async Task EnsureLoaded(bool force){
  if(disposed)return;
  if(!source.Connected||connecting){RenderTimeline();return;}var visible=VisibleDays;var start=visible[0];var end=visible[visible.Length-1].AddDays(1);
  if(!force&&snapshot!=null&&snapshot.Start==start&&snapshot.End==end&&DateTime.UtcNow-snapshot.FetchedUtc<TimeSpan.FromMinutes(5)){RenderTimeline();return;}
  if(busy&&loadingStart==start&&!force&&loading!=null&&!loading.IsCancellationRequested)return;if(loading!=null){loading.Cancel();loading.Dispose();}loading=new CancellationTokenSource();var token=loading.Token;int version=++revision;loadingStart=start;busy=true;error=null;RenderTimeline();
  try{var data=await source.Load(start,end,force,token);if(version==revision&&!token.IsCancellationRequested)ShowSnapshot(data);}
  catch(OperationCanceledException){}catch(Exception e){if(version==revision)error=e is System.Net.Http.HttpRequestException?"Google is unavailable. Check your connection.":e.Message;}
  finally{if(version==revision){busy=false;RenderDays();RenderTimeline();}}
 }
 async Task Connect(){
  if(disposed||connecting)return;if(loading!=null)loading.Cancel();revision++;busy=false;connecting=true;error=null;authorization=new CancellationTokenSource();RenderTimeline();
  try{
   if(!source.HasClient){ModalActive=true;try{var chooser=new OpenFileDialog{Title="Choose Google Desktop app credentials",Filter="Google OAuth client (*.json)|*.json",CheckFileExists=true};if(chooser.ShowDialog(Window.GetWindow(this))!=true)return;source.ImportClient(chooser.FileName);}finally{ModalActive=false;}}
   status.Text="Finish signing in in your browser.";await source.Connect(authorization.Token);snapshot=null;
  }catch(OperationCanceledException){error="Sign-in timed out or was cancelled. You can try again.";}catch(Exception e){error=e is System.Security.Cryptography.CryptographicException?"Windows could not protect Google sign-in data for this account.":e.Message;}
  finally{connecting=false;authorization.Dispose();authorization=null;if(!disposed)RenderTimeline();}
  if(!disposed&&source.Connected&&IsVisible)await EnsureLoaded(true);
 }
 async Task ManageFeeds(){
  var connections=source as CalendarConnections;if(connections==null||connecting)return;ModalActive=true;bool changed=false;
  try{changed=new CalendarFeedDialog(Window.GetWindow(this),connections.Feeds).ShowDialog()==true;}
  finally{ModalActive=false;}
  if(!changed)return;if(loading!=null)loading.Cancel();revision++;busy=false;snapshot=null;error=null;RenderDays();RenderTimeline();if(IsVisible)await EnsureLoaded(true);
 }
 static void Open(string url){try{Uri uri;if(Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https")Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch{}}
}
