using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

static class WallpaperCategoryChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Set(object target,string name,object value){typeof(WallpaperPicker).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);}
 public static void Run(){
  var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-categories-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var previousStyle=BarAppearance.Settings.wallpaperIndicatorStyle;
  try{
   Directory.CreateDirectory(Path.Combine(root,"Nature","Mountains"));Directory.CreateDirectory(Path.Combine(root,"Genshin"));Directory.CreateDirectory(Path.Combine(root,"Empty"));
   var names=new[]{"root.jpg",@"Nature\10.png",@"Nature\2.jpg",@"Nature\Mountains\lake.bmp",@"Genshin\1.jpeg"};foreach(var name in names)File.WriteAllText(Path.Combine(root,name),"");File.WriteAllText(Path.Combine(root,"Nature","notes.txt"),"ignored");
   var groups=WallpaperLibrary.Scan(root);Check(groups[0].Key=="all"&&groups[0].Images.Count==5,"All must include every supported image across categories");Check(groups.Count==4&&groups.Last().Key=="uncategorized","Root images need Uncategorized; empty folders should not become categories");var nature=groups.Single(g=>g.Name=="Nature");Check(nature.Images.Count==3&&Path.GetFileName(nature.Images[0])=="2.jpg"&&nature.Images.Any(p=>p.EndsWith("lake.bmp")),"Category scans must include nested folders and numeric sorting");
   foreach(var style in AppearanceSettings.WallpaperIndicatorStyles){BarAppearance.Update(s=>s.wallpaperIndicatorStyle=style);
    var state=Path.Combine(root,"browser-"+style+".json");var picker=new WallpaperPicker{LibraryFolder=()=>root,BrowserStatePath=state,LoadThumbnail=(path,image,width)=>{},PrepareWallpaper=path=>{}};int applied=0;string chosen=null;picker.ApplyWallpaper=path=>{applied++;chosen=path;return Task.FromResult(0);};
    typeof(WallpaperPicker).GetMethod("ConfigureLibrary",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(picker,new object[]{groups[0].Images});
    var current=Path.Combine(root,"root.jpg");typeof(WallpaperPicker).GetMethod("Fill",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(picker,new object[]{groups[0].Images,current,style});Set(picker,"holding",true);Set(picker,"open",true);
    int position=groups.FindIndex(g=>g.Name=="Nature");picker.ChooseCategory(position);Check(picker.CategoryName=="Nature"&&picker.SelectedWallpaper==nature.Images[0],"Selecting a category must filter the carousel");picker.Step(null,null,1);var remembered=picker.SelectedWallpaper;picker.ChooseCategory(0);picker.ChooseCategory(position);Check(picker.SelectedWallpaper==remembered,"Each category must remember its selected image");
    var frame=(System.Windows.FrameworkElement)picker.Content;frame.Measure(new System.Windows.Size(double.PositiveInfinity,double.PositiveInfinity));frame.Arrange(new System.Windows.Rect(frame.DesiredSize));frame.UpdateLayout();var preview=new RenderTargetBitmap((int)Math.Ceiling(frame.DesiredSize.Width),(int)Math.Ceiling(frame.DesiredSize.Height),96,96,System.Windows.Media.PixelFormats.Pbgra32);preview.Render(frame);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(preview));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-categories-"+style+".png")))png.Save(output);
    string copied=null;picker.CopyWallpaper=path=>{copied=path;return Task.FromResult(0);};picker.CopySelected().GetAwaiter().GetResult();Check(copied==remembered&&copied!=current&&applied==0&&picker.SelectedWallpaper==remembered,"Copy must use the highlighted category image without applying or moving selection");
    picker.CopyWallpaper=path=>{throw new IOException("clipboard busy");};picker.CopySelected().GetAwaiter().GetResult();Check(applied==0&&picker.IsBrowsing,"Copy failure must leave browsing intact");
    picker.Release();Check(applied==1&&chosen==remembered,"Release must apply a changed path even when category indices match");
    var saved=WallpaperBrowserState.Load(state,root);Check(saved.category==nature.Key&&saved.positions[nature.Key]==remembered,"Category and image positions must persist");
    Set(picker,"holding",true);Set(picker,"open",true);picker.ChangeCategory(1);picker.Cancel();picker.Release();Check(applied==1,"Category navigation followed by Escape must not apply wallpaper");picker.Close();
   }
   var empty=WallpaperLibrary.Scan(Path.Combine(root,"missing"));Check(empty.Count==1&&empty[0].Images.Count==0,"Missing folders need a safe empty All category");
   Console.WriteLine("PASS: wallpaper category discovery, All/root/nested images, numeric sorting, all three styles, remembered positions, release/cancel and persistent state. Selected-image clipboard routing/error recovery verified with a fake writer; no real clipboard or wallpaper changed.");
  }finally{BarAppearance.Update(s=>s.wallpaperIndicatorStyle=previousStyle);var safe=Path.GetFullPath(root);if(safe.StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)+"wallpaper-categories-check-",StringComparison.OrdinalIgnoreCase))Directory.Delete(safe,true);}
 }
}
