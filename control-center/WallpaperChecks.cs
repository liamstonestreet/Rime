using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

static class WallpaperChecks {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static BitmapSource Landscape(int index){
  var colors=new[]{"#475F7C","#A57E77","#647F7D","#877E9A","#526B8E","#8F9982","#A28D70","#607E8B"};var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){
   var sky=(Color)ColorConverter.ConvertFromString(colors[index%colors.Length]);drawing.DrawRectangle(new LinearGradientBrush(sky,Color.FromRgb(30,40,60),90),null,new Rect(0,0,640,360));drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(170,235,225,200)),null,new Point(450-index%3*60,85),29,29);
   for(int layer=0;layer<3;layer++){var shape=new StreamGeometry();using(var path=shape.Open()){path.BeginFigure(new Point(0,360),true,true);path.LineTo(new Point(0,210+layer*22),true,false);for(int x=0;x<=800;x+=80)path.LineTo(new Point(x,170+layer*40+(x/80%2==0?index*3:-36)),true,false);path.LineTo(new Point(640,360),true,false);}drawing.DrawGeometry(new SolidColorBrush(Color.FromRgb((byte)(35-layer*5),(byte)(57-layer*7),(byte)(78-layer*8))),null,shape);}
  }var bitmap=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
 }
 static void Set(object target,string name,object value){target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);}
 internal static void Run(Window owner){
  var samples=Enumerable.Range(0,8).Select(Landscape).ToArray();var files=Enumerable.Range(0,80).Select(i=>Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Wallpaper "+i.ToString("00")+".png")).ToList();int requests=0;
  Action<string,Image,int> load=(path,image,width)=>{requests++;int number=int.Parse(Path.GetFileNameWithoutExtension(path).Split(' ').Last());image.Source=samples[number%samples.Length];};
  foreach(var style in new[]{"showcase","filmstrip"}){
   requests=0;var view=new WallpaperVariants(style,files,load);view.Select(3,false);Check(requests<12,"New wallpaper styles must not decode the whole library");Check(view.VisibleIndices.Length==(style=="showcase"?3:7),"Unexpected wallpaper preview count");
   for(int i=0;i<40;i++){view.Select(i,true);Check(view.RetainedCards<=(style=="showcase"?9:13),"Rapid browsing must keep the card pool bounded");}view.Settle();Check(view.SelectedIndex==39&&view.RetainedCards<=(style=="showcase"?5:9),"Wallpaper animations must settle at the newest selection");view.Select(0,false);Check(view.VisibleIndices.Contains(79),"Carousel neighbors must wrap at the library ends");
   int beforeRelease=requests;view.ReleaseImages();view.Select(0,false);Check(requests>beforeRelease,"Released hidden previews must reload when reopened on the same selection");
   foreach(int count in new[]{0,1,2}){var small=new WallpaperVariants(style,files.Take(count).ToList(),load);small.Select(0,false);small.Settle();Check(small.RetainedCards<=count,"Small wallpaper libraries must not create duplicate cards");}
  }
  foreach(var style in AppearanceSettings.WallpaperIndicatorStyles){
   var picker=new WallpaperPicker{LoadThumbnail=load};int applied=0,prepared=0;string chosen=null;picker.PrepareWallpaper=path=>{prepared++;};picker.ApplyWallpaper=path=>{applied++;chosen=path;return Task.FromResult(0);};var fill=typeof(WallpaperPicker).GetMethod("Fill",BindingFlags.NonPublic|BindingFlags.Instance);
   Action<string> open=current=>{Set(picker,"holding",true);fill.Invoke(picker,new object[]{files,current,style});Set(picker,"open",true);};
   open(files[3]);picker.Release();Check(applied==0,"Opening and releasing without browsing must not apply wallpaper");
   open(files[3]);picker.Step(files,files[3],1);picker.Release();Check(applied==1&&chosen==files[4]&&prepared>0,"Release must apply exactly the selected wallpaper");
   open(files[3]);picker.Step(files,files[3],1);picker.Cancel();picker.Release();Check(applied==1,"Escape/cancel must never apply wallpaper");
   open(files[3]);picker.Step(files,files[3],1);picker.Step(files,files[3],-1);picker.Release();Check(applied==1,"Returning to the original wallpaper must not reapply it");
   open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"not-in-library.jpg"));picker.Release();Check(applied==1,"Unknown current wallpaper must not accidentally apply the first image on release");
   open(files[3]);var frame=(FrameworkElement)picker.Content;System.Windows.Documents.TextElement.SetFontFamily(frame,owner.FontFamily);System.Windows.Documents.TextElement.SetForeground(frame,ControlCenterTheme.Current.Brush("Text"));frame.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));frame.Arrange(new Rect(frame.DesiredSize));frame.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(frame.DesiredSize.Width*2),(int)Math.Ceiling(frame.DesiredSize.Height*2),192,192,PixelFormats.Pbgra32);image.Render(frame);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-"+style+".png")))png.Save(file);
   var fit=(Viewbox)typeof(WallpaperPicker).GetField("fit",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(picker);fit.MaxWidth=480;fit.MaxHeight=220;fit.InvalidateMeasure();fit.Measure(new Size(480,220));Check(fit.DesiredSize.Width<=480&&fit.DesiredSize.Height<=220,"Wallpaper style must scale down for smaller/scaled monitors");picker.Cancel();picker.Close();
  }
  Console.WriteLine("PASS: Classic/Showcase/Filmstrip browsing and release/cancel behavior, unknown-current safety, carousel wraparound, small libraries, bounded thumbnail pools, rapid-change settling and fit-to-monitor scaling. Synthetic previews only; no wallpaper changed.");
 }
}
