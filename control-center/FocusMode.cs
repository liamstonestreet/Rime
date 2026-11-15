using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;

static class FocusMode {
 static volatile bool stopping;static readonly object gate=new object();static volatile FocusState state=new FocusState();static Timer timer;
 static readonly Dictionary<IntPtr,HiddenFocusWindow> hidden=new Dictionary<IntPtr,HiddenFocusWindow>();
 internal static Func<string[],HiddenFocusWindow[]> FindWindows=FindVisible;
 internal static Action<HiddenFocusWindow> HideWindow=window=>ShowWindowAsync(new IntPtr(window.handle),0);
 internal static Func<HiddenFocusWindow,bool> RestoreWindow=RestoreOne;
 public static volatile bool Transitioning;public static string Error="";
 public static FocusState State {get{return state;}}
 static string Root {get{return AppDomain.CurrentDomain.BaseDirectory;}}
 static string Config {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@".glzr\glazewm\config.yaml");}}
 static void Write(string name,object value){string path=Path.Combine(Root,name);File.WriteAllText(path+".tmp",new JavaScriptSerializer().Serialize(value));if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);}
 static void Save(){state.hiddenWindows=hidden.Values.ToArray();Write("focus-state.json",state);}
 public static FocusSettings Settings(){var path=Path.Combine(Root,"focus-mode.json");var config=File.Exists(path)?new JavaScriptSerializer().Deserialize<FocusSettings>(File.ReadAllText(path)):new FocusSettings();Validate(config);return config;}
 public static void Validate(FocusSettings s){
  if(s==null||s.blockedProcesses==null||s.hiddenProcesses==null||s.disabledWorkspaces==null||s.defaultMinutes<1||s.defaultMinutes>1440||s.breakMinutes<1||s.breakMinutes>1440||!Regex.IsMatch(s.fallbackWorkspace??"",@"^[a-zA-Z0-9_-]+$")||s.disabledWorkspaces.Contains(s.fallbackWorkspace))throw new Exception("Choose work and break lengths from 1 to 1440 minutes.");
  if(s.hiddenProcesses.Length>40)throw new Exception("Choose up to 40 apps to hide.");foreach(var raw in s.blockedProcesses.Concat(s.hiddenProcesses)){var name=Path.GetFileNameWithoutExtension(raw);if(!Regex.IsMatch(raw,@"^[a-zA-Z0-9 _.-]+$")||new[]{"explorer","winlogon","csrss","lsass","services","svchost","system","glazewm","zebar","NordControlCenter","NordWorkspaceIndicator","NordRuntime","NordSpotlight","dwm"}.Contains(name,StringComparer.OrdinalIgnoreCase))throw new Exception("This app cannot be hidden: "+raw);}
  foreach(var name in s.disabledWorkspaces)if(!Regex.IsMatch(name,@"^[a-zA-Z0-9_-]+$"))throw new Exception("Invalid workspace name");foreach(var color in new[]{s.edge,s.background,s.accent})if(!Regex.IsMatch(color??"",@"^#[0-9a-fA-F]{6}$"))throw new Exception("Theme colors must use #RRGGBB");
 }
 static void EnsureTimer(){if(stopping)return;if(timer==null)timer=new Timer(Tick,null,1000,1000);else timer.Change(1000,1000);}
 public static void Initialize(){stopping=false;state=FocusState.Read();Task.Run(()=>{lock(gate){if(stopping)return;try{foreach(var item in state.hiddenWindows??new HiddenFocusWindow[0])hidden[new IntPtr(item.handle)]=item;if(!state.HasSession&&state.endsUtc!=null)RecoverLegacy();if(state.HasSession){state.settings.blockedProcesses=new string[0];state.settings.disabledWorkspaces=new string[0];Validate(state.settings);FocusSession.Advance(state,DateTime.UtcNow);if(!state.Active)RestoreHidden();Save();}else if(RestoreHidden())Save();if(state.HasSession||hidden.Count>0)EnsureTimer();}catch(Exception ex){Error=ex.Message;if(state.HasSession){FocusSession.Pause(state,DateTime.UtcNow);try{RestoreHidden();Save();}catch{}}EnsureTimer();}}});}
 public static Task Start(FocusSettings settings,string goal){return Change(()=>{Validate(settings);if(state.HasSession)throw new Exception("End the current session before starting another.");settings.blockedProcesses=new string[0];settings.disabledWorkspaces=new string[0];settings.hiddenProcesses=settings.hiddenProcesses.Select(ProcessName).Where(n=>n.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();Write("focus-mode.json",settings);RestoreHidden();state=FocusSession.Start(settings,goal,DateTime.UtcNow);EnsureTimer();});}
 public static Task Command(string command){return Change(()=>{
  var now=DateTime.UtcNow;FocusSession.Advance(state,now);
  if(command=="end"){RestoreHidden();state=new FocusState();}
  else if(state.HasSession){if(command=="pause")FocusSession.Pause(state,now);else if(command=="resume")FocusSession.Resume(state,now);else if(command=="extend")FocusSession.Extend(state,now);else if(command=="break"){FocusSession.Phase(state,"break",now,false);state.notice="Take your time. Your windows are restored.";}else if(command=="work"){FocusSession.Phase(state,"work",now,false);state.notice="";}}
 });}
 static Task Change(Action action){return Task.Run(()=>{lock(gate){if(stopping)return;try{Error="";if(!state.HasSession&&state.endsUtc!=null)RecoverLegacy();action();if(state.Active)HideSelected();else RestoreHidden();Save();if(state.HasSession||hidden.Count>0)EnsureTimer();else if(timer!=null)timer.Change(Timeout.Infinite,Timeout.Infinite);}catch(Exception ex){Error=ex.Message;}}});}
 static void Tick(object unused){lock(gate){if(stopping)return;try{bool changed=false;if(!state.HasSession&&state.endsUtc!=null){RecoverLegacy();changed=true;}var deadline=state.endsUtc;if(FocusSession.Advance(state,DateTime.UtcNow)){changed=true;NotificationCenter.FocusCompleted(deadline,state.notice,state.goal);}if(state.Active)changed|=HideSelected();else changed|=RestoreHidden();if(changed)Save();if(!state.HasSession&&hidden.Count==0&&timer!=null)timer.Change(Timeout.Infinite,Timeout.Infinite);}catch(Exception ex){Error=ex.Message;}}}
 public static void Stop(){stopping=true;lock(gate){if(timer!=null){timer.Dispose();timer=null;}try{if(RestoreHidden())Save();}catch(Exception ex){Error=ex.Message;WallpaperSwitcher.Log("focus shutdown: "+ex.Message);}}}
 internal static string Snapshot(){lock(gate){return new JavaScriptSerializer().Serialize(new {focus=new{active=state.Active,themeActive=state.ThemeActive,session=state.HasSession,phase=state.phase??"",paused=state.paused,waiting=state.waiting,remainingSeconds=(int)Math.Ceiling(state.Remaining(DateTime.UtcNow)),goal=state.goal??"",completedSessions=state.completedSessions,notice=state.notice??"",endsUtc=state.endsUtc,settings=state.settings,error=Error}});}}
 static string Key(Process p){return p.Id+":"+p.StartTime.ToUniversalTime().Ticks;}
 static string ProcessName(string name){name=name.Trim();return name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?name.Substring(0,name.Length-4):name;}
 static bool HideSelected(){
  var windows=FindWindows(state.settings.hiddenProcesses);var added=windows.Where(w=>{HiddenFocusWindow prior;return !hidden.TryGetValue(new IntPtr(w.handle),out prior)||prior.pid!=w.pid||prior.processKey!=w.processKey;}).ToArray();var previous=added.ToDictionary(w=>new IntPtr(w.handle),w=>hidden.ContainsKey(new IntPtr(w.handle))?hidden[new IntPtr(w.handle)]:null);foreach(var window in added)hidden[new IntPtr(window.handle)]=window;
  // Journal the identities before hiding anything, so a crash/restart can restore them.
  if(added.Length>0)try{Save();}catch{foreach(var pair in previous){if(pair.Value==null)hidden.Remove(pair.Key);else hidden[pair.Key]=pair.Value;}throw;}
  foreach(var window in windows)HideWindow(window);return added.Length>0;
 }
 static HiddenFocusWindow[] FindVisible(string[] names){var selected=new Dictionary<uint,string>();foreach(var name in names)foreach(var process in Process.GetProcessesByName(name))using(process){try{selected[(uint)process.Id]=Key(process);}catch{}}var windows=new List<HiddenFocusWindow>();if(selected.Count>0)EnumWindows(delegate(IntPtr h,IntPtr unused){uint pid;GetWindowThreadProcessId(h,out pid);string key;if(selected.TryGetValue(pid,out key)&&IsWindowVisible(h)&&!GameWindows.IsGame(h))windows.Add(new HiddenFocusWindow{handle=h.ToInt64(),pid=pid,processKey=key});return true;},IntPtr.Zero);return windows.ToArray();}
 static bool RestoreOne(HiddenFocusWindow window){var handle=new IntPtr(window.handle);uint pid;GetWindowThreadProcessId(handle,out pid);bool matches=false;try{if(pid==window.pid)using(var process=Process.GetProcessById((int)pid))matches=Key(process)==window.processKey;}catch{}if(!matches)return true;ShowWindowAsync(handle,8);return IsWindowVisible(handle);}
 static bool RestoreHidden(){bool changed=false;foreach(var pair in hidden.ToArray())if(RestoreWindow(pair.Value)){hidden.Remove(pair.Key);changed=true;}return changed;}
 // One-time recovery of the old hard-focus mode. New sessions never patch GlazeWM or kill processes.
 static void RecoverLegacy(){Transitioning=true;try{using(var mutex=new Mutex(false,"Local\\LiamNordWorkspaceRules")){bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new Exception("Workspace configuration is busy");if(File.Exists(Config)){var text=File.ReadAllText(Config);var restored=Restore(text,state);if(restored!=text){File.WriteAllText(Config,restored);try{GlazeCommand.ReloadConfig();}catch{File.WriteAllText(Config,text);throw;}}}RestoreHidden();state=new FocusState();Save();}finally{if(held)mutex.ReleaseMutex();}}}finally{Transitioning=false;}}
 public static string Patch(string text,FocusState target){var before=new List<string>();var after=new List<string>();var lines=text.Replace("\r\n","\n").Split('\n');bool effects=false;for(int i=0;i<lines.Length;i++){string old=lines[i],line=old;if(Regex.IsMatch(line,@"^window_effects:"))effects=true;else if(Regex.IsMatch(line,@"^\w"))effects=false;if(effects&&Regex.IsMatch(line,@"^\s+color:"))line=Regex.Replace(line,@"color:.*$","color: '"+target.settings.edge+"'");if(line.Contains("commands:"))foreach(var name in target.settings.disabledWorkspaces)line=Regex.Replace(line,@"\b(focus|move) --workspace "+Regex.Escape(name)+@"(?=['""\s,\]])","$1 --workspace "+target.settings.fallbackWorkspace);if(line!=old){line+=" # nord-focus-owned-"+before.Count;before.Add(old);after.Add(line);lines[i]=line;}}target.originals=before.ToArray();target.applied=after.ToArray();return String.Join("\n",lines);}
 public static string Restore(string text,FocusState prior){for(int i=0;i<(prior.applied??new string[0]).Length;i++)text=text.Replace(prior.applied[i],prior.originals[i]);return text;}
 delegate bool Visitor(IntPtr hwnd,IntPtr unused);
 [DllImport("user32.dll")] static extern bool EnumWindows(Visitor visitor,IntPtr unused);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr hwnd,int command);
}
