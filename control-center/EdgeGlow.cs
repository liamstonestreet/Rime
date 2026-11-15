using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

/// <summary>
/// The "Glow" edge style for WPF surfaces: a blurred copy of the edge line, clipped to the surface's own
/// side, the way the bar's glow is an inner shadow that shines into the bar. Clipping a container (not the
/// path) cuts the blur too, so nothing spills onto the desktop or across a neighbouring surface's line.
/// </summary>
static class EdgeGlow {
 public static FrameworkElement Inside(Geometry line,Brush stroke,double thickness,Geometry side){
  // WPF's radius spans about three Gaussian sigmas; CSS drop-shadow(0 0 5px)
  // uses a 5px sigma. Keep the source stroke narrow too, rather than tripling it.
  var path=new Path{Data=line,Opacity=.85,IsHitTestVisible=false,Effect=new BlurEffect{Radius=15},HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
  var host=new Grid{Clip=side,IsHitTestVisible=false};host.Children.Add(path);
  SetStroke(host,stroke,thickness);return host;
 }
 /// <summary>The same source line as Chromium, clipped after blur to the surface side.</summary>
 public static void SetStroke(FrameworkElement glow,Brush stroke,double thickness){var path=(Path)((Grid)glow).Children[0];path.Stroke=stroke;path.StrokeThickness=thickness;}
}
