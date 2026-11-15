using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

// Listener/handshake/read lifetime only; application commands and state ownership
// are supplied by the composition layer. No tokens or payloads are logged.
sealed class BarBridgeServer:IDisposable {
 readonly Func<string,string> dispatch;readonly Func<string[]> initial;readonly Func<string,bool> authenticate;readonly BridgeHub hub;readonly Action<string> log;readonly int port;
 readonly object gate=new object();readonly HashSet<TcpClient> clients=new HashSet<TcpClient>();TcpListener listener;bool disposed;
 public BarBridgeServer(BridgeHub hub,Func<string,string> dispatch,Func<string[]> initial,Func<string,bool> authenticate,Action<string> log,int port=6138){this.hub=hub;this.dispatch=dispatch;this.initial=initial;this.authenticate=authenticate;this.log=log;this.port=port;}
 internal int Port {get{lock(gate)return listener==null?0:((IPEndPoint)listener.LocalEndpoint).Port;}}
 internal int ClientCount {get{lock(gate)return clients.Count;}}
 public void Start(){lock(gate){if(disposed)throw new ObjectDisposedException("BarBridgeServer");if(listener!=null)return;var next=new TcpListener(IPAddress.Loopback,port);try{next.Start();}catch{next.Stop();throw;}listener=next;var thread=new Thread(()=>Accept(next)){IsBackground=true};thread.Start();}}
 void Accept(TcpListener source){while(true){TcpClient client;try{client=source.AcceptTcpClient();}catch{lock(gate)if(disposed)return;log("bridge listener stopped unexpectedly");return;}
  lock(gate){if(disposed||clients.Count>=8){client.Close();continue;}clients.Add(client);}
  var worker=new Thread(()=>{try{Serve(client);}catch(EndOfStreamException){}catch(IOException){}catch(ObjectDisposedException){}catch(Exception ex){log("bridge connection: "+ex.GetType().Name);}finally{client.Close();lock(gate)clients.Remove(client);}}){IsBackground=true};worker.Start();
 }}
 void Serve(TcpClient client){client.NoDelay=true;client.ReceiveTimeout=5000;client.SendTimeout=3000;using(var stream=client.GetStream()){
  var header=new StringBuilder();while(header.Length<8192){int next=stream.ReadByte();if(next<0)return;header.Append((char)next);if(header.Length>=4&&header.ToString(header.Length-4,4)=="\r\n\r\n")break;}
  if(!header.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal))return;
  var lines=header.ToString().Split(new[]{"\r\n"},StringSplitOptions.None);if(!authenticate(lines[0]))return;string key=null;foreach(var line in lines)if(line.StartsWith("Sec-WebSocket-Key:",StringComparison.OrdinalIgnoreCase))key=line.Substring(18).Trim();if(key==null||Convert.FromBase64String(key).Length!=16)return;
  string accept;using(var hash=SHA1.Create())accept=Convert.ToBase64String(hash.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
  var response=Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n");stream.Write(response,0,response.Length);client.ReceiveTimeout=0;
  using(var connection=new BridgeConnection(stream,retired=>{hub.Remove(retired);if(retired.Failure!=null)log("bridge disconnected: "+retired.Failure);})){while(true){var frame=BridgeWire.ReadFrame(stream);if(frame.Opcode==8){connection.CloseHandshake();return;}if(frame.Opcode==9){if(!connection.Enqueue(10,frame.Bytes))return;continue;}if(frame.Opcode==10)continue;if(frame.Opcode!=1)return;
   var text=Encoding.UTF8.GetString(frame.Bytes);var reply=dispatch(text);if(text=="subscribe"||text=="widgets")hub.Subscribe(connection,initial,reply);else if(!connection.Enqueue(1,BridgeWire.Text(reply)))return;
  }}
 }}
 public void Dispose(){TcpClient[] active;lock(gate){if(disposed)return;disposed=true;if(listener!=null){listener.Stop();listener=null;}active=new List<TcpClient>(clients).ToArray();}foreach(var client in active)try{client.Close();}catch{}}
}
