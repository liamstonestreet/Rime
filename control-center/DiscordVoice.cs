using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

sealed class DiscordVoiceState {
 public bool available {get;set;}
 public bool? muted {get;set;}
 public bool? deafened {get;set;}
 public string error {get;set;}
}
static class DiscordVoice {
 static readonly SemaphoreSlim gate=new SemaphoreSlim(1);
 internal static Func<string,DiscordVoiceState> Query=Read;
 public static async Task<DiscordVoiceState> Run(string action){
  if(action!="status"&&action!="mute"&&action!="deafen")return new DiscordVoiceState{error="Unknown Discord action."};
  if(!gate.Wait(0))return new DiscordVoiceState{error="Discord is still responding. Try again in a moment."};
  var work=Task.Run(()=>{try{return Query(action);}catch{return new DiscordVoiceState{error="Discord's voice controls are unavailable. Open its main window once and try again."};}finally{gate.Release();}});
  if(await Task.WhenAny(work,Task.Delay(2500))!=work)return new DiscordVoiceState{error="Discord did not respond. Open its main window and try again."};
  return await work;
 }
 internal static string Identify(string label){
  var match=Regex.Match((label??"").Trim(),@"^(unmute|undeafen|mute|deafen)(?=\s*(?:$|\(|microphone\b|audio\b))",RegexOptions.IgnoreCase);
  return match.Success?match.Groups[1].Value.ToLowerInvariant():null;
 }
 internal static bool? ResolveState(string label,bool? toggled){return toggled??(Identify(label)==null?(bool?)null:Identify(label)=="unmute"||Identify(label)=="undeafen");}
 static bool? State(AutomationElement button){
  object pattern;bool? toggled=null;
  if(button.TryGetCurrentPattern(TogglePattern.Pattern,out pattern)){var state=((TogglePattern)pattern).Current.ToggleState;if(state!=ToggleState.Indeterminate)toggled=state==ToggleState.On;}
  return ResolveState(button.Current.Name,toggled);
 }
 static DiscordVoiceState Read(string action){
  var processes=new HashSet<uint>();foreach(var name in new[]{"Discord","DiscordPTB","DiscordCanary"})foreach(var p in Process.GetProcessesByName(name))using(p)processes.Add((uint)p.Id);
  if(processes.Count==0)return new DiscordVoiceState{error="Discord isn't running."};
  var windows=new List<IntPtr>();EnumWindows((h,l)=>{uint pid;GetWindowThreadProcessId(h,out pid);if(processes.Contains(pid)&&IsWindowVisible(h))windows.Add(h);return true;},IntPtr.Zero);
  foreach(var window in windows){
   var root=AutomationElement.FromHandle(window);var buttons=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
   AutomationElement mute=null,deafen=null;
   foreach(AutomationElement button in buttons){string name=Identify(button.Current.Name);if(name=="mute"||name=="unmute"){mute=button;}else if(name=="deafen"||name=="undeafen"){deafen=button;}}
   if(mute==null||deafen==null)continue;
   if(action!="status"){
    var target=action=="mute"?mute:deafen;if(!target.Current.IsEnabled)throw new InvalidOperationException();object pattern;
    if(target.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))((InvokePattern)pattern).Invoke();
    else if(target.TryGetCurrentPattern(TogglePattern.Pattern,out pattern))((TogglePattern)pattern).Toggle();
    else throw new InvalidOperationException();
    Thread.Sleep(100);
   }
   return new DiscordVoiceState{available=true,muted=State(mute),deafened=State(deafen)};
  }
  return new DiscordVoiceState{error="Discord's voice controls aren't accessible. Open its main window once and try again."};
 }
 delegate bool EnumProc(IntPtr h,IntPtr l);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,IntPtr data);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
}
