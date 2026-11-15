using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

interface IPowerActions {void Execute(string action);}
sealed class PowerActionDispatcher {
 readonly IPowerActions actions;
 public PowerActionDispatcher(IPowerActions actions){this.actions=actions;}
 public void Execute(string action,bool preview){if(action==null||preview)return;if(Array.IndexOf(PowerMenu.Actions,action)<0)throw new ArgumentException("Unknown power action");actions.Execute(action);}
}
sealed class WindowsPowerActions:IPowerActions {
 [DllImport("user32.dll",SetLastError=true)]static extern bool LockWorkStation();
 [DllImport("powrprof.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)] bool hibernate,[MarshalAs(UnmanagedType.U1)] bool force,[MarshalAs(UnmanagedType.U1)] bool disableWake);
 [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool LookupPrivilegeValue(string system,string name,out long luid);
 [StructLayout(LayoutKind.Sequential,Pack=1)] struct Privilege {public uint count;public long luid;public uint attributes;}
 [DllImport("advapi32.dll",SetLastError=true)] static extern bool AdjustTokenPrivileges(IntPtr token,bool disable,ref Privilege state,uint size,IntPtr previous,IntPtr returned);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 public static string SaverPath(){var configured=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop","SCRNSAVE.EXE",null) as string;configured=Environment.ExpandEnvironmentVariables((configured??"").Trim('"'));return Path.IsPathRooted(configured)&&File.Exists(configured)&&Path.GetExtension(configured).Equals(".scr",StringComparison.OrdinalIgnoreCase)?configured:Path.Combine(Environment.SystemDirectory,"scrnsave.scr");}
 public void Execute(string action){if(action=="Lock"){if(!LockWorkStation())throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return;}if(action=="Shutdown"||action=="Restart"){Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"shutdown.exe"),action=="Shutdown"?"/s /t 0":"/r /t 0"){UseShellExecute=false,CreateNoWindow=true});}else if(action=="Screensaver"){Process.Start(new ProcessStartInfo(SaverPath(),"/s"){UseShellExecute=true});}else if(action=="Sleep"){IntPtr token;if(!OpenProcessToken(Process.GetCurrentProcess().Handle,0x28,out token))throw new System.ComponentModel.Win32Exception();try{long luid;if(!LookupPrivilegeValue(null,"SeShutdownPrivilege",out luid))throw new System.ComponentModel.Win32Exception();var privilege=new Privilege{count=1,luid=luid,attributes=2};if(!AdjustTokenPrivileges(token,false,ref privilege,0,IntPtr.Zero,IntPtr.Zero)||Marshal.GetLastWin32Error()!=0)throw new System.ComponentModel.Win32Exception();if(!SetSuspendState(false,false,false))throw new System.ComponentModel.Win32Exception();}finally{CloseHandle(token);}}}
}
