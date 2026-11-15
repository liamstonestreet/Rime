using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;

sealed class WallpaperCategory {public string Key,Name;public List<string> Images=new List<string>();}
interface IWallpaperLibrary {List<WallpaperCategory> Scan(string folder);}
sealed class WallpaperLibraryService:IWallpaperLibrary {
 static readonly string[] Types={".jpg",".jpeg",".png",".bmp"};
 internal static List<string> Sort(IEnumerable<string> paths){return paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>{long n;return Int64.TryParse(Path.GetFileNameWithoutExtension(p),out n)?n:Int64.MaxValue;}).ThenBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p,StringComparer.OrdinalIgnoreCase).ToList();}
 List<string> Files(string folder,bool recursive){var files=new List<string>();try{files.AddRange(Directory.GetFiles(folder).Where(p=>Types.Contains(Path.GetExtension(p).ToLowerInvariant())));if(recursive)foreach(var child in Directory.GetDirectories(folder))try{if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)files.AddRange(Files(child,true));}catch{}}catch{}return files;}
 public List<WallpaperCategory> Scan(string folder){var groups=new List<WallpaperCategory>();if(!Directory.Exists(folder))return new List<WallpaperCategory>{new WallpaperCategory{Key="all",Name="All"}};folder=Path.GetFullPath(folder);
  var root=Sort(Files(folder,false));try{foreach(var child in Directory.GetDirectories(folder).OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase)){try{if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)continue;var images=Sort(Files(child,true));if(images.Count>0){var name=Path.GetFileName(child);groups.Add(new WallpaperCategory{Key="folder:"+name,Name=name=="All"?"All (folder)":name,Images=images});}}catch{}}}catch{}
  if(root.Count>0)groups.Add(new WallpaperCategory{Key="uncategorized",Name=groups.Any(g=>g.Name=="Uncategorized")?"Uncategorized (root)":"Uncategorized",Images=root});
  groups.Insert(0,new WallpaperCategory{Key="all",Name="All",Images=Sort(groups.SelectMany(g=>g.Images))});return groups;
 }
}
sealed class WallpaperBrowserState {
 public string root {get;set;}public string category {get;set;}public Dictionary<string,string> positions {get;set;}
 public WallpaperBrowserState(){category="all";positions=new Dictionary<string,string>();}
 public static WallpaperBrowserState Load(string path,string root){return new WallpaperBrowserStateStore().Load(path,root);}
 public void Save(string path){new WallpaperBrowserStateStore().Save(path,this);}
}
static class WallpaperLibrary {
 static readonly WallpaperLibraryService service=new WallpaperLibraryService();
 public static List<WallpaperCategory> Scan(string folder){return service.Scan(folder);}
 internal static List<string> Sort(IEnumerable<string> paths){return WallpaperLibraryService.Sort(paths);}
}
