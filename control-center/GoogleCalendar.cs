using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// Native desktop OAuth (PKCE + loopback callback). Nothing is sent through the Zebar bridge.
// Only client metadata and tokens are saved, encrypted with Windows DPAPI for the current user.
sealed class GoogleCalendarSource : ICalendarSource,IDisposable {
 public void Dispose(){http.Dispose();lock(gate){accountRevision++;cached=null;saved=null;}}
 const string Scopes="https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/tasks.readonly";
 readonly string folder;readonly object gate=new object();readonly SemaphoreSlim authGate=new SemaphoreSlim(1,1),loadGate=new SemaphoreSlim(1,1);
 readonly HttpClient http=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};
 JavaScriptSerializer json {get{return new JavaScriptSerializer{MaxJsonLength=8*1024*1024};}}
 CalendarSnapshot cached;int accountRevision;Token saved;
 internal sealed class Client {public string Id {get;set;}public string Secret {get;set;}}
 internal sealed class Token {public string Access {get;set;}public string Refresh {get;set;}public string Scope {get;set;}public DateTime ExpiresUtc {get;set;}}
 public GoogleCalendarSource():this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NordRice","GoogleCalendar")){}
 internal GoogleCalendarSource(string folder){this.folder=folder;}
 string ClientPath {get{return Path.Combine(folder,"client.bin");}} string TokenPath {get{return Path.Combine(folder,"token.bin");}}
 public bool HasClient {get{return File.Exists(ClientPath);}}
 public bool Connected {get{return File.Exists(TokenPath);}}
 static readonly byte[] entropy=Encoding.UTF8.GetBytes("NordRice.GoogleCalendar.v1");
 void Write(string path,object value){Directory.CreateDirectory(folder);var bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(json.Serialize(value)),entropy,DataProtectionScope.CurrentUser);File.WriteAllBytes(path+".tmp",bytes);if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);}
 T Read<T>(string path){return json.Deserialize<T>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),entropy,DataProtectionScope.CurrentUser)));}
 public void ImportClient(string path){
  if(new FileInfo(path).Length>65536)throw new InvalidOperationException("This does not look like a Google desktop app file.");
  Dictionary<string,object> root;try{root=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));}catch{throw new InvalidOperationException("This file is not a valid Google desktop app JSON file.");}var installed=CalendarData.Map(root,"installed");
  string id=CalendarData.Text(installed,"client_id");if(installed==null||!id.EndsWith(".apps.googleusercontent.com",StringComparison.Ordinal))throw new InvalidOperationException("Choose credentials created for a Google Desktop app.");
  lock(gate){accountRevision++;saved=null;cached=null;if(File.Exists(TokenPath))File.Delete(TokenPath);Write(ClientPath,new Client{Id=id,Secret=CalendarData.Text(installed,"client_secret")});}
 }
 public void Disconnect(){lock(gate){accountRevision++;saved=null;cached=null;if(File.Exists(TokenPath))File.Delete(TokenPath);}}
 internal static string Base64Url(byte[] data){return Convert.ToBase64String(data).TrimEnd('=').Replace('+','-').Replace('/','_');}
 static string Random(){var data=new byte[32];using(var random=new RNGCryptoServiceProvider())random.GetBytes(data);return Base64Url(data);}
 internal static string Challenge(string verifier){using(var hash=SHA256.Create())return Base64Url(hash.ComputeHash(Encoding.ASCII.GetBytes(verifier)));}
 static string Query(IDictionary<string,string> values){return String.Join("&",values.Where(p=>!String.IsNullOrEmpty(p.Value)).Select(p=>Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value)));}
 internal static bool SameState(string first,string second){if(first==null||second==null||first.Length!=second.Length)return false;int difference=0;for(int i=0;i<first.Length;i++)difference|=first[i]^second[i];return difference==0;}
 public async Task Connect(CancellationToken token){
  Client client;int revision;lock(gate){client=Read<Client>(ClientPath);accountRevision++;cached=null;revision=accountRevision;}
  using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(token)){
   cancel.CancelAfter(TimeSpan.FromMinutes(5));string verifier=Random(),state=Random();var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
   string redirect="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/";
   try{using(cancel.Token.Register(()=>listener.Stop())){
    string url="https://accounts.google.com/o/oauth2/v2/auth?"+Query(new Dictionary<string,string>{{"client_id",client.Id},{"redirect_uri",redirect},{"response_type","code"},{"scope",Scopes},{"access_type","offline"},{"prompt","consent select_account"},{"state",state},{"code_challenge",Challenge(verifier)},{"code_challenge_method","S256"}});
    Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    string code=await Task.Run(()=>Callback(listener,state,cancel.Token),cancel.Token).ConfigureAwait(false);
    var form=new Dictionary<string,string>{{"client_id",client.Id},{"client_secret",client.Secret},{"code",code},{"code_verifier",verifier},{"redirect_uri",redirect},{"grant_type","authorization_code"}};
    var response=await Exchange(form,cancel.Token).ConfigureAwait(false);Store(response,null,revision);
   }}catch(SocketException){cancel.Token.ThrowIfCancellationRequested();throw;}finally{listener.Stop();}
  }
 }
 static string Callback(TcpListener listener,string expected,CancellationToken token){
  while(true){token.ThrowIfCancellationRequested();using(var client=listener.AcceptTcpClient()){
   client.ReceiveTimeout=5000;client.SendTimeout=5000;using(var stream=client.GetStream()){
    try{var header=new StringBuilder();while(header.Length<8192){int next=stream.ReadByte();if(next<0)break;header.Append((char)next);if(header.Length>=4&&header.ToString(header.Length-4,4)=="\r\n\r\n")break;}
     var line=header.ToString().Split(new[]{"\r\n"},StringSplitOptions.None)[0].Split(' ');if(line.Length<2||line[0]!="GET")continue;
     Uri uri;if(!Uri.TryCreate("http://127.0.0.1"+line[1],UriKind.Absolute,out uri)||uri.AbsolutePath!="/")continue;
     var values=new Dictionary<string,string>();foreach(var pair in uri.Query.TrimStart('?').Split('&')){int equals=pair.IndexOf('=');if(equals>0)values[WebUtility.UrlDecode(pair.Substring(0,equals))]=WebUtility.UrlDecode(pair.Substring(equals+1));}
     string state,code,error;bool valid=values.TryGetValue("state",out state)&&SameState(expected,state);
     string body="<!doctype html><meta charset=utf-8><title>Nord Calendar</title><body style='background:#2e3440;color:#eceff4;font:16px system-ui;padding:48px'>"+(valid?"You can close this tab and return to Control Center.":"This sign-in response could not be verified.")+"</body>";
     var bytes=Encoding.UTF8.GetBytes(body);var response=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");stream.Write(response,0,response.Length);stream.Write(bytes,0,bytes.Length);
     if(!valid)continue;if(values.TryGetValue("error",out error))throw new InvalidOperationException("Google sign-in was not completed. You can try again.");
     if(values.TryGetValue("code",out code)&&!String.IsNullOrWhiteSpace(code))return code;
    }catch(IOException){} // A stray local request must not consume or block the authorization attempt.
   }
  }}
 }
 async Task<Dictionary<string,object>> Exchange(Dictionary<string,string> fields,CancellationToken token){
  using(var content=new FormUrlEncodedContent(fields.Where(p=>!String.IsNullOrEmpty(p.Value))))using(var response=await http.PostAsync("https://oauth2.googleapis.com/token",content,token).ConfigureAwait(false)){
   if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Google could not authorize this app. Check its setup or reconnect your account.");
   return json.Deserialize<Dictionary<string,object>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
  }
 }
 Token Store(Dictionary<string,object> data,Token old,int revision){
  var value=new Token{Access=CalendarData.Text(data,"access_token"),Refresh=CalendarData.Text(data,"refresh_token",old==null?"":old.Refresh),Scope=CalendarData.Text(data,"scope",old==null?Scopes:old.Scope),ExpiresUtc=DateTime.UtcNow.AddSeconds(Convert.ToDouble(data["expires_in"],CultureInfo.InvariantCulture))};
  if(String.IsNullOrEmpty(value.Access)||String.IsNullOrEmpty(value.Refresh))throw new InvalidOperationException("Google did not provide an offline sign-in. Connect again and allow access.");
  lock(gate){if(revision!=accountRevision)throw new OperationCanceledException();Write(TokenPath,value);saved=value;}return value;
 }
 async Task<Token> Access(bool force,CancellationToken token){
  await authGate.WaitAsync(token).ConfigureAwait(false);try{
   Token current;Client client;int revision;lock(gate){current=saved??Read<Token>(TokenPath);client=Read<Client>(ClientPath);revision=accountRevision;}
   if(!force&&current.ExpiresUtc.ToUniversalTime()>DateTime.UtcNow.AddSeconds(60))return current;
   return Store(await Exchange(new Dictionary<string,string>{{"client_id",client.Id},{"client_secret",client.Secret},{"refresh_token",current.Refresh},{"grant_type","refresh_token"}},token).ConfigureAwait(false),current,revision);
  }finally{authGate.Release();}
 }
 async Task<Dictionary<string,object>> Get(string url,CancellationToken token){
  for(int attempt=0;attempt<2;attempt++){
   var access=await Access(attempt>0,token).ConfigureAwait(false);using(var request=new HttpRequestMessage(HttpMethod.Get,url)){
    request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",access.Access);using(var response=await http.SendAsync(request,token).ConfigureAwait(false)){
     if(response.StatusCode==HttpStatusCode.Unauthorized&&attempt==0)continue;
     if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Google returned "+(int)response.StatusCode+". Check API access or reconnect.");
     return json.Deserialize<Dictionary<string,object>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    }
   }
  }throw new InvalidOperationException("Google sign-in expired. Reconnect your account.");
 }
 async Task<List<Dictionary<string,object>>> Pages(string url,CancellationToken token){
  var items=new List<Dictionary<string,object>>();string next=null;do{
   var data=await Get(url+(next==null?"":"&pageToken="+Uri.EscapeDataString(next)),token).ConfigureAwait(false);object raw;
   if(data.TryGetValue("items",out raw)&&raw is IEnumerable)foreach(var item in (IEnumerable)raw){var map=item as Dictionary<string,object>;if(map!=null)items.Add(map);}
   next=CalendarData.Text(data,"nextPageToken");
  }while(!String.IsNullOrEmpty(next));return items;
 }
 static string Instant(DateTime date){return Uri.EscapeDataString(DateTime.SpecifyKind(date,DateTimeKind.Local).ToUniversalTime().ToString("o",CultureInfo.InvariantCulture));}
 public async Task<CalendarSnapshot> Load(DateTime start,DateTime end,bool force,CancellationToken token){
  await loadGate.WaitAsync(token).ConfigureAwait(false);try{
   lock(gate)if(!force&&cached!=null&&cached.Start==start&&cached.End==end&&DateTime.UtcNow-cached.FetchedUtc<TimeSpan.FromMinutes(5))return cached;
   int revision;lock(gate)revision=accountRevision;
   var snapshot=new CalendarSnapshot{Start=start,End=end};var notices=new List<string>();
   var calendars=await Pages("https://www.googleapis.com/calendar/v3/users/me/calendarList?maxResults=250&showHidden=false&fields=nextPageToken,items(id,summary,backgroundColor,selected,primary,deleted,hidden)",token).ConfigureAwait(false);
   foreach(var calendar in calendars.Where(c=>!CalendarData.Flag(c,"deleted")&&!CalendarData.Flag(c,"hidden")&&(CalendarData.Flag(c,"selected")||CalendarData.Flag(c,"primary")))){
    token.ThrowIfCancellationRequested();string id=CalendarData.Text(calendar,"id");
    try{var events=await Pages("https://www.googleapis.com/calendar/v3/calendars/"+Uri.EscapeDataString(id)+"/events?singleEvents=true&orderBy=startTime&showDeleted=false&maxResults=2500&timeZone=UTC&fields=nextPageToken,items(id,summary,status,start,end,htmlLink,location)&timeMin="+Instant(start)+"&timeMax="+Instant(end),token).ConfigureAwait(false);
     snapshot.CalendarCount++;foreach(var data in events){try{var item=CalendarData.Event(data,CalendarData.Text(calendar,"summary"),CalendarData.Text(calendar,"backgroundColor"));if(item!=null)snapshot.Items.Add(item);}catch(FormatException){}}
    }catch(OperationCanceledException){throw;}catch{notices.Add("A calendar could not be refreshed.");}
   }
   try{
    var lists=await Pages("https://tasks.googleapis.com/tasks/v1/users/@me/lists?maxResults=100&fields=nextPageToken,items(id,title)",token).ConfigureAwait(false);
    foreach(var list in lists){
     var tasks=await Pages("https://tasks.googleapis.com/tasks/v1/lists/"+Uri.EscapeDataString(CalendarData.Text(list,"id"))+"/tasks?maxResults=100&fields=nextPageToken,items(id,title,status,due,deleted,hidden,webViewLink)&showCompleted=false&showHidden=false&showDeleted=false&showAssigned=true&dueMin="+start.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"T00%3A00%3A00Z&dueMax="+end.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"T00%3A00%3A00Z",token).ConfigureAwait(false);
     foreach(var data in tasks){var item=CalendarData.TaskItem(data,CalendarData.Text(list,"title"));if(item!=null&&item.Start<end&&item.End>start)snapshot.Items.Add(item);}
    }
   }catch(OperationCanceledException){throw;}catch{notices.Add("Google Tasks is unavailable. Check that its API and read access are enabled.");}
   if(snapshot.CalendarCount==0&&calendars.Any(c=>CalendarData.Flag(c,"selected")||CalendarData.Flag(c,"primary")))throw new InvalidOperationException("Your calendars could not be loaded. Check API access or reconnect.");
   snapshot.Notice=String.Join(" ",notices.Distinct());snapshot.FetchedUtc=DateTime.UtcNow;
   lock(gate){if(revision!=accountRevision)throw new OperationCanceledException();cached=snapshot;}return snapshot;
  }finally{loadGate.Release();}
 }
}
