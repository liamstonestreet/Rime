using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

/// <summary>
/// Reads and edits single values in GlazeWM's config.yaml by key path ("gaps/outer_gap/top"), leaving
/// every other line (and its comments) untouched, then reloads GlazeWM. Shares the config lock with the
/// other editors (workspace rules, lock-in, border colors).
/// </summary>
static class GlazeConfig {
 static readonly string Config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@".glzr\glazewm\config.yaml");

 public static string Get(string path){string value=null;Walk(File.ReadAllText(Config),(p,v,set)=>{if(p==path)value=v;});return value;}

 /// <summary>Sets each path to its value (YAML text, e.g. "true" or "'20px'") and reloads GlazeWM if anything changed.</summary>
 public static void Set(IDictionary<string,string> changes){
  using(var mutex=new Mutex(false,"Local\\LiamNordWorkspaceRules")){
   bool held=false;
   try{
    try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}
    if(!held)throw new Exception("GlazeWM's config is busy");
    var text=File.ReadAllText(Config);
    var next=Walk(text,(p,v,set)=>{string want;if(changes.TryGetValue(p,out want)&&want!=v)set(want);});
    if(next==text)return;
    File.WriteAllText(Config,next);
    try{GlazeCommand.ReloadConfig();}catch{File.WriteAllText(Config,text);throw;}
   }finally{if(held)mutex.ReleaseMutex();}
  }
 }

 /// <summary>Visits every "key: value" line with its full key path; visit may replace the value.</summary>
 static string Walk(string text,Action<string,string,Action<string>> visit){
  var newline=text.Contains("\r\n")?"\r\n":"\n";var lines=text.Replace("\r\n","\n").Split('\n');
  var stack=new List<KeyValuePair<int,string>>();
  for(int i=0;i<lines.Length;i++){
   var m=Regex.Match(lines[i],@"^( *)([A-Za-z_]+):[ ]*([^#]*?)\s*(#.*)?$");if(!m.Success)continue;
   int indent=m.Groups[1].Length;while(stack.Count>0&&stack[stack.Count-1].Key>=indent)stack.RemoveAt(stack.Count-1);
   stack.Add(new KeyValuePair<int,string>(indent,m.Groups[2].Value));
   var path=String.Join("/",stack.Select(s=>s.Value));int line=i;
   visit(path,m.Groups[3].Value,value=>{lines[line]=m.Groups[1].Value+m.Groups[2].Value+": "+value+(m.Groups[4].Success?" "+m.Groups[4].Value:"");});
  }
  return String.Join(newline,lines);
 }
}

/// <summary>The control center's GlazeWM section: GlazeWM's window effects and gaps, applied right away.</summary>
static class GlazeTab {
 static readonly string[] Windows={"window_effects/focused_window","window_effects/other_windows"};

 public static StackPanel Build(){
  var panel=new StackPanel{Name="ConfigureGlazeSection",Margin=new Thickness(0,18,0,0)};
  panel.Children.Add(new TextBlock{Text="GlazeWM",FontSize=19,FontWeight=FontWeights.Medium});
  var note=new TextBlock{Text="Window effects and gaps, written to GlazeWM's config and applied right away.",FontSize=11,Foreground=B("Muted"),Margin=new Thickness(0,6,0,10),TextWrapping=TextWrapping.Wrap};
  panel.Children.Add(note);
  Action<Dictionary<string,string>> apply=changes=>{try{GlazeConfig.Set(changes);FocusBorder.Refresh();note.Foreground=B("Muted");note.Text="Window effects and gaps, written to GlazeWM's config and applied right away.";}catch(Exception e){note.Foreground=B("Error");note.Text=e.Message;}};
  Func<string,string> get=path=>{try{return GlazeConfig.Get(path)??"";}catch{return "";}};

  // Both window states get the same title bar, corner and border settings.
  Func<Func<string,Dictionary<string,string>>,Dictionary<string,string>> both=part=>{var all=new Dictionary<string,string>();foreach(var w in Windows)foreach(var kv in part(w))all[kv.Key]=kv.Value;return all;};
  Choice(panel,"Title bars",new[]{"Show","Hide"},get(Windows[0]+"/hide_title_bar/enabled")=="true"?"Hide":"Show",
   v=>apply(both(w=>new Dictionary<string,string>{{w+"/hide_title_bar/enabled",v=="Hide"?"true":"false"}})));
  // Windows 11 offers three corner styles for app windows (no free radius): square, small (~4 px), rounded (~8 px).
  var corner=get(Windows[0]+"/corner_style/enabled")!="true"?"Default":new Dictionary<string,string>{{"'square'","Square"},{"'small_rounded'","Small"},{"'rounded'","Rounded"}}.Where(kv=>kv.Key==get(Windows[0]+"/corner_style/style")).Select(kv=>kv.Value).FirstOrDefault()??"Default";
  Action<string> showCorner;
  var corners=Choice(panel,"Window corners",new[]{"Default","Square","Small","Rounded"},corner,v=>apply(GlazeCorners.Changes(v)),out showCorner);
  var match=RiceLayout.Button("Match the rice's corner radius","GlazeSyncCorners");match.Margin=new Thickness(0,-4,0,10);panel.Children.Add(match);panel.Children.Add(RiceLayout.Hint("Automatically matches app-window corners to the Configure corner-radius setting instead of a manual style."));
  Action paintMatch=()=>{bool on=BarAppearance.Settings.syncCorners;match.Background=B(on?"Selected":"Raised");match.Foreground=B(on?"Text":"Muted");corners.IsEnabled=!on;if(on)showCorner(GlazeCorners.Style(BarAppearance.Settings.radius));};
  match.Click+=delegate{BarAppearance.Update(s=>s.syncCorners=!s.syncCorners);paintMatch();GlazeCorners.Sync();};
  Action appearanceChanged=()=>{paintMatch();};panel.Loaded+=delegate{BarAppearance.Changed-=appearanceChanged;BarAppearance.Changed+=appearanceChanged;paintMatch();};panel.Unloaded+=delegate{BarAppearance.Changed-=appearanceChanged;};
  paintMatch();
  Choice(panel,"Window borders",new[]{"On","Off"},get(Windows[0]+"/border/enabled")=="false"?"Off":"On",
   v=>apply(both(w=>new Dictionary<string,string>{{w+"/border/enabled",v=="On"?"true":"false"}})));
  // Thicker than Windows' 1 px line: drawn by the control center around the focused window (FocusBorder).
  panel.Children.Add(new TextBlock{Text="Custom thick borders are temporarily disabled. Windows borders remain available above.",TextWrapping=TextWrapping.Wrap,Foreground=B("Muted"),FontSize=11,Margin=new Thickness(0,4,0,10)});

  // Cloaking keeps hidden workspaces' windows "open" to Windows; some apps (often Electron ones) misbehave
  // while cloaked, and GlazeWM's alternative hides them outright.
  Choice(panel,"Hidden workspaces",new[]{"Cloak","Hide"},get("general/hide_method")=="'hide'"?"Hide":"Cloak",
   v=>apply(new Dictionary<string,string>{{"general/hide_method",v=="Hide"?"'hide'":"'cloak'"}}));
  // Unfocused windows fade by this much (opacity 100% minus the value); 0 turns the effect off.
  int fade=get("window_effects/other_windows/transparency/enabled")=="true"?100-Number(get("window_effects/other_windows/transparency/opacity"),100):0;
  Slider(panel,"Fade unfocused windows",fade,60,v=>apply(new Dictionary<string,string>{{"window_effects/other_windows/transparency/enabled",v>0?"true":"false"},{"window_effects/other_windows/transparency/opacity","'"+(100-v)+"%'"}}));
  Slider(panel,"Gap between windows",Number(get("gaps/inner_gap"),20),40,v=>apply(new Dictionary<string,string>{{"gaps/inner_gap","'"+v+"px'"}}),"px");
  // With a screen frame, the config's edge gaps include its thickness (ScreenFrame); the slider shows the gap inside it.
  int frame=BarAppearance.Settings.frame;
  Slider(panel,"Gap at screen edges",Math.Max(0,Number(get("gaps/outer_gap/left"),20)-frame),40,v=>{var g="'"+(v+BarAppearance.Settings.frame)+"px'";apply(new Dictionary<string,string>{{"gaps/outer_gap/left",g},{"gaps/outer_gap/right",g},{"gaps/outer_gap/bottom",g}});},"px");
  Slider(panel,"Gap below the bar",Number(get("gaps/outer_gap/top"),12),40,v=>apply(new Dictionary<string,string>{{"gaps/outer_gap/top","'"+v+"px'"}}),"px");
  return panel;
 }

 static void Choice(StackPanel panel,string text,string[] values,string current,Action<string> pick){Action<string> ignore;Choice(panel,text,values,current,pick,out ignore);}
 /// <summary>A row of options; <paramref name="show"/> highlights a value set elsewhere without applying it.</summary>
 static Grid Choice(StackPanel panel,string text,string[] values,string current,Action<string> pick,out Action<string> show){
  panel.Children.Add(new TextBlock{Text=text,FontSize=12,Margin=new Thickness(0,0,0,4)});
  var row=new Grid{Margin=new Thickness(0,0,0,10)};panel.Children.Add(row);var buttons=new List<Button>();
  Action paint=()=>{foreach(var b in buttons){bool on=(string)b.Tag==current;b.Background=B(on?"Selected":"Raised");b.Foreground=B(on?"Text":"Muted");}};
  foreach(var option in values){
   var value=option;var b=RiceLayout.Button(option,"Glaze"+text.Replace(" ","")+"_"+option);b.Tag=option;b.Margin=new Thickness(buttons.Count==0?0:6,0,0,0);
   row.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(b,buttons.Count);row.Children.Add(b);buttons.Add(b);
   b.Click+=delegate{if(value==current)return;current=value;paint();pick(value);};
  }
  paint();show=value=>{current=value;paint();};panel.Children.Add(RiceLayout.Hint(RiceLayout.Explain(text)));return row;
 }

 /// <summary>A slider that applies after the user pauses dragging (each apply reloads GlazeWM).</summary>
 static void Slider(StackPanel panel,string text,int value,int max,Action<int> change,string unit="%",int min=0){
  var cell=new StackPanel{Tag="GlazeSliderCell"};var label=new TextBlock{FontSize=12,Margin=new Thickness(0,4,0,4)};cell.Children.Add(label);
  var slider=new Slider{Minimum=min,Maximum=max,TickFrequency=1,IsSnapToTickEnabled=true,Value=Math.Max(min,Math.Min(max,value)),Name="Glaze"+text.Replace(" ","")};
  Action show=()=>label.Text=text+"  "+(int)slider.Value+unit;show();
  var settle=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};settle.Tick+=delegate{settle.Stop();change((int)slider.Value);};
  slider.ValueChanged+=delegate{show();settle.Stop();settle.Start();};
  cell.Children.Add(slider);cell.Children.Add(RiceLayout.Hint(RiceLayout.Explain(text)));var previous=panel.Children.Count==0?null:panel.Children[panel.Children.Count-1] as StackPanel;if(previous!=null&&Object.Equals(previous.Tag,"GlazeSliderCell")){panel.Children.Remove(previous);panel.Children.Add(RiceLayout.Pair(previous,cell));}else panel.Children.Add(cell);
 }

 static int Number(string text,int fallback){var m=Regex.Match(text??"",@"\d+");return m.Success?Int32.Parse(m.Value):fallback;}
 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
}

/// <summary>
/// GlazeWM's window corners from the rice's corner radius when "Match" is on: 0% square, under 50% small,
/// otherwise rounded (Windows' three styles). Applied half a second after the radius settles, and GlazeWM only
/// reloads when the style actually changes.
/// </summary>
static class GlazeCorners {
 static readonly string[] Windows={"window_effects/focused_window","window_effects/other_windows"};
 static DispatcherTimer settle;static Action changed;

 public static void Start(){
  if(settle!=null)return;settle=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};settle.Tick+=delegate{settle.Stop();Sync();};
  changed=()=>{if(settle!=null&&BarAppearance.Settings.syncCorners){settle.Stop();settle.Start();}};BarAppearance.Changed+=changed;
  Sync();
 }

 public static void Stop(){if(changed!=null){BarAppearance.Changed-=changed;changed=null;}if(settle!=null){settle.Stop();settle=null;}}
 public static string Style(int radius){return radius<=0?"Square":radius<50?"Small":"Rounded";}

 /// <summary>The config changes for a corner choice, for both focused and other windows.</summary>
 public static Dictionary<string,string> Changes(string choice){
  var changes=new Dictionary<string,string>();
  foreach(var w in Windows){changes[w+"/corner_style/enabled"]=choice=="Default"?"false":"true";changes[w+"/corner_style/style"]=choice=="Small"?"'small_rounded'":choice=="Rounded"?"'rounded'":"'square'";}
  return changes;
 }

 public static void Sync(){
  if(!BarAppearance.Settings.syncCorners)return;
  try{GlazeConfig.Set(Changes(Style(BarAppearance.Settings.radius)));FocusBorder.Refresh();}
  catch(Exception e){WallpaperSwitcher.Log("glazewm corners: "+e.Message);}
 }
}
