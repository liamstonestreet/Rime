using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

interface IAppearanceSettingsStore {AppearanceSettings Load();void Save(AppearanceSettings settings);}
// One file owner. Preserve unknown fields, validate known fields and atomically
// replace the file; malformed documents are backed up before being superseded.
sealed class AppearanceSettingsStore:IAppearanceSettingsStore {
 readonly string path;readonly object gate=new object();
 public AppearanceSettingsStore(string path){this.path=path;}
 public AppearanceSettings Load(){lock(gate){try{var result=new JavaScriptSerializer().Deserialize<AppearanceSettings>(File.ReadAllText(path))??new AppearanceSettings();result.Normalize();return result;}catch{return new AppearanceSettings();}}}
 public void Save(AppearanceSettings settings){lock(gate){var serializer=new JavaScriptSerializer();var copy=settings.Copy();copy.Normalize();Dictionary<string,object> document=null;
  if(File.Exists(path))try{document=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));}catch{}
  if(document==null)document=new Dictionary<string,object>();
  foreach(var pair in serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(copy)))document[pair.Key]=pair.Value;
  string folder=Path.GetDirectoryName(path);if(!String.IsNullOrEmpty(folder))Directory.CreateDirectory(folder);
  string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllText(temp,serializer.Serialize(document));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
 }}
}
