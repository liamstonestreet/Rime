using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

sealed class BridgeFrame {
 public readonly int Opcode;public readonly byte[] Bytes;public readonly TaskCompletionSource<bool> Sent;
 public BridgeFrame(int opcode,byte[] bytes,TaskCompletionSource<bool> sent=null){Opcode=opcode;Bytes=bytes;Sent=sent;}
}
// One ordered writer per connection. Enqueue never waits for network I/O. A slow
// or failed client is closed independently; queued payloads have a byte budget.
sealed class BridgeConnection:IDisposable {
 readonly Stream stream;readonly Action<BridgeConnection> closed;readonly object gate=new object();readonly Queue<BridgeFrame> queue=new Queue<BridgeFrame>();readonly int limit;
 int buffered;bool writing,disposed;
 public BridgeConnection(Stream stream,Action<BridgeConnection> closed,int limit=16*1024*1024){this.stream=stream;this.closed=closed;this.limit=limit;}
 public string Failure {get;private set;}
 internal void MarkFailure(string reason){lock(gate)if(Failure==null)Failure=reason;}
 public bool Enqueue(int opcode,byte[] bytes){return Enqueue(new[]{new BridgeFrame(opcode,bytes)});}
 public bool Enqueue(BridgeFrame[] frames){lock(gate){if(disposed)return false;long added=0;foreach(var frame in frames)added+=frame.Bytes.Length+10;if(added+buffered>limit){Failure="outbound backlog limit";return false;}
  foreach(var frame in frames){queue.Enqueue(frame);buffered+=frame.Bytes.Length+10;}if(!writing&&queue.Count>0){writing=true;ThreadPool.QueueUserWorkItem(delegate{Drain();});}return true;
 }}
 public void CloseHandshake(){var sent=new TaskCompletionSource<bool>();if(Enqueue(new[]{new BridgeFrame(8,new byte[0],sent)}))sent.Task.Wait(3000);}
 void Drain(){while(true){BridgeFrame frame;lock(gate){if(disposed||queue.Count==0){writing=false;return;}frame=queue.Dequeue();}
  try{BridgeWire.Write(stream,frame.Opcode,frame.Bytes);if(frame.Sent!=null)frame.Sent.TrySetResult(true);}
  catch{MarkFailure("socket write failed");if(frame.Sent!=null)frame.Sent.TrySetResult(false);Dispose();return;}
  lock(gate)if(!disposed)buffered-=frame.Bytes.Length+10;
 }}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;foreach(var frame in queue)if(frame.Sent!=null)frame.Sent.TrySetResult(false);queue.Clear();buffered=0;}
  try{stream.Dispose();}catch{}if(closed!=null)closed(this);
 }
}

// RFC 6455 framing retained behind transport; incoming commands remain capped at
// 512 KiB. All writes (including pong/close) use the connection's ordered writer.
static class BridgeWire {
 public static byte[] Read(Stream stream,int count){var bytes=new byte[count];int offset=0;while(offset<count){int read=stream.Read(bytes,offset,count-offset);if(read==0)throw new EndOfStreamException();offset+=read;}return bytes;}
 public static void Write(Stream stream,int opcode,byte[] bytes){stream.WriteByte((byte)(0x80|opcode));if(bytes.Length<126)stream.WriteByte((byte)bytes.Length);else if(bytes.Length<65536){stream.WriteByte(126);stream.WriteByte((byte)(bytes.Length>>8));stream.WriteByte((byte)bytes.Length);}else{stream.WriteByte(127);for(int shift=56;shift>=0;shift-=8)stream.WriteByte((byte)((long)bytes.Length>>shift));}stream.Write(bytes,0,bytes.Length);}
 public static BridgeFrame ReadFrame(Stream stream){var prefix=Read(stream,2);int opcode=prefix[0]&15;if((prefix[0]&0x80)==0||(prefix[0]&0x70)!=0||(prefix[1]&0x80)==0)throw new IOException("Invalid WebSocket frame");
  long size=prefix[1]&127;if(size==126){var extended=Read(stream,2);size=(extended[0]<<8)|extended[1];}else if(size==127){var extended=Read(stream,8);size=0;foreach(byte b in extended){if(size>524288)throw new IOException("Oversized WebSocket frame");size=(size<<8)|b;}}
  if(size>524288||((opcode&8)!=0&&size>125))throw new IOException("Oversized WebSocket frame");var mask=Read(stream,4);var payload=Read(stream,(int)size);for(int i=0;i<payload.Length;i++)payload[i]^=mask[i%4];return new BridgeFrame(opcode,payload);
 }
 public static byte[] Text(string value){return Encoding.UTF8.GetBytes(value??"{}");}
}
