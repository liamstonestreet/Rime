using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class FocusBorderChecks {
 sealed class Fixture:Form {
  public Fixture(){ShowInTaskbar=false;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=new System.Drawing.Rectangle(-32000,-32000,2,2);}
  protected override bool ShowWithoutActivation {get{return true;}}
 }
 public static void Run(){
  var foreground=GetForegroundWindow();
  using(var target=new Fixture())using(var border=new Fixture())using(var overlay=new Fixture()){
   target.Show();border.Show();overlay.Show();
   foreach(bool topmost in new[]{false,true,false}){
    SetWindowPos(target.Handle,new IntPtr(topmost?-1:-2),0,0,0,0,0x0013);
    SetWindowPos(overlay.Handle,new IntPtr(-1),0,0,0,0,0x0013);
    FocusBorder.PlaceBehind(border.Handle,target.Handle,-32000,-32000,2,2);
    Check(GetWindow(target.Handle,2)==border.Handle,"border must sit immediately behind target");
    Check(Above(overlay.Handle,border.Handle),"topmost popup must cover border");
    Check(((GetWindowLong(border.Handle,-20)&8)!=0)==topmost,"border must follow target's topmost band");
    // Simulate an ordinary app moving above the target, then a target being raised again.
    if(!topmost){
     SetWindowPos(overlay.Handle,new IntPtr(-2),0,0,0,0,0x0013);
     SetWindowPos(overlay.Handle,IntPtr.Zero,0,0,0,0,0x0013);
     FocusBorder.PlaceBehind(border.Handle,target.Handle,-32000,-32000,2,2);
     Check(Above(overlay.Handle,border.Handle),"normal popup must cover border");
     SetWindowPos(target.Handle,IntPtr.Zero,0,0,0,0,0x0013);
     FocusBorder.PlaceBehind(border.Handle,target.Handle,-32000,-32000,2,2);
     Check(GetWindow(target.Handle,2)==border.Handle,"raised target must retain its decoration");
    }
    FocusBorder.PlaceBehind(border.Handle,target.Handle,-31990,-31990,2,2);
    Check(GetWindow(target.Handle,2)==border.Handle,"move must preserve stacking");
   }
   Check(GetForegroundWindow()==foreground,"decoration must never activate");
  }
  Console.WriteLine("PASS: native focus-border stacking behind normal/topmost targets, popup occlusion, band changes, raising and movement; no activation.");
 }
 static bool Above(IntPtr first,IntPtr second){for(var h=GetWindow(second,3);h!=IntPtr.Zero;h=GetWindow(h,3))if(h==first)return true;return false;}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr h,uint command);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int index);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
}
