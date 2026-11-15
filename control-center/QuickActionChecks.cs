using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
static class QuickActionChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){
  string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"quick-action-fixtures-"+Guid.NewGuid().ToString("N"));
  var launch=QuickActions.Launch;
  try{
   string file=QuickActions.Reflection(folder,new DateTime(2026,10,31,23,59,0));Check(Path.GetFileName(file)=="2026-10-31.md","Reflection date must use the local day's invariant yyyy-MM-dd name");
   File.WriteAllText(file,"Existing reflection must survive.");
   Parallel.For(0,12,i=>QuickActions.Reflection(folder,new DateTime(2026,10,31)));
   Check(File.ReadAllText(file)=="Existing reflection must survive."&&Directory.GetFiles(folder).Length==1,"Repeated and concurrent clicks must never overwrite or duplicate a reflection");
   QuickActions.Reflection(folder,new DateTime(2026,11,1));Check(Directory.GetFiles(folder).Length==2,"Midnight must select a new daily note");
   string special=Path.Combine(folder,"A & B note.md"),uri=QuickActions.NoteUri(special);Check(uri.StartsWith("obsidian://open?path=")&&uri.Contains("%20")&&uri.Contains("%26")&&Uri.UnescapeDataString(uri.Substring(21))==Path.GetFullPath(special),"Obsidian URI must safely encode the full note path");
   int launches=0;string opened=null;QuickActions.Launch=value=>{launches++;opened=value;};
   var reply=MediaBridge.Receive("quick-action:github");Check(opened=="https://github.com/"&&reply.Contains("\"ok\":true"),"GitHub must use the fixed URL and the shell's default-browser handler");
   MediaBridge.Receive("quick-action:not-an-action");Check(launches==1,"Unknown actions must never launch an arbitrary URI");
  }finally{QuickActions.Launch=launch;if(Directory.Exists(folder)){foreach(var f in Directory.GetFiles(folder))File.Delete(f);Directory.Delete(folder);}}
  foreach(var label in new[]{"Mute","Mute (Ctrl+Shift+M)","Unmute microphone"})Check(DiscordVoice.Identify(label)!=null,"Discord mute control not recognized");
  Check(DiscordVoice.Identify("Undeafen (Ctrl+Shift+D)")=="undeafen"&&DiscordVoice.Identify("Mute channel")==null&&DiscordVoice.Identify("Mute server")==null,"Discord controls must exclude unrelated channel/server mute buttons");
  Check(DiscordVoice.ResolveState("Mute",true)==true&&DiscordVoice.ResolveState("Deafen",true)==true&&DiscordVoice.ResolveState("Unmute",false)==false&&DiscordVoice.ResolveState("Undeafen",null)==true,"Actual Discord toggle state must take precedence over a static button name");
  foreach(bool microphone in new[]{true,false}){
   var path=new System.Windows.Shapes.Path();var label=new System.Windows.Controls.TextBlock();var row=new System.Windows.Controls.StackPanel();row.Children.Add(new System.Windows.Controls.Border{Child=path});row.Children.Add(label);var button=new System.Windows.Controls.Button{Content=row};
   QuickActions.PaintVoiceRow(button,microphone,false);int normal=System.Windows.Media.PathGeometry.CreateFromGeometry(path.Data).Figures.Count;
   QuickActions.PaintVoiceRow(button,microphone,true);Check(label.Text==(microphone?"Unmute microphone":"Undeafen audio")&&System.Windows.Media.PathGeometry.CreateFromGeometry(path.Data).Figures.Count==normal+1,"Disabled voice state must show the inverse action and a slashed icon");
   QuickActions.PaintVoiceRow(button,microphone,false);Check(label.Text==(microphone?"Mute microphone":"Deafen audio")&&System.Windows.Media.PathGeometry.CreateFromGeometry(path.Data).Figures.Count==normal,"Restoring voice state must remove the slash and restore the action label");
  }
  Task.Run(async()=>{
   var query=DiscordVoice.Query;var started=new ManualResetEventSlim();var release=new ManualResetEventSlim();Task<DiscordVoiceState> first=null;
   try{
    DiscordVoice.Query=action=>{started.Set();release.Wait();return new DiscordVoiceState{available=true,muted=true,deafened=false};};
    first=DiscordVoice.Run("mute");Check(started.Wait(1000),"Discord worker did not start");var busy=await DiscordVoice.Run("deafen");Check(!busy.available&&!String.IsNullOrEmpty(busy.error),"Repeated Discord actions must not accumulate UIA workers");release.Set();Check((await first).muted==true,"Discord result state lost");
    DiscordVoice.Query=action=>{throw new InvalidOperationException();};var failure=await DiscordVoice.Run("mute");Check(!failure.available&&!String.IsNullOrEmpty(failure.error),"Inaccessible Discord must produce an honest error");
   }finally{release.Set();if(first!=null)first.GetAwaiter().GetResult();DiscordVoice.Query=query;started.Dispose();release.Dispose();}
  }).GetAwaiter().GetResult();
  Console.WriteLine("PASS: daily reflection naming, midnight, concurrent reopen without overwrite, encoded Obsidian URI, default-browser GitHub routing and bounded/error-safe Discord actions. No real notes/apps/voice settings changed.");
 }
}
