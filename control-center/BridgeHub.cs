using System;
using System.Collections.Generic;
using System.Linq;

// The subscription barrier is connection-local. Capture happens outside this
// gate: state samplers may publish while holding their own locks.
sealed class BridgeHub {
 sealed class Subscriber {public bool Initializing=true;public readonly List<BridgeFrame> Pending=new List<BridgeFrame>();public int Bytes;}
 readonly object gate=new object();readonly Dictionary<BridgeConnection,Subscriber> subscribers=new Dictionary<BridgeConnection,Subscriber>();readonly int limit;
 public BridgeHub(int limit=16*1024*1024){this.limit=limit;}
 internal int Count {get{lock(gate)return subscribers.Count;}}
 public void Remove(BridgeConnection connection){lock(gate)subscribers.Remove(connection);}
 public void Subscribe(BridgeConnection connection,Func<string[]> capture,string reply){
  bool existing;lock(gate){existing=subscribers.ContainsKey(connection);if(!existing)subscribers.Add(connection,new Subscriber());}
  if(existing){if(!connection.Enqueue(1,BridgeWire.Text(reply)))connection.Dispose();return;}
  try{var initial=capture().Select(text=>new BridgeFrame(1,BridgeWire.Text(text))).ToList();initial.Add(new BridgeFrame(1,BridgeWire.Text(reply)));bool failed=false;
   lock(gate){Subscriber subscriber;if(!subscribers.TryGetValue(connection,out subscriber))return;initial.AddRange(subscriber.Pending);subscriber.Pending.Clear();subscriber.Bytes=0;subscriber.Initializing=false;if(!connection.Enqueue(initial.ToArray())){subscribers.Remove(connection);failed=true;}}
   if(failed)connection.Dispose();
  }catch{connection.MarkFailure("initial state capture failed");Remove(connection);connection.Dispose();throw;}
 }
 public void Publish(string json){var frame=new BridgeFrame(1,BridgeWire.Text(json));var failed=new List<BridgeConnection>();lock(gate){foreach(var pair in subscribers){var subscriber=pair.Value;
   if(subscriber.Initializing){if((long)subscriber.Bytes+frame.Bytes.Length+10>limit){pair.Key.MarkFailure("initial state backlog limit");failed.Add(pair.Key);continue;}subscriber.Pending.Add(frame);subscriber.Bytes+=frame.Bytes.Length+10;}
   else if(!pair.Key.Enqueue(new[]{frame}))failed.Add(pair.Key);
  }foreach(var connection in failed)subscribers.Remove(connection);}
  foreach(var connection in failed)connection.Dispose();
 }
}
