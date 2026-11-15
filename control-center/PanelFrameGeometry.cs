using System;
using System.Windows;

// Pure outline calculation; WPF path mutation and theme brushes stay in the view.
sealed class PanelFrameGeometry {
 public readonly Point Start,FlatLeft,WingLeft,SideLeft,CornerLeft,Floor,CornerRight,SideRight,WingRight,FlatRight,CoverRight,CoverLeft;
 public readonly Size WingSize,CornerSize;
 PanelFrameGeometry(double width,double depth,double edge,double line,double wing,double radius,bool transparent){
  double y0=edge-line/2,yb=y0+depth,xl=wing+line/2,xr=width-wing-line/2;
  double w=Math.Min(wing*Math.Min(radius,1),depth/2),c=Math.Min(Math.Min(wing*radius,depth/2),(xr-xl)/2);
  Start=new Point(0,y0);FlatLeft=new Point(xl-w,y0);WingLeft=new Point(xl,y0+w);SideLeft=new Point(xl,yb-c);CornerLeft=new Point(xl+c,yb);
  Floor=new Point(xr-c,yb);CornerRight=new Point(xr,yb-c);SideRight=new Point(xr,y0+w);WingRight=new Point(xr+w,y0);FlatRight=new Point(width,y0);
  double cover=transparent?edge:edge-line;CoverRight=new Point(xr+w,cover);CoverLeft=new Point(xl-w,cover);WingSize=new Size(w,w);CornerSize=new Size(c,c);
 }
 public static PanelFrameGeometry Calculate(double width,double depth,double edge,double line,double wing,double radius,bool transparent){return new PanelFrameGeometry(width,Math.Max(0,depth),edge,line,wing,Math.Max(0,radius),transparent);}
}
