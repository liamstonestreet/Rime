using System;
using System.IO;
using System.Threading;

// Compatibility facade; transport, state and dispatch have separate owners.
sealed class MediaBridge:IDisposable {
 static readonly BridgeHub hub=new BridgeHub();
 internal static readonly MediaMetadataStore Metadata=new MediaMetadataStore(()=>DateTime.UtcNow);
 static readonly BridgeCommandRouter router=new BridgeCommandRouter(BridgeApplicationCommands.Execute,WallpaperSwitcher.Log);
 readonly BarBridgeServer server;
 public static Action<string> Hover,Pin,OpenFocus,OpenNotifications;static long barPing;
 internal static void Heartbeat(){Interlocked.Exchange(ref barPing,DateTime.UtcNow.Ticks);}
 public static bool BarAlive {get{return DateTime.UtcNow.Ticks-Interlocked.Read(ref barPing)<TimeSpan.FromSeconds(25).Ticks;}}
 public static string Current(string title){return Metadata.Current(title);}
 public static MediaLinks Links(string title){return Metadata.Links(title);}
 public static bool? ShuffleState(string title){return Metadata.Shuffle(title);}
 public static bool Shuffle(string title,bool value){var url=Metadata.Current(title);if(url==null||!Metadata.Shuffle(title).HasValue)return false;Broadcast(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{mediaControl=new{action="shuffle",url,value}}));return true;}
 public static string[] Context(string title){return Metadata.Context(title);}
 public static string Receive(string payload){return router.Receive(payload);}
 public static void Broadcast(string json){hub.Publish(json);}
 public MediaBridge(Func<string,string> callback){server=new BarBridgeServer(hub,callback,BridgeApplicationCommands.Initial,request=>BridgeTokens.Matches(request,BridgeTokens.Center),WallpaperSwitcher.Log);}
 public void Start(){server.Start();}
 public void Dispose(){server.Dispose();}
}

// Icons are resolved on bridge workers, once per HWND/process pair, never on the UI thread.
static class TaskIcons {
 sealed class CachedIcon {public string Png;public DateTime Loaded;}
 static readonly System.Collections.Generic.Dictionary<string,CachedIcon> cache=new System.Collections.Generic.Dictionary<string,CachedIcon>();
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
 public static string Get(string value){long id;if(!long.TryParse(value,out id))return "{}";uint pid;var hwnd=new IntPtr(id);GetWindowThreadProcessId(hwnd,out pid);string resource=AppShortcuts.WindowIcon(hwnd);string key=value+":"+pid+":"+resource;string png;
 lock(cache){CachedIcon cached;if(cache.TryGetValue(key,out cached)&&DateTime.UtcNow-cached.Loaded>TimeSpan.FromSeconds(2))return Reply(value,cached.Png);}
 png="";try{
 System.Drawing.Icon icon=resource==null?null:System.Drawing.Icon.ExtractAssociatedIcon(resource);
 if(icon==null)foreach(int size in new[]{2,0,1}){IntPtr h;SendMessageTimeout(hwnd,0x7f,new IntPtr(size),IntPtr.Zero,2,40,out h);if(h!=IntPtr.Zero&&h.ToInt64()!=1){icon=(System.Drawing.Icon)System.Drawing.Icon.FromHandle(h).Clone();break;}}
 if(icon==null){using(var process=System.Diagnostics.Process.GetProcessById((int)pid))icon=System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule.FileName);}
 if(icon!=null)using(icon)using(var bitmap=icon.ToBitmap())using(var stream=new MemoryStream()){bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);png="data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}
 }catch{}
 lock(cache){if(cache.Count>256)cache.Clear();if(!String.IsNullOrEmpty(png))cache[key]=new CachedIcon{Png=png,Loaded=DateTime.UtcNow};}return Reply(value,png);
 }
 static string Reply(string handle,string png){return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {taskIcon=handle,icon=png});}
}
