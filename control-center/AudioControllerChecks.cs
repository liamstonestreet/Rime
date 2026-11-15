using System;
using System.Threading.Tasks;

static class AudioControllerChecks {
 internal sealed class Endpoint:IAudioEndpointService,IDefaultAudioEndpointService {
  public float Master=.61f;public float[] Channels={.61f,0};public bool Muted,Stereo=true;public string Id="speakers";public int Writes,Reads,MuteWrites,DefaultVolumeWrites;public bool Fail;
  AudioSnapshot State(){return new AudioSnapshot(Id,"mic",Master,Stereo?StereoBalance.Read(Channels[0],Channels[1]):0,Muted,Stereo,new[]{new AudioDeviceChoice(Id,"Speakers")},new[]{new AudioDeviceChoice("mic","Microphone")});}
  public AudioSnapshot ReadDefaultOutput(){Reads++;if(Fail)throw new InvalidOperationException("Device removed");return State();}
  public AudioSnapshot SetDefaultVolume(float volume){DefaultVolumeWrites++;Master=volume;return State();}
  public AudioSnapshot SetDefaultMute(bool muted){MuteWrites++;Muted=muted;return State();}
  public AudioSnapshot ReadDevices(){return State();}public AudioSnapshot ReadOutput(string id){if(Fail)throw new InvalidOperationException("Device removed");return State();}
  public AudioSnapshot SetBalance(string id,float pan){if(Stereo)StereoBalance.Apply(pan,()=>Master,(i,v)=>{Writes++;Channels[i]=v;Master=Math.Max(Channels[0],Channels[1]);},i=>Channels[i]);return State();}
  public AudioSnapshot SetVolume(string id,float value,float balance){Master=value;return SetBalance(id,balance);}
  public AudioSnapshot ToggleMute(string id){Muted=!Muted;return State();}public void SelectDefault(string id){Id=id;}
 }
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){var endpoint=new Endpoint();var audio=new AudioController(endpoint);audio.AcceptDiscovery(endpoint.ReadDevices(),0,false);
  audio.SetBalance(0);Check(endpoint.Writes==2&&Math.Abs(audio.Current.Volume-.61f)<.0001&&audio.Current.Balance==0,"Controller reset lost loudness or skipped writes");
  audio.SetBalance(0);Check(endpoint.Writes==4,"Centered reset must still write");
  long ticket=audio.Revision;var stale=endpoint.ReadDevices();audio.SetBalance(1);audio.AcceptDiscovery(stale,ticket,false);Check(audio.Current.Balance==1,"Stale discovery overwrote pan edit");
  ticket=audio.Revision;stale=endpoint.ReadDevices();audio.ToggleMute();audio.AcceptDiscovery(stale,ticket,false);Check(audio.Current.Muted,"Stale discovery overwrote mute");
  audio.SetVolume(.3f,-1);Check(Math.Abs(audio.Current.Volume-.3f)<.0001&&audio.Current.Balance==-1,"Volume command lost balance");
  audio.SetVolume(0,0);Check(audio.Current.Volume==0&&audio.Current.Balance==0,"Zero volume reset failed");
  endpoint.Master=.4f;audio.RefreshLevels();Check(audio.Current.Volume==.4f,"External levels not refreshed");
  endpoint.Fail=true;try{audio.RefreshLevels();throw new Exception("Missing endpoint error swallowed");}catch(InvalidOperationException){}Check(audio.Current.Volume==.4f,"Failed read damaged last state");endpoint.Fail=false;
  endpoint.Stereo=false;audio.AcceptDiscovery(endpoint.ReadDevices(),audio.Revision,false);int writes=endpoint.Writes;audio.SetBalance(0);Check(endpoint.Writes==writes,"Mono device received channel writes");
  audio.SelectDefaultAsync("headphones").GetAwaiter().GetResult();audio.AcceptDiscovery(endpoint.ReadDevices(),audio.Revision,false);Check(audio.Current.OutputId=="headphones","Device selection not adopted");
  audio.AcceptDiscovery(new AudioSnapshot(null,null,0,0,false,false,new AudioDeviceChoice[0],new AudioDeviceChoice[0]),audio.Revision,false);audio.SetBalance(0);audio.ToggleMute();Check(!audio.HasOutput,"Missing endpoint not handled");
  var barEndpoint=new Endpoint();var time=new DateTime(2026,11,3);var bar=new BarAudioCommands(barEndpoint,()=>time);
  var status=bar.Receive("audio-status");Check(status.Contains("Speakers")&&barEndpoint.Reads==1,"Bar lost output name");bar.Receive("audio-status");Check(barEndpoint.Reads==1,"Bar status cache bypassed");
  bar.Receive("audio-volume:25");Check(barEndpoint.Master==.25f&&barEndpoint.DefaultVolumeWrites==1,"Bar volume conversion failed");
  bar.Receive("audio-mute:1");bar.Receive("audio-mute:1");Check(barEndpoint.Muted&&barEndpoint.MuteWrites==2,"Bar mute must set, not toggle");bar.Receive("audio-mute:0");Check(!barEndpoint.Muted,"Bar unmute failed");
  foreach(var invalid in new[]{"NaN","Infinity","-1","101","abc"})Check(bar.Receive("audio-volume:"+invalid).Contains("audioError"),"Invalid bar volume accepted");
  Check(barEndpoint.DefaultVolumeWrites==1&&bar.Receive("unknown")=="{}","Invalid command reached endpoint");
  time=time.AddSeconds(1);barEndpoint.Fail=true;Check(bar.Receive("audio-status")=="{\"audio\":null}","Bar removal fallback changed");barEndpoint.Fail=false;time=time.AddSeconds(1);Check(bar.Receive("audio-status").Contains("Speakers"),"Bar did not recover");
  Console.WriteLine("PASS: shared bar audio adapter commands, status cache, explicit mute/unmute, validation and missing-device recovery. Fake endpoints only.");
  Console.WriteLine("PASS: extracted audio controller reset/readback, stale discovery, mute, volume, zero/mono/device removal and selection. Fake endpoints only; no live audio writes.");
 }
}
