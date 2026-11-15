using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;

sealed class IndicatorKeySettings {
 public string workspace="leftalt",wallpaper="rightctrl";
}
sealed class IndicatorKeyChoice {
 public string Name {get;set;}public string FriendlyName {get;set;}public int Code {get;set;}
 public override string ToString(){return FriendlyName;}
}
static class IndicatorKeys {
 public static readonly IndicatorKeyChoice[] Choices=Build();
 static IndicatorKeyChoice[] Build(){var list=new System.Collections.Generic.List<IndicatorKeyChoice>{new IndicatorKeyChoice{Name="leftalt",FriendlyName="Left Alt",Code=0xA4},new IndicatorKeyChoice{Name="rightalt",FriendlyName="Right Alt / AltGr",Code=0xA5},new IndicatorKeyChoice{Name="leftctrl",FriendlyName="Left Ctrl",Code=0xA2},new IndicatorKeyChoice{Name="rightctrl",FriendlyName="Right Ctrl",Code=0xA3},new IndicatorKeyChoice{Name="leftshift",FriendlyName="Left Shift",Code=0xA0},new IndicatorKeyChoice{Name="rightshift",FriendlyName="Right Shift",Code=0xA1}};for(int i=1;i<=24;i++)list.Add(new IndicatorKeyChoice{Name="f"+i,FriendlyName="F"+i,Code=0x6F+i});list.Add(new IndicatorKeyChoice{Name="none",FriendlyName="Disabled",Code=0});return list.ToArray();}
 static string PathName {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"indicator-keys.json");}}
 public static int Code(string name){var item=Choices.FirstOrDefault(c=>c.Name==name);return item==null?-1:item.Code;}
 public static void Validate(IndicatorKeySettings settings){if(settings==null||Code(settings.workspace)<0||Code(settings.wallpaper)<0)throw new InvalidOperationException("Choose a key from each list.");if(settings.workspace==settings.wallpaper&&settings.workspace!="none")throw new InvalidOperationException("Choose different keys for the two indicators.");}
 public static IndicatorKeySettings Load(){if(!File.Exists(PathName))return new IndicatorKeySettings();var settings=new JavaScriptSerializer().Deserialize<IndicatorKeySettings>(File.ReadAllText(PathName));Validate(settings);return settings;}
 public static void Save(IndicatorKeySettings settings){Validate(settings);File.WriteAllText(PathName+".tmp",new JavaScriptSerializer().Serialize(settings));if(File.Exists(PathName))File.Replace(PathName+".tmp",PathName,null);else File.Move(PathName+".tmp",PathName);}
 public static IDisposable Watch(Action<IndicatorKeySettings> apply){return new Watcher(apply);}
 sealed class Watcher : IDisposable {
  readonly FileSystemWatcher watcher;readonly DispatcherTimer debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};readonly Action<IndicatorKeySettings> apply;
  public Watcher(Action<IndicatorKeySettings> apply){this.apply=apply;debounce.Tick+=delegate{debounce.Stop();Read();};watcher=new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory,"indicator-keys.json"){NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size};FileSystemEventHandler changed=delegate{Application.Current.Dispatcher.BeginInvoke(new Action(()=>{debounce.Stop();debounce.Start();}));};watcher.Changed+=changed;watcher.Created+=changed;watcher.Deleted+=changed;watcher.Renamed+=delegate{Application.Current.Dispatcher.BeginInvoke(new Action(()=>{debounce.Stop();debounce.Start();}));};watcher.EnableRaisingEvents=true;Read();}
  void Read(){try{apply(Load());}catch{ /* Keep the working binding while an external edit is invalid. */ }}
  public void Dispose(){watcher.Dispose();debounce.Stop();}
 }
}
