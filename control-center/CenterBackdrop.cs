using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

/// <summary>
/// The frosted glass behind the control center: the part of its monitor's blurred wallpaper that lies
/// under the window (<see cref="FrostFile"/>), clipped to the shell beneath the panel's tint. It is
/// only an image, so the rounded corners and wings come for free and it lines up with the bar's frost.
/// </summary>
sealed class CenterBackdrop {
 public readonly Rectangle Layer;
 string painted;

 public CenterBackdrop(Geometry shape){
  Layer=new Rectangle{Clip=shape,IsHitTestVisible=false,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed};
  // The retained image brush already keeps its texture; another full-panel raster is redundant.
 }

 /// <summary>For a window at (x, y) physical pixels, width x height DIPs at this DPI scale.</summary>
 public void Paint(int x,int y,double width,double height,double scale){
  var key=x+"|"+y+"|"+width+"|"+height+"|"+scale+"|"+FrostMaterial.Revision;
  if(key==painted){Layer.Visibility=Layer.Fill==null?Visibility.Collapsed:Visibility.Visible;return;}
  painted=key;
  var brush=FrostFile.Behind(x,y,width*scale,height*scale);
  Layer.Fill=ThemeTransition.Crossfade(Layer.Fill,brush);Layer.Width=width;Layer.Height=height;
  Layer.Visibility=brush==null?Visibility.Collapsed:Visibility.Visible;
 }

 public void Hide(){Layer.Visibility=Visibility.Collapsed;Layer.Fill=null;painted=null;}
}
