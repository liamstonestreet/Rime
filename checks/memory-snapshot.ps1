param([string]$OutFile)
$ErrorActionPreference = 'Stop'
# Toolhelp reads parent PIDs without WMI/admin access. Do not count other apps' WebViews.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class RiceProcessTree {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Entry {
  public uint size,usage,pid;public IntPtr heap;public uint module,threads,parent;public int priority;public uint flags;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string name;
 }
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool Process32First(IntPtr snapshot,ref Entry entry);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool Process32Next(IntPtr snapshot,ref Entry entry);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 public static Dictionary<int,int> Parents(){
  var result=new Dictionary<int,int>();var handle=CreateToolhelp32Snapshot(2,0);if(handle==new IntPtr(-1))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  try{var entry=new Entry{size=(uint)Marshal.SizeOf(typeof(Entry))};if(Process32First(handle,ref entry))do{result[(int)entry.pid]=(int)entry.parent;}while(Process32Next(handle,ref entry));}finally{CloseHandle(handle);}return result;
 }
}
'@
$processes = @(Get-Process)
$names = @('glazewm','glazewm-watcher','zebar','NordRuntime','NordControlCenter','NordSpotlight','NordWorkspaceIndicator')
$selected = @($processes | Where-Object { $_.ProcessName -in $names } | ForEach-Object { $_.Id })
$browserTree = @($processes | Where-Object ProcessName -eq zebar | ForEach-Object { $_.Id })
$parents = [RiceProcessTree]::Parents()
do {
  $children = @($parents.Keys | Where-Object { $parents[$_] -in $browserTree -and $_ -notin $browserTree })
  $browserTree += $children
} while ($children.Count)
$selected = @($selected + $browserTree | Select-Object -Unique)
$rows = @(foreach ($process in $processes | Where-Object { $_.Id -in $selected }) {
  try { [pscustomobject]@{ Name=$process.ProcessName; Pid=$process.Id; PrivateMB=[math]::Round($process.PrivateMemorySize64/1MB,2); WorkingSetMB=[math]::Round($process.WorkingSet64/1MB,2); Threads=$process.Threads.Count; Handles=$process.HandleCount } } catch {}
})
$report = [pscustomobject]@{ Captured=(Get-Date).ToString('o'); TotalPrivateMB=[math]::Round(($rows | Measure-Object PrivateMB -Sum).Sum,2); Processes=$rows }
$rows | Sort-Object PrivateMB -Descending | Format-Table -AutoSize
Write-Output ('Total private committed memory: {0} MB (rice helpers and Zebar descendants only).' -f $report.TotalPrivateMB)
if ($OutFile) { $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutFile -Encoding UTF8 }
