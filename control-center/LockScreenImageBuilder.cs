using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

static class LockScreenImageBuilder {
 internal static void Build(string source,string destination,int blur){
  int width;using(var input=File.OpenRead(source)){var header=BitmapFrame.Create(input,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);width=Math.Max(1,(int)Math.Round(header.PixelWidth*Math.Min(1,1920.0/Math.Max(header.PixelWidth,header.PixelHeight))));}
  var decoded=ScaledDecode.Load(source,width);var pixels=new FormatConvertedBitmap(decoded,PixelFormats.Bgra32,null,0);
  using(var bitmap=new System.Drawing.Bitmap(pixels.PixelWidth,pixels.PixelHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
   var data=bitmap.LockBits(new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.WriteOnly,bitmap.PixelFormat);try{pixels.CopyPixels(System.Windows.Int32Rect.Empty,data.Scan0,data.Stride*data.Height,data.Stride);}finally{bitmap.UnlockBits(data);}
   Blur(bitmap,blur);var temp=destination+".tmp";try{bitmap.Save(temp,System.Drawing.Imaging.ImageFormat.Jpeg);if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 internal static void Blur(System.Drawing.Bitmap bitmap,int amount){int radius=(int)Math.Round(Math.Max(0,Math.Min(100,amount))*.6);if(radius==0)return;var data=bitmap.LockBits(new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.ReadWrite,System.Drawing.Imaging.PixelFormat.Format32bppArgb);try{var bytes=new byte[data.Stride*data.Height];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);for(int pass=0;pass<3;pass++){FrostMaterial.Box(bytes,bitmap.Width,bitmap.Height,4,data.Stride,radius);FrostMaterial.Box(bytes,bitmap.Height,bitmap.Width,data.Stride,4,radius);}Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{bitmap.UnlockBits(data);}}
}
