using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
// Read-only process ancestry; no injected code or access to game memory.
static class GameWindows {
 static readonly object gate=new object();static DateTime sampled;static Dictionary<uint,Entry> processes=new Dictionary<uint,Entry>();
 sealed class Entry {public uint Parent;public string Name;}
 static string Name(string name){return System.IO.Path.GetFileNameWithoutExtension(name??"").ToLowerInvariant();}
 static readonly string[] Launchers={"steam","steamwebhelper","prismlauncher","prism launcher","battle.net","battle.net launcher","epicgameslauncher","eadesktop","ealauncher","ubisoftconnect","upc","crashpad_handler","crashreporter","gameoverlayui","steamerrorreporter","chrome","msedge","firefox","explorer","cmd","powershell","pwsh"};
 internal static bool Matches(string name,string path,IEnumerable<string> parents){name=Name(name);if(Launchers.Contains(name)||name.StartsWith("nord")||name=="zebar")return false;
  if(name=="overwatch"||name=="valorant"||name=="valorant-win64-shipping")return true;
  if((path??"").Replace('/','\\').IndexOf(@"\steamapps\common\",StringComparison.OrdinalIgnoreCase)>=0)return true;
  var ancestors=parents.Select(Name).ToArray();return ancestors.Contains("steam")||((name=="java"||name=="javaw")&&(ancestors.Contains("prismlauncher")||ancestors.Contains("prism launcher")));
 }
 public static bool IsGame(IntPtr window){uint pid;GetWindowThreadProcessId(window,out pid);if(pid==0)return false;lock(gate){
  if(DateTime.UtcNow-sampled>TimeSpan.FromSeconds(1)){processes=Snapshot();sampled=DateTime.UtcNow;}
  Entry entry;if(!processes.TryGetValue(pid,out entry))return false;var parents=new List<string>();uint current=entry.Parent;var seen=new HashSet<uint>{pid};
  for(int i=0;i<32&&seen.Add(current);i++){Entry parent;if(!processes.TryGetValue(current,out parent))break;parents.Add(parent.Name);current=parent.Parent;}
  var path=Path(pid);return Matches(String.IsNullOrEmpty(path)?entry.Name:System.IO.Path.GetFileName(path),path,parents);
 }}
 public static IntPtr ForegroundGame {get{var window=GetForegroundWindow();return window!=IntPtr.Zero&&IsGame(window)?window:IntPtr.Zero;}}
 static string Path(uint pid){var handle=OpenProcess(0x1000,false,pid);if(handle==IntPtr.Zero)return "";try{var text=new StringBuilder(32768);int size=text.Capacity;return QueryFullProcessImageName(handle,0,text,ref size)?text.ToString():"";}finally{CloseHandle(handle);}}
 static Dictionary<uint,Entry> Snapshot(){var result=new Dictionary<uint,Entry>();var snapshot=CreateToolhelp32Snapshot(2,0);if(snapshot==new IntPtr(-1))return result;try{var value=new PROCESSENTRY32{Size=(uint)Marshal.SizeOf(typeof(PROCESSENTRY32))};if(Process32First(snapshot,ref value))do{result[value.Pid]=new Entry{Parent=value.Parent,Name=value.Name};}while(Process32Next(snapshot,ref value));}finally{CloseHandle(snapshot);}return result;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct PROCESSENTRY32 {public uint Size,Usage,Pid;public UIntPtr Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;}
 [DllImport("kernel32.dll")]static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,EntryPoint="Process32FirstW")]static extern bool Process32First(IntPtr h,ref PROCESSENTRY32 entry);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,EntryPoint="Process32NextW")]static extern bool Process32Next(IntPtr h,ref PROCESSENTRY32 entry);
 [DllImport("kernel32.dll")]static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder path,ref int size);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
}
