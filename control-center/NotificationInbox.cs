using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

interface INotificationStore {NotificationItem[] Load();void Save(NotificationItem[] items);}
sealed class ProtectedNotificationStore:INotificationStore {
 readonly string path;readonly object gate=new object();
 public ProtectedNotificationStore(string path){this.path=path;}
 public NotificationItem[] Load(){lock(gate){if(!File.Exists(path))return new NotificationItem[0];if(new FileInfo(path).Length>16777216)throw new IOException("Notification history is too large");var plain=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);return new JavaScriptSerializer{MaxJsonLength=16777216}.Deserialize<NotificationItem[]>(Encoding.UTF8.GetString(plain))??new NotificationItem[0];}}
 public void Save(NotificationItem[] items){lock(gate){var plain=Encoding.UTF8.GetBytes(new JavaScriptSerializer{MaxJsonLength=16777216}.Serialize(items));var encrypted=ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser);Directory.CreateDirectory(Path.GetDirectoryName(path));var temp=path+".tmp";try{File.WriteAllBytes(temp,encrypted);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}}
}
sealed class NotificationInbox:IDisposable {
 readonly object gate=new object(),writeGate=new object();readonly INotificationStore store;readonly Func<DateTime> now;readonly List<NotificationItem> items=new List<NotificationItem>();Timer writer;bool disposed,dirty;string storageError;
 public event Action Changed;public string SourceStatus {get;private set;}public string StorageError {get{lock(gate)return storageError;}}
 public NotificationInbox(INotificationStore store,Func<DateTime> now){this.store=store;this.now=now;SourceStatus="Windows notifications are not connected.";}
 public void Start(){lock(gate){if(disposed||writer!=null)return;try{items.AddRange(store.Load().Where(Valid).Select(i=>i.Copy()));foreach(var item in items)item.NativeActive=false;Prune();}catch{storageError="Encrypted history could not be opened. Existing history has been preserved.";}writer=new Timer(unused=>Flush(),null,Timeout.Infinite,Timeout.Infinite);}Notify();}
 static string Limit(string value,int length){return (value??"").Substring(0,Math.Min((value??"").Length,length));}
 static bool Valid(NotificationItem i){return i!=null&&!String.IsNullOrEmpty(i.Id)&&i.Id.Length<=700&&i.Title!=null&&i.Body!=null&&i.Title.Length<=512&&i.Body.Length<=4096&&i.Source!=null&&i.Source.Length<=128&&i.CreatedUtc.Year>=2020&&(String.IsNullOrEmpty(i.AppIcon)||(i.AppIcon.Length<=32768&&System.Text.RegularExpressions.Regex.IsMatch(i.AppIcon,@"^[A-Za-z0-9+/]+={0,2}$")));}
 void Prune(){items.RemoveAll(i=>i.CreatedUtc<now().AddDays(-7)&&!i.Pinned);if(items.Count>500){var keep=items.OrderByDescending(i=>i.Pinned).ThenByDescending(i=>i.CreatedUtc).Take(500).ToArray();items.Clear();items.AddRange(keep);}}
 void Dirty(){dirty=true;if(writer!=null)writer.Change(300,Timeout.Infinite);}
 void Notify(){var changed=Changed;if(changed!=null)foreach(Action subscriber in changed.GetInvocationList())try{subscriber();}catch{}}
 public NotificationItem[] Snapshot(){lock(gate)return items.OrderByDescending(i=>i.CreatedUtc).Select(i=>i.Copy()).ToArray();}
 public int Unread {get{lock(gate)return items.Count(i=>!i.Read&&!i.Archived);}}
 public void SetStatus(string status){lock(gate){if(disposed||SourceStatus==status)return;SourceStatus=Limit(status,300);if(status!="Windows notifications connected")foreach(var item in items.Where(i=>i.Provider=="windows"))item.NativeActive=false;}Notify();}
 public void SyncWindows(NotificationItem[] incoming,string status){if(incoming==null||incoming.Length>500)throw new ArgumentException("Invalid notification batch");var clean=incoming.Where(Valid).Where(i=>i.Provider=="windows"&&i.Id.StartsWith("windows:",StringComparison.Ordinal)&&i.NativeId>0).Select(i=>i.Copy()).ToArray();if(clean.Length!=incoming.Length)throw new ArgumentException("Invalid notification record");
  lock(gate){if(disposed)return;SourceStatus=Limit(status,300);var seen=new HashSet<string>(clean.Select(i=>i.Id));foreach(var old in items.Where(i=>i.Provider=="windows"&&!seen.Contains(i.Id))){old.NativeActive=false;old.Archived=true;old.Read=true;}
   foreach(var item in clean){item.CreatedUtc=item.CreatedUtc.ToUniversalTime();if(item.CreatedUtc>now().AddMinutes(5))continue;item.NativeActive=true;var old=items.FirstOrDefault(i=>i.Id==item.Id);if(old!=null){item.Read=old.Read;item.Pinned=old.Pinned;item.Archived=old.Archived;if(!item.HasIcon)item.AppIcon=old.AppIcon;items.Remove(old);}else{item.Read=item.Pinned=item.Archived=false;}items.Add(item);}
   // Reuse only a matching Windows app identity, never a sender/title match.
   var logos=clean.Concat(items).Where(i=>i.Provider=="windows"&&!String.IsNullOrEmpty(i.AppId)&&i.HasIcon).GroupBy(i=>i.AppId,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().AppIcon,StringComparer.Ordinal);
   foreach(var item in items.Where(i=>i.Provider=="windows"&&!String.IsNullOrEmpty(i.AppId))){string logo;if(logos.TryGetValue(item.AppId,out logo))item.AppIcon=logo;}
   Prune();Dirty();}Notify();
 }
 public void AddShell(string id,string title,string body){lock(gate){if(disposed||items.Any(i=>i.Id=="shell:"+id))return;items.Add(new NotificationItem{Id="shell:"+Limit(id,600),Provider="shell",Source="Nord Rice",Title=Limit(title,512),Body=Limit(body,4096),CreatedUtc=now()});Prune();Dirty();}Notify();}
 public void Change(string[] ids,string action){lock(gate){if(disposed)return;var selected=new HashSet<string>(ids);foreach(var item in items.Where(i=>selected.Contains(i.Id))){switch(action){case "read":item.Read=!item.Read;break;case "mark-read":item.Read=true;break;case "pin":item.Pinned=!item.Pinned;break;case "archive":item.Archived=true;item.Read=true;break;case "restore":item.Archived=false;break;case "delete":break;default:throw new ArgumentException("Unknown inbox action");}}if(action=="delete")items.RemoveAll(i=>selected.Contains(i.Id));Dirty();}Notify();}
 void Flush(){lock(writeGate){NotificationItem[] copy;lock(gate){if(!dirty||storageError!=null)return;copy=items.Select(i=>i.Copy()).ToArray();dirty=false;}try{store.Save(copy);}catch{lock(gate){storageError="History could not be encrypted or saved. No plaintext fallback was written.";}Notify();}}}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;if(writer!=null){writer.Dispose();writer=null;}}Flush();Changed=null;}
}
