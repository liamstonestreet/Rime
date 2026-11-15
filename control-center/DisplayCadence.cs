using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

// Animation rate is a display setting, not a hand-entered rice preference.
static class DisplayCadence {
 [StructLayout(LayoutKind.Explicit,Size=220,CharSet=CharSet.Unicode)] struct Mode {
  [FieldOffset(68)] public ushort size;
  [FieldOffset(184)] public uint frequency;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplaySettings(string device,int mode,ref Mode settings);
 public static int Refresh(string device){var mode=new Mode{size=220};return EnumDisplaySettings(device,-1,ref mode)&&mode.frequency>1&&mode.frequency<=1000?(int)mode.frequency:60;}
 public static int Rate(FrameworkElement owner){var window=owner as Window??Window.GetWindow(owner);var handle=window==null?IntPtr.Zero:new WindowInteropHelper(window).Handle;return Refresh((handle==IntPtr.Zero?System.Windows.Forms.Screen.PrimaryScreen:System.Windows.Forms.Screen.FromHandle(handle)).DeviceName);}
 public static DoubleAnimation At(FrameworkElement owner,DoubleAnimation animation){Timeline.SetDesiredFrameRate(animation,Rate(owner));return animation;}
 public static void Initialize(){int rate=60;foreach(var screen in System.Windows.Forms.Screen.AllScreens)rate=Math.Max(rate,Refresh(screen.DeviceName));Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline),new FrameworkPropertyMetadata((int?)rate));}
}
