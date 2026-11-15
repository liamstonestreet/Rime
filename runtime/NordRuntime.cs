using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// A single per-session owner. No shell polling processes, screenshot work, or UI focus changes.
sealed class NordRuntime : ApplicationContext {
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=5000};
 readonly string folder=AppDomain.CurrentDomain.BaseDirectory;
 readonly string zebar;readonly int session=Process.GetCurrentProcess().SessionId;
 volatile bool suspended,ending;DateTime ready=DateTime.MinValue;
 public NordRuntime(){
  var config=Path.Combine(folder,"runtime-zebar.txt");zebar=File.Exists(config)?File.ReadAllText(config).Trim():Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"glzr.io","Zebar","zebar.exe");
  SystemEvents.PowerModeChanged+=Power;SystemEvents.SessionEnding+=Ending;
  timer.Tick+=delegate{Ensure();};timer.Start();Ensure();
  ThreadPool.QueueUserWorkItem(delegate{RepairSpotify();});
 }
 void RepairSpotify(){try{
  if(ending||Installing())return;
  string roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  string extension=Path.Combine(roaming,@"spicetify\Extensions\nord-media-link.js"),config=Path.Combine(roaming,@"spicetify\config-xpui.ini");
  if(!File.Exists(extension)||!File.Exists(config))return;
  string spotify=Path.Combine(roaming,"Spotify");foreach(var line in File.ReadAllLines(config)){var parts=line.Split(new[]{'='},2);if(parts.Length==2&&parts[0].Trim()=="spotify_path")spotify=parts[1].Trim();}
  string patched=Path.Combine(spotify,@"Apps\xpui\extensions\nord-media-link.js");
  if(File.Exists(patched)&&File.ReadAllText(patched)==File.ReadAllText(extension))return;
  string exe=Path.Combine(local,@"spicetify\spicetify.exe");if(!File.Exists(exe))return;
  Log("Repairing missing/outdated Spotify metadata extension");
  if(ending||Installing())return;
  using(var process=Process.Start(new ProcessStartInfo(exe,"backup apply"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(exe)})){
   if(!process.WaitForExit(60000)){Log("Spotify repair is still running; no duplicate repair scheduled");return;}
   Log("Spotify repair exit code: "+process.ExitCode+". An already-open Spotify may need reopening.");
  }
 }catch(Exception e){Log("Spotify repair: "+e.Message);}}
 void Power(object sender,PowerModeChangedEventArgs e){suspended=e.Mode==PowerModes.Suspend;if(e.Mode==PowerModes.Resume)ready=DateTime.UtcNow.AddSeconds(3);}
 void Ending(object sender,SessionEndingEventArgs e){ending=true;timer.Stop();}
 [System.Runtime.InteropServices.DllImport("kernel32.dll")]static extern bool ProcessIdToSessionId(uint pid,out uint value);
 bool Running(string name){foreach(var p in Process.GetProcessesByName(name))using(p){uint value;if(ProcessIdToSessionId((uint)p.Id,out value)&&value==session)return true;}return false;}
 void Start(string path,string args){string name=Path.GetFileNameWithoutExtension(path);if(Running(name)||!File.Exists(path))return;try{
  Process.Start(new ProcessStartInfo(path,args){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=folder}).Dispose();Log("Started "+name);
 }catch(Exception e){Log(name+": "+e.Message);}}
 bool Installing(){using(var mutex=new Mutex(false,"Local\\NordRiceInstall")){bool acquired=false;try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}return !acquired;}finally{if(acquired)mutex.ReleaseMutex();}}}
 void Ensure(){if(ending||suspended||DateTime.UtcNow<ready||Installing())return;try{
  // Exiting the window manager intentionally also disables recovery.
  if(!Running("glazewm"))return;
  Start(Path.Combine(folder,"NordControlCenter.exe"),"--register");Start(zebar,"");
  Start(Path.Combine(folder,"NordSpotlight.exe"),"--background");
  Start(Path.Combine(folder,"NordWorkspaceIndicator.exe"),"");
 }catch(Exception e){Log(e.Message);}finally{ready=DateTime.UtcNow.AddSeconds(5);}}
 void Log(string text){try{var path=Path.Combine(folder,"runtime.log");if(File.Exists(path)&&new FileInfo(path).Length>65536)File.Move(path,path+"."+DateTime.UtcNow.Ticks+".old");File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine);}catch{}}
 protected override void Dispose(bool disposing){if(disposing){ending=true;timer.Stop();timer.Dispose();SystemEvents.PowerModeChanged-=Power;SystemEvents.SessionEnding-=Ending;}base.Dispose(disposing);}
 [STAThread]static void Main(string[] args){bool created;using(var mutex=new Mutex(true,"Local\\NordRiceRuntime",out created)){if(!created)return;Application.Run(new NordRuntime());}}
}
