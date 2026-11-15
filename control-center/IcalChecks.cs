using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

static class IcalChecks {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 const string Zone="BEGIN:VTIMEZONE\nTZID:America/New_York\nBEGIN:STANDARD\nDTSTART:19701101T020000\nTZOFFSETFROM:-0400\nTZOFFSETTO:-0500\nRRULE:FREQ=YEARLY;BYMONTH=11;BYDAY=1SU\nEND:STANDARD\nBEGIN:DAYLIGHT\nDTSTART:19700308T020000\nTZOFFSETFROM:-0500\nTZOFFSETTO:-0400\nRRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=2SU\nEND:DAYLIGHT\nEND:VTIMEZONE\n";
 static string Event(string body){return "BEGIN:VEVENT\n"+body+"\nEND:VEVENT\n";}
 static string Feed(string body){return "BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//Nord Rice checks//EN\n"+Zone+body+"END:VCALENDAR\n";}
 static List<CalendarItem> Parse(string body,DateTime start,DateTime end,out int skipped){return IcalData.Parse(Feed(body),"Personal","#88C0D0",start,end,CancellationToken.None,out skipped);}
 sealed class Handler : HttpMessageHandler {
  public int Calls;public HttpStatusCode Status=HttpStatusCode.OK;public string Body="BEGIN:VCALENDAR\nEND:VCALENDAR";
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();Calls++;return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(Body)});}
 }
 static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject node){yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<System.Windows.DependencyObject>())foreach(var descendant in Descendants(child))yield return descendant;}
 static void Reject(Action action,string message){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,message);}
 internal static void Run(Window owner){
  int skipped;var start=new DateTime(2026,10,19);var end=new DateTime(2026,11,9);
  var weekly=Event("UID:weekly\nDTSTART;TZID=America/New_York:20261026T090000\nDTEND;TZID=America/New_York:20261026T100000\nRRULE:FREQ=WEEKLY;COUNT=5;BYDAY=MO,WE,FR\nEXDATE;TZID=America/New_York:20261028T090000\nRDATE;TZID=America/New_York:20261103T090000\nSUMMARY:Study\\, math\\nwith caf\u00e9\n  friends\nLOCATION:Desk")+
   Event("UID:weekly\nRECURRENCE-ID;TZID=America/New_York:20261030T090000\nDTSTART;TZID=America/New_York:20261031T110000\nDTEND;TZID=America/New_York:20261031T120000\nSUMMARY:Moved meeting")+
   Event("UID:weekly\nRECURRENCE-ID;TZID=America/New_York:20261102T090000\nSTATUS:CANCELLED");
  var items=Parse(weekly,start,end,out skipped);Check(skipped==0&&items.Count==4,"Weekly repeat/exclusion/addition/override expansion failed");
  Check(items.Any(i=>i.Title=="Moved meeting"&&i.AbsoluteStart.Value.UtcDateTime==new DateTime(2026,10,31,15,0,0)),"Moved instance must replace its original date");
  Check(items.Any(i=>i.AbsoluteStart.Value.UtcDateTime==new DateTime(2026,10,26,13,0,0))&&items.Any(i=>i.AbsoluteStart.Value.UtcDateTime==new DateTime(2026,11,4,14,0,0)),"VTIMEZONE recurrence must preserve wall time over DST");
  Check(items.Any(i=>i.Title=="Study, math\nwith caf\u00e9 friends"),"Escapes, Unicode, or folded lines were lost");
  items=Parse(weekly,new DateTime(2026,10,29),end,out skipped);Check(items.Count==3,"COUNT must include occurrences before the visible range");
  items=Parse(Event("UID:daily\nDTSTART:20261026T120000Z\nDURATION:PT45M\nRRULE:FREQ=DAILY;INTERVAL=2;UNTIL=20261101T120000Z\nSUMMARY:Daily"),start,end,out skipped);Check(skipped==0&&items.Count==4&&items.All(i=>(i.AbsoluteEnd.Value-i.AbsoluteStart.Value).TotalMinutes==45),"Daily interval/UNTIL/duration failed");
  items=Parse(Event("UID:month\nDTSTART:20261030T090000Z\nDTEND:20261030T100000Z\nRRULE:FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=-1\nSUMMARY:Last workday"),new DateTime(2026,11,1),new DateTime(2026,11,9),out skipped);Check(skipped==0&&items.Count==0,"BYSETPOS must use the whole month, not the visible range");
  items=Parse(Event("UID:month\nDTSTART:20261013T090000Z\nDTEND:20261013T100000Z\nRRULE:FREQ=MONTHLY;COUNT=3;BYDAY=2TU\nSUMMARY:Second Tuesday"),new DateTime(2026,10,1),new DateTime(2027,1,1),out skipped);Check(items.Count==3&&items.Any(i=>i.AbsoluteStart.Value.UtcDateTime.Date==new DateTime(2026,11,10)),"Ordinal monthly weekday failed");
  items=Parse(Event("UID:leap\nDTSTART;VALUE=DATE:20240229\nRRULE:FREQ=YEARLY;COUNT=3\nSUMMARY:Leap day"),new DateTime(2024,1,1),new DateTime(2033,1,1),out skipped);Check(items.Count==3&&items.Any(i=>i.Start==new DateTime(2032,2,29)),"Yearly invalid dates must be skipped");
  items=Parse(Event("UID:all\nDTSTART;VALUE=DATE:20261030\nDTEND;VALUE=DATE:20261101\nSUMMARY:Trip")+Event("UID:fold\nDTSTART:20261101T054500Z\nDTEND:20261101T063000Z\nSUMMARY:DST fold"),start,end,out skipped);Check(items.Count==2&&items[0].OccursOn(new DateTime(2026,10,31))&&!items[0].OccursOn(new DateTime(2026,11,1))&&(items[1].AbsoluteEnd.Value-items[1].AbsoluteStart.Value).TotalMinutes==45,"All-day exclusive ends or UTC DST crossing failed");
  items=Parse(Event("UID:unsupported\nDTSTART:20261030T090000Z\nRRULE:FREQ=HOURLY\nSUMMARY:Unsupported")+Event("UID:good\nDTSTART:20261030T120000Z\nSUMMARY:Good"),start,end,out skipped);Check(skipped==1&&items.Count==1,"Unsupported recurrence must be reported without discarding good events");
  items=Parse(Event("UID:unknown\nDTSTART;TZID=Unrecognized/Zone:20261030T090000\nSUMMARY:Unknown"),start,end,out skipped);Check(skipped==1&&items.Count==0,"Unknown timezones must not silently become local time");
  Reject(()=>IcalData.Read("<html>Sign in</html>"),"HTML must not be accepted as a calendar");
  const string url="https://calendar.google.com/calendar/ical/test%40example.com/private-test123/basic.ics";
  Check(IcalCalendarSource.ValidateUrl(url)==url,"Valid private Google feed rejected");foreach(var bad in new[]{"http://calendar.google.com/calendar/ical/a/private-test/basic.ics","https://calendar.google.com.evil.test/calendar/ical/a/private-test/basic.ics","https://calendar.google.com:444/calendar/ical/a/private-test/basic.ics",url+"?token=secret","https://example.com/calendar.ics","file:///C:/secret.ics"})Reject(()=>IcalCalendarSource.ValidateUrl(bad),"Invalid feed endpoint was allowed");
  var five=IcalCalendarSource.Validate(Enumerable.Range(0,5).Select(i=>new CalendarFeed{Name="Calendar "+i,Url=url.Replace("test123","test"+i)}));Check(five.Count==5&&five.Select(f=>f.Color).Distinct().Count()==5,"Five calendars need distinct names/colors");Reject(()=>IcalCalendarSource.Validate(new[]{five[0],five[0]}),"Duplicate feeds must be rejected");
  var combined=new CalendarSnapshot{Start=start,End=end,FetchedUtc=DateTime.UtcNow};foreach(var feed in five){int omitted;combined.Items.AddRange(IcalData.Parse(Feed(Event("UID:same-id\nDTSTART;VALUE=DATE:20261030\nSUMMARY:One per calendar")),feed.Name,feed.Color,start,end,CancellationToken.None,out omitted));}Check(combined.ForDay(new DateTime(2026,10,30)).Length==5&&combined.Items.Select(i=>i.Source).Distinct().Count()==5,"Five feeds must merge without cross-calendar UID collisions");
  var handler=new Handler();var folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ical-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var source=new IcalCalendarSource(Path.Combine(folder,"feeds.bin"),handler);
  try{
   Check(source.DownloadCore(url,CancellationToken.None).GetAwaiter().GetResult().StartsWith("BEGIN:VCALENDAR"),"Feed download failed");handler.Status=HttpStatusCode.Redirect;Reject(()=>source.DownloadCore(url,CancellationToken.None).GetAwaiter().GetResult(),"Redirects must not forward a secret feed link");int requests=handler.Calls;Reject(()=>source.DownloadCore("https://example.com/private",CancellationToken.None).GetAwaiter().GetResult(),"Untrusted endpoint was requested");Check(handler.Calls==requests,"Validation must happen before network access");
   var cancellation=new CancellationTokenSource();cancellation.Cancel();Reject(()=>IcalData.Parse(Feed(weekly),"Test","#88C0D0",start,end,cancellation.Token,out skipped),"Cancelled parsing must stop");
   try{source.Save(five);Check(source.Read().Count==5,"Encrypted feed list did not round-trip");Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,"feeds.bin"))).Contains("calendar.google.com"),"Secret feed links must be encrypted at rest");source.Disconnect();Check(!source.Connected,"Disconnect must remove saved feed connections");Console.WriteLine("PASS: private calendar DPAPI round-trip and disconnect.");}
   catch(System.Security.Cryptography.CryptographicException){Check(!File.Exists(Path.Combine(folder,"feeds.bin")),"Failed encryption must leave no plaintext links");Console.WriteLine("SKIP: private-feed DPAPI round-trip needs a loaded Windows profile; no plaintext fallback.");}
   var dialog=new CalendarFeedDialog(owner,source);var controls=Descendants((System.Windows.DependencyObject)dialog.Content).ToArray();var add=controls.OfType<Button>().Single(b=>b.Name=="CalendarAddFeed");for(int i=0;i<4;i++)add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Descendants((System.Windows.DependencyObject)dialog.Content).OfType<PasswordBox>().Count()==5,"Calendar dialog must support five masked feed fields");var bounds=(FrameworkElement)dialog.Content;bounds.Measure(new Size(520,600));bounds.Arrange(new Rect(new Size(520,bounds.DesiredSize.Height)));bounds.UpdateLayout();var visual=(System.Windows.Media.Visual)dialog.Content;var image=new RenderTargetBitmap((int)Math.Ceiling(bounds.ActualWidth*2),(int)Math.Ceiling(bounds.ActualHeight*2),192,192,System.Windows.Media.PixelFormats.Pbgra32);image.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"calendar-feeds.png")))png.Save(file);dialog.Close();
  }finally{if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),StringComparison.OrdinalIgnoreCase))Directory.Delete(folder,true);}
  Console.WriteLine("PASS: private iCal parsing, five-calendar merge/colors, daily/weekly/monthly/yearly repeats, COUNT/UNTIL/BYSETPOS, EXDATE/RDATE, moved/cancelled instances, VTIMEZONE/DST, URL validation, rejected redirects and cancellation. No Google requests made.");
 }
}
