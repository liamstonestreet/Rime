using System;using System.IO;using System.Runtime.InteropServices;using Windows.UI.Notifications.Management;
class NotificationProbe {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern int GetCurrentPackageFullName(ref uint length,System.Text.StringBuilder name);
 [STAThread]static void Main(string[] args){string result;uint n=0;int package=GetCurrentPackageFullName(ref n,null);try{var access=UserNotificationListener.Current.GetAccessStatus();result="Package identity status: "+package+Environment.NewLine+"Notification access: "+access;}catch(Exception e){result="Package identity status: "+package+Environment.NewLine+"Notification listener: "+e.GetType().Name+" (0x"+e.HResult.ToString("X8")+") "+e.Message;}if(args.Length==1)File.WriteAllText(args[0],result);else Console.WriteLine(result);}
}
