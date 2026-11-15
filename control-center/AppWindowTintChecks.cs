using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
static class AppWindowTintChecks {
 sealed class Target:IWindowTintTarget {public bool Available=true;public bool Installed {get{return Available;}}public readonly Dictionary<string,object> Values=new Dictionary<string,object>();public int Writes,Reloads;public bool FailReload;public object Read(string key){object value;return Values.TryGetValue(key,out value)?value:null;}public void Write(string key,object value){Writes++;if(value==null)Values.Remove(key);else Values[key]=value;}public void Reload(){if(FailReload)throw new System.IO.IOException("fixture reload failure");Reloads++;}}
 sealed class Journal:IWindowTintJournal {string text;public bool Fail;public WindowTintBackup Load(){return text==null?null:new JavaScriptSerializer().Deserialize<WindowTintBackup>(text);}public void Save(WindowTintBackup value){if(Fail)throw new System.IO.IOException("fixture journal failure");text=new JavaScriptSerializer().Serialize(value);}public void Clear(){text=null;}}
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 internal static void Run(){var target=new Target();var journal=new Journal();var control=new WindowTintController(target,journal);
  control.Apply(false,false,"");Check(target.Writes==0&&target.Reloads==0,"Disabled tint must not change Windows");
  target.Available=false;Check(control.Apply(true,false,"").Contains("Install")&&target.Writes==0,"Missing mod must be actionable without writes");target.Available=true;
  target.Values["Disabled"]=1;target.Values["ExcludeCustom"]="original.exe";target.Values["Settings/BackgroundEffects.type"]="acrylicblur";
  control.Apply(true,false,"game.exe, editor.exe");Check((string)target.Read("Settings/BackgroundEffects.type")=="mica"&&(int)target.Read("Disabled")==0&&target.Reloads==1,"Enabled tint preset failed");
  var exclusions=((string)target.Read("ExcludeCustom")).Split('|');Check(exclusions.Contains("original.exe")&&exclusions.Contains("game.exe")&&WindowTintController.Protected.All(exclusions.Contains),"Exclusions must protect rice and preserve user choices");
  Check((int)target.Read("Settings/RenderingMod.Syscolors")==0,"Global system colors must remain disabled");int writes=target.Writes;control.Apply(true,false,"game.exe, editor.exe");Check(target.Writes==writes&&target.Reloads==1,"Unchanged settings must not reload the mod");
  control.Apply(true,true,"game.exe, editor.exe");Check((string)target.Read("Settings/BackgroundEffects.type")=="mica_tabbed"&&target.Reloads==2,"Mica Alt mode failed");
  // Restart the owner with persisted journal, then preserve an independent Windhawk edit.
  var restart=new WindowTintController(target,journal);target.Values["Settings/FlyoutsEffects"]=1;
  try{restart.Apply(true,true,"");throw new Exception("External edits must require release");}catch(InvalidOperationException){}
  restart.Apply(false,false,"");Check((string)target.Read("Settings/BackgroundEffects.type")=="acrylicblur"&&(string)target.Read("ExcludeCustom")=="original.exe"&&(int)target.Read("Disabled")==1&&(int)target.Read("Settings/FlyoutsEffects")==1&&target.Read("Settings/RenderingMod.ThemeBackground")==null&&journal.Load()==null,"Restoration must recover original settings and preserve external edits");
  var failed=new Target();var broken=new WindowTintController(failed,new Journal{Fail=true});try{broken.Apply(true,false,"");throw new Exception("Journal failure must abort changes");}catch(System.IO.IOException){}Check(failed.Writes==0,"Must journal before modifying Windhawk");
  var retryTarget=new Target{FailReload=true};var retryJournal=new Journal();var retryOwner=new WindowTintController(retryTarget,retryJournal);try{retryOwner.Apply(true,false,"");throw new Exception("Reload failure ignored");}catch(System.IO.IOException){}int initialWrites=retryTarget.Writes;retryTarget.FailReload=false;retryOwner.Apply(true,false,"");Check(retryTarget.Reloads==1&&retryTarget.Writes==initialWrites&&!retryJournal.Load().NeedsReload,"Retry must finish a failed reload without rewriting settings");
  try{WindowTintController.Exclusions("*.exe|../app.exe");throw new Exception("Invalid exclusions accepted");}catch(ArgumentException){}
  Check(WindowTintController.Exclusions("game.exe, GAME.exe").Length==1,"Exclusions must deduplicate");
  Console.WriteLine("PASS: opt-in Mica/Mica Alt preset, helper exclusions, no repeated reload, persisted rollback, external-edit preservation and journal-failure safety. Fake settings only; no registry or app-window changes.");
 }
}
