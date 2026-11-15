using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

sealed class CalendarFeed {
 public string Name {get;set;}public string Url {get;set;}public string Color {get;set;}
}
sealed class IcalCalendarSource:IDisposable {
 public void Dispose(){http.Dispose();}
 internal static readonly string[] Colors={"#88C0D0","#A3BE8C","#B48EAD","#EBCB8B","#D08770","#81A1C1","#BF616A"};
 readonly string path;readonly object gate=new object();
 readonly HttpClient http;int revision;
 static readonly byte[] entropy=Encoding.UTF8.GetBytes("NordRice.PrivateCalendars.v1");
 public IcalCalendarSource():this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NordRice","GoogleCalendar","feeds.bin")){}
 internal IcalCalendarSource(string path,HttpMessageHandler handler=null){this.path=path;http=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(30)};}
 public bool Connected {get{return File.Exists(path);}}
 internal static string ValidateUrl(string value){
  Uri uri;if(value==null||value.Length>4096||!Uri.TryCreate(value.Trim(),UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="calendar.google.com"||!uri.IsDefaultPort||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||!System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath,@"^/calendar/ical/[^/]+/(private-[a-zA-Z0-9]+|public)/(basic|full)\.ics$"))throw new InvalidOperationException("Use the Google Calendar Secret address in iCal format, under Settings > Integrate calendar.");return uri.AbsoluteUri;
 }
 public List<CalendarFeed> Read(){lock(gate){if(!Connected)return new List<CalendarFeed>();try{return new JavaScriptSerializer().Deserialize<List<CalendarFeed>>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),entropy,DataProtectionScope.CurrentUser)));}catch{throw new InvalidOperationException("Windows could not unlock saved calendar links. Remove the connections and add them again.");}}}
 internal static List<CalendarFeed> Validate(IEnumerable<CalendarFeed> values){var feeds=values.Select((f,i)=>new CalendarFeed{Name=(f.Name??"").Trim(),Url=ValidateUrl(f.Url),Color=Colors.Contains(f.Color)?f.Color:Colors[i%Colors.Length]}).ToList();if(feeds.Count>20)throw new InvalidOperationException("Add up to 20 calendars.");if(feeds.Any(f=>f.Name.Length==0||f.Name.Length>60))throw new InvalidOperationException("Give each calendar a name of 1 to 60 characters.");if(feeds.Select(f=>f.Url).Distinct(StringComparer.Ordinal).Count()!=feeds.Count)throw new InvalidOperationException("That calendar link is already in the list.");return feeds;}
 public void Save(IEnumerable<CalendarFeed> values){var feeds=Validate(values);lock(gate){if(feeds.Count==0){Disconnect();return;}var bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(feeds)),entropy,DataProtectionScope.CurrentUser);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path+".tmp",bytes);if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);revision++;}}
 public void Disconnect(){lock(gate){if(File.Exists(path))File.Delete(path);if(File.Exists(path+".tmp"))File.Delete(path+".tmp");revision++;}}
 async Task<string> Download(string url,CancellationToken token){
  using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){timeout.CancelAfter(TimeSpan.FromSeconds(30));return await DownloadCore(url,timeout.Token).ConfigureAwait(false);}
 }
 internal async Task<string> DownloadCore(string url,CancellationToken token){
  using(var request=new HttpRequestMessage(HttpMethod.Get,ValidateUrl(url)))using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false)){
   // No redirects: the private token must stay on the intended Calendar endpoint.
   if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidOperationException("Calendar feed is unavailable. Check that its private link is still valid.");
   const int maximum=8*1024*1024;if(response.Content.Headers.ContentLength>maximum)throw new InvalidOperationException("Calendar feed exceeds 8 MB.");
   using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var memory=new MemoryStream()){
    var buffer=new byte[16384];int count;while((count=await stream.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))>0){if(memory.Length+count>maximum)throw new InvalidOperationException("Calendar feed exceeds 8 MB.");memory.Write(buffer,0,count);}return Encoding.UTF8.GetString(memory.ToArray());
   }
  }
 }
 public async Task<CalendarSnapshot> Load(DateTime start,DateTime end,CancellationToken token){
  List<CalendarFeed> feeds;int version;lock(gate){feeds=Read();version=revision;}var data=new CalendarSnapshot{Start=start,End=end,FetchedUtc=DateTime.UtcNow};var failures=new List<string>();int skipped=0;
  foreach(var feed in feeds){token.ThrowIfCancellationRequested();try{using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(45));var text=await Download(feed.Url,deadline.Token).ConfigureAwait(false);int omitted=0;var items=await Task.Run(()=>IcalData.Parse(text,feed.Name,feed.Color,start,end,deadline.Token,out omitted),deadline.Token).ConfigureAwait(false);data.Items.AddRange(items);skipped+=omitted;data.CalendarCount++;}}
   catch(OperationCanceledException){if(token.IsCancellationRequested)throw;failures.Add(feed.Name);}
   catch{failures.Add(feed.Name);}}
  token.ThrowIfCancellationRequested();lock(gate){if(version!=revision)throw new OperationCanceledException();}
  if(feeds.Count>0&&data.CalendarCount==0)throw new InvalidOperationException("Could not load any calendar feeds. Check your connection and private links, then Refresh.");
  data.Notice="Private iCal \u00b7 "+data.CalendarCount+" calendar"+(data.CalendarCount==1?"":"s")+" \u00b7 Updated "+data.FetchedUtc.ToLocalTime().ToString("h:mm tt");if(failures.Count>0)data.Notice+=" \u00b7 Unavailable: "+String.Join(", ",failures);if(skipped>0)data.Notice+=" \u00b7 "+skipped+" event series skipped (unsupported or invalid iCal data).";return data;
 }
}

// The two connection methods remain independent. A feed-only setup never starts OAuth.
sealed class CalendarConnections : ICalendarSource,IDisposable {
 public void Dispose(){google.Dispose();Feeds.Dispose();}
 readonly GoogleCalendarSource google=new GoogleCalendarSource();internal readonly IcalCalendarSource Feeds=new IcalCalendarSource();
 public bool HasClient {get{return google.HasClient;}}public bool Connected {get{return google.Connected||Feeds.Connected;}}
 public bool GoogleConnected {get{return google.Connected;}}
 public void ImportClient(string path){google.ImportClient(path);}public Task Connect(CancellationToken token){return google.Connect(token);}
 public void Disconnect(){google.Disconnect();Feeds.Disconnect();}
 public async Task<CalendarSnapshot> Load(DateTime start,DateTime end,bool force,CancellationToken token){
  var data=new CalendarSnapshot{Start=start,End=end,FetchedUtc=DateTime.UtcNow};var notices=new List<string>();bool loaded=false;
  if(Feeds.Connected){try{var feed=await Feeds.Load(start,end,token).ConfigureAwait(false);data.Items.AddRange(feed.Items);data.CalendarCount+=feed.CalendarCount;notices.Add(feed.Notice);loaded=true;}catch(OperationCanceledException){throw;}catch{notices.Add("Private iCal unavailable. Check your connection and private links, then Refresh.");}}
  if(google.Connected){try{var api=await google.Load(start,end,force,token).ConfigureAwait(false);data.Items.AddRange(api.Items);data.CalendarCount+=api.CalendarCount;if(!String.IsNullOrEmpty(api.Notice))notices.Add(api.Notice);loaded=true;}catch(OperationCanceledException){throw;}catch{notices.Add("Google sign-in connection unavailable. Try reconnecting.");}}
  if(!loaded)throw new InvalidOperationException(String.Join(" ",notices));data.Notice=String.Join("\n",notices);return data;
 }
}
