using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Media;

/// <summary>
/// The wallpaper-matched palette shared by the helpers: wallpaper-theme.json beside them, holding the
/// bar's CSS variable names (--nord-0, --edge, …) mapped to #rrggbb. The control center writes it while
/// "Match wallpaper colors" is on and deletes it when off; the Alt indicator reads it.
/// </summary>
static class WallpaperPalette {
 internal static Dictionary<string,string> Defaults(){
  var p=new Dictionary<string,string>();var colors=new[]{"2e3440","3b4252","434c5e","4c566a","d8dee9","e5e9f0","eceff4","8fbcbb","88c0d0","81a1c1","5e81ac","bf616a","d08770","ebcb8b","a3be8c","b48ead"};
  for(int i=0;i<colors.Length;i++)p["--nord-"+i]="#"+colors[i];
  p["--edge"]="#6b86a3";p["--muted"]="#aab6c8";p["--muted-2"]="#9aa7ba";p["--muted-3"]="#87909f";p["--shade"]="#242933";p["--danger-soft"]="#d8a0a5";return p;
 }
 internal static Dictionary<string,string> Interpolate(Dictionary<string,string> from,Dictionary<string,string> to,double t){
  var result=Defaults();foreach(var key in new List<string>(result.Keys)){var c=ThemeTransition.Mix(Get(from,key,result[key]),Get(to,key,result[key]),t);result[key]=String.Format("#{0:x2}{1:x2}{2:x2}",c.R,c.G,c.B);}return result;
 }
 static string Path {get{return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-theme.json");}}

 /// <summary>The active palette, or null when wallpaper colors are off.</summary>
 public static Dictionary<string,string> Load(){
  try{return File.Exists(Path)?new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path)):null;}catch{return null;}
 }
 public static void Save(Dictionary<string,string> palette){
  if(palette==null){if(File.Exists(Path))File.Delete(Path);return;}
  File.WriteAllText(Path+".tmp",new JavaScriptSerializer().Serialize(palette));
  if(File.Exists(Path))File.Replace(Path+".tmp",Path,null);else File.Move(Path+".tmp",Path);
 }
 public static Color Get(Dictionary<string,string> palette,string name,string fallback){
  string value;
  try{return (Color)ColorConverter.ConvertFromString(palette!=null&&palette.TryGetValue(name,out value)?value:fallback);}
  catch{return (Color)ColorConverter.ConvertFromString(fallback);}
 }
}
