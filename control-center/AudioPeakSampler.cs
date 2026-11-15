using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;

interface IAudioPeakSource:IDisposable {float Read();}
sealed class WindowsAudioPeakSource:IAudioPeakSource {
 readonly MMDeviceEnumerator enumerator;readonly MMDevice device;
 public WindowsAudioPeakSource(){enumerator=new MMDeviceEnumerator();try{device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);}catch{enumerator.Dispose();throw;}}
 public float Read(){if(device.State!=DeviceState.Active)throw new InvalidOperationException("Audio device disconnected");return device.AudioMeterInformation.MasterPeakValue;}
 public void Dispose(){try{device.Dispose();}finally{enumerator.Dispose();}}
}
// No cross-thread retained COM endpoint: creation, sampling, error recovery and
// disposal all occur on this worker. It blocks without polling between requests.
sealed class AudioPeakSampler:IDisposable {
 readonly object gate=new object();readonly BlockingCollection<TaskCompletionSource<float>> requests=new BlockingCollection<TaskCompletionSource<float>>(1);readonly Func<IAudioPeakSource> create;
 Thread worker;volatile bool disposed;readonly TaskCompletionSource<bool> stopped=new TaskCompletionSource<bool>();
 internal Task Stopped {get{return stopped.Task;}}
 public AudioPeakSampler():this(()=>new WindowsAudioPeakSource()){}
 internal AudioPeakSampler(Func<IAudioPeakSource> create){this.create=create;}
 public Task<float> Read(){var result=new TaskCompletionSource<float>();lock(gate){if(disposed){result.SetCanceled();return result.Task;}if(worker==null){worker=new Thread(Run){IsBackground=true,Name="Nord audio peak"};worker.SetApartmentState(ApartmentState.MTA);worker.Start();}if(!requests.TryAdd(result))result.TrySetCanceled();}return result.Task;}
 void Run(){IAudioPeakSource source=null;try{foreach(var request in requests.GetConsumingEnumerable()){if(disposed){request.TrySetCanceled();continue;}try{if(source==null)source=create();float value=source.Read();if(disposed)request.TrySetCanceled();else request.TrySetResult(value);}catch(Exception ex){if(source!=null){try{source.Dispose();}catch{}source=null;}request.TrySetException(ex);}}}
  finally{if(source!=null)try{source.Dispose();}catch{}requests.Dispose();stopped.TrySetResult(true);}
 }
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;requests.CompleteAdding();if(worker==null){requests.Dispose();stopped.TrySetResult(true);}}}
}
