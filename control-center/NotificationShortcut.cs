using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

static class NotificationShortcut {
 static HwndSource sink;static string requested;static int active;public static string Status="";public static event Action Changed;
 internal static bool Parse(string value,out uint modifiers,out uint key){modifiers=key=0;if(String.IsNullOrWhiteSpace(value))return true;var parts=value.ToLowerInvariant().Split('+').Select(p=>p.Trim()).ToArray();if(parts.Any(p=>p.Length==0))return false;
  foreach(var part in parts){uint bit=part=="win"?8u:part=="alt"?1u:part=="ctrl"||part=="control"?2u:part=="shift"?4u:0u;if(bit!=0){if((modifiers&bit)!=0)return false;modifiers|=bit;continue;}if(key!=0)return false;int f;if(part.Length==1&&Char.IsLetterOrDigit(part[0])&&part[0]<128)key=Char.ToUpperInvariant(part[0]);else if(part.StartsWith("f")&&Int32.TryParse(part.Substring(1),out f)&&f>=1&&f<=24)key=(uint)(0x6F+f);else return false;}
  return key!=0&&(modifiers!=0||key>=0x70&&key<=0x87);
 }
 public static void Start(){sink=new HwndSource(new HwndSourceParameters("Nord notification shortcut"){ParentWindow=new IntPtr(-3)});sink.AddHook(Hook);BarAppearance.Changed+=Apply;Apply();}
 public static void Stop(){BarAppearance.Changed-=Apply;if(sink!=null){if(active!=0)UnregisterHotKey(sink.Handle,active);sink.Dispose();sink=null;}requested=null;active=0;Changed=null;}
 static void Apply(){if(sink==null)return;var value=BarAppearance.Settings.notificationShortcut;if(value==requested)return;uint modifiers,key;if(!Parse(value,out modifiers,out key)){Status="Invalid shortcut. Use modifiers plus a letter, number or F-key. The previous binding stays active.";Notify();return;}if(key==0){if(active!=0)UnregisterHotKey(sink.Handle,active);active=0;requested=value;Status="Shortcut disabled.";Notify();return;}
  int next=active==4401?4402:4401;if(!RegisterHotKey(sink.Handle,next,modifiers|0x4000,key)){Status="Shortcut is already in use or blocked by Windows. The previous binding stays active.";Notify();return;}if(active!=0)UnregisterHotKey(sink.Handle,active);active=next;requested=value;Status="Active: "+value.ToUpperInvariant()+". Opens on the active window's monitor.";Notify();
 }
 static void Notify(){var changed=Changed;if(changed!=null)changed();}
 static IntPtr Hook(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x0312&&w.ToInt32()==active){handled=true;Toggle();}return IntPtr.Zero;}
 static void Toggle(){if(GameWindows.ForegroundGame!=IntPtr.Zero)return;var handle=GetForegroundWindow();var screen=handle!=IntPtr.Zero?System.Windows.Forms.Screen.FromHandle(handle):System.Windows.Forms.Screen.PrimaryScreen;var bounds=screen.Bounds;double scale=ScaleAt(bounds);int bar=ScreenFrame.BarBottom(bounds,scale);NotificationPopup.Toggle(new NotificationAnchor{Pin=true,Left=bounds.Left+1,Right=bounds.Left+5,Top=bounds.Top,Bottom=bar,BarBottom=bar,Scale=scale});}
 internal static double ScaleAt(System.Drawing.Rectangle bounds){try{uint x,y;return GetDpiForMonitor(MonitorFromPoint(new POINT{X=bounds.Left+1,Y=bounds.Top+1},2),0,out x,out y)==0&&x>=48&&x<=480?x/96.0:1;}catch{return 1;}}
 [StructLayout(LayoutKind.Sequential)]struct POINT{public int X,Y;}
 [DllImport("user32.dll")]static extern IntPtr MonitorFromPoint(POINT point,uint flags);
 [DllImport("shcore.dll")]static extern int GetDpiForMonitor(IntPtr h,int type,out uint x,out uint y);
 [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
}
sealed class NotificationPanelControls:StackPanel {
 readonly TextBlock status=new TextBlock{FontSize=10,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};bool watching;
 public NotificationPanelControls(){Margin=new Thickness(0,12,0,4);
  Children.Add(new TextBlock{Text="Notification center",FontSize=12,Margin=new Thickness(0,8,0,5)});var input=new TextBox{Name="ConfigureNotificationShortcut",Text=BarAppearance.Settings.notificationShortcut??"win+shift+n",Padding=new Thickness(9,6,9,6)};Children.Add(input);Children.Add(RiceLayout.Hint("Example: win+shift+n (default), ctrl+shift+n, or F12. Press Enter or leave the field to apply. Blank disables the shortcut. Games suppress the panel; other shortcuts are unchanged."));status.Foreground=ControlCenterTheme.Current.Brush("Muted");Children.Add(status);
  Action save=()=>{uint modifiers,key;if(!NotificationShortcut.Parse(input.Text,out modifiers,out key)){status.Text="Use a modifier combination plus a letter, number or F-key; for example win+shift+n.";return;}BarAppearance.Update(s=>s.notificationShortcut=input.Text.Trim().ToLowerInvariant());Paint();};input.LostKeyboardFocus+=delegate{save();};input.PreviewKeyDown+=(sender,e)=>{if(e.Key==System.Windows.Input.Key.Enter){save();e.Handled=true;}};
  Loaded+=delegate{if(!watching){NotificationShortcut.Changed+=Paint;watching=true;}Paint();};Unloaded+=delegate{if(watching){NotificationShortcut.Changed-=Paint;watching=false;}};Paint();
 }
 void Paint(){status.Text=NotificationShortcut.Status;}
}
