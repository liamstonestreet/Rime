using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

// This adapter owns only a small preset in an already installed Windhawk mod.
// Mica follows Windows' wallpaper automatically: no palette polling or per-frame registry writes.
interface IWindowTintTarget {bool Installed {get;}object Read(string key);void Write(string key,object value);void Reload();}
interface IWindowTintJournal {WindowTintBackup Load();void Save(WindowTintBackup value);void Clear();}
sealed class WindowTintBackup {public bool NeedsReload;public Dictionary<string,object> Before=new Dictionary<string,object>();public Dictionary<string,object> Written=new Dictionary<string,object>();}
sealed class WindowTintController {
 readonly IWindowTintTarget target;readonly IWindowTintJournal journal;
 internal static readonly string[] Keys={"Disabled","ExcludeCustom","Settings/RenderingMod.ThemeBackground","Settings/RenderingMod.Syscolors","Settings/RenderingMod.AccentColorControls","Settings/BackgroundEffects.type","Settings/FlyoutsEffects"};
 internal static readonly string[] Protected={"zebar.exe","msedgewebview2.exe","NordControlCenter.exe","NordSpotlight.exe","NordWorkspaceIndicator.exe","NordRuntime.exe","lightshot.exe","windhawk.exe","Overwatch.exe","javaw.exe","java.exe",@"*\steamapps\common\*"};
 public WindowTintController(IWindowTintTarget target,IWindowTintJournal journal){this.target=target;this.journal=journal;}
 internal static bool Same(object a,object b){return a==null?b==null:b!=null&&a.GetType()==b.GetType()&&Object.Equals(a,b);}
 internal static string[] Exclusions(string text){var values=(text??"").Split(new[]{',',';','\r','\n','|'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(values.Length>64||values.Any(s=>!Regex.IsMatch(s,@"^[A-Za-z0-9_. -]+\.exe$")))throw new ArgumentException("Use up to 64 process names, separated by commas (for example: game.exe, editor.exe).");return values;}
 public string Apply(bool enabled,bool rich,string excluded){
  var backup=journal.Load()??new WindowTintBackup();
  if(!target.Installed)return enabled?"Install and enable Translucent Windows in Windhawk, then click Retry. Standard installed Windhawk is supported; portable mode is not.":backup.Written.Count>0?"The mod is missing. Its restore record is retained until you reinstall it and retry.":"Off. App windows keep their own theme.";
  if(!enabled){bool changed=false;foreach(var pair in backup.Written){if(!Keys.Contains(pair.Key))throw new IOException("Invalid window tint restore record");if(Same(target.Read(pair.Key),pair.Value)){target.Write(pair.Key,backup.Before[pair.Key]);changed=true;}}if(changed||backup.Written.Count>0)target.Reload();journal.Clear();return "Off. Previous Windhawk settings restored; edits made outside CC are preserved.";}
  foreach(var pair in backup.Written)if(!Same(target.Read(pair.Key),pair.Value))throw new InvalidOperationException("Windhawk settings changed outside CC. Turn this option off to release the preset, then enable it again.");
  object original;string previous=backup.Before.TryGetValue("ExcludeCustom",out original)?Convert.ToString(original):Convert.ToString(target.Read("ExcludeCustom"));
  var exclusions=String.Join("|",(previous??"").Split('|').Concat(Protected).Concat(Exclusions(excluded)).Where(s=>s.Length>0).Distinct(StringComparer.OrdinalIgnoreCase));
  var plan=new Dictionary<string,object>{{"Disabled",0},{"ExcludeCustom",exclusions},{"Settings/RenderingMod.ThemeBackground",1},{"Settings/RenderingMod.Syscolors",0},{"Settings/RenderingMod.AccentColorControls",0},{"Settings/BackgroundEffects.type",rich?"mica_tabbed":"mica"},{"Settings/FlyoutsEffects",0}};
  bool wrote=false;foreach(var pair in plan){if(Same(target.Read(pair.Key),pair.Value))continue;if(!backup.Before.ContainsKey(pair.Key))backup.Before[pair.Key]=target.Read(pair.Key);backup.Written[pair.Key]=pair.Value;backup.NeedsReload=true;journal.Save(backup);target.Write(pair.Key,pair.Value);wrote=true;}
  if(wrote||backup.NeedsReload){target.Reload();backup.NeedsReload=false;journal.Save(backup);}return "Mica preset applied. Windows follows the wallpaper automatically in supported app backgrounds. Some apps need reopening; custom-drawn apps may stay unchanged.";
 }
}
sealed class WindhawkTintTarget:IWindowTintTarget {
 const string PathName=@"SOFTWARE\Windhawk\Engine\Mods\translucent-windows";
 public bool Installed {get{using(var key=Registry.LocalMachine.OpenSubKey(PathName))return key!=null&&key.GetValue("LibraryFileName") is string;}}
 static void Split(string name,out string path,out string value){if(!WindowTintController.Keys.Contains(name))throw new ArgumentException("Unknown preset setting");int slash=name.IndexOf('/');path=PathName+(slash<0?"":"\\Settings");value=slash<0?name:name.Substring(slash+1);}
 public object Read(string name){string path,value;Split(name,out path,out value);using(var key=Registry.LocalMachine.OpenSubKey(path))return key==null?null:key.GetValue(value);}
 public void Write(string name,object data){string path,value;Split(name,out path,out value);using(var key=Registry.LocalMachine.OpenSubKey(path,true)){if(key==null)throw new IOException("Windhawk setting key is missing");if(data==null)key.DeleteValue(value,false);else if(data is int)key.SetValue(value,data,RegistryValueKind.DWord);else if(data is string)key.SetValue(value,data,RegistryValueKind.String);else throw new IOException("Unsupported Windhawk value type");}}
 public void Reload(){using(var key=Registry.LocalMachine.OpenSubKey(PathName,true)){if(key==null)throw new IOException("Windhawk mod is missing");long now=(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;int prior=Convert.ToInt32(key.GetValue("SettingsChangeTime",0));key.SetValue("SettingsChangeTime",Math.Max((int)now,prior+1),RegistryValueKind.DWord);}}
}
sealed class WindowTintJournal:IWindowTintJournal {
 readonly string path=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NordRice","window-tint-restore.json");
 public WindowTintBackup Load(){if(!File.Exists(path))return null;var value=new JavaScriptSerializer().Deserialize<WindowTintBackup>(File.ReadAllText(path));if(value==null||value.Before==null||value.Written==null||value.Written.Keys.Any(k=>!WindowTintController.Keys.Contains(k)||!value.Before.ContainsKey(k)))throw new IOException("Window tint restore record is invalid; preserved for recovery.");return value;}
 public void Save(WindowTintBackup value){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));var temp=path+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(value));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
 public void Clear(){if(File.Exists(path))File.Delete(path);}
}
static class AppWindowTint {
 static readonly WindowTintController controller=new WindowTintController(new WindhawkTintTarget(),new WindowTintJournal());static DispatcherTimer timer;static bool started;public static event Action Changed;public static string Status="Off. App windows keep their own theme.";
 public static void Start(){if(started)return;started=true;timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};timer.Tick+=delegate{timer.Stop();Refresh();};BarAppearance.Changed+=Schedule;Refresh();}
 static void Schedule(){if(timer==null)return;timer.Stop();timer.Start();}
 public static void Refresh(){try{var s=BarAppearance.Settings;Status=controller.Apply(s.appWindowTint,s.appWindowTintRich,s.appWindowTintExcluded);}catch(UnauthorizedAccessException){Status="Windhawk settings need administrator access. Run CC in GlazeWM's elevated session, then Retry. No automatic elevation is performed.";}catch(System.Security.SecurityException){Status="Windows blocked access to Windhawk settings. Retry from the elevated GlazeWM session.";}catch(Exception e){Status=e.Message;}var changed=Changed;if(changed!=null)changed();}
 public static void Stop(){started=false;BarAppearance.Changed-=Schedule;if(timer!=null){timer.Stop();timer=null;}Changed=null;}
}
sealed class AppWindowTintControls:StackPanel {
 readonly Button toggle,rich;readonly TextBlock status;readonly TextBox excluded;bool subscribed;
 public AppWindowTintControls(){Margin=new Thickness(0,4,0,14);Children.Add(new TextBlock{Text="App windows",FontSize=15,FontWeight=FontWeights.Medium,Margin=new Thickness(0,8,0,8)});
  var row=new WrapPanel();toggle=RiceLayout.Button("Wallpaper tint","ConfigureAppWindowTint");rich=RiceLayout.Button("Richer tint","ConfigureAppWindowTintRich");var setup=RiceLayout.Button("Windhawk setup","ConfigureAppWindowTintSetup");var retry=RiceLayout.Button("Retry","ConfigureAppWindowTintRetry");foreach(var button in new[]{toggle,rich,setup,retry}){button.Margin=new Thickness(0,0,6,6);row.Children.Add(button);}Children.Add(row);
  toggle.Click+=delegate{BarAppearance.Update(s=>s.appWindowTint=!s.appWindowTint);Paint();};rich.Click+=delegate{BarAppearance.Update(s=>s.appWindowTintRich=!s.appWindowTintRich);Paint();};setup.Click+=delegate{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://windhawk.net/mods/translucent-windows"){UseShellExecute=true});}catch{}};retry.Click+=delegate{AppWindowTint.Refresh();Paint();};
  Children.Add(RiceLayout.Hint("Optional Translucent Windows mod: applies Windows Mica to supported app backgrounds. Tint follows your wallpaper automatically; richer tint uses Mica Alt. Custom-drawn apps (Signal, Discord, Spotify and browsers) often keep their own colors. Requires Windows 11 22H2+, transparency effects on, and energy saver off. This does not change whole-window opacity."));
  Children.Add(new TextBlock{Text="Excluded apps",FontSize=12,Margin=new Thickness(0,5,0,5)});excluded=new TextBox{Name="ConfigureAppWindowTintExcluded",Text=BarAppearance.Settings.appWindowTintExcluded??"",Padding=new Thickness(9,6,9,6)};Children.Add(excluded);Children.Add(RiceLayout.Hint("Process names separated by commas, for example game.exe, editor.exe. Press Enter or leave the field to apply. Rice helpers and Lightshot are always excluded. Turn wallpaper tint off to restore the previous mod settings."));
  Action save=()=>{try{WindowTintController.Exclusions(excluded.Text);if(excluded.Text!=BarAppearance.Settings.appWindowTintExcluded)BarAppearance.Update(s=>s.appWindowTintExcluded=excluded.Text);Paint();}catch(Exception e){status.Text=e.Message;}};
  excluded.LostKeyboardFocus+=delegate{save();};excluded.PreviewKeyDown+=(sender,e)=>{if(e.Key==System.Windows.Input.Key.Enter){save();e.Handled=true;}};
  status=new TextBlock{FontSize=10,TextWrapping=TextWrapping.Wrap,Foreground=ControlCenterTheme.Current.Brush("Muted"),Margin=new Thickness(0,6,0,0)};Children.Add(status);Loaded+=delegate{if(!subscribed){AppWindowTint.Changed+=Paint;subscribed=true;}Paint();};Unloaded+=delegate{if(subscribed){AppWindowTint.Changed-=Paint;subscribed=false;}};Paint();
 }
 void Paint(){var s=BarAppearance.Settings;toggle.Background=ControlCenterTheme.Current.Brush(s.appWindowTint?"Selected":"Raised");rich.Background=ControlCenterTheme.Current.Brush(s.appWindowTintRich?"Selected":"Raised");status.Text=AppWindowTint.Status;}
}
