using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Film grain: a tile of light and dark specks, the same pattern the bar draws (grainTile in
/// src/lib/appearance.ts uses this generator and seed), so bar and panel share one texture.
/// </summary>
static class Grain {
 const int Size=128;
 static readonly BitmapSource tile=Create();

 static BitmapSource Create(){
  var pixels=new byte[Size*Size*4];uint seed=7;
  for(int i=0;i<Size*Size;i++){
   seed+=0x6D2B79F5;uint t=seed;t=(t^(t>>15))*(t|1);t=(t+(t^(t>>7))*(t|61))^t;double v=(t^(t>>14))/4294967296.0;
   byte alpha=(byte)(Math.Abs(v-.5)*2*255),white=v>.5?alpha:(byte)0; // premultiplied
   pixels[i*4]=pixels[i*4+1]=pixels[i*4+2]=white;pixels[i*4+3]=alpha;
  }
  var bitmap=BitmapSource.Create(Size,Size,96,96,PixelFormats.Pbgra32,null,pixels,Size*4);bitmap.Freeze();return bitmap;
 }

 /// <summary>Tiles one speck per physical pixel at this DPI scale.</summary>
 public static Brush Brush(double scale,double screenX=0,double screenY=0){
  // All surfaces sample the same monitor-anchored tile, including negative monitor origins.
  double x=-((screenX%Size+Size)%Size)/scale,y=-((screenY%Size+Size)%Size)/scale;
  var brush=new ImageBrush(tile){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(x,y,Size/scale,Size/scale),Stretch=Stretch.Fill};
  RenderOptions.SetBitmapScalingMode(brush,BitmapScalingMode.NearestNeighbor);brush.Freeze();return brush;
 }
}
