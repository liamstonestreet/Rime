using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

static class ThemeCoordinatorChecks {
 sealed class Store:IAppearanceSettingsStore {
  public AppearanceSettings Value=new AppearanceSettings{wallpaperColors=true,frost=true};public bool Fail;
  public AppearanceSettings Load(){return Value.Copy();}public void Save(AppearanceSettings value){if(Fail)throw new IOException("Read-only settings");Value=value.Copy();}
 }
 sealed class Host:IThemeHost {
  public double Now {get{return 10000;}}public bool AnimationEnabled {get;set;}
  public string Path="A";public bool Missing,Pending,HoldUi;public int Extracts,Saves,Applies,Notifications,FontReads,LastBlur,Clears;public int FrostRevision {get;private set;}
  public readonly List<string> Messages=new List<string>();public ThemeTransitionData Last;public Dictionary<string,string> Saved;
  public readonly ManualResetEventSlim UiReady=new ManualResetEventSlim();Action queued;TaskCompletionSource<int> uiTask;
  public Host(){AnimationEnabled=true;}
  public string CurrentWallpaper(){return Missing?null:Path;}public string Stamp(string path){return path==null?null:path+"|1";}
  public Dictionary<string,string> LoadPalette(){return new Dictionary<string,string>{{"--edge","#123456"}};}
  public Dictionary<string,string> ExtractPalette(string path){Extracts++;return Missing?null:new Dictionary<string,string>{{"--edge",path=="A"?"#112233":"#445566"}};}
  public Dictionary<string,string> CurrentColors(Dictionary<string,string> fallback){return fallback;}
  public void RenderFrost(string path,int blur,int brightness){Pending=true;LastBlur=blur;}public void ClearFrost(){Clears++;Pending=false;}public void CommitFrost(){if(Pending){Pending=false;FrostRevision++;}}public void NotifyFrost(){Notifications++;}
  public bool FrostPending {get{return Pending;}}public object FrostStrips {get{return new object[0];}}public object BarBounds {get{return new object[0];}}
  public IntPtr[] PrepareWindows(){return new IntPtr[0];}public void SavePalette(Dictionary<string,string> palette){Saves++;Saved=palette;}
  public void PublishTransition(ThemeTransitionData data){Last=data;}public void ApplyTransition(ThemeTransitionData data,IntPtr[] windows){Applies++;}
  public Task OnUiAsync(Action action){if(!HoldUi){action();return Task.FromResult(0);}queued=action;uiTask=new TaskCompletionSource<int>();UiReady.Set();return uiTask.Task;}
  public void ReleaseUi(){HoldUi=false;var done=uiTask;queued();done.SetResult(0);}
  public string FontData(string family){FontReads++;return "fixture:"+family;}public void Broadcast(string text){Messages.Add(text);}public void Log(string message){}
 }
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static long Revision(ThemeCoordinator coordinator){var root=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(coordinator.Snapshot());return Convert.ToInt64(((Dictionary<string,object>)root["appearance"])["revision"]);}
 public static void Run(){
  var store=new Store();var host=new Host();using(var coordinator=new ThemeCoordinator(store,host)){
   var first=coordinator.SynchronizeAsync(true).GetAwaiter().GetResult();Check(first!=null&&first.duration==0&&first.startsAt==10180&&host.Applies==1,"Initial coordinated commit/clock failed");
   var settings=coordinator.Settings;settings.wallpaperColors=false;var colors=coordinator.Palette;colors["--edge"]="#ffffff";Check(coordinator.Settings.wallpaperColors&&coordinator.Palette["--edge"]=="#112233","Mutable state escaped coordinator");
   int extracted=host.Extracts;coordinator.WarmWallpaperLook("B");var next=coordinator.BeginWallpaper("B").GetAwaiter().GetResult();Check(host.Extracts==extracted+1&&next.duration==700&&next.startsAt==10180&&host.Notifications==2,"Warm palette reuse/shared transition failed");next.to["--edge"]="#ffffff";Check(coordinator.Palette["--edge"]=="#445566","Returned transition mutated coordinator");
   // Release the explicit switch without scheduling an unrelated poll in this fixture.
   coordinator.AutoSync=false;coordinator.EndWallpaper();coordinator.AutoSync=true;
   int saved=host.Saves;host.Missing=true;var fallback=coordinator.SynchronizeAsync(true).GetAwaiter().GetResult();Check(fallback!=null&&host.Saves==saved&&coordinator.Palette["--edge"]=="#445566","Missing source erased last-good palette");host.Missing=false;
   coordinator.AutoSync=false;long revision=Revision(coordinator);store.Fail=true;try{coordinator.Update(s=>s.radius=20);throw new Exception("Save failure swallowed");}catch(IOException){}Check(Revision(coordinator)==revision&&coordinator.Settings.radius==100,"Failed save changed live settings");store.Fail=false;
   string font=coordinator.FontSnapshot();int reads=host.FontReads;coordinator.Update(s=>s.transparency=30);Check(Revision(coordinator)>revision&&Object.ReferenceEquals(font,coordinator.FontSnapshot())&&host.FontReads==reads,"Tint edit invalidated font channel/revision");
   coordinator.Update(s=>s.fontRevision++);coordinator.FontSnapshot();Check(host.FontReads==reads+1,"Font edit did not invalidate payload");
   coordinator.Update(s=>s.motion=0);coordinator.AutoSync=true;var still=coordinator.BeginWallpaper("C").GetAwaiter().GetResult();Check(still.duration==0,"Motion-off ignored");coordinator.AutoSync=false;coordinator.EndWallpaper();coordinator.Update(s=>{s.motion=100;s.wallpaperColors=false;});coordinator.AutoSync=true;
   coordinator.SynchronizeAsync(true).GetAwaiter().GetResult();Check(coordinator.Palette==null&&host.Saved==null,"Explicit match-off failed to restore default palette");
  }
  // A setting edit while a commit waits for UI invalidates that prepared generation.
  store=new Store();host=new Host{HoldUi=true};using(var coordinator=new ThemeCoordinator(store,host)){
   var pending=coordinator.SynchronizeAsync(true);Check(host.UiReady.Wait(3000),"Preparation did not reach UI boundary");coordinator.AutoSync=false;coordinator.Update(s=>s.blur=80);host.ReleaseUi();Check(pending.GetAwaiter().GetResult()==null&&host.Applies==0,"Stale preparation committed after settings edit");coordinator.AutoSync=true;Check(coordinator.SynchronizeAsync(true).GetAwaiter().GetResult()!=null,"Fresh preparation did not recover");
  }
  store=new Store();host=new Host{HoldUi=true};using(var coordinator=new ThemeCoordinator(store,host)){
   var pending=coordinator.BeginWallpaper("updated-during-prepare");Check(host.UiReady.Wait(3000),"Explicit switch did not prepare");coordinator.Update(s=>s.blur=77);host.ReleaseUi();var result=pending.GetAwaiter().GetResult();Check(result!=null&&host.LastBlur==77&&host.Applies==1,"Explicit switch did not reprepare after a settings edit");
  }
  store=new Store();store.Value.frost=false;host=new Host{HoldUi=true};using(var coordinator=new ThemeCoordinator(store,host)){
   var pending=coordinator.SynchronizeAsync(true);Check(host.UiReady.Wait(3000)&&host.Clears==0,"Frost cleared before coordinated commit");host.ReleaseUi();pending.GetAwaiter().GetResult();Check(host.Clears==1,"Frost-off commit failed");
  }
  var publicationHost=new Host();var publication=new AppearancePublisher(publicationHost);var publicationSettings=new AppearanceSettings();publication.Publish(()=>publicationSettings,()=>"fixture-state");publication.Publish(()=>publicationSettings,()=>"fixture-state");Check(publicationHost.Messages.Count==2&&publicationHost.FontReads==1,"Unchanged state or fonts rebroadcast");
  store=new Store();host=new Host{HoldUi=true};var disposable=new ThemeCoordinator(store,host);var disposedTask=disposable.SynchronizeAsync(true);Check(host.UiReady.Wait(3000),"Dispose fixture did not prepare");disposable.Dispose();host.ReleaseUi();Check(disposedTask.GetAwaiter().GetResult()==null&&host.Applies==0,"Disposed coordinator committed queued effects");
  CheckStore();
  Console.WriteLine("PASS: instance theme preparation/commit, shared clock, warm reuse, last-good palette, copied state, save rollback, font channel, stale generation/disposal and settings migration. Fake host only; no desktop, borders or bridge effects.");
 }
 static void CheckStore(){string folder=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"theme-store-"+Guid.NewGuid().ToString("N")));Check(folder.StartsWith(System.IO.Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),StringComparison.OrdinalIgnoreCase),"Fixture must remain under checks");Directory.CreateDirectory(folder);string path=System.IO.Path.Combine(folder,"appearance.json");try{
  File.WriteAllText(path,"{\"centerWidth\":99999,\"motionStyle\":\"invalid\",\"futureField\":{\"keep\":true}}");var settingsStore=new AppearanceSettingsStore(path);var settings=settingsStore.Load();Check(settings.centerWidth==1200&&settings.motionStyle=="smooth","Loaded settings not normalized");settings.transparency=1000;settingsStore.Save(settings);Check(settingsStore.Load().transparency==100&&File.ReadAllText(path).Contains("futureField")&&File.Exists(path+".bak"),"Atomic save lost validation/unknown fields/backup");
  File.WriteAllText(path,"invalid JSON");settingsStore.Save(new AppearanceSettings());Check(File.ReadAllText(path+".bak")=="invalid JSON"&&settingsStore.Load().centerWidth==759,"Malformed original not retained in backup");Check(!Directory.GetFiles(folder,"*.tmp").Any(),"Settings write leaked temp files");
 }finally{Directory.Delete(folder,true);}}
}
