using System;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

// Each operation acquires and disposes its own endpoints on the calling thread.
// No retained callbacks/COM wrappers survive a device refresh, suspend or worker return.
sealed class WindowsAudioEndpointService:IAudioEndpointService,IDefaultAudioEndpointService {
 static readonly AudioDeviceChoice[] Empty=new AudioDeviceChoice[0];
 static AudioSnapshot Snapshot(MMDevice device){var endpoint=device.AudioEndpointVolume;bool stereo=endpoint.Channels.Count>=2;return new AudioSnapshot(device.ID,null,endpoint.MasterVolumeLevelScalar,stereo?StereoBalance.Read(endpoint.Channels[0].VolumeLevelScalar,endpoint.Channels[1].VolumeLevelScalar):0,endpoint.Mute,stereo,new[]{new AudioDeviceChoice(device.ID,device.FriendlyName)},Empty);}
 public AudioSnapshot ReadDevices(){using(var e=new MMDeviceEnumerator()){
  Func<DataFlow,AudioDeviceChoice[]> list=flow=>e.EnumerateAudioEndPoints(flow,DeviceState.Active).Select(d=>{using(d)return new AudioDeviceChoice(d.ID,d.FriendlyName);}).ToArray();
  var outputs=list(DataFlow.Render);var inputs=list(DataFlow.Capture);AudioSnapshot output=null;string input=null;
  try{using(var device=e.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia))output=Snapshot(device);}catch(COMException){}
  try{using(var device=e.GetDefaultAudioEndpoint(DataFlow.Capture,Role.Multimedia))input=device.ID;}catch(COMException){}
  return new AudioSnapshot(output==null?null:output.OutputId,input,output==null?0:output.Volume,output==null?0:output.Balance,output!=null&&output.Muted,output!=null&&output.Stereo,outputs,inputs);
 }}
 AudioSnapshot WithOutput(string id,Action<MMDevice> action){using(var e=new MMDeviceEnumerator())using(var d=e.GetDevice(id)){action(d);return Snapshot(d);}}
 AudioSnapshot WithDefault(Action<MMDevice> action){using(var e=new MMDeviceEnumerator())using(var d=e.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia)){action(d);return Snapshot(d);}}
 public AudioSnapshot ReadDefaultOutput(){return WithDefault(d=>{});}
 public AudioSnapshot SetDefaultVolume(float volume){return WithDefault(d=>d.AudioEndpointVolume.MasterVolumeLevelScalar=Math.Max(0,Math.Min(1,volume)));}
 public AudioSnapshot SetDefaultMute(bool muted){return WithDefault(d=>d.AudioEndpointVolume.Mute=muted);}
 static void Balance(MMDevice device,float pan){var endpoint=device.AudioEndpointVolume;if(endpoint.Channels.Count>=2)StereoBalance.Apply(pan,()=>endpoint.MasterVolumeLevelScalar,(i,v)=>endpoint.Channels[i].VolumeLevelScalar=v,i=>endpoint.Channels[i].VolumeLevelScalar);}
 public AudioSnapshot ReadOutput(string id){return WithOutput(id,d=>{});}
 public AudioSnapshot SetVolume(string id,float volume,float balance){return WithOutput(id,d=>{d.AudioEndpointVolume.MasterVolumeLevelScalar=Math.Max(0,Math.Min(1,volume));Balance(d,balance);});}
 public AudioSnapshot SetBalance(string id,float balance){return WithOutput(id,d=>Balance(d,balance));}
 public AudioSnapshot ToggleMute(string id){return WithOutput(id,d=>d.AudioEndpointVolume.Mute=!d.AudioEndpointVolume.Mute);}
 public void SelectDefault(string id){var policy=(IPolicyConfig)new PolicyConfig();try{for(int role=0;role<3;role++)Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id,role));}finally{Marshal.ReleaseComObject(policy);}}
}

[ComImport,Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")] class PolicyConfig {}
[ComImport,Guid("f8679f50-850a-41cf-9c72-430f290290c8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IPolicyConfig {
 void GetMixFormat();void GetDeviceFormat();void ResetDeviceFormat();void SetDeviceFormat();void GetProcessingPeriod();void SetProcessingPeriod();void GetShareMode();void SetShareMode();void GetPropertyValue();void SetPropertyValue();
 [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)]string id,int role);void SetEndpointVisibility();
}

