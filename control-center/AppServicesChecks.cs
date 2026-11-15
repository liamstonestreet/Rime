using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

static class AppServicesChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){
  var events=new List<string>();var owner=new AppServices(new[]{
   new AppServiceStep("first",()=>events.Add("start1"),()=>events.Add("stop1")),
   new AppServiceStep("second",()=>events.Add("start2"),()=>{events.Add("stop2");throw new Exception("cleanup");})},s=>events.Add("logged"));
  owner.Start();owner.Start();owner.Dispose();owner.Dispose();
  Check(String.Join(",",events)=="start1,start2,stop2,logged,stop1","Lifetime order or cleanup isolation failed");
  try{owner.Start();throw new Exception("Restart accepted");}catch(ObjectDisposedException){}
  events.Clear();owner=new AppServices(new[]{
   new AppServiceStep("first",()=>events.Add("start1"),()=>events.Add("stop1")),
   new AppServiceStep("partial",()=>{events.Add("partial");throw new InvalidOperationException("startup");},()=>events.Add("undo")),
   new AppServiceStep("unused",()=>events.Add("unused"),()=>events.Add("unused-stop"))},s=>{});
  try{owner.Start();throw new Exception("Startup failure swallowed");}catch(InvalidOperationException){}
  owner.Dispose();Check(String.Join(",",events)=="start1,partial,undo,stop1","Partial startup did not unwind exactly once");
  var context=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(null);try{
   events.Clear();var pending=new Queue<Action>();Func<Action,Task> dispatch=action=>{var ready=new TaskCompletionSource<int>();pending.Enqueue(()=>{try{action();ready.SetResult(0);}catch(Exception e){ready.SetException(e);}});return ready.Task;};
   owner=new AppServices(new[]{new AppServiceStep("frame",()=>events.Add("frame"),()=>events.Add("stop-frame")),new AppServiceStep("panel",()=>events.Add("panel"),()=>events.Add("stop-panel"))},unused=>{});
   var startup=owner.StartAsync(dispatch);Check(events.Count==0&&pending.Count==1&&!startup.IsCompleted,"Startup must yield before dispatched stages");pending.Dequeue()();Check(String.Join(",",events)=="frame"&&pending.Count==1&&!startup.IsCompleted,"Frame paint opportunity must precede panel construction");pending.Dequeue()();startup.GetAwaiter().GetResult();owner.Dispose();Check(String.Join(",",events)=="frame,panel,stop-panel,stop-frame","Async startup must retain ordered reverse cleanup");
   events.Clear();owner=new AppServices(new[]{new AppServiceStep("frame",()=>events.Add("frame"),()=>events.Add("stop-frame")),new AppServiceStep("partial",()=>{events.Add("partial");throw new InvalidOperationException("fixture");},()=>events.Add("undo"))},unused=>{});startup=owner.StartAsync(dispatch);pending.Dequeue()();pending.Dequeue()();try{startup.GetAwaiter().GetResult();throw new Exception("Async startup failure swallowed");}catch(InvalidOperationException){}Check(String.Join(",",events)=="frame,partial,undo,stop-frame","Async partial failure must unwind both stages");
   events.Clear();owner=new AppServices(new[]{new AppServiceStep("frame",()=>events.Add("frame"),()=>events.Add("stop-frame")),new AppServiceStep("unused",()=>events.Add("unused"),()=>events.Add("unused-stop"))},unused=>{});startup=owner.StartAsync(dispatch);pending.Dequeue()();owner.Dispose();pending.Dequeue()();try{startup.GetAwaiter().GetResult();throw new Exception("Disposed startup continued");}catch(ObjectDisposedException){}Check(String.Join(",",events)=="frame,stop-frame","Shutdown must prevent queued stages from starting");
  }finally{SynchronizationContext.SetSynchronizationContext(context);}
  Console.WriteLine("PASS application lifetime: ordered/yielding startup, frame-before-panel dispatch, reverse cleanup, partial failure and queued-shutdown safety");
 }
}

static class AudioPeakSamplerChecks {
 sealed class Source:IAudioPeakSource {
  public readonly int Created=Thread.CurrentThread.ManagedThreadId;public int ReadThread,DisposeThread;public bool Fail;public ManualResetEvent Entered,Release;
  public float Read(){ReadThread=Thread.CurrentThread.ManagedThreadId;if(Entered!=null){Entered.Set();Release.WaitOne(3000);}if(Fail)throw new InvalidOperationException("disconnected");return .25f;}
  public void Dispose(){DisposeThread=Thread.CurrentThread.ManagedThreadId;}
 }
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Finish(AudioPeakSampler sampler){sampler.Dispose();Check(sampler.Stopped.Wait(3000),"Peak worker did not stop");sampler.Dispose();Check(sampler.Read().IsCanceled,"Disposed sampler accepted work");}
 public static void Run(){
  int count=0;Source first=null,second=null;var sampler=new AudioPeakSampler(()=>{count++;var source=new Source();if(count==1)first=source;else second=source;return source;});
  Check(count==0,"Peak source eagerly allocated");Check(sampler.Read().Result==.25f&&sampler.Read().Result==.25f&&count==1,"Peak source not reused");
  first.Fail=true;try{sampler.Read().GetAwaiter().GetResult();throw new Exception("Read failure swallowed");}catch(InvalidOperationException){}
  Check(sampler.Read().Result==.25f&&count==2,"Peak source did not recover");Finish(sampler);
  Check(first.Created==first.ReadThread&&first.Created==first.DisposeThread&&second.Created==second.DisposeThread,"Peak endpoint crossed ownership threads");
  using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)){
   Source blocked=null;sampler=new AudioPeakSampler(()=>blocked=new Source{Entered=entered,Release=release});var read=sampler.Read();Check(entered.WaitOne(3000),"Peak worker never entered");sampler.Dispose();release.Set();Check(sampler.Stopped.Wait(3000)&&read.IsCanceled&&blocked.DisposeThread==blocked.Created,"In-flight peak shutdown leaked or published stale data");
  }
  sampler=new AudioPeakSampler(()=>{throw new Exception("Unused source created");});Finish(sampler);
  Console.WriteLine("PASS peak sampler: lazy allocation, thread ownership, recovery and in-flight shutdown");
 }
}
