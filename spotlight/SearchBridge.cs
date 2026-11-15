using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

// Loopback-only, token-authenticated WebSocket. Idle connections block in the OS;
// no timers, process spawning, or files are involved in search requests.
sealed class SearchBridge {
    static readonly System.Collections.Generic.List<Stream> connections=new System.Collections.Generic.List<Stream>();
    public static void Broadcast(string json){Stream[] targets;lock(connections)targets=connections.ToArray();var bytes=Encoding.UTF8.GetBytes(json);ThreadPool.QueueUserWorkItem(delegate{foreach(var stream in targets)try{lock(stream)Reply(stream,1,bytes);}catch{}});}
    readonly Func<string,string> dispatch;
    readonly Semaphore clients = new Semaphore(4,4);
    public SearchBridge(Func<string,string> callback) { dispatch = callback; }
    public void Start() {
        var listener = new TcpListener(IPAddress.Loopback,6137); listener.Start();
        var thread = new Thread(delegate() {
            while (true) {
                var client = listener.AcceptTcpClient();
                if (!clients.WaitOne(0)) { client.Close(); continue; }
                var worker = new Thread(delegate() { try { Serve(client); } catch { } finally { client.Close(); clients.Release(); } });
                worker.IsBackground = true; worker.Start();
            }
        }); thread.IsBackground = true; thread.Start();
    }
    static byte[] Read(Stream stream,int count) {
        var bytes = new byte[count]; int offset=0;
        while(offset<count) { int n=stream.Read(bytes,offset,count-offset); if(n==0) throw new EndOfStreamException(); offset+=n; }
        return bytes;
    }
    static void Reply(Stream stream,int opcode,byte[] bytes) {
        stream.WriteByte((byte)(0x80|opcode));
        if(bytes.Length<126) stream.WriteByte((byte)bytes.Length);
        else { stream.WriteByte(126); stream.WriteByte((byte)(bytes.Length>>8)); stream.WriteByte((byte)bytes.Length); }
        stream.Write(bytes,0,bytes.Length);
    }
    void Serve(TcpClient client) {
        client.NoDelay=true; client.ReceiveTimeout=5000;
        using(var stream=client.GetStream()) {
            var header=new StringBuilder();
            while(header.Length<8192) {
                int next=stream.ReadByte(); if(next<0) return; header.Append((char)next);
                if(header.Length>=4 && header.ToString(header.Length-4,4)=="\r\n\r\n") break;
            }
            var lines=header.ToString().Split(new[]{"\r\n"},StringSplitOptions.None);
            if(!BridgeTokens.Matches(lines[0],BridgeTokens.Spotlight)) return;
            string key=null;
            foreach(var line in lines) if(line.StartsWith("Sec-WebSocket-Key:",StringComparison.OrdinalIgnoreCase)) key=line.Substring(18).Trim();
            if(key==null || Convert.FromBase64String(key).Length!=16) return;
            string accept;
            using(var hash=SHA1.Create()) accept=Convert.ToBase64String(hash.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            var response=Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n");
            stream.Write(response,0,response.Length); client.ReceiveTimeout=0;
            lock(connections)connections.Add(stream);
            try { while(true) {
                var prefix=Read(stream,2); int opcode=prefix[0]&15;
                if((prefix[0]&0x80)==0 || (prefix[0]&0x70)!=0 || (prefix[1]&0x80)==0) return;
                int size=prefix[1]&127;
                if(size==126) { var extended=Read(stream,2); size=(extended[0]<<8)|extended[1]; }
                else if(size==127) return;
                if(size>4096) return;
                var mask=Read(stream,4); var payload=Read(stream,size);
                for(int i=0;i<size;i++) payload[i]^=mask[i%4];
                if(opcode==8) { lock(stream)Reply(stream,8,new byte[0]); return; }
                if(opcode==9) { lock(stream)Reply(stream,10,payload); continue; }
                if(opcode!=1) return;
                var reply=dispatch(Encoding.UTF8.GetString(payload));
                lock(stream)Reply(stream,1,Encoding.UTF8.GetBytes(reply));
            } } finally {lock(connections)connections.Remove(stream);}
        }
    }
}
