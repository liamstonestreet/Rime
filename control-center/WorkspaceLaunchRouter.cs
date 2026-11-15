using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// Two persistent GlazeWM sockets: lifecycle notifications and ordered queries /
// targeted moves followed by focus. No idle polling or config reloads.
sealed class WorkspaceLaunchRouter:IDisposable {
 readonly WorkspaceLaunchPolicy policy=new WorkspaceLaunchPolicy();readonly CancellationTokenSource stop=new CancellationTokenSource();Task worker;bool disposed;
 public void Start(){if(!disposed&&worker==null)worker=Task.Run((Func<Task>)Run);}
 public void Dispose(){if(disposed)return;disposed=true;stop.Cancel();if(worker!=null)worker.ContinueWith(task=>stop.Dispose());else stop.Dispose();}
 async Task Run(){while(!stop.IsCancellationRequested){try{
  using(var events=new ClientWebSocket())using(var query=new ClientWebSocket()){
   events.Options.Proxy=null;query.Options.Proxy=null;
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){timeout.CancelAfter(3000);await events.ConnectAsync(new Uri("ws://127.0.0.1:6123"),timeout.Token).ConfigureAwait(false);await query.ConnectAsync(new Uri("ws://127.0.0.1:6123"),timeout.Token).ConfigureAwait(false);}
   await Send(events,"sub -e window_managed window_unmanaged user_config_changed",stop.Token).ConfigureAwait(false);await Receive(events,stop.Token).ConfigureAwait(false);
   await Sample(query,policy).ConfigureAwait(false);
   while(!stop.IsCancellationRequested){await Receive(events,stop.Token).ConfigureAwait(false);await Sample(query,policy).ConfigureAwait(false);for(int retry=0;retry<20&&policy.HasPending;retry++){await Task.Delay(400,stop.Token).ConfigureAwait(false);await Sample(query,policy).ConfigureAwait(false);}}
  }
 }catch(Exception ex){if(stop.IsCancellationRequested)return;WallpaperSwitcher.Log("workspace launch router: "+ex.Message);}
  try{await Task.Delay(2000,stop.Token).ConfigureAwait(false);}catch(OperationCanceledException){return;}
 }}
 async Task Sample(ClientWebSocket socket,WorkspaceLaunchPolicy policy){
  var response=await Request(socket,"query windows").ConfigureAwait(false);var data=(Dictionary<string,object>)response["data"];
  var windows=new List<WorkspaceLaunchWindow>();foreach(Dictionary<string,object> item in (IEnumerable)data["windows"]){long handle=Convert.ToInt64(item["handle"]);if(GameWindows.IsGame(new IntPtr(handle))){Guid gameId;if(Guid.TryParse(Convert.ToString(item["id"]),out gameId)){await Request(socket,"command --id "+gameId+" ignore").ConfigureAwait(false);WallpaperSwitcher.Log("game excluded from GlazeWM: "+Convert.ToString(item["processName"]));}continue;}windows.Add(new WorkspaceLaunchWindow{Id=Convert.ToString(item["id"]),Handle=handle,Process=Convert.ToString(item["processName"]),Title=Convert.ToString(item["title"]),AppId=AppShortcuts.WindowIdentity(new IntPtr(handle)),StartedUtc=Started(new IntPtr(handle))});}
  foreach(var move in policy.Observe(windows.ToArray(),AppCatalog.Rules(),WorkspaceConfig.RoutingEnabled(),DateTime.UtcNow)){
   // Revalidate immediately before moving: a second instance may have appeared
   // while the first snapshot was being read. Never act on a reused window ID.
   var latest=await Request(socket,"query windows").ConfigureAwait(false);var peers=((IEnumerable)((Dictionary<string,object>)latest["data"])["windows"]).Cast<Dictionary<string,object>>().ToArray();
   var target=peers.FirstOrDefault(w=>Convert.ToString(w["id"])==move.WindowId);if(target==null)continue;
   var rule=AppCatalog.Rules().FirstOrDefault(r=>WorkspaceLaunchPolicy.RuleKey(r)==move.RuleStamp&&WorkspaceLaunchPolicy.Matches(r,new WorkspaceLaunchWindow{Process=Convert.ToString(target["processName"]),Title=Convert.ToString(target["title"]),AppId=AppShortcuts.WindowIdentity(new IntPtr(Convert.ToInt64(target["handle"])))}));if(rule==null)continue;
   if(peers.Any(w=>Convert.ToString(w["id"])!=move.WindowId&&!move.LaunchPeers.Contains(Convert.ToString(w["id"]))&&WorkspaceLaunchPolicy.Matches(rule,new WorkspaceLaunchWindow{Process=Convert.ToString(w["processName"]),Title=Convert.ToString(w["title"]),AppId=AppShortcuts.WindowIdentity(new IntPtr(Convert.ToInt64(w["handle"])))})))continue;
   if(WorkspaceConfig.RoutingEnabled()){await Request(socket,"command --id "+move.WindowId+" move --workspace "+move.Workspace).ConfigureAwait(false);await Request(socket,"command focus --container-id "+move.WindowId).ConfigureAwait(false);WallpaperSwitcher.Log("workspace launch routed: "+Convert.ToString(target["processName"])+" window "+move.WindowId+" -> "+move.Workspace);}
  }
 }
 static DateTime Started(IntPtr handle){try{uint pid;GetWindowThreadProcessId(handle,out pid);using(var process=System.Diagnostics.Process.GetProcessById((int)pid))return process.StartTime.ToUniversalTime();}catch{return default(DateTime);}}
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr handle,out uint pid); async Task<Dictionary<string,object>> Request(ClientWebSocket socket,string command){using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){timeout.CancelAfter(3000);await Send(socket,command,timeout.Token).ConfigureAwait(false);return await Receive(socket,timeout.Token).ConfigureAwait(false);}}
 static Task Send(ClientWebSocket socket,string text,CancellationToken token){return socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)),WebSocketMessageType.Text,true,token);}
 static async Task<Dictionary<string,object>> Receive(ClientWebSocket socket,CancellationToken token){using(var stream=new MemoryStream()){var buffer=new byte[8192];WebSocketReceiveResult part;do{part=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),token).ConfigureAwait(false);if(part.MessageType==WebSocketMessageType.Close)throw new IOException("GlazeWM disconnected");stream.Write(buffer,0,part.Count);if(stream.Length>4194304)throw new IOException("Unexpected GlazeWM response size");}while(!part.EndOfMessage);var result=new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(stream.ToArray()));object success;if(result.TryGetValue("success",out success)&&success is bool&&!(bool)success)throw new IOException("GlazeWM rejected routing request");return result;}}
}
