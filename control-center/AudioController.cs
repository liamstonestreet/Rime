using System;
using System.Threading.Tasks;

sealed class AudioDeviceChoice {
 public string ID {get;private set;} public string FriendlyName {get;private set;}
 public AudioDeviceChoice(string id,string name){ID=id;FriendlyName=name;}
 public override string ToString(){return FriendlyName;}
}

// Detached values only: no endpoint or COM object crosses the worker/UI boundary.
sealed class AudioSnapshot {
 public readonly string OutputId,InputId;public readonly float Volume,Balance;
 public readonly bool Muted,Stereo;public readonly AudioDeviceChoice[] Outputs,Inputs;
 public AudioSnapshot(string output,string input,float volume,float balance,bool muted,bool stereo,AudioDeviceChoice[] outputs,AudioDeviceChoice[] inputs){OutputId=output;InputId=input;Volume=volume;Balance=balance;Muted=muted;Stereo=stereo;Outputs=outputs;Inputs=inputs;}
}
interface IAudioEndpointService {
 AudioSnapshot ReadDevices();
 AudioSnapshot ReadOutput(string id);
 AudioSnapshot SetVolume(string id,float volume,float balance);
 AudioSnapshot SetBalance(string id,float balance);
 AudioSnapshot ToggleMute(string id);
 void SelectDefault(string id);
}
// Bar commands target whichever endpoint is default at execution time. A single adapter
// operation resolves and writes it, avoiding a discovery/write race or full device scan.
interface IDefaultAudioEndpointService {
 AudioSnapshot ReadDefaultOutput();
 AudioSnapshot SetDefaultVolume(float volume);
 AudioSnapshot SetDefaultMute(bool muted);
}

// Owned by the CC window. State and commands are used on its dispatcher; only detached
// device discovery runs on a worker. Revision tickets reject reads overtaken by edits.
sealed class AudioController {
 readonly IAudioEndpointService endpoints;long revision;
 public AudioSnapshot Current {get;private set;}
 public long Revision {get{return revision;}}
 public bool HasOutput {get{return Current!=null&&Current.OutputId!=null;}}
 public AudioController(IAudioEndpointService endpoints){this.endpoints=endpoints;}
 public Task<AudioSnapshot> DiscoverAsync(){return Task.Run(()=>endpoints.ReadDevices());}
 public bool AcceptDiscovery(AudioSnapshot result,long started,bool interacting){
  bool preserve=HasOutput&&Current.OutputId==result.OutputId&&(started!=revision||interacting);
  Current=preserve?new AudioSnapshot(result.OutputId,result.InputId,Current.Volume,Current.Balance,Current.Muted,result.Stereo,result.Outputs,result.Inputs):result;
  return !preserve;
 }
 void Adopt(AudioSnapshot state){Current=new AudioSnapshot(state.OutputId,Current.InputId,state.Volume,state.Balance,state.Muted,state.Stereo,Current.Outputs,Current.Inputs);}
 public void RefreshLevels(){if(HasOutput)Adopt(endpoints.ReadOutput(Current.OutputId));}
 public void SetVolume(float value,float balance){if(!HasOutput)return;revision++;Adopt(endpoints.SetVolume(Current.OutputId,value,balance));}
 public void SetBalance(float value){if(!HasOutput||!Current.Stereo)return;revision++;Adopt(endpoints.SetBalance(Current.OutputId,value));}
 public void ToggleMute(){if(!HasOutput)return;revision++;Adopt(endpoints.ToggleMute(Current.OutputId));}
 public Task SelectDefaultAsync(string id){revision++;return Task.Run(()=>endpoints.SelectDefault(id));}
}
