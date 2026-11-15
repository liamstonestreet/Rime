using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

sealed class WallpaperBrowserStateStore {
 readonly object gate=new object();
 public WallpaperBrowserState Load(string path,string root){lock(gate){try{var state=new JavaScriptSerializer().Deserialize<WallpaperBrowserState>(File.ReadAllText(path));if(state!=null&&String.Equals(state.root,root,StringComparison.OrdinalIgnoreCase)){if(state.positions==null)state.positions=new Dictionary<string,string>();return state;}}catch{}return new WallpaperBrowserState{root=root};}}
 public void Save(string path,WallpaperBrowserState state){lock(gate){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(state));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}}
}
