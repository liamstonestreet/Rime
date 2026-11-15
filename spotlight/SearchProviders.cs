using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

sealed class SpotlightResult {
 public string Name {get;set;} public string Detail {get;set;} public string Kind {get;set;} public string Value {get;set;}
 public AppEntry App {get;set;} public long Handle {get;set;} public uint ProcessId {get;set;}
 public ImageSource Icon {get{return App==null?null:App.Icon;}}
 public string Symbol {get{return App!=null?"":Kind=="copy"?"=":Kind=="window"?"\u25a3":Kind=="path"?"\u25a1":Kind=="uri"?"\u2197":"/";}}
 public string Key {get{return Kind+"|"+Value;}}
}

static class SpotlightSearch {
 public static SpotlightResult Result(string name,string detail,string kind,string value){return new SpotlightResult{Name=name,Detail=detail,Kind=kind,Value=value};}
 static readonly string[][] commands={
  new[]{"/apps","Find installed apps","Spotify"},new[]{"/calc","Calculate or convert units","sqrt(144) + 8"},new[]{"/files","Find files in your personal folders","report"},new[]{"/folders","Find folders","projects"},new[]{"/windows","Switch to a running window","Discord"},new[]{"/web","Search the web","your search"},new[]{"/settings","Open a Windows settings page","sound"},new[]{"/copy","Copy text to the clipboard","text"},
  new[]{"/home","Open your home folder",""},new[]{"/desktop","Open Desktop",""},new[]{"/documents","Open Documents",""},new[]{"/downloads","Open Downloads",""},new[]{"/recycle","Open Recycle Bin",""},new[]{"/date","Copy today's date",""},new[]{"/time","Copy the current time",""},new[]{"/openall","Open your configured apps",""},new[]{"/closeall","Force-close apps across all workspaces",""},new[]{"/refresh","Refresh installed apps and search caches",""},new[]{"/lock","Lock Windows",""},new[]{"/help","Browse commands",""}
 };
 // Microsoft documents these ms-settings targets at learn.microsoft.com/windows/apps/develop/launch/launch-settings.
 static readonly string[][] settings={new[]{"Display","display"},new[]{"Sound","sound"},new[]{"Bluetooth","bluetooth"},new[]{"Wi-Fi","network-wifi"},new[]{"Network","network-status"},new[]{"VPN","network-vpn"},new[]{"Installed apps","appsfeatures"},new[]{"Default apps","defaultapps"},new[]{"Notifications","notifications"},new[]{"Storage","storagesense"},new[]{"Power","powersleep"},new[]{"Windows Update","windowsupdate"},new[]{"Personalization","personalization"},new[]{"Fonts","fonts"},new[]{"Microphone privacy","privacy-microphone"}};
 static SpotlightResult Shortcut(string name){
  switch(name){
   case "/home":return Result("Open home folder",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"path",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
   case "/desktop":return Result("Open Desktop","Personal folder","path",Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
   case "/documents":return Result("Open Documents","Personal folder","path",Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
   case "/downloads":return Result("Open Downloads","Personal folder","path",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"));
   case "/recycle":return Result("Open Recycle Bin","Browse deleted files","recycle","");
   case "/date":return Result(DateTime.Now.ToString("yyyy-MM-dd"),"Copy today's date","copy",DateTime.Now.ToString("yyyy-MM-dd"));
   case "/time":return Result(DateTime.Now.ToString("HH:mm:ss"),"Copy the current local time","copy",DateTime.Now.ToString("HH:mm:ss"));
   case "/openall":return Result("/openall","Open selected apps; configure in CC > Rice","openall","");
   case "/closeall":return Result("/closeall","Immediately terminate workspace apps; unsaved changes will be lost","closeall","");
   case "/refresh":return Result("Refresh search","Refresh installed apps, running windows and the file cache","refresh","");
   case "/lock":return Result("Lock Windows","Lock this Windows session","lock","");
   default:return Result("Browse commands","Type / to filter commands","query","/");
  }
 }
 public static SpotlightResult Web(string query){return Result("Search web for \u201c"+query+"\u201d","Open in your default browser","uri","https://www.google.com/search?q="+Uri.EscapeDataString(query));}
 public static string WebAddress(string query){Uri uri;if(Uri.TryCreate(query,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http"))return uri.AbsoluteUri;if(Regex.IsMatch(query,@"^(?:localhost(?::\d+)?|(?:[a-z0-9-]+\.)+[a-z]{2,})(?::\d+)?(?:/\S*)?$",RegexOptions.IgnoreCase))return "https://"+query;return null;}
 static bool MathLike(string text){return Regex.IsMatch(text,@"^(?:[=+\-]?[\d.(]|(?:sqrt|abs|round|floor|ceil|sin|cos|tan|log|ln|exp|min|max|pow)\s*\(|pi\b|e\b|\u03c0)",RegexOptions.IgnoreCase);}
 static SpotlightResult Calculate(string text,bool explicitMode){
  if(text.StartsWith("="))text=text.Substring(1).Trim();if(!explicitMode&&!MathLike(text))return null;
  try{string answer;if(!SpotlightCalculator.Convert(text,out answer))answer=SpotlightCalculator.Format(SpotlightCalculator.Evaluate(text));return Result(answer,text+"  \u00b7  Enter to copy","copy",answer);}
  catch(FormatException e){return explicitMode||Regex.IsMatch(text,@"[+*/^%()]|\s+(?:to|in)\s+")?Result("Check your calculation",e.Message,"hint",""):null;}
 }
 static SpotlightResult[] Apps(AppEntry[] apps,string text){return NordSpotlight.RankApps(apps,text).Select(app=>new SpotlightResult{Name=app.Name,Detail="Application",Kind="app",Value=app.Key,App=app}).ToArray();}
 static IEnumerable<SpotlightResult> Windows(SpotlightResult[] windows,string text){return windows.Where(w=>NordSpotlight.Score(w.Name+" "+w.Detail,text)>0).Take(7);}
 public static SpotlightResult[] Quick(string query,AppEntry[] apps,SpotlightResult[] windows){
  string text=query.Trim();if(text.Length==0)return Apps(apps,"");if(text=="~")return new[]{Shortcut("/home")};
  if(text.StartsWith("/")){
   int split=text.IndexOf(' ');string command=split<0?text:text.Substring(0,split);string arg=split<0?"":text.Substring(split+1).Trim();command=command.ToLowerInvariant();if(command=="/launch")command="/openall";
   if(command=="/help")return commands.Where(c=>c[0]!="/help"&&(arg.Length==0||c[0].IndexOf(arg,StringComparison.OrdinalIgnoreCase)>=0||c[1].IndexOf(arg,StringComparison.OrdinalIgnoreCase)>=0)).Select(c=>Command(c)).ToArray();
   if(command=="/apps")return Apps(apps,arg);
   if(command=="/settings")return settings.Where(s=>arg.Length==0||NordSpotlight.Score(s[0],arg)>0).Select(s=>Result(s[0],"Windows settings","uri","ms-settings:"+s[1])).ToArray();
   if(command=="/windows")return Windows(windows,arg).ToArray();
   if(command=="/calc")return new[]{arg.Length==0?Result("Calculator","Try 20% of 150, sqrt(144), or 5 km to mi","query","/calc "):Calculate(arg,true)};
   if(command=="/web")return new[]{arg.Length==0?Result("Search the web","Type a search after /web","query","/web "):Web(arg)};
   if(command=="/copy")return new[]{arg.Length==0?Result("Copy text","Type text after /copy","query","/copy "):Result(arg,"Enter to copy","copy",arg)};
   if(command=="/files"||command=="/folders")return arg.Length==0?new[]{Result(command=="/files"?"Find a file":"Find a folder","Search Desktop, Documents and Downloads","query",command+" ")}:new SpotlightResult[0];
   var exact=commands.FirstOrDefault(c=>c[0]==command);if(exact!=null)return new[]{Command(exact)};
   return commands.Where(c=>c[0].StartsWith(command,StringComparison.OrdinalIgnoreCase)).Select(c=>Command(c)).ToArray();
  }
  var result=new List<SpotlightResult>();var calculation=Calculate(text,false);if(calculation!=null){result.Add(calculation);return result.ToArray();}
  string address=WebAddress(text);if(address!=null){result.Add(Result(address,"Open website","uri",address));return result.ToArray();}
  result.AddRange(Apps(apps,text).Take(5));result.AddRange(Windows(windows,text).Take(2));result.Add(Web(text));return result.ToArray();
 }
 static SpotlightResult Command(string[] command){if(command[2].Length==0)return Shortcut(command[0]);return Result(command[0],command[1]+" \u00b7 "+command[2],"query",command[0]+" ");}
 public static bool FileQuery(string query,out string term,out bool folders){
  term=query.Trim();folders=false;if(term.StartsWith("/files ",StringComparison.OrdinalIgnoreCase))term=term.Substring(7).Trim();else if(term.StartsWith("/folders ",StringComparison.OrdinalIgnoreCase)){term=term.Substring(9).Trim();folders=true;}else if(term.StartsWith("/")||term.StartsWith("=")||WebAddress(term)!=null)return false;
  return term.Length>=2;
 }
 public static SpotlightResult[] MergeFiles(SpotlightResult[] quick,SpotlightResult[] files){return quick.Where(r=>r.Kind!="uri").Concat(files).Concat(quick.Where(r=>r.Kind=="uri")).Take(12).ToArray();}
}

static class SpotlightFiles {
 sealed class Entry {public string Path,Name;public bool Folder;}
 static readonly object gate=new object();static Entry[] entries;static DateTime built;static Task<Entry[]> building;static int revision;
 static readonly HashSet<string> skip=new HashSet<string>(new[]{"node_modules",".git",".svn",".cache","AppData","bin","obj"},StringComparer.OrdinalIgnoreCase);
 public static void Invalidate(){lock(gate){entries=null;building=null;revision++;}}
 static Entry[] Build(int generation){
  var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")};
  var result=Scan(roots,50000,TimeSpan.FromSeconds(2));lock(gate){if(generation==revision){entries=result;built=DateTime.UtcNow;}}return result;
 }
 internal static EntryProxy[] CheckScan(string root){return Scan(new[]{root},100,TimeSpan.FromSeconds(1)).Select(e=>new EntryProxy{Path=e.Path,Folder=e.Folder}).ToArray();}
 internal sealed class EntryProxy {public string Path;public bool Folder;}
 static Entry[] Scan(string[] roots,int limit,TimeSpan budget){
  var result=new List<Entry>();var queue=new Queue<Tuple<string,int>>(roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Select(r=>Tuple.Create(r,0)));var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var clock=System.Diagnostics.Stopwatch.StartNew();
  while(queue.Count>0&&result.Count<limit&&clock.Elapsed<budget){var current=queue.Dequeue();if(!seen.Add(current.Item1))continue;
   try{foreach(var path in Directory.EnumerateFileSystemEntries(current.Item1)){
    if(result.Count>=limit||clock.Elapsed>=budget)break;
    try{var attributes=File.GetAttributes(path);if((attributes&(FileAttributes.ReparsePoint|FileAttributes.Hidden|FileAttributes.System))!=0)continue;bool folder=(attributes&FileAttributes.Directory)!=0;var name=Path.GetFileName(path);if(folder&&skip.Contains(name))continue;
     result.Add(new Entry{Path=path,Name=name,Folder=folder});if(folder&&current.Item2<8)queue.Enqueue(Tuple.Create(path,current.Item2+1));
    }catch{}
   }}catch{}
  }return result.ToArray();
 }
 public static async Task<SpotlightResult[]> Search(string term,bool folders,CancellationToken token){
  // Direct local paths support ~ and environment variables without launching a command interpreter.
  var direct=await Task.Run(()=>{
   var path=Environment.ExpandEnvironmentVariables(term.Trim('"'));if(path=="~"||path.StartsWith("~\\")||path.StartsWith("~/"))path=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+path.Substring(1);
   if(path.StartsWith(@"\\")||!Path.IsPathRooted(path))return null;
   return Directory.Exists(path)||!folders&&File.Exists(path)?SpotlightSearch.Result(Path.GetFileName(path.TrimEnd('\\','/')),path,"path",path):null;
  }).ConfigureAwait(false);
  token.ThrowIfCancellationRequested();if(direct!=null)return new[]{direct};
  Task<Entry[]> task;lock(gate){if(entries!=null&&DateTime.UtcNow-built<TimeSpan.FromMinutes(2))task=Task.FromResult(entries);else{if(building==null||building.IsCompleted){int generation=revision;building=Task.Run(()=>Build(generation));}task=building;}}
  var data=await task.ConfigureAwait(false);token.ThrowIfCancellationRequested();
  return data.Where(e=>!folders||e.Folder).Select(e=>new{Entry=e,Score=NordSpotlight.Score(e.Name,term)}).Where(e=>e.Score>0).OrderByDescending(e=>e.Score).ThenBy(e=>e.Entry.Name.Length).ThenBy(e=>e.Entry.Name,StringComparer.OrdinalIgnoreCase).Take(5).Select(e=>SpotlightSearch.Result(e.Entry.Name,(e.Entry.Folder?"Folder \u00b7 ":"File \u00b7 ")+e.Entry.Path,"path",e.Entry.Path)).ToArray();
 }
}
