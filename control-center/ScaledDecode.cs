using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

/// <summary>
/// Downsized copies of wallpapers on disk. Decoding a 24-50 megapixel photo costs 70-190 MB at its peak,
/// even when only a thumbnail is wanted (WPF and WIC decode progressive JPEGs whole), and Windows keeps much
/// of that reserved for the process. So each wallpaper is decoded whole once, one at a time, and saved at
/// 1280 px (thumbnails, palette, frost) and screen width (the fade); later loads, across restarts too, read
/// the small copy. Keyed by path and modification time, so an edited file gets new copies.
/// </summary>
static class ScaledDecode {
 static readonly string Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-cache");
 static readonly object gate=new object();
 const int Small=1280,MaxFiles=80;

 /// <summary>The image at about this width (at least it, up to the original's), frozen.</summary>
 public static BitmapSource Load(string path,int width){return Decode(Copy(path,width),width);}

 /// <summary>A file holding the image at least this wide: the original when it is small enough, else a cached copy.</summary>
 static string Copy(string path,int width){
  int size=width<=Small?Small:Math.Max(width,Screen());
  try{
   // Read dimensions through an owned stream: the URI decoder otherwise retains a
   // file handle until GC, even though we only wanted its header.
   using(var input=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
    var header=BitmapFrame.Create(input,BitmapCreateOptions.DelayCreation|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.None);
    if(header.PixelWidth<=size*1.25)return path; // already close to the size: nothing to gain
   }
   var cached=Path.Combine(Folder,String.Format("{0:x8}-{1:x}-{2}.jpg",path.ToLowerInvariant().GetHashCode(),File.GetLastWriteTimeUtc(path).Ticks,size));
   if(File.Exists(cached))return cached;
   lock(gate){ // one whole-photo decode at a time
    if(File.Exists(cached))return cached;
    Directory.CreateDirectory(Folder);
    var encoder=new JpegBitmapEncoder{QualityLevel=92};encoder.Frames.Add(BitmapFrame.Create(Decode(path,size)));
    var temp=cached+".tmp";using(var file=File.Create(temp))encoder.Save(file);File.Move(temp,cached);
    Trim();
    return cached;
   }
  }catch{return path;}
 }

 static BitmapSource Decode(string path,int width){
  var image=new BitmapImage();image.BeginInit();image.UriSource=new Uri(path);image.DecodePixelWidth=Math.Max(1,width);
  image.CacheOption=BitmapCacheOption.OnLoad;image.CreateOptions=BitmapCreateOptions.IgnoreColorProfile|BitmapCreateOptions.IgnoreImageCache;image.EndInit();image.Freeze();return image;
 }

 /// <summary>The widest monitor, so the fade's copy is sharp on every screen.</summary>
 static int Screen(){return System.Windows.Forms.Screen.AllScreens.Max(s=>s.Bounds.Width);}

 /// <summary>Keeps the cache to the most recent copies.</summary>
 static void Trim(){
  var files=new DirectoryInfo(Folder).GetFiles("*.jpg").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(MaxFiles);
  foreach(var file in files)try{file.Delete();}catch{}
 }
}
