using System;
using System.Runtime.InteropServices;
// Activating the packaged app (rather than directly starting its EXE) preserves
// package identity and allows Windows to present the notification-access prompt.
static class NotificationActivation {
 [ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IManager {
  [PreserveSig]int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string id,[MarshalAs(UnmanagedType.LPWStr)]string args,uint options,out uint pid);
 }
 public static void Launch(string id,string args){var manager=(IManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));try{uint pid;Marshal.ThrowExceptionForHR(manager.ActivateApplication(id,args,2,out pid));}finally{Marshal.FinalReleaseComObject(manager);}}
}
