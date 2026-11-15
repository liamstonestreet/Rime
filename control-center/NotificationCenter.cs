using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Compatibility entry points; AppServices owns the single inbox lifetime.
static class NotificationCenter {
 public static readonly NotificationInbox Current=new NotificationInbox(new ProtectedNotificationStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NordRice","notifications.bin")),()=>DateTime.UtcNow);
 public static event Action BadgeChanged;static bool started;static string lastBadge;static System.Threading.Timer health;static long lastReceive,lastLaunch;
 public static bool ValidAppId(string id){return !String.IsNullOrEmpty(id)&&id.Length<=256&&Regex.IsMatch(id,@"^[A-Za-z0-9_.!{}-]+$");}
 public static void Start(){if(started)return;started=true;Current.Changed+=Publish;BarAppearance.Changed+=Publish;Current.Start();health=new System.Threading.Timer(unused=>{long ticks=System.Threading.Interlocked.Read(ref lastReceive),launch=System.Threading.Interlocked.Read(ref lastLaunch);if(launch>ticks&&DateTime.UtcNow.Ticks-launch>TimeSpan.FromSeconds(15).Ticks)Current.SetStatus("Windows companion did not connect. Retry Connect Windows, or reinstall with install.ps1 -Notifications.");else if(ticks>0&&DateTime.UtcNow.Ticks-ticks>TimeSpan.FromSeconds(45).Ticks)Current.SetStatus("Windows companion disconnected. Click Connect Windows to reconnect.");},null,10000,10000);System.Windows.Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{if(started)LaunchCompanion(true);}));}
 public static void Stop(){if(!started)return;started=false;if(health!=null){health.Dispose();health=null;}Current.Changed-=Publish;BarAppearance.Changed-=Publish;Current.Dispose();BadgeChanged=null;}
 public static string Badge(){return new JavaScriptSerializer().Serialize(new{notifications=new{unread=Current.Unread,bell=true}});}
 static void Publish(){var value=Badge();if(value!=lastBadge){lastBadge=value;MediaBridge.Broadcast(value);var changed=BadgeChanged;if(changed!=null)changed();}}
 sealed class Batch {public string status {get;set;}public NotificationItem[] items {get;set;}}
 public static string Receive(string json){if(json.Length>4194304)return "{\"ok\":false}";var batch=new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<Batch>(json);System.Threading.Interlocked.Exchange(ref lastReceive,DateTime.UtcNow.Ticks);if(batch==null)return "{\"ok\":false}";if(batch.items==null)Current.SetStatus(batch.status);else Current.SyncWindows(batch.items,batch.status);return "{\"ok\":true}";}
 public static void Dismiss(NotificationItem item){if(item.Provider=="windows"&&item.NativeActive)MediaBridge.Broadcast(new JavaScriptSerializer().Serialize(new{notificationAction=new{action="dismiss",id=item.NativeId,key=item.Id}}));Current.Change(new[]{item.Id},"archive");}
 public static void FocusCompleted(string deadline,string message,string goal){if(started)Current.AddShell("focus:"+deadline,message,(goal??"").Length>0?"Goal: "+goal:"Your timer has finished.");}
 public static void OpenApp(string id){if(ValidAppId(id))Process.Start(new ProcessStartInfo("explorer.exe","shell:AppsFolder\\"+id){UseShellExecute=true});}
 public static bool LaunchCompanion(bool background=false){try{var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"notification-companion.json");if(!File.Exists(path)){Current.SetStatus("Windows notifications are not connected: the companion is not installed. Run install.ps1 -Notifications, then click Connect Windows and allow access.");return false;}var config=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(path));string id;if(!config.TryGetValue("appId",out id)||!ValidAppId(id))throw new IOException();Current.SetStatus(background?"Connecting to Windows notifications...":"Opening Windows access setup. Allow access in the Nord Rice Notifications window.");System.Threading.Interlocked.Exchange(ref lastLaunch,DateTime.UtcNow.Ticks);NotificationActivation.Launch(id,background?"--background":"--connect");return true;}catch(Exception ex){Current.SetStatus("Windows companion could not start (0x"+ex.HResult.ToString("X8")+"). Reinstall with install.ps1 -Notifications.");return false;}}
}
