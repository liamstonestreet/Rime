using System;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

// Compatibility facade for existing bridge callers; endpoint ownership lives in the adapter.
static class BarAudio {
 static readonly BarAudioCommands commands=new BarAudioCommands(new WindowsAudioEndpointService(),()=>DateTime.UtcNow);
 public static string Receive(string command){return commands.Receive(command);}
}
sealed class BarAudioCommands {
 readonly object gate=new object();readonly IDefaultAudioEndpointService endpoints;readonly Func<DateTime> now;
 DateTime sampled;string cached="{\"audio\":null}";
 public BarAudioCommands(IDefaultAudioEndpointService endpoints,Func<DateTime> now){this.endpoints=endpoints;this.now=now;}
 public string Receive(string command){lock(gate){
  if(command=="audio-status"&&now()-sampled<TimeSpan.FromMilliseconds(500))return cached;
  float value=0;bool volume=command.StartsWith("audio-volume:");
  if(volume&&(!float.TryParse(command.Substring(13),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||value<0||value>100))return "{\"audioError\":\"Invalid volume\"}";
  if(!volume&&command!="audio-status"&&command!="audio-mute:1"&&command!="audio-mute:0")return "{}";
  try{
   var state=volume?endpoints.SetDefaultVolume(value/100):command=="audio-status"?endpoints.ReadDefaultOutput():endpoints.SetDefaultMute(command=="audio-mute:1");
   var choice=state.Outputs.FirstOrDefault(d=>d.ID==state.OutputId);
   cached=new JavaScriptSerializer().Serialize(new {audio=new {name=choice==null?"":choice.FriendlyName,volume=Math.Round(state.Volume*100),isMuted=state.Muted}});
  }catch{cached="{\"audio\":null}";}
  sampled=now();return cached;
 }}
}
