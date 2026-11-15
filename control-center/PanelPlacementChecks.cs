using System;
using System.Drawing;

static class PanelPlacementChecks {
 internal sealed class Monitors:IPanelMonitorSource {
  public Rectangle Bounds=new Rectangle(0,0,1920,1080);
  public MonitorContext At(int x,int y){return new MonitorContext(Bounds);}
  public MonitorContext Primary {get{return new MonitorContext(Bounds);}}
 }
 internal sealed class Target:IPanelPositionTarget {
  public double Width {get;set;}public System.Drawing.Point Origin;public int Moves;public Action Moved;
  public Target(){Width=759;}
  public bool TryOrigin(out System.Drawing.Point origin){origin=Origin;return true;}
  public void Position(int x,int y){Origin=new System.Drawing.Point(x,y);Moves++;if(Moved!=null)Moved();}
 }
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 public static void Run(){var monitors=new Monitors();var target=new Target();var placement=new PanelPlacementController(monitors,target,4);
  placement.KeepPosition();Check(target.Moves==0,"Unplaced window must not move");
  foreach(var bounds in new[]{new Rectangle(0,0,1920,1080),new Rectangle(-2560,-200,2560,1440)})foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0}){
   monitors.Bounds=bounds;double center=bounds.Left+bounds.Width/2.0,bottom=bounds.Top+50*scale;
   placement.Place(center,bottom,scale,759);Check(target.Origin.X==(int)Math.Round(center-target.Width*scale/2)&&target.Origin.Y==(int)Math.Round(bottom-4*scale),"Physical anchor placement shifted");
   double oldCenter=placement.X+target.Width*scale/2,oldBottom=placement.Y+4*scale;placement.ApplyWidth(1100);
   Check(Math.Abs(placement.X+target.Width*scale/2-oldCenter)<=.5&&placement.Y+4*scale==oldBottom,"Width edit moved anchor");
   Check((placement.MaxHeight(50)+4)*scale<=bounds.Height/2.0+.001,"Monitor height limit wrong");
   Check(target.Width*scale<=bounds.Width+.001,"Width exceeds monitor");
   int before=target.Moves;placement.KeepPosition();Check(before==target.Moves,"Stable position should be idle");
   target.Origin=new System.Drawing.Point(placement.X+5,placement.Y+5);target.Moved=placement.KeepPosition;placement.KeepPosition();target.Moved=null;Check(target.Moves==before+1&&target.Origin.Y==placement.Y,"Position correction must be reentrancy safe");
  }
  Check(PanelPlacementController.ConstrainWidth(5000,320,2)==136,"Narrow-monitor width constraint failed");
  Check(PanelPlacementController.HeightLimit(1080,1,0,4)==266&&PanelPlacementController.HeightLimit(1080,1,100,4)==968,"Height setting clamp failed");
  foreach(ControlCenterTab id in Enum.GetValues(typeof(ControlCenterTab)))Check(ControlCenterTabDefinition.All[ControlCenterTabDefinition.Index(id)].Id==id,"Tab identity/order mapping failed");
  foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0})foreach(double radius in new[]{0.0,1.0,2.0})foreach(double depth in new[]{0.0,1.0,300.0}){
   double line=Math.Floor(3*scale+1e-6)/scale;var frame=PanelFrameGeometry.Calculate(759,depth,4,line,24,radius,true);
   Check(frame.FlatLeft.X+frame.WingRight.X==759&&frame.CornerLeft.X+frame.Floor.X==759,"Outline lost left/right symmetry");
   Check(frame.WingSize.Width<=depth/2&&frame.CornerSize.Width<=depth/2&&frame.CoverLeft.Y==4,"Outline radius/cover limits changed");
   if(depth==0)Check(frame.WingSize.Width==0&&frame.CornerSize.Width==0&&frame.Start.Y==4-line/2,"Closed outline must remain a flat bar join");
  }
  Console.WriteLine("PASS: pure frame geometry preserves symmetry, fractional-DPI joins, radius limits and flat closed state.");
  Console.WriteLine("PASS: extracted placement controller across positive/negative monitor origins and five DPI scales, anchor-preserving width, monitor limits and reentrant correction. Fake position target; no native moves/focus/workspace changes.");
 }
}
