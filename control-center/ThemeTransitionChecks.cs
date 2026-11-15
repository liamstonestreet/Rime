using System;
using System.Windows;
using System.Windows.Media;
static class ThemeTransitionChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){
  var data=new ThemeTransitionData{revision="check-timeline",startsAt=1000,duration=450};
  Check(ThemeTransition.Progress(data,900)==0&&ThemeTransition.Progress(data,1000)==0&&Math.Abs(ThemeTransition.Progress(data,1225)-.5)<.000001&&ThemeTransition.Progress(data,1450)==1&&ThemeTransition.Progress(data,9000)==1,"Shared theme clock endpoints/midpoint/late arrival");
  data.duration=0;Check(ThemeTransition.Progress(data,900)==1,"Motion-off theme must settle immediately");
  var middle=ThemeTransition.Mix(Colors.Black,Colors.White,.5);Check(middle.R==128&&middle.G==128&&middle.B==128,"Native and browser sRGB interpolation must match");
  var from=new SolidColorBrush(Colors.Black);var to=new SolidColorBrush(Colors.White);var live=new SolidColorBrush(Colors.Black);
  ThemeTransition.BlendBrush(live,from,to,.5);Check(live.Color==middle,"Retained solid brush interpolation");
  var gradient=new LinearGradientBrush(Colors.Black,Colors.Black,90);ThemeTransition.BlendBrush(gradient,new LinearGradientBrush(Colors.Black,Colors.Black,90),new LinearGradientBrush(Colors.White,Colors.White,90),.5);Check(gradient.GradientStops[0].Color==middle,"Retained gradient interpolation");
  var frost=new SolidColorBrush(Colors.Black);frost.Freeze();var tint=new SolidColorBrush(Colors.Red);var composite=FrostFile.Under(frost,tint);Check(!tint.IsFrozen&&!composite.IsFrozen,"Frost must retain live theme brushes");tint.Color=Colors.Blue;
  var drawing=(DrawingGroup)((DrawingBrush)composite).Drawing;Check(((SolidColorBrush)((GeometryDrawing)drawing.Children[1]).Brush).Color==Colors.Blue,"Frost tint must follow updates without rebuilding its visual tree");
  // A translucent tint over opaque wallpaper must never sample the app behind it.
  var surface=FrostFile.Under(new SolidColorBrush(Color.FromRgb(70,30,100)),new SolidColorBrush(Color.FromArgb(153,25,20,35)));
  byte[][] rendered=new byte[2][];for(int pass=0;pass<2;pass++){
   var visual=new DrawingVisual();using(var context=visual.RenderOpen()){context.DrawRectangle(pass==0?Brushes.Black:Brushes.White,null,new Rect(0,0,12,12));context.DrawRectangle(surface,null,new Rect(0,0,12,12));}
   var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(12,12,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);rendered[pass]=new byte[12*12*4];bitmap.CopyPixels(rendered[pass],12*4,0);
  }
  for(int i=0;i<rendered[0].Length;i++)Check(rendered[0][i]==rendered[1][i],"Wallpaper material changes over light/dark app backgrounds");
  Console.WriteLine("PASS: wallpaper/tint material renders identically over black and white app backgrounds.");
  var defaults=WallpaperPalette.Interpolate(null,null,.5);Check(defaults["--edge"]=="#6b86a3","Nord fallback must remain stable");
  string fixture=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"theme-frost-fixture.png");
  try{
   using(var bitmap=new System.Drawing.Bitmap(8,8)){using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.Clear(System.Drawing.Color.CornflowerBlue);bitmap.Save(fixture,System.Drawing.Imaging.ImageFormat.Png);}
   FrostMaterial.Clear();int revision=FrostMaterial.Revision;var strips=FrostMaterial.Strips;
   FrostMaterial.Render(fixture,0,50,false);
   Check(FrostMaterial.HasPending&&FrostMaterial.Revision==revision&&Object.ReferenceEquals(strips,FrostMaterial.Strips),"Preparing frost must not publish one monitor early");
   var staged=System.IO.Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory,"frost-*.jpg.pending");var stamps=Array.ConvertAll(staged,System.IO.File.GetLastWriteTimeUtc);
   FrostMaterial.Render(fixture,0,50,false);for(int i=0;i<staged.Length;i++)Check(System.IO.File.GetLastWriteTimeUtc(staged[i])==stamps[i],"Applying a warmed wallpaper must reuse prepared frost without rebuilding it");
   FrostMaterial.Commit();Check(!FrostMaterial.HasPending&&FrostMaterial.Revision==revision+1&&FrostMaterial.Strips.Count==System.Windows.Forms.Screen.AllScreens.Length,"All frost strips must publish in one generation");
  }finally{FrostMaterial.Clear();System.IO.File.Delete(fixture);}
  int frames=0;Action observed=()=>frames++;ThemeTransition.Frame+=observed;
  try{
   ThemeTransition.Use(new ThemeTransitionData{revision="native-clock-check",startsAt=ThemeTransition.Now+25,duration=60});
   var fade=ThemeTransition.Crossfade(new SolidColorBrush(Colors.Black),new SolidColorBrush(Colors.White));
   var group=(DrawingGroup)((DrawingBrush)fade).Drawing;Check(group.Children.Count==2,"Frost fade needs old and new layers while active");
   Pump(160);Check(!ThemeTransition.Active&&group.Children.Count==1,"Completed native fade must release the old image layer");
   int completedFrames=frames;Pump(60);Check(frames==completedFrames,"Shared animation clock must stop when settled");
  }finally{ThemeTransition.Frame-=observed;}
  var read=GlazeBorders.ReadConfig;var write=GlazeBorders.WriteConfig;var execute=GlazeCommand.Execute;var reload=GlazeBorders.ReloadNative;
  try{
   string config="window_effects:\n  focused_window:\n    border:\n      color: '#000000'\n  other_windows:\n    border:\n      color: '#000000'\nkeybindings: []\n";int writes=0,commands=0;
   GlazeBorders.ReadConfig=()=>config;GlazeBorders.WriteConfig=text=>{config=text;writes++;};GlazeCommand.Execute=args=>{commands++;throw new Exception("Border persistence must not invoke GlazeWM: "+args);};
   GlazeBorders.Sync(null);GlazeBorders.Sync(null);
   Check(writes==1&&commands==0&&config.Contains("'#6b86a3'")&&config.EndsWith("keybindings: []\n"),"Border persistence must save once, preserve unrelated config and never reload/refocus GlazeWM");
   int reloads=0;GlazeBorders.ReloadNative=()=>reloads++;
   var finalPalette=new System.Collections.Generic.Dictionary<string,string>{{"--edge","#123456"},{"--nord-0","#101010"}};
   ThemeTransition.Use(new ThemeTransitionData{revision="border-immediate-check",startsAt=ThemeTransition.Now,duration=300,to=finalPalette});
   GlazeBorders.Sync(finalPalette,true);GlazeBorders.Sync(finalPalette,true);
   Check(reloads==1&&writes==2&&config.Contains("'#123456'"),"Final borders must persist and refresh native configuration once during the shared transition");
   GlazeBorders.Sync(null,true);Check(reloads==1&&writes==2,"Stale queued border colors must not overwrite the new palette");  }finally{GlazeBorders.ReadConfig=read;GlazeBorders.WriteConfig=write;GlazeCommand.Execute=execute;GlazeBorders.ReloadNative=reload;}
  Console.WriteLine("PASS: shared theme clock, motion-off, retained brushes, warmed frost reuse, idle-clock cleanup and border persistence with one mocked native refresh per palette and stale-write rejection. No live borders changed.");
 }
 static void Pump(int milliseconds){var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);}
}
