using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

static class BridgeOrderingChecks {
 sealed class Receiver:MemoryStream {
  readonly object gate=new object();readonly List<string> messages=new List<string>();
  public override void Write(byte[] bytes,int offset,int count){lock(gate)messages.Add(Encoding.UTF8.GetString(bytes,offset,count));}
  public string[] Messages {get{lock(gate)return messages.ToArray();}}
 }
 sealed class Broken:MemoryStream {public override void WriteByte(byte value){throw new IOException("Disconnected fixture");}}
 sealed class Slow:MemoryStream {
  public readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim();
  public override void Write(byte[] bytes,int offset,int count){Entered.Set();if(!Release.Wait(5000))throw new IOException("Slow fixture timed out");}
  protected override void Dispose(bool disposing){Release.Set();base.Dispose(disposing);}
 }
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Wait(Func<bool> done,string message){Check(SpinWait.SpinUntil(done,3000),message);}
 public static void Run(){var hub=new BridgeHub();using(var receiver=new Receiver())using(var broken=new Broken())using(var healthy=new BridgeConnection(receiver,hub.Remove))using(var failed=new BridgeConnection(broken,hub.Remove)){
  hub.Subscribe(healthy,()=>new[]{"font","appearance","widgets"},"ack");hub.Subscribe(failed,()=>new[]{"snapshot"},"ack");for(int i=0;i<40;i++)hub.Publish("update-"+i);
  Wait(()=>receiver.Messages.Length==44,"Healthy subscriber did not receive all ordered frames");Check(receiver.Messages.SequenceEqual(new[]{"font","appearance","widgets","ack"}.Concat(Enumerable.Range(0,40).Select(i=>"update-"+i))),"Broadcast order changed");Wait(()=>hub.Count==1,"Failed subscriber retained");
 }
  hub=new BridgeHub();using(var receiver=new Receiver())using(var connection=new BridgeConnection(receiver,hub.Remove)){
   hub.Subscribe(connection,()=>{hub.Publish("during-capture");Check(receiver.Messages.Length==0,"Live update escaped initial-state barrier");return new[]{"font","appearance","all-channels"};},"ack");hub.Publish("after-join");
   Wait(()=>receiver.Messages.Length==6,"Initial-state barrier did not drain");Check(receiver.Messages.SequenceEqual(new[]{"font","appearance","all-channels","ack","during-capture","after-join"}),"Snapshot/live ordering violated");
   hub.Subscribe(connection,()=>{throw new Exception("Repeated subscription recaptured state");},"again");Wait(()=>receiver.Messages.Length==7,"Repeated subscribe reply missing");
  }
  hub=new BridgeHub();using(var slow=new Slow())using(var receiver=new Receiver())using(var blocked=new BridgeConnection(slow,hub.Remove,128))using(var healthy=new BridgeConnection(receiver,hub.Remove)){
   hub.Subscribe(blocked,()=>new[]{"initial"},"ack");Check(slow.Entered.Wait(3000),"Slow fixture did not block");hub.Subscribe(healthy,()=>new[]{"healthy"},"ack");hub.Publish(new string('x',200));Wait(()=>receiver.Messages.Length==3,"Slow connection blocked healthy delivery");Wait(()=>hub.Count==1,"Overflowed subscriber retained");Check(blocked.Failure=="outbound backlog limit","Overflow reason missing");
  }
  hub=new BridgeHub(32);using(var receiver=new Receiver())using(var connection=new BridgeConnection(receiver,hub.Remove)){hub.Subscribe(connection,()=>{hub.Publish(new string('x',40));return new[]{"snapshot"};},"ack");Check(hub.Count==0&&receiver.Messages.Length==0,"Initial backlog overflow escaped barrier");}
  hub=new BridgeHub();using(var receiver=new Receiver())using(var connection=new BridgeConnection(receiver,hub.Remove)){bool threw=false;try{hub.Subscribe(connection,()=>{throw new IOException("Fixture snapshot failure");},"ack");}catch(IOException){threw=true;}Check(threw&&hub.Count==0,"Failed snapshot left subscriber registered");}
  hub=new BridgeHub();using(var receiver=new Receiver())using(var connection=new BridgeConnection(receiver,hub.Remove))using(var entered=new ManualResetEventSlim())using(var capturing=new ManualResetEventSlim()){
   var stateGate=new object();var producer=Task.Run(()=>{lock(stateGate){entered.Set();if(!capturing.Wait(3000))throw new Exception("Capture did not start");hub.Publish("state-update");}});Check(entered.Wait(3000),"State producer did not start");
   var subscription=Task.Run(()=>hub.Subscribe(connection,()=>{capturing.Set();lock(stateGate)return new[]{"state-snapshot"};},"ack"));Check(Task.WaitAll(new[]{producer,subscription},3000),"Snapshot capture deadlocked a publishing state owner");Wait(()=>receiver.Messages.Length==3,"Concurrent state subscription did not drain");Check(receiver.Messages.SequenceEqual(new[]{"state-snapshot","ack","state-update"}),"Concurrent state barrier order changed");
  }
  Console.WriteLine("PASS: isolated bridge ordering, initial snapshot barrier, repeated subscribe, slow-client isolation, bounded backlogs and failure cleanup. Memory streams only; no live subscribers touched.");
 }
}
