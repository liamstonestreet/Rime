using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

static class SpotlightWindows {
 static string Glaze {get{var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"glzr.io\GlazeWM\cli\glazewm.exe");return File.Exists(path)?path:"glazewm.exe";}}
 // Only fixed commands or validated container IDs reach this process helper.
 internal static string Run(string executable,string arguments,int timeout){
  var output=new StringBuilder();
  using(var process=new Process{StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8}}){
   process.OutputDataReceived+=(s,e)=>{if(e.Data!=null)lock(output)if(output.Length<4*1024*1024)output.AppendLine(e.Data);};process.ErrorDataReceived+=(s,e)=>{};
   process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();if(!process.WaitForExit(timeout)){try{process.Kill();}catch{}throw new TimeoutException("Search provider timed out");}process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException("Search provider is unavailable");return output.ToString();
  }
 }
 public static SpotlightResult[] Read(){
  try{
   var response=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Run(Glaze,"query windows",1800));var data=(Dictionary<string,object>)response["data"];
   var found=new List<SpotlightResult>();foreach(Dictionary<string,object> window in (IEnumerable)data["windows"]){
    var process=Convert.ToString(window["processName"]);if(new[]{"NordSpotlight","NordControlCenter","NordWorkspaceIndicator","Lightshot","zebar"}.Contains(process,StringComparer.OrdinalIgnoreCase))continue;
    var id=Convert.ToString(window["id"]);Guid container;if(!Guid.TryParse(id,out container))continue;
    var title=Convert.ToString(window["title"]);found.Add(SpotlightSearch.Result(String.IsNullOrWhiteSpace(title)?process:title,process+" \u00b7 Running window","window",id));
   }return found.ToArray();
  }catch{return NativeWindows();}
 }
 static SpotlightResult[] NativeWindows(){
  var result=new List<SpotlightResult>();EnumWindows((handle,parameter)=>{
   if(!IsWindowVisible(handle)||GetWindow(handle,4)!=IntPtr.Zero)return true;
   var title=new StringBuilder(512);GetWindowText(handle,title,title.Capacity);if(title.Length==0||title.ToString()=="Program Manager")return true;
   uint pid;GetWindowThreadProcessId(handle,out pid);
   try{using(var process=Process.GetProcessById((int)pid)){
    if(new[]{"NordSpotlight","NordControlCenter","NordWorkspaceIndicator","Lightshot","zebar"}.Contains(process.ProcessName,StringComparer.OrdinalIgnoreCase))return true;
    var item=SpotlightSearch.Result(title.ToString(),process.ProcessName+" \u00b7 Running window","window",handle.ToInt64().ToString());item.Handle=handle.ToInt64();item.ProcessId=pid;result.Add(item);
   }}catch{}return true;
  },IntPtr.Zero);return result.ToArray();
 }
 public static void Focus(SpotlightResult result){
  if(result.Handle==0){Guid id;if(!Guid.TryParse(result.Value,out id))throw new InvalidOperationException("Invalid window identifier");Run(Glaze,"command focus --container-id "+id,1800);return;}
  var handle=new IntPtr(result.Handle);uint pid;GetWindowThreadProcessId(handle,out pid);if(pid!=result.ProcessId||!IsWindow(handle))throw new InvalidOperationException("This window has closed");
  if(IsIconic(handle))ShowWindowAsync(handle,9);if(!SetForegroundWindow(handle))throw new InvalidOperationException("Windows could not focus this window");
 }
 public static AppEntry[] RefreshApps(){
  const string script="[Console]::OutputEncoding=[Text.Encoding]::UTF8; $shell=New-Object -ComObject Shell.Application; $folder=$shell.Namespace('shell:AppsFolder'); $apps=@(Get-StartApps | Sort-Object Name,AppID -Unique | ForEach-Object { $target=''; try { $item=$folder.ParseName($_.AppID); if($item){$target=[string]$item.ExtendedProperty('System.Link.TargetParsingPath')} } catch {}; [pscustomobject]@{Name=$_.Name;AppID=$_.AppID;Executable=$target} }); ConvertTo-Json -InputObject $apps -Compress";
  return AppShortcuts.Merge(new JavaScriptSerializer().Deserialize<AppEntry[]>(Run("powershell.exe","-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),10000))??new AppEntry[0],true);
 }
 public static void Lock(){if(!LockWorkStation())throw new InvalidOperationException("Windows could not lock the session");}
 delegate bool EnumProc(IntPtr window,IntPtr value);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int command);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint relation);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder title,int length);
 [DllImport("user32.dll")] static extern bool LockWorkStation();
}
