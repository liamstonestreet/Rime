using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

// One sampler for every monitor. Subscribers get a complete initial state, then only changes.
// Polling remains a fallback for Windows/device changes and edits made outside the control center.
static class BarState {
 static readonly object gate=new object();
 static readonly Dictionary<string,string> cache=new Dictionary<string,string>();
 static volatile bool stopping;static Timer timer;static DateTime sampled,slowSampled;
 static bool audioEnabled=true,vpnEnabled=true;
 static DateTime widgetStamp;static long widgetLength=-1;
 static readonly string[] order={"widgets","focus-status","audio-status","workspace-pins","workspace-rules:status","vpn-status"};
 public static void Start(){if(timer!=null)return;stopping=false;timer=new Timer(delegate{if(!stopping&&MediaBridge.BarAlive)Refresh(true);},null,1000,1000);}
 public static void Stop(){stopping=true;var old=Interlocked.Exchange(ref timer,null);if(old!=null)old.Dispose();}
 internal static bool Changed(Dictionary<string,string> values,string key,string value){string old;if(values.TryGetValue(key,out old)&&old==value)return false;values[key]=value;return true;}
 static void Refresh(bool publish,bool wait=false){
  if(stopping&&!wait)return;
  if(wait)Monitor.Enter(gate);else if(!Monitor.TryEnter(gate))return;
  try{
   var now=DateTime.UtcNow;if(now-sampled<TimeSpan.FromMilliseconds(750))return;sampled=now;
   bool slow=now-slowSampled>=TimeSpan.FromSeconds(5);if(slow)slowSampled=now;
   foreach(var command in order){
    if(stopping&&!wait)break;
    if(!slow&&Array.IndexOf(order,command)>=3)continue;
    if(command=="audio-status"&&!audioEnabled||command=="vpn-status"&&!vpnEnabled)continue;
    try{
     string value;
     if(command=="widgets"){
      var file=new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rice-widgets.json"));
      if(cache.ContainsKey(command)&&file.Exists&&file.LastWriteTimeUtc==widgetStamp&&file.Length==widgetLength)continue;
      value=file.Exists?File.ReadAllText(file.FullName):new RiceSettings().Json();
      // Keep the last good layout while an external editor is halfway through writing JSON.
      new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(value);
      var widgets=RiceSettings.Parse(value);audioEnabled=widgets.Enabled["volume"];vpnEnabled=widgets.Enabled["vpn"];
      widgetStamp=file.Exists?file.LastWriteTimeUtc:DateTime.MinValue;widgetLength=file.Exists?file.Length:-1;
     }else value=MediaBridge.Receive(command);
     if(Changed(cache,command,value)&&publish&&!stopping)MediaBridge.Broadcast(value);
    }catch{} // One unavailable device must not prevent the other channels from updating.
   }
  }finally{Monitor.Exit(gate);}
 }
 public static string[] Initial(){
  Refresh(true,true);
  lock(gate)return order.Where(cache.ContainsKey).Select(key=>cache[key]).ToArray();
 }
}
