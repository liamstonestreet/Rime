using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

static class FrameGlowChecks {
 static readonly Brush stroke=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f18fb5"));
 static BitmapSource Render(double scale,double radius,bool reference,bool glow){
  const double w=640,h=180,t=14;double top=Math.Floor(50*scale)/scale,line=Math.Floor(3*scale)/scale;
  var shape=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,top,w,h-top)),new RectangleGeometry(new Rect(t,top,w-2*t,h-top-t),radius,radius));
  Geometry side=reference?(Geometry)new CombinedGeometry(GeometryCombineMode.Union,shape,new RectangleGeometry(new Rect(0,0,w,top))):shape;
  var root=new Grid{Width=w,Height=h};if(glow)root.Children.Add(EdgeGlow.Inside(ScreenFrame.Outline(w,h,top,t,radius,line,true),stroke,line,side));
  root.Children.Add(new System.Windows.Shapes.Path{Data=ScreenFrame.Outline(w,h,top,t,radius,line,reference),Stroke=stroke,StrokeThickness=line,Clip=reference?new RectangleGeometry(new Rect(0,0,w,h)):new RectangleGeometry(new Rect(0,top,w,h-top))});root.Measure(new Size(w,h));root.Arrange(new Rect(0,0,w,h));root.UpdateLayout();
  var bitmap=new RenderTargetBitmap((int)(w*scale),(int)(h*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(root);bitmap.Freeze();return bitmap;
 }
 static byte[] Pixels(BitmapSource bitmap){var result=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(result,bitmap.PixelWidth*4,0);return result;}
 static void Save(string path,BitmapSource image){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))png.Save(file);}
 internal static void Run(){
  foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0}){
   foreach(var monitor in new[]{new System.Drawing.Rectangle(0,0,2560,1440),new System.Drawing.Rectangle(-1920,-100,1920,1080)}){
    var predicted=ScreenFrame.StartupBarBounds(monitor,scale);var actual=ScreenFrame.DrawableBarBounds(new System.Drawing.Rectangle(monitor.Left,monitor.Top,monitor.Width,(int)Math.Ceiling(50*scale)),scale);
    if(predicted!=actual)throw new Exception("Startup frame must use the same bar boundary before WebView2 appears");
   }

   foreach(double height in new[]{0.0,1.0,4.0,8.0,9.0,1080.0}){var surface=ScreenFrame.FrameSurface(1920,height,scale);if(height<=8/scale?!surface.IsEmpty:surface.IsEmpty||surface.Height<=0||Math.Abs(surface.Bottom-height)>.00001)throw new Exception("Startup frame layout must defer undersized staging windows without negative rectangles");}
   if(!ScreenFrame.FrameSurface(1,1,scale).IsEmpty||!ScreenFrame.FrameSurface(0,1080,scale).IsEmpty)throw new Exception("Staging HWND must not draw a full frame");
  }
  foreach(var area in new[]{new System.Drawing.Rectangle(0,50,1920,1030),new System.Drawing.Rectangle(-2560,63,2560,1377)}){
   if(ScreenFrame.PlacementMatches(area,new System.Drawing.Rectangle(-32000,-32000,1,1)))throw new Exception("Staging bounds must trigger startup placement repair");
   var placed=new System.Drawing.Rectangle(area.Left,area.Top-8,area.Width,area.Height+8);
   if(!ScreenFrame.PlacementMatches(area,placed))throw new Exception("Correct physical bounds must avoid repeated placement");
   placed.Width--;if(ScreenFrame.PlacementMatches(area,placed))throw new Exception("Late WPF bounds changes must be repaired");
  }
  Console.WriteLine("PASS: startup placement detects staging and overwritten bounds on both monitor origins; settled bounds remain unchanged.");
  Console.WriteLine("PASS: startup screen-frame staging sizes defer safely across five DPI scales; real monitor bounds remain intact.");
  var folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"frame-glow");if(!Directory.Exists(folder)){Console.WriteLine("SKIP: cross-renderer glow images not present; run node checks/frame-glow/check.mjs first.");return;}
  if(File.Exists(System.IO.Path.Combine(folder,"thickness-1-50.png"))){
   if(ScreenFrame.DrawableBarBounds(new System.Drawing.Rectangle(0,0,2560,63),1.25).Bottom!=62||ScreenFrame.DrawableBarBounds(new System.Drawing.Rectangle(2560,0,1920,50),1).Bottom!=50)throw new Exception("CC and screen-frame anchors must share the bar's drawable bottom across monitors");
   int tested=0;foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0})foreach(int host in new System.Collections.Generic.HashSet<int>(new[]{(int)Math.Floor(50*scale),(int)Math.Ceiling(50*scale)})){
    var image=new BitmapImage(new Uri(System.IO.Path.Combine(folder,"thickness-"+scale+"-"+host+".png")));var pixels=Pixels(image);int column=image.PixelWidth/2,bottom=(int)Math.Floor(50*scale);double coverage=0;
    int solid=0,first=-1,last=-1;for(int y=0;y<bottom;y++){int alpha=pixels[(y*image.PixelWidth+column)*4+3];coverage+=alpha/255.0;if(alpha>=250){solid++;if(first<0)first=y;last=y;}}
    double expected=Math.Floor(3*scale+1e-6);if(Math.Abs(coverage-expected)>.12||solid!=expected||first!=bottom-(int)expected||last!=bottom-1)throw new Exception("Bar border must have the full opaque, bottom-aligned CC stroke at scale "+scale+", host height "+host+": "+solid+" solid pixels, coverage "+coverage.ToString("F3")+" vs "+expected);tested++;
   }Console.WriteLine("PASS: "+tested+" fractional-DPI host-height cases preserve the full CC-matching stroke at 100/125/150/175/200%.");
  }
  double worst=0;int cases=0;
  foreach(double scale in new[]{1.0,1.25,1.5,2.0})foreach(double radius in new[]{0.0,16.0,32.0})foreach(string edge in new[]{"flat","glow"}){
   string name=edge+"-"+radius+"-"+scale;var path=System.IO.Path.Combine(folder,"bar-"+name+".png");if(!File.Exists(path))throw new Exception("Missing frame-corner browser fixture: "+path);var css=new BitmapImage(new Uri(path));var reference=Render(scale,radius,true,edge=="glow");var native=Render(scale,radius,false,edge=="glow");var actual=Pixels(css);var expected=Pixels(reference);int width=reference.PixelWidth,top=(int)Math.Floor(50*scale);double error=0;int count=0;
   for(int y=Math.Max(0,top-(int)(14*scale));y<top;y++)for(int x=0;x<width;x++){if(x>(14+radius+24)*scale&&x<width-(14+radius+24)*scale)continue;error+=Math.Abs(actual[(y*width+x)*4+3]-expected[(y*width+x)*4+3]);count++;}
   double mean=error/count;worst=Math.Max(worst,mean);
   if(mean>6)throw new Exception("Bar/frame corner glow mismatch: "+name+" mean alpha error "+mean.ToString("F2"));
   var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(27,31,39)),null,new Rect(0,0,640,180));drawing.DrawImage(css,new Rect(0,0,640,180));drawing.DrawImage(native,new Rect(0,0,640,180));}var joined=new RenderTargetBitmap(width,reference.PixelHeight,96*scale,96*scale,PixelFormats.Pbgra32);joined.Render(visual);Save(System.IO.Path.Combine(folder,"joined-"+name+".png"),joined);cases++;
  }
  Console.WriteLine("PASS: "+cases+" bar/frame corner joins match a continuous native reference across both sides, 100/125/150/200% and 0/100/200% radius; worst mean alpha difference "+worst.ToString("F2")+"/255.");
 }
}
