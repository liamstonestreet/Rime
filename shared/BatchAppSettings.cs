using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
sealed class BatchAppSettings {
 public string[] apps {get;set;}public string[] appIds {get;set;}public Dictionary<string,string> processNames {get;set;}
 public BatchAppSettings(){apps=new string[0];appIds=new string[0];processNames=new Dictionary<string,string>();}
 public static string PathName {get{return Path.Combine(AppCatalog.Folder,"launch-apps.json");}}
 public static BatchAppSettings Load(){return new JavaScriptSerializer().Deserialize<BatchAppSettings>(File.ReadAllText(PathName))??new BatchAppSettings();}
 public bool Includes(AppEntry app){return (appIds??new string[0]).Contains(app.Key,StringComparer.OrdinalIgnoreCase)||(apps??new string[0]).Contains(app.Name,StringComparer.OrdinalIgnoreCase);}
 public static void Save(string[] ids,string[] legacyNames=null){var serializer=new JavaScriptSerializer();var root=File.Exists(PathName)?serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(PathName)):new Dictionary<string,object>();var old=File.Exists(PathName)?Load():new BatchAppSettings();var catalog=AppCatalog.Load();root["appIds"]=ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();root["apps"]=(legacyNames??old.apps??new string[0]).Where(name=>!catalog.Any(a=>String.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase))).ToArray();var temp=PathName+".tmp";File.WriteAllText(temp,serializer.Serialize(root));if(File.Exists(PathName))File.Replace(temp,PathName,PathName+".previous");else File.Move(temp,PathName);}
}
