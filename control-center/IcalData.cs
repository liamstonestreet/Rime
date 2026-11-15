using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

// A deliberately bounded reader for Google Calendar's RFC 5545 event feeds.
// Unsupported rules are reported, never silently displayed as a different recurrence.
static class IcalData {
 internal sealed class Property {
  public string Name,Value;public Dictionary<string,string> Parameters=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  public string Param(string name){string value;return Parameters.TryGetValue(name,out value)?value:"";}
 }
 internal sealed class Component {
  public string Name;public List<Property> Properties=new List<Property>();public List<Component> Children=new List<Component>();
  public Property Get(string name){return Properties.FirstOrDefault(p=>p.Name==name);}
  public string Text(string name){var p=Get(name);return p==null?"":Unescape(p.Value);}
  public IEnumerable<Property> All(string name){return Properties.Where(p=>p.Name==name);}
 }
 internal sealed class Stamp {
  public DateTime Wall;public bool Date,Utc;public string Zone="";
  public Stamp At(DateTime wall){return new Stamp{Wall=wall,Date=Date,Utc=Utc,Zone=Zone};}
 }
 internal static string Unescape(string value){return Regex.Replace(value,@"\\([nN,;\\])",m=>m.Groups[1].Value.Equals("n",StringComparison.OrdinalIgnoreCase)?"\n":m.Groups[1].Value);}
 static List<string> Split(string value,char delimiter){var parts=new List<string>();int start=0;bool quoted=false;for(int i=0;i<value.Length;i++){if(value[i]=='"')quoted=!quoted;if(value[i]==delimiter&&!quoted){parts.Add(value.Substring(start,i-start));start=i+1;}}parts.Add(value.Substring(start));return parts;}
 internal static Component Read(string text){
  if(text.Length>8*1024*1024)throw new FormatException("Calendar feed is too large.");
  text=text.TrimStart('\uFEFF');var unfolded=Regex.Replace(text.Replace("\r\n","\n"),"\n[ \t]","");var stack=new Stack<Component>();Component root=null;int count=0;
  foreach(var raw in unfolded.Split('\n')){var line=raw.TrimEnd('\r');if(line.Length==0)continue;if(++count>150000)throw new FormatException("Calendar feed has too many entries.");
   bool quoted=false;int colon=-1;for(int i=0;i<line.Length;i++){if(line[i]=='"')quoted=!quoted;if(line[i]==':'&&!quoted){colon=i;break;}}if(colon<1)throw new FormatException("Invalid calendar content.");
   var fields=Split(line.Substring(0,colon),';');string name=fields[0].ToUpperInvariant(),value=line.Substring(colon+1);
   if(name=="BEGIN"){var component=new Component{Name=value.ToUpperInvariant()};if(stack.Count>12)throw new FormatException("Calendar nesting is too deep.");if(stack.Count>0)stack.Peek().Children.Add(component);else{if(root!=null)throw new FormatException("Expected one calendar.");root=component;}stack.Push(component);}
   else if(name=="END"){if(stack.Count==0||stack.Pop().Name!=value.ToUpperInvariant())throw new FormatException("Incomplete calendar.");}
   else{if(stack.Count==0)throw new FormatException("Expected calendar content.");var p=new Property{Name=name,Value=value};foreach(var field in fields.Skip(1)){int equal=field.IndexOf('=');if(equal>0)p.Parameters[field.Substring(0,equal)]=field.Substring(equal+1).Trim('"');}stack.Peek().Properties.Add(p);}
  }
  if(root==null||root.Name!="VCALENDAR"||stack.Count!=0)throw new FormatException("This link did not return an iCal calendar.");return root;
 }
 internal static Stamp Date(Property property,string value=null){
  if(property==null)throw new FormatException("Missing event date.");string raw=value??property.Value;bool date=property.Param("VALUE")=="DATE"||raw.Length==8,utc=raw.EndsWith("Z",StringComparison.Ordinal);DateTime wall;
  if(!DateTime.TryParseExact(raw,date?"yyyyMMdd":utc?"yyyyMMdd'T'HHmmss'Z'":"yyyyMMdd'T'HHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out wall))throw new FormatException("Unsupported calendar date.");return new Stamp{Wall=wall,Date=date,Utc=utc,Zone=property.Param("TZID")};
 }
 static Dictionary<string,string> Rule(string value){var result=new Dictionary<string,string>();foreach(var field in value.Split(';')){var pair=field.Split('=');if(pair.Length!=2||result.ContainsKey(pair[0]))throw new FormatException("Invalid recurrence rule.");result.Add(pair[0],pair[1]);}return result;}
 static int Number(Dictionary<string,string> rule,string key,int fallback){string value;int result;if(!rule.TryGetValue(key,out value))return fallback;if(!int.TryParse(value,out result)||result<1)throw new FormatException("Invalid recurrence interval.");return result;}
 static int[] Integers(Dictionary<string,string> rule,string key){string value;if(!rule.TryGetValue(key,out value))return new int[0];return value.Split(',').Select(v=>int.Parse(v,CultureInfo.InvariantCulture)).ToArray();}
 static readonly string[] weekdays={"SU","MO","TU","WE","TH","FR","SA"};
 static int DayNumber(string name){int index=Array.IndexOf(weekdays,name);if(index<0)throw new FormatException("Invalid recurrence weekday.");return index;}
 static DateTime Week(DateTime day,int first){return day.Date.AddDays(-(((int)day.DayOfWeek-first+7)%7));}
 static bool DayMatch(DateTime day,string spec,bool yearly){
  var match=Regex.Match(spec,@"^([+-]?\d+)?(SU|MO|TU|WE|TH|FR|SA)$");if(!match.Success)throw new FormatException("Unsupported weekday rule.");if((int)day.DayOfWeek!=DayNumber(match.Groups[2].Value))return false;if(!match.Groups[1].Success)return true;
  int n=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);if(n==0)throw new FormatException("Invalid ordinal weekday.");int index=yearly?day.DayOfYear:day.Day,total=yearly?(DateTime.IsLeapYear(day.Year)?366:365):DateTime.DaysInMonth(day.Year,day.Month);return n>0?(index-1)/7+1==n:(total-index)/7+1==-n;
 }
 // Expands the daily/weekly/monthly/yearly rules emitted by Google's standard repeat UI.
 // COUNT starts at DTSTART, including dates preceding the requested range.
 static List<Stamp> Recurrences(Stamp start,string ruleText,DateTime horizon,Func<Stamp,DateTimeOffset> instant,CancellationToken token){
  var rule=Rule(ruleText);string frequency;if(!rule.TryGetValue("FREQ",out frequency)||!new[]{"DAILY","WEEKLY","MONTHLY","YEARLY"}.Contains(frequency))throw new FormatException("Unsupported recurrence frequency.");
  var allowed=new[]{"FREQ","INTERVAL","COUNT","UNTIL","BYDAY","BYMONTHDAY","BYMONTH","BYSETPOS","WKST"};if(rule.Keys.Any(k=>!allowed.Contains(k)))throw new FormatException("Unsupported recurrence filter.");
  int interval=Number(rule,"INTERVAL",1),limit=Number(rule,"COUNT",int.MaxValue);var months=Integers(rule,"BYMONTH");var monthDays=Integers(rule,"BYMONTHDAY");var positions=Integers(rule,"BYSETPOS");string byDay;var daySpecs=rule.TryGetValue("BYDAY",out byDay)?byDay.Split(','):new string[0];
  if(months.Any(n=>n<1||n>12)||monthDays.Any(n=>n==0||n < -31||n>31)||positions.Any(n=>n==0||n < -366||n>366))throw new FormatException("Invalid recurrence filter.");
  if(daySpecs.Any(s=>Regex.IsMatch(s,@"^[+-]?\d"))&&(frequency=="WEEKLY"||frequency=="DAILY"))throw new FormatException("Unsupported ordinal weekday.");
  if(positions.Length>0&&months.Length+monthDays.Length+daySpecs.Length==0)throw new FormatException("BYSETPOS needs a recurrence filter.");
  string wkst;int first=rule.TryGetValue("WKST",out wkst)?DayNumber(wkst):1;Stamp until=null;string rawUntil;if(rule.TryGetValue("UNTIL",out rawUntil))until=Date(new Property{Value=rawUntil});
  var result=new List<Stamp>();var bucket=new List<DateTime>();string previous=null;int scanned=0,emitted=0;
  Action flush=()=>{var chosen=positions.Length==0?bucket:bucket.Where((d,i)=>positions.Contains(i+1)||positions.Contains(i-bucket.Count)).ToList();foreach(var day in chosen){var stamp=start.At(day+start.Wall.TimeOfDay);if(stamp.Wall<start.Wall)continue;if(until!=null&&(until.Date?stamp.Wall.Date>until.Wall.Date:until.Utc?instant(stamp)>instant(until):stamp.Wall>until.Wall))continue;if(++emitted>limit)break;result.Add(stamp);}bucket.Clear();};
  // Complete the last recurrence period before applying BYSETPOS. Truncating a
  // month at the visible range would turn its last visible weekday into its last weekday.
  DateTime bound=horizon.Date.AddDays(2);if(frequency=="WEEKLY")bound=Week(bound,first).AddDays(6);else if(frequency=="MONTHLY")bound=new DateTime(bound.Year,bound.Month,DateTime.DaysInMonth(bound.Year,bound.Month));else if(frequency=="YEARLY")bound=new DateTime(bound.Year,12,31);
  for(var day=start.Wall.Date;day<=bound;day=day.AddDays(1)){
   if((++scanned&255)==0)token.ThrowIfCancellationRequested();if(scanned>200000)throw new FormatException("Recurrence history exceeds the supported range.");
   string key=frequency=="DAILY"?day.ToString("yyyyMMdd"):frequency=="WEEKLY"?Week(day,first).ToString("yyyyMMdd"):frequency=="MONTHLY"?day.ToString("yyyyMM"):day.Year.ToString();if(previous!=null&&key!=previous){flush();if(emitted>=limit)break;}previous=key;
   int distance=frequency=="DAILY"?(day-start.Wall.Date).Days:frequency=="WEEKLY"?(Week(day,first)-Week(start.Wall,first)).Days/7:frequency=="MONTHLY"?(day.Year-start.Wall.Year)*12+day.Month-start.Wall.Month:day.Year-start.Wall.Year;if(distance%interval!=0)continue;
   if(months.Length>0&&!months.Contains(day.Month))continue;
   if(monthDays.Length>0&&!monthDays.Contains(day.Day)&&!monthDays.Contains(day.Day-DateTime.DaysInMonth(day.Year,day.Month)-1))continue;
   if(daySpecs.Length>0&&!daySpecs.Any(s=>DayMatch(day,s,frequency=="YEARLY"&&months.Length==0)))continue;
   if(daySpecs.Length==0&&monthDays.Length==0){if(frequency=="WEEKLY"&&day.DayOfWeek!=start.Wall.DayOfWeek)continue;if((frequency=="MONTHLY"||frequency=="YEARLY")&&day.Day!=start.Wall.Day)continue;}
   if(frequency=="YEARLY"&&months.Length==0&&daySpecs.Length==0&&monthDays.Length==0&&day.Month!=start.Wall.Month)continue;bucket.Add(day);
  }
  if(emitted<limit)flush();return result;
 }
 sealed class Transition {public DateTime Wall;public TimeSpan From,To;}
 sealed class Zones {
  readonly Dictionary<string,Component> definitions;readonly Dictionary<string,List<Transition>> cache=new Dictionary<string,List<Transition>>();readonly CancellationToken token;
  public Zones(Component calendar,CancellationToken token){this.token=token;definitions=calendar.Children.Where(c=>c.Name=="VTIMEZONE").GroupBy(c=>c.Text("TZID")).ToDictionary(g=>g.Key,g=>g.Last());}
  static TimeSpan Offset(string value){if(!Regex.IsMatch(value,@"^[+-]\d{4}(\d{2})?$"))throw new FormatException("Invalid timezone offset.");int sign=value[0]=='-'?-1:1;return TimeSpan.FromSeconds(sign*(int.Parse(value.Substring(1,2))*3600+int.Parse(value.Substring(3,2))*60+(value.Length==7?int.Parse(value.Substring(5,2)):0)));}
  public DateTimeOffset Instant(Stamp stamp){
   if(stamp.Utc)return new DateTimeOffset(stamp.Wall,TimeSpan.Zero);if(String.IsNullOrEmpty(stamp.Zone))return new DateTimeOffset(DateTime.SpecifyKind(stamp.Wall,DateTimeKind.Local));
   Component zone;if(definitions.TryGetValue(stamp.Zone,out zone)){
    string key=stamp.Zone+"|"+stamp.Wall.Year;List<Transition> transitions;if(!cache.TryGetValue(key,out transitions)){
     transitions=new List<Transition>();foreach(var observance in zone.Children.Where(c=>c.Name=="STANDARD"||c.Name=="DAYLIGHT")){
      var start=Date(observance.Get("DTSTART"));var from=Offset(observance.Text("TZOFFSETFROM"));var to=Offset(observance.Text("TZOFFSETTO"));var dates=new List<Stamp>{start};foreach(var rule in observance.All("RRULE"))dates.AddRange(Recurrences(start,rule.Value,new DateTime(stamp.Wall.Year+1,12,31),s=>new DateTimeOffset(s.Wall,s.Utc?TimeSpan.Zero:from),token));foreach(var property in observance.All("RDATE"))foreach(var value in property.Value.Split(','))dates.Add(Date(property,value));foreach(var date in dates)transitions.Add(new Transition{Wall=date.Wall,From=from,To=to});
     }transitions=transitions.OrderBy(t=>t.Wall).ToList();if(transitions.Count==0)throw new FormatException("Timezone has no transitions.");cache[key]=transitions;
    }
    var active=transitions.LastOrDefault(t=>t.Wall<=stamp.Wall);TimeSpan offset=active==null?transitions[0].From:active.To;if(active!=null&&active.To>active.From&&stamp.Wall<active.Wall+(active.To-active.From))offset=active.From;return new DateTimeOffset(stamp.Wall,offset);
   }
   // Some feeds omit VTIMEZONE for a system timezone. Windows IDs work directly;
   // common IANA IDs map to the same Windows rules. Unknown IDs fail visibly.
   var aliases=new Dictionary<string,string>{{"Etc/UTC","UTC"},{"UTC","UTC"},{"America/New_York","Eastern Standard Time"},{"America/Chicago","Central Standard Time"},{"America/Denver","Mountain Standard Time"},{"America/Los_Angeles","Pacific Standard Time"},{"America/Phoenix","US Mountain Standard Time"},{"Europe/London","GMT Standard Time"},{"Europe/Paris","Romance Standard Time"},{"Europe/Berlin","W. Europe Standard Time"},{"Asia/Tokyo","Tokyo Standard Time"},{"Asia/Kolkata","India Standard Time"},{"Australia/Sydney","AUS Eastern Standard Time"}};
   string id;if(!aliases.TryGetValue(stamp.Zone,out id))id=stamp.Zone;TimeZoneInfo system;try{system=TimeZoneInfo.FindSystemTimeZoneById(id);}catch{throw new FormatException("Timezone not recognized; this feed needs VTIMEZONE data.");}var local=stamp.Wall;TimeSpan chosen=system.IsAmbiguousTime(local)?system.GetAmbiguousTimeOffsets(local).Max():system.GetUtcOffset(local);if(system.IsInvalidTime(local))chosen=system.GetUtcOffset(local.AddHours(-3));return new DateTimeOffset(local,chosen);
  }
 }
 static IEnumerable<Stamp> Dates(Component component,string key){foreach(var property in component.All(key))foreach(var value in property.Value.Split(',')){if(property.Param("VALUE")=="PERIOD")throw new FormatException("RDATE periods are not supported.");yield return Date(property,value);}}
 static TimeSpan Duration(string value){var weeks=Regex.Match(value,@"^P(\d+)W$");return weeks.Success?TimeSpan.FromDays(int.Parse(weeks.Groups[1].Value,CultureInfo.InvariantCulture)*7.0):XmlConvert.ToTimeSpan(value);}
 static string Identity(Stamp stamp,Zones zones){return stamp.Date?"D"+stamp.Wall.ToString("yyyyMMdd"):"T"+zones.Instant(stamp).UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture);}
 static CalendarItem Item(Component component,Stamp occurrence,Zones zones,string name,string color){
  var original=Date(component.Get("DTSTART"));var finish=component.Get("DTEND");var duration=component.Get("DURATION");TimeSpan length=finish!=null?Date(finish).Wall-original.Wall:duration!=null?Duration(duration.Value):occurrence.Date?TimeSpan.FromDays(1):TimeSpan.Zero;
  if(length<TimeSpan.Zero)throw new FormatException("Event ends before it starts.");var item=new CalendarItem{Id=component.Text("UID"),Title=component.Text("SUMMARY"),Source=name,Color=color,Location=component.Text("LOCATION"),Url=component.Text("URL"),AllDay=occurrence.Date};if(String.IsNullOrWhiteSpace(item.Title))item.Title="Untitled event";
  if(item.AllDay){item.Start=occurrence.Wall.Date;item.End=item.Start+length;}
  else{item.AbsoluteStart=zones.Instant(occurrence);var end=finish!=null?zones.Instant(Date(finish).At(occurrence.Wall+length)):item.AbsoluteStart.Value+length;item.AbsoluteEnd=end>item.AbsoluteStart?end:item.AbsoluteStart.Value.AddSeconds(1);item.Start=item.AbsoluteStart.Value.LocalDateTime;item.End=item.AbsoluteEnd.Value.LocalDateTime;}
  return item;
 }
 internal static List<CalendarItem> Parse(string text,string name,string color,DateTime start,DateTime end,CancellationToken token,out int skipped){
  var calendar=Read(text);var zones=new Zones(calendar,token);var result=new List<CalendarItem>();skipped=0;
  foreach(var group in calendar.Children.Where(c=>c.Name=="VEVENT").GroupBy(c=>c.Text("UID"))){token.ThrowIfCancellationRequested();
   try{
    // Google may export a master plus moved/cancelled RECURRENCE-ID exceptions.
    var masters=group.Where(c=>c.Get("RECURRENCE-ID")==null).ToArray();if(masters.Length>1)throw new FormatException("Duplicate event master.");var master=masters.FirstOrDefault();var overrides=group.Where(c=>c.Get("RECURRENCE-ID")!=null).ToArray();
    if(overrides.Any(c=>c.Get("RECURRENCE-ID").Param("RANGE").Length>0))throw new FormatException("Range overrides are not supported.");
    var replacements=overrides.GroupBy(c=>Identity(Date(c.Get("RECURRENCE-ID")),zones)).ToDictionary(g=>g.Key,g=>g.OrderBy(c=>{int n;return int.TryParse(c.Text("SEQUENCE"),out n)?n:0;}).Last());var staged=new List<CalendarItem>();
    if(master!=null&&master.Text("STATUS")!="CANCELLED"){
     var original=Date(master.Get("DTSTART"));var dates=new List<Stamp>{original};foreach(var rule in master.All("RRULE"))dates.AddRange(Recurrences(original,rule.Value,end.AddDays(2),zones.Instant,token));dates.AddRange(Dates(master,"RDATE"));var excluded=new HashSet<string>(Dates(master,"EXDATE").Select(d=>Identity(d,zones)));
     var finish=master.Get("DTEND");var duration=master.Get("DURATION");var length=finish!=null?Date(finish).Wall-original.Wall:duration!=null?Duration(duration.Value):TimeSpan.FromDays(1);var earliest=start.AddDays(-2)-length;
     // COUNT is evaluated above from the original date, but only nearby instances
     // need timezone conversion, deduplication and UI objects.
     foreach(var stamp in dates.Where(d=>d.Wall>=earliest&&d.Wall<end.AddDays(2)).GroupBy(d=>Identity(d,zones)).Select(g=>g.First())){token.ThrowIfCancellationRequested();var id=Identity(stamp,zones);if(excluded.Contains(id)||replacements.ContainsKey(id))continue;var item=Item(master,stamp,zones,name,color);if(item.Start<end&&item.End>start)staged.Add(item);}
    }
    foreach(var replacement in replacements.Values){if(replacement.Text("STATUS")=="CANCELLED")continue;var item=Item(replacement,Date(replacement.Get("DTSTART")),zones,name,color);if(item.Start<end&&item.End>start)staged.Add(item);}result.AddRange(staged);if(result.Count>20000)throw new FormatException("Too many visible events.");
   }catch(OperationCanceledException){throw;}catch{skipped++;}
  }
  return result;
 }
}
