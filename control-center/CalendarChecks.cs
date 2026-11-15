using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

static class CalendarChecks {
 internal sealed class FakeSource : ICalendarSource {
  public CalendarSnapshot Data;public bool HasClient {get{return true;}}public bool Connected {get;private set;}
  public FakeSource(CalendarSnapshot data){Data=data;Connected=true;}
  public void ImportClient(string path){} public Task Connect(CancellationToken token){Connected=true;return Task.FromResult(0);}public void Disconnect(){Connected=false;}
  public Task<CalendarSnapshot> Load(DateTime start,DateTime end,bool force,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(Data);}
 }
 internal static FakeSource Fixture(DateTime? day=null){
  var date=(day??DateTime.Today).Date;var visible=CalendarData.MonthDays(date);var start=visible[0];var data=new CalendarSnapshot{Start=start,End=visible.Last().AddDays(1),FetchedUtc=DateTime.UtcNow,CalendarCount=2};
  data.Items.Add(new CalendarItem{Id="all-day",Title="Project milestone",Source="Personal",AllDay=true,Start=date,End=date.AddDays(1),Color="#88C0D0"});
  data.Items.Add(new CalendarItem{Id="task",Title="Review notes",Source="Personal tasks",Task=true,AllDay=true,Start=date,End=date.AddDays(1)});
  data.Items.Add(new CalendarItem{Id="morning",Title="Deep work",Source="Work",Start=date.AddHours(9),End=date.AddHours(10),Color="#8FBCBB"});
  data.Items.Add(new CalendarItem{Id="afternoon",Title="Project sync",Source="Work",Location="Google Meet",Start=date.AddHours(14),End=date.AddHours(15),Color="#88C0D0"});return new FakeSource(data);
 }
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static System.Collections.Generic.Dictionary<string,object> Parse(string text){return new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(text);}
 public static void Run(Window owner){
  IcalChecks.Run(owner);
  var date=new DateTime(2026,10,30);var source=Fixture(date);var view=new CalendarView(source,()=>date);view.ShowSnapshot(source.Data);
  Check(view.VisibleDays.Length==35&&view.VisibleDays[0]==new DateTime(2026,9,27)&&view.VisibleDays[34]==new DateTime(2026,10,31),"Full-month calendar range is incorrect");
  Check(CalendarData.WeekStart(new DateTime(2026,11,1))==new DateTime(2026,11,1)&&CalendarData.WeekStart(new DateTime(2026,10,31))==new DateTime(2026,10,25),"Sunday must start a week and Saturday must end it");
  var cells=(Button[])typeof(CalendarView).GetField("cells",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);var timeline=(StackPanel)typeof(CalendarView).GetField("timeline",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
  var menuToggle=(Button)typeof(CalendarView).GetField("compactToggle",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);bool oldCalendarSetting=BarAppearance.Settings.calendarCompact;menuToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(BarAppearance.Settings.calendarCompact!=oldCalendarSetting&&view.Children.Contains(menuToggle),"Compact calendar toggle must live in Menu and update the saved setting");menuToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(BarAppearance.Settings.calendarCompact==oldCalendarSetting,"Compact calendar toggle did not restore the setting");
  Check(timeline.Children.Count==4,"Selected-day timeline must contain all fixture events and tasks");
  cells[34].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(view.SelectedDate==new DateTime(2026,10,31)&&source.Data.ForDay(view.SelectedDate).Length==0,"Day click did not select the new day");
  view.SelectDay(date);Check(timeline.Children.Count==4,"Returning to a day should restore its timeline");
  view.SelectDay(date.AddDays(40));Check(view.VisibleDays.Contains(view.SelectedDate),"Selecting an outside date must bring it into the visible range");view.SelectDay(date);
  var move=typeof(CalendarView).GetMethod("MoveMonth",BindingFlags.NonPublic|BindingFlags.Instance);((Task)move.Invoke(view,new object[]{1})).GetAwaiter().GetResult();Check(view.VisibleDays[0]==new DateTime(2026,11,1)&&view.SelectedDate==new DateTime(2026,11,30),"Next-month navigation failed");((Task)move.Invoke(view,new object[]{-1})).GetAwaiter().GetResult();Check(view.VisibleDays[0]==new DateTime(2026,9,27),"Previous-month navigation failed");
  Check(CalendarData.MonthDays(new DateTime(2026,2,1)).Length==28&&CalendarData.MonthDays(new DateTime(2024,2,1)).Length==35&&CalendarData.MonthDays(new DateTime(2026,8,1)).Length==42,"Month grid must handle four/five/six weeks and leap February");
  view.SelectDay(new DateTime(2026,12,31));((Task)move.Invoke(view,new object[]{1})).GetAwaiter().GetResult();Check(view.SelectedDate==new DateTime(2027,1,31),"Month navigation must cross year boundaries");((Task)move.Invoke(view,new object[]{1})).GetAwaiter().GetResult();Check(view.SelectedDate==new DateTime(2027,2,28),"Month navigation must clamp selection to shorter months");view.SelectDay(date);
  var viewport=(Grid)typeof(CalendarView).GetField("dayViewport",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);var slide=(System.Windows.Media.TranslateTransform)typeof(CalendarView).GetField("weekSlide",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);Check(viewport.ClipToBounds&&viewport.Height==260&&slide.Y==0,"Calendar viewport must fit the whole month and settle at zero");var dayGrid=(Grid)typeof(CalendarView).GetField("days",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
  var snapshotImage=new System.Windows.Media.Imaging.RenderTargetBitmap(600,260,96,96,System.Windows.Media.PixelFormats.Pbgra32);
  typeof(CalendarView).GetMethod("AnimateWeek",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,new object[]{snapshotImage,1});
  var outgoing=(Image)typeof(CalendarView).GetField("outgoingWeek",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
  Check(slide.HasAnimatedProperties&&slide.Y==0&&dayGrid.Opacity==1&&!dayGrid.HasAnimatedProperties&&outgoing.Opacity==1&&!outgoing.HasAnimatedProperties,"Month navigation must slide horizontally without animating opacity");
  typeof(CalendarView).GetMethod("ResetWeekMotion",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
  Check(slide.X==0&&!slide.HasAnimatedProperties&&viewport.Children.Count==1,"Interrupted month motion must release its snapshot and reset position");
  view.ShowSnapshot(source.Data);  var multi=new CalendarItem{AllDay=true,Start=date.AddDays(-1),End=date.AddDays(1)};Check(multi.OccursOn(date)&&!multi.OccursOn(date.AddDays(1)),"All-day end dates must be exclusive");
  var midnight=new CalendarItem{Start=date.AddHours(-1),End=date};Check(!midnight.OccursOn(date),"An event ending at midnight must not spill into the next day");
  var task=CalendarData.TaskItem(Parse("{\"id\":\"task\",\"title\":\"Date-only task\",\"status\":\"needsAction\",\"due\":\"2026-10-30T00:00:00.000Z\"}"),"Tasks");Check(task.Start==date&&task.OccursOn(date),"Google Tasks due dates must not shift through UTC conversion");
  Check(CalendarData.TaskItem(Parse("{\"status\":\"completed\",\"due\":\"2026-10-30T00:00:00Z\"}"),"Tasks")==null,"Completed tasks should not appear");
  var fold=CalendarData.Event(Parse("{\"summary\":\"DST crossing\",\"start\":{\"dateTime\":\"2026-11-01T01:45:00-04:00\"},\"end\":{\"dateTime\":\"2026-11-01T01:30:00-05:00\"}}"),"Work","#88C0D0");Check(fold!=null&&(fold.AbsoluteEnd.Value-fold.AbsoluteStart.Value).TotalMinutes==45,"DST fallback event duration must use absolute timestamps");
  var order=source.Data.ForDay(date);Check(order[0].AllDay&&!order[0].Task&&order[1].Task&&order[2].Start.Hour==9,"Timeline grouping/order is incorrect");
  Check(GoogleCalendarSource.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")=="E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM","PKCE S256 test vector failed");Check(GoogleCalendarSource.SameState("valid-state","valid-state")&&!GoogleCalendarSource.SameState("valid-state","wrong-state"),"OAuth state verification failed");
  var serializer=new JavaScriptSerializer();var expiry=DateTime.UtcNow.AddHours(1);var roundTrip=serializer.Deserialize<GoogleCalendarSource.Token>(serializer.Serialize(new GoogleCalendarSource.Token{ExpiresUtc=expiry}));Check(Math.Abs((roundTrip.ExpiresUtc.ToUniversalTime()-expiry).TotalSeconds)<1,"Saved token expiry must survive serialization without a timezone shift");
  var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"calendar-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  try{var file=Path.Combine(folder,"desktop-client.json");File.WriteAllText(file,"{\"installed\":{\"client_id\":\"test-client.apps.googleusercontent.com\",\"client_secret\":\"test-only\"}}");var vault=Path.Combine(folder,"vault");var google=new GoogleCalendarSource(vault);
   try{google.ImportClient(file);Check(google.HasClient&&!google.Connected,"Importing a client must not pretend the user is signed in");Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(vault,"client.bin"))).Contains("test-client"),"Google client must be encrypted at rest");google.Disconnect();Check(!google.Connected,"Disconnected account must not remain connected");Console.WriteLine("PASS: Windows DPAPI client round-trip and disconnected state.");}
   catch(System.Security.Cryptography.CryptographicException){Check(!File.Exists(Path.Combine(vault,"client.bin")),"Failed encryption must not leave plaintext credentials");Console.WriteLine("SKIP: DPAPI round-trip needs a loaded Windows user profile, unavailable in this sandbox. Verified failure leaves no plaintext credentials.");}
  }
  finally{if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),StringComparison.OrdinalIgnoreCase))Directory.Delete(folder,true);}
  var frame=new Border{Width=488,Padding=new Thickness(0),Background=ControlCenterTheme.Current.Brush("Surface"),Resources=owner.Resources,Child=view};System.Windows.Documents.TextElement.SetFontFamily(frame,owner.FontFamily);System.Windows.Documents.TextElement.SetForeground(frame,ControlCenterTheme.Current.Brush("Text"));frame.Measure(new Size(488,double.PositiveInfinity));frame.Arrange(new Rect(frame.DesiredSize));frame.UpdateLayout();
  var image=new System.Windows.Media.Imaging.RenderTargetBitmap(976,(int)Math.Ceiling(frame.ActualHeight*2),192,192,System.Windows.Media.PixelFormats.Pbgra32);image.Render(frame);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"calendar-timeline.png")))png.Save(file);
  bool oldCompact=BarAppearance.Settings.calendarCompact;try{BarAppearance.Update(s=>s.calendarCompact=true);var compactView=new CalendarView(Fixture(date),()=>date);compactView.Resources=owner.Resources;compactView.SetValue(System.Windows.Documents.TextElement.ForegroundProperty,ControlCenterTheme.Current.Brush("Text"));compactView.ShowSnapshot(source.Data);compactView.Measure(new Size(720,double.PositiveInfinity));compactView.Arrange(new Rect(0,0,720,compactView.DesiredSize.Height));compactView.UpdateLayout();compactView.ApplyLayout();var compactTimeline=(StackPanel)typeof(CalendarView).GetField("timelinePanel",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(compactView);var month=(StackPanel)typeof(CalendarView).GetField("monthPanel",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(compactView);Check(compactView.CompactLayout&&Grid.GetColumn(compactTimeline)==2&&compactTimeline.TranslatePoint(new Point(),compactView).X>month.ActualWidth&&compactView.SelectedDate==date,"Compact calendar must show today's timeline beside the month");var imageCompact=new System.Windows.Media.Imaging.RenderTargetBitmap(720,(int)Math.Ceiling(compactView.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);imageCompact.Render(compactView);var compactPng=new System.Windows.Media.Imaging.PngBitmapEncoder();compactPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(imageCompact));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"calendar-compact.png")))compactPng.Save(output);compactView.Measure(new Size(480,double.PositiveInfinity));compactView.Arrange(new Rect(0,0,480,compactView.DesiredSize.Height));compactView.UpdateLayout();compactView.ApplyLayout();Check(!compactView.CompactLayout&&Grid.GetRow(compactTimeline)==1&&Grid.GetColumn(compactTimeline)==0,"Narrow compact calendar must stack without clipping");compactView.Dispose();}finally{BarAppearance.Update(s=>s.calendarCompact=oldCompact);}
  Console.WriteLine("PASS: full-month selectable dates, Sunday-first four/five/six-week grids, month/year navigation and short-month selection, timeline selection/order, exclusive all-day ends, midnight/DST boundaries, date-only Tasks and PKCE/state. No Google requests made.");
 }
}
