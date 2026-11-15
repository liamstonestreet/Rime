using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

sealed class CalendarItem {
 public string Id,Title,Source,Color,Location,Url;
 public bool AllDay,Task;
 public DateTime Start,End; // timed items are local; all-day/tasks are date-only, with exclusive End
 public DateTimeOffset? AbsoluteStart,AbsoluteEnd;
 public bool OccursOn(DateTime day){day=day.Date;if(AbsoluteStart.HasValue&&AbsoluteEnd.HasValue){var start=new DateTimeOffset(DateTime.SpecifyKind(day,DateTimeKind.Local));var end=new DateTimeOffset(DateTime.SpecifyKind(day.AddDays(1),DateTimeKind.Local));return AbsoluteStart.Value<end&&AbsoluteEnd.Value>start;}return Start<day.AddDays(1)&&End>day;}
 public string TimeLabel(DateTime day){if(Task)return "Task";if(AllDay)return "All day";return Start.Date<day.Date?"Ongoing":Start.ToString("h:mm tt");}
 public string Details(DateTime day){
  var parts=new List<string>();if(!Task&&!AllDay)parts.Add(End.Date>day.Date?"Continues tomorrow":"Until "+End.ToString("h:mm tt"));
  if(!String.IsNullOrWhiteSpace(Source))parts.Add(Source);if(!String.IsNullOrWhiteSpace(Location))parts.Add(Location);return String.Join("  \u00b7  ",parts);
 }
}
sealed class CalendarSnapshot {
 public DateTime Start,End,FetchedUtc;
 public List<CalendarItem> Items=new List<CalendarItem>();
 public string Notice;public int CalendarCount;
 public CalendarItem[] ForDay(DateTime day){return Items.Where(item=>item.OccursOn(day)).OrderBy(item=>item.Task?1:item.AllDay?0:2).ThenBy(item=>item.AbsoluteStart.HasValue?item.AbsoluteStart.Value.UtcDateTime.Ticks:item.Start.Ticks).ThenBy(item=>item.Title).ToArray();}
}
interface ICalendarSource {
 bool HasClient {get;} bool Connected {get;}
 void ImportClient(string path);
 Task Connect(CancellationToken token);
 void Disconnect();
 Task<CalendarSnapshot> Load(DateTime start,DateTime end,bool force,CancellationToken token);
}
static class CalendarData {
 public static DateTime WeekStart(DateTime date){return date.Date.AddDays(-(int)date.DayOfWeek);}
 public static DateTime MonthStart(DateTime date){return new DateTime(date.Year,date.Month,1);}
 public static DateTime[] MonthDays(DateTime date){var month=MonthStart(date);var start=WeekStart(month);var end=WeekStart(month.AddMonths(1).AddDays(-1)).AddDays(7);return Enumerable.Range(0,(end-start).Days).Select(i=>start.AddDays(i)).ToArray();}
 internal static string Text(Dictionary<string,object> data,string key,string fallback=""){object value;return data!=null&&data.TryGetValue(key,out value)&&value!=null?Convert.ToString(value,CultureInfo.InvariantCulture):fallback;}
 internal static Dictionary<string,object> Map(Dictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)?value as Dictionary<string,object>:null;}
 internal static bool Flag(Dictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)&&value is bool&&(bool)value;}
 public static CalendarItem Event(Dictionary<string,object> data,string calendar,string color){
  if(Text(data,"status")=="cancelled")return null;var start=Map(data,"start");var end=Map(data,"end");if(start==null||end==null)return null;
  var item=new CalendarItem{Id=Text(data,"id"),Title=Text(data,"summary","Untitled event"),Source=calendar,Color=color,Location=Text(data,"location"),Url=Text(data,"htmlLink")};
  if(String.IsNullOrWhiteSpace(item.Title))item.Title="Untitled event";
  var date=Text(start,"date");item.AllDay=date.Length>0;
  if(item.AllDay){item.Start=DateTime.ParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture);item.End=DateTime.ParseExact(Text(end,"date"),"yyyy-MM-dd",CultureInfo.InvariantCulture);}
  else{item.AbsoluteStart=DateTimeOffset.Parse(Text(start,"dateTime"),CultureInfo.InvariantCulture);item.AbsoluteEnd=DateTimeOffset.Parse(Text(end,"dateTime"),CultureInfo.InvariantCulture);item.Start=item.AbsoluteStart.Value.LocalDateTime;item.End=item.AbsoluteEnd.Value.LocalDateTime;}
  return item.AllDay?item.End>item.Start?item:null:item.AbsoluteEnd>item.AbsoluteStart?item:null;
 }
 public static CalendarItem TaskItem(Dictionary<string,object> data,string list){
  if(Text(data,"status")=="completed"||Flag(data,"deleted")||Flag(data,"hidden"))return null;
  string due=Text(data,"due");if(due.Length<10)return null;
  // Google Tasks stores only a due DATE. Converting its midnight-Z timestamp to local time
  // would put tasks on the previous day west of UTC.
  DateTime date;if(!DateTime.TryParseExact(due.Substring(0,10),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date))return null;
  return new CalendarItem{Id=Text(data,"id"),Title=Text(data,"title","Untitled task"),Source=list,Task=true,AllDay=true,Start=date,End=date.AddDays(1),Url=Text(data,"webViewLink")};
 }
}
