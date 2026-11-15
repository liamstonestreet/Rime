using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

sealed class RiceSettings {
 public static readonly string[] Left={"notifications","workspaces","direction","search","rules"};
 public static readonly string[] Right={"quickActions","volume","spotify","helium","tray","memory","cpu","vpn","power"};
 public static readonly string[] Keys=Left.Concat(Right).ToArray();
 public static readonly Dictionary<string,string> Labels=new Dictionary<string,string>{{"notifications","Notifications"},{"quickActions","Quick actions"},{"workspaces","Workspaces"},{"direction","Tiling direction"},{"search","Search bar"},{"rules","App rules"},{"volume","Volume"},{"spotify","Spotify"},{"helium","Helium"},{"tray","Tray"},{"memory","Memory"},{"cpu","CPU"},{"vpn","VPN"},{"power","Power"}};
 public Dictionary<string,bool> Enabled=new Dictionary<string,bool>();public List<string> left=new List<string>(Left),right=new List<string>(Right);
 Dictionary<string,object> extras=new Dictionary<string,object>();
 public RiceSettings(){foreach(var key in Keys)Enabled[key]=true;}
 public static RiceSettings Parse(string text){var result=new RiceSettings();try{var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(text);result.extras=data;foreach(var key in Keys)if(data.ContainsKey(key)&&data[key] is bool)result.Enabled[key]=(bool)data[key];
  object raw;var seen=new HashSet<string>();
  if(data.TryGetValue("layout",out raw)&&raw is Dictionary<string,object>){var layout=(Dictionary<string,object>)raw;
   Func<string,List<string>> lane=name=>{var list=new List<string>();object items;if(layout.TryGetValue(name,out items)&&items is IEnumerable && !(items is string))foreach(var item in (IEnumerable)items){var key=item as string;if(key!=null&&Labels.ContainsKey(key)&&seen.Add(key))list.Add(key);}return list;};
   result.left=lane("left");result.right=lane("right");if(seen.Add("notifications"))result.left.Insert(0,"notifications");if(seen.Add("quickActions"))result.right.Insert(0,"quickActions");foreach(var key in Left)if(seen.Add(key))result.left.Add(key);foreach(var key in Right)if(seen.Add(key))result.right.Add(key);
  }
 }catch{}return result;}
 public static RiceSettings Load(){try{return Parse(File.ReadAllText(Path.Combine(AppCatalog.Folder,"rice-widgets.json")));}catch{return new RiceSettings();}}
 public string Json(){var data=new Dictionary<string,object>(extras);data.Remove("battery");foreach(var pair in Enabled)data[pair.Key]=pair.Value;data["layout"]=new{version=1,left=left.ToArray(),right=right.ToArray()};return new JavaScriptSerializer().Serialize(data);}
 public RiceSettings Clone(){return Parse(Json());}
 public void Move(string key,bool toRight,int delta){var source=left.Contains(key)?left:right;int index=source.IndexOf(key);if(index<0)return;var destination=toRight?right:left;if(source!=destination){source.RemoveAt(index);destination.Add(key);}else{int next=Math.Max(0,Math.Min(source.Count-1,index+delta));source.RemoveAt(index);source.Insert(next,key);}}
 public void Save(){var path=Path.Combine(AppCatalog.Folder,"rice-widgets.json");var text=Json();File.WriteAllText(path+".tmp",text);if(File.Exists(path))File.Replace(path+".tmp",path,path+".previous");else File.Move(path+".tmp",path);}
}
static class RiceLayout {
 public static TextBlock Hint(string text){return new TextBlock{Text=text,FontSize=10,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,9)};}
 public static string Explain(string text){switch(text){
  case "Transparency":return "Higher values reveal more wallpaper behind the panel and bar.";
  case "Surface contrast":return "Separates buttons and fields from their background.";
  case "Frost blur":return "Softens the wallpaper visible through frosted surfaces.";
  case "Frost brightness":return "50% preserves brightness; lower darkens and higher lightens the frost.";
  case "Grain":return "Adds subtle texture to the bar and panels. 0% removes it.";
  case "Corner radius":return "Rounds panel, widget and indicator corners. 0% makes them square.";
  case "Animation speed":return "Changes animation timing. 0% disables motion.";
  case "Edge":return "Chooses the outline style around the bar and Control Center.";
  case "Motion":return "Smooth uses gentle easing; Springy adds a small bounce.";
  case "Fade unfocused windows":return "Dims windows that are not focused. 0% keeps them fully opaque.";
  case "Gap between windows":return "Space between tiled windows, in pixels.";
  case "Gap at screen edges":return "Space between windows and the sides/bottom of the screen frame.";
  case "Gap below the bar":return "Space between the top bar and tiled windows.";
  case "Window borders":return "Shows or hides Windows' thin window outlines.";
  case "Title bars":return "Shows or hides the app's Windows title bar; some apps draw their own.";
  case "Window corners":return "Uses Windows' default, square, small-rounded or rounded window corners.";
  case "Hidden workspaces":return "Cloak keeps windows managed but invisible; Hide uses Windows' normal hiding behavior.";
  default:return "Choose how "+text.ToLowerInvariant()+" behaves. Changes apply immediately.";
 }}
 static StackPanel LockScreenControl(){var panel=new StackPanel{Margin=new Thickness(0,14,0,0)};panel.Children.Add(new TextBlock{Text="Lock screen",FontSize=19,FontWeight=FontWeights.Medium});var toggle=Button("","RiceLockScreenSync");Action paint=()=>toggle.Content=BarAppearance.Settings.lockScreenSync?"Sync wallpaper: On":"Sync wallpaper: Off";paint();var settle=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(450)};settle.Tick+=delegate{settle.Stop();LockScreenWallpaper.Queue(WallpaperSource.Windows());};toggle.Click+=delegate{BarAppearance.Update(s=>s.lockScreenSync=!s.lockScreenSync);paint();settle.Stop();settle.Start();};panel.Children.Add(toggle);panel.Children.Add(Hint("After a wallpaper change, sets a blurred copy as your Windows lock-screen picture. Your desktop image stays sharp."));var label=new TextBlock{FontSize=12,Margin=new Thickness(0,4,0,4)};var blur=new Slider{Name="RiceLockScreenBlur",Minimum=0,Maximum=100,TickFrequency=5,IsSnapToTickEnabled=true,Value=BarAppearance.Settings.lockScreenBlur};Action show=()=>label.Text="Lock-screen blur  "+(int)blur.Value+"%";show();blur.ValueChanged+=delegate{show();BarAppearance.Update(s=>s.lockScreenBlur=(int)blur.Value);settle.Stop();settle.Start();};panel.Children.Add(label);panel.Children.Add(blur);panel.Children.Add(Hint("0% keeps the picture sharp. Higher values soften details. Changing this also updates the current lock-screen copy."));var status=Hint(LockScreenWallpaper.Status);panel.Children.Add(status);Action update=()=>status.Text=LockScreenWallpaper.Status;panel.Loaded+=delegate{LockScreenWallpaper.Changed+=update;update();};panel.Unloaded+=delegate{LockScreenWallpaper.Changed-=update;settle.Stop();};return panel;}
 public static Grid Pair(FrameworkElement left,FrameworkElement right){
  var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});row.ColumnDefinitions.Add(new ColumnDefinition());row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});row.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});row.Children.Add(left);row.Children.Add(right);Grid.SetColumn(right,2);
  row.SizeChanged+=delegate{bool compact=row.ActualWidth<500;row.ColumnDefinitions[1].Width=new GridLength(compact?0:16);row.ColumnDefinitions[2].Width=compact?new GridLength(0):new GridLength(1,GridUnitType.Star);Grid.SetColumn(right,compact?0:2);Grid.SetRow(right,compact?1:0);right.Margin=new Thickness(0,compact?10:0,0,0);};return row;
 }
 public static StackPanel RibbonControl(){var panel=new StackPanel{Margin=new Thickness(0,0,0,12)};var label=new TextBlock{FontSize=12,Margin=new Thickness(0,0,0,4)};var slider=new Slider{Name="AudioRibbonSensitivity",Minimum=25,Maximum=400,TickFrequency=5,IsSnapToTickEnabled=true,Value=BarAppearance.Settings.ribbonSensitivity};Action show=()=>label.Text="Ribbon sensitivity  "+(int)slider.Value+"%";show();slider.ValueChanged+=delegate{show();BarAppearance.Update(s=>s.ribbonSensitivity=(int)slider.Value);};panel.Children.Add(label);panel.Children.Add(slider);panel.Children.Add(new TextBlock{Text="Higher sensitivity makes the ribbons rise and move more with quiet audio.",FontSize=10,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0)});var heightPanel=new StackPanel();var heightLabel=new TextBlock{FontSize=12,Margin=new Thickness(0,0,0,4)};var heightSlider=new Slider{Name="AudioPlayerHeight",Minimum=75,Maximum=175,TickFrequency=5,IsSnapToTickEnabled=true,Value=BarAppearance.Settings.mediaPlayerHeight};Action showHeight=()=>heightLabel.Text="Player height  "+(int)heightSlider.Value+"%";showHeight();heightSlider.ValueChanged+=delegate{showHeight();BarAppearance.Update(s=>s.mediaPlayerHeight=(int)heightSlider.Value);};heightPanel.Children.Add(heightLabel);heightPanel.Children.Add(heightSlider);heightPanel.Children.Add(new TextBlock{Text="100% keeps the current height. Artwork and controls stay aligned.",FontSize=10,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0)});var controls=new StackPanel{Margin=new Thickness(0,0,0,12)};panel.Margin=new Thickness(0);controls.Children.Add(Pair(panel,heightPanel));return controls;}
 internal static bool FontPickerOpen;
 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
 // Bar appearance applies immediately (no Apply step), separately from the staged layout below.
 static StackPanel Appearance(){
  var panel=new StackPanel();
  panel.Children.Add(new TextBlock{Text="Appearance",FontSize=19,FontWeight=FontWeights.Medium});
  panel.Children.Add(new TextBlock{Text="Applies to all bars right away. Lock-in keeps its own colors.",FontSize=11,Foreground=B("Muted"),Margin=new Thickness(0,6,0,10),TextWrapping=TextWrapping.Wrap});
  panel.Children.Add(new TextBlock{Text="Font",FontSize=12,Margin=new Thickness(0,0,0,4)});
  var font=new ComboBox{Name="RiceFont",IsEditable=true,IsTextSearchEnabled=false,StaysOpenOnEdit=true,MaxDropDownHeight=240,ItemsSource=RiceFont.Names,Text=BarAppearance.Settings.fontFamily??"Rubik",Margin=new Thickness(0,0,0,10),ToolTip=RiceFont.Folder+" - Select a font, or type a family name and press Enter."};
  // The normal audio picker template displays FriendlyName; this one also exposes an editor.
  font.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'><Grid><Border Background='{DynamicResource ThemeRaised}' BorderBrush='{DynamicResource ThemeBorder}' BorderThickness='1' CornerRadius='8'/><ToggleButton Width='32' HorizontalAlignment='Right' Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='Transparent'><Path Data='M0,0 L4,4 L8,0' Stroke='{DynamicResource ThemeMuted}' StrokeThickness='1.5' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><TextBox x:Name='PART_EditableTextBox' Margin='10,6,34,6' Background='Transparent' Foreground='{DynamicResource ThemeText}' CaretBrush='{DynamicResource ThemeText}' BorderThickness='0' VerticalContentAlignment='Center'/><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'><Border Background='{DynamicResource ThemeRaised}' BorderBrush='{DynamicResource ThemeBorder}' BorderThickness='1' Padding='3' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'><ScrollViewer MaxHeight='240'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate>");
  Action applyFont=()=>{var name=font.Text.Trim();if(name.Length==0){font.Text=BarAppearance.Settings.fontFamily??"Rubik";return;}if(name!=BarAppearance.Settings.fontFamily)BarAppearance.Update(s=>s.fontFamily=name);};
  font.DropDownClosed+=delegate{applyFont();};font.LostKeyboardFocus+=delegate{if(!font.IsKeyboardFocusWithin&&!font.IsDropDownOpen)applyFont();};
  font.PreviewKeyDown+=(sender,e)=>{if(e.Key==System.Windows.Input.Key.Enter){applyFont();font.IsDropDownOpen=false;e.Handled=true;}};
  var fontRow=new Grid{Margin=new Thickness(0,0,0,10)};fontRow.ColumnDefinitions.Add(new ColumnDefinition());fontRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  font.Margin=new Thickness(0);fontRow.Children.Add(font);
  var refreshFonts=Button("Refresh","RiceFontRefresh");refreshFonts.Margin=new Thickness(6,0,0,0);refreshFonts.ToolTip="Rescan the Fonts folder";Grid.SetColumn(refreshFonts,1);fontRow.Children.Add(refreshFonts);
  refreshFonts.Click+=delegate{var text=font.Text;RiceFont.Refresh();font.ItemsSource=RiceFont.Names;font.Text=text;BarAppearance.Update(s=>s.fontRevision=(s.fontRevision+1)&Int32.MaxValue);};
  font.DropDownOpened+=delegate{FontPickerOpen=true;};font.DropDownClosed+=delegate{FontPickerOpen=false;};font.Unloaded+=delegate{FontPickerOpen=false;};
  panel.Children.Add(fontRow);
  panel.Children.Add(Hint("Choose a font or type its family name and press Enter. Refresh rescans your Fonts folder."));
  var dimensions=new Grid{Margin=new Thickness(0,0,0,10)};dimensions.ColumnDefinitions.Add(new ColumnDefinition());dimensions.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});dimensions.ColumnDefinitions.Add(new ColumnDefinition());var widthPanel=new StackPanel();var heightPanel=new StackPanel();Grid.SetColumn(heightPanel,2);dimensions.Children.Add(widthPanel);dimensions.Children.Add(heightPanel);panel.Children.Add(dimensions);
  var widthLabel=new TextBlock{Text="Control Center width  "+BarAppearance.Settings.centerWidth+" px",FontSize=12,Margin=new Thickness(0,4,0,4)};widthPanel.Children.Add(widthLabel);
  var centerWidth=new Slider{Name="RiceCenterWidth",Minimum=480,Maximum=1200,TickFrequency=8,IsSnapToTickEnabled=true,Value=BarAppearance.Settings.centerWidth,ToolTip="Width in logical pixels; fits the monitor automatically."};
  centerWidth.ValueChanged+=delegate{int value=(int)Math.Round(centerWidth.Value);widthLabel.Text="Control Center width  "+value+" px";BarAppearance.Update(s=>s.centerWidth=value);};widthPanel.Children.Add(centerWidth);widthPanel.Children.Add(Hint("Panel width in logical pixels. Fits the monitor automatically."));
  var heightLabel=new TextBlock{Text="Max height  "+BarAppearance.Settings.centerMaxHeight+"% of monitor",FontSize=12,Margin=new Thickness(0,4,0,4)};heightPanel.Children.Add(heightLabel);var centerHeight=new Slider{Name="RiceCenterMaxHeight",Minimum=25,Maximum=90,TickFrequency=5,IsSnapToTickEnabled=true,Value=BarAppearance.Settings.centerMaxHeight};centerHeight.ValueChanged+=delegate{int value=(int)centerHeight.Value;heightLabel.Text="Max height  "+value+"% of monitor";BarAppearance.Update(s=>s.centerMaxHeight=value);};heightPanel.Children.Add(centerHeight);heightPanel.Children.Add(Hint("Maximum panel height as a percentage of the monitor it is on."));
  var toggles=new Grid{Margin=new Thickness(0,0,0,10)};for(int i=0;i<3;i++)toggles.ColumnDefinitions.Add(new ColumnDefinition{Width=i==1?new GridLength(6):new GridLength(1,GridUnitType.Star)});panel.Children.Add(toggles);
  var source=new TextBlock{FontSize=11,Foreground=B("Muted"),Margin=new Thickness(0,8,0,0),TextTrimming=TextTrimming.CharacterEllipsis};
  Action showSource=()=>{var path=BarAppearance.Wallpaper;source.Text=BarAppearance.Settings.wallpaperColors?(path==null?"Reading wallpaper…":"Colors from "+Path.GetFileName(Path.GetDirectoryName(path))+"\\"+Path.GetFileName(path)):"";};
  Func<string,string,Func<AppearanceSettings,bool>,Action<AppearanceSettings>,Button> toggle=(text,name,get,flip)=>{
   var button=Button(text,name);button.Margin=new Thickness(0);
   Action paint=()=>{bool on=get(BarAppearance.Settings);button.Background=B(on?"Selected":"Raised");button.Foreground=B(on?"Text":"Muted");};
   button.Click+=delegate{BarAppearance.Update(flip);paint();showSource();};paint();return button;};
  var frost=toggle("Frosted glass","RiceFrost",s=>s.frost,s=>s.frost=!s.frost);
  var colors=toggle("Match wallpaper colors","RiceWallpaperColors",s=>s.wallpaperColors,s=>s.wallpaperColors=!s.wallpaperColors);
  Grid.SetColumn(colors,2);toggles.Children.Add(frost);toggles.Children.Add(colors);
  panel.Children.Add(Hint("Frosted glass shows a softened wallpaper behind panels. Match wallpaper colors derives the theme from your current image."));
  panel.Children.Add(new AppWindowTintControls());
  // A row of mutually exclusive options (one highlighted), applied as they are clicked.
  Action<string,string[],Func<AppearanceSettings,string>,Action<AppearanceSettings,string>> choice=(text,values,get,set)=>{
   panel.Children.Add(new TextBlock{Text=text,FontSize=12,Margin=new Thickness(0,0,0,4)});
   var row=new Grid{Margin=new Thickness(0,0,0,10)};panel.Children.Add(row);var buttons=new List<Button>();
   Action paint=()=>{foreach(var b in buttons){bool on=(string)b.Tag==get(BarAppearance.Settings);b.Background=B(on?"Selected":"Raised");b.Foreground=B(on?"Text":"Muted");}};
   foreach(var option in values){
    var value=option;var b=Button(Char.ToUpperInvariant(option[0])+option.Substring(1),"Rice"+text.Replace(" ","")+"_"+option);b.Tag=option;b.Margin=new Thickness(buttons.Count==0?0:6,0,0,0);
    row.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetColumn(b,buttons.Count);row.Children.Add(b);buttons.Add(b);
    b.Click+=delegate{BarAppearance.Update(s=>set(s,value));paint();};
   }
   paint();if(text=="Edge"||text=="Motion")panel.Children.Add(Hint(Explain(text)));};
  // The bar's bottom line (and the control center's outline), and how everything moves.
  choice("Edge",AppearanceSettings.EdgeStyles,s=>s.edgeStyle,(s,v)=>s.edgeStyle=v);
  choice("Motion",AppearanceSettings.MotionStyles,s=>s.motionStyle,(s,v)=>s.motionStyle=v);
  choice("CC animation",AppearanceSettings.PanelAnimations,s=>s.centerAnimation??"slide",(s,v)=>s.centerAnimation=v);
  panel.Children.Add(Hint("Fade changes opacity in place. Slide uses the current attached opening motion."));
  choice("Notification animation",AppearanceSettings.PanelAnimations,s=>s.notificationAnimation??"slide",(s,v)=>s.notificationAnimation=v);
  panel.Children.Add(Hint("Sets notification opening and closing independently of the Control Center."));
  choice("Workspace indicator",AppearanceSettings.IndicatorStyles,s=>s.indicatorStyle,(s,v)=>s.indicatorStyle=v);
  panel.Children.Add(new TextBlock{Text="Classic: all workspaces. Gallery: large neighboring cards. Ribbon: a quiet number and sliding rail. Applies on the next indicator open.",FontSize=10,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,-4,0,10)});
  choice("Wallpaper indicator",AppearanceSettings.WallpaperIndicatorStyles,s=>s.wallpaperIndicatorStyle,(s,v)=>s.wallpaperIndicatorStyle=v);
  panel.Children.Add(new TextBlock{Text="Classic: thumbnail strip. Showcase: a large cover carousel. Filmstrip: a compact numbered contact strip. Applies on the next indicator open.",FontSize=10,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,-4,0,10)});
  // 0-100% sliders that apply as they move.
  StackPanel pendingSlider=null;
  Action<string,string,int,Action<AppearanceSettings,int>,int> percent=(text,name,value,set,max)=>{
   var cell=new StackPanel();var label=new TextBlock{FontSize=12,Margin=new Thickness(0,4,0,4)};cell.Children.Add(label);
   var slider=new Slider{Minimum=name=="RiceRibbonSensitivity"?25:0,Maximum=max,TickFrequency=5,IsSnapToTickEnabled=true,Value=value,Name=name};
   Action showLabel=()=>label.Text=text+"  "+(int)slider.Value+"%";showLabel();
   slider.ValueChanged+=delegate{showLabel();int next=(int)slider.Value;BarAppearance.Update(s=>set(s,next));};
   cell.Children.Add(slider);cell.Children.Add(Hint(Explain(text)));if(pendingSlider==null){panel.Children.Add(cell);pendingSlider=cell;}else{panel.Children.Remove(pendingSlider);panel.Children.Add(Pair(pendingSlider,cell));pendingSlider=null;}};
  percent("Transparency","RiceTransparency",BarAppearance.Settings.transparency,(s,v)=>s.transparency=v,100);
  percent("Surface contrast","RiceSurfaceContrast",BarAppearance.Settings.surfaceContrast,(s,v)=>s.surfaceContrast=v,100);
  panel.Children.Add(new TextBlock{Text="Separates search, app groups and CC controls from their background. 0% keeps the original look; try 25\u201335% for a subtle lift.",FontSize=10,Foreground=B("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});
  percent("Frost blur","RiceBlur",BarAppearance.Settings.blur,(s,v)=>s.blur=v,100);
  percent("Frost brightness","RiceBrightness",BarAppearance.Settings.brightness,(s,v)=>s.brightness=v,100);
  percent("Grain","RiceGrain",BarAppearance.Settings.grain,(s,v)=>s.grain=v,100);
  percent("Corner radius","RiceRadius",BarAppearance.Settings.radius,(s,v)=>s.radius=v,200);
  percent("Animation speed","RiceMotion",BarAppearance.Settings.motion,(s,v)=>s.motion=v,200);
  // Screen frame thickness: − and + step one pixel, Off removes the frame (0). Its inner corners follow the
  // corner radius above, and GlazeWM's edge gaps follow the thickness (ScreenFrame).
  var frameLabel=new TextBlock{FontSize=12,Margin=new Thickness(0,4,0,4)};panel.Children.Add(frameLabel);
  var frameRow=new Grid{Margin=new Thickness(0,0,0,10)};panel.Children.Add(frameRow);
  foreach(var width in new[]{GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto})frameRow.ColumnDefinitions.Add(new ColumnDefinition{Width=width});
  var minus=Button("\u2212","RiceFrameMinus");var plus=Button("+","RiceFramePlus");var off=Button("Off","RiceFrameOff");
  minus.Width=plus.Width=34;minus.Margin=new Thickness(0);plus.Margin=new Thickness(0);off.Margin=new Thickness(6,0,0,0);off.Padding=new Thickness(12,7,12,7);
  var frameSlider=new Slider{Minimum=0,Maximum=24,TickFrequency=1,IsSnapToTickEnabled=true,Value=BarAppearance.Settings.frame,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,10,0),Name="RiceFrame"};
  Grid.SetColumn(frameSlider,1);Grid.SetColumn(plus,2);Grid.SetColumn(off,3);
  frameRow.Children.Add(minus);frameRow.Children.Add(frameSlider);frameRow.Children.Add(plus);frameRow.Children.Add(off);
  Action showFrame=()=>{int v=(int)frameSlider.Value;frameLabel.Text="Screen frame thickness  "+(v==0?"off":v+" px");off.Background=B(v==0?"Selected":"Raised");off.Foreground=B(v==0?"Text":"Muted");};
  frameSlider.ValueChanged+=delegate{showFrame();int v=(int)frameSlider.Value;BarAppearance.Update(s=>s.frame=v);};
  minus.Click+=delegate{frameSlider.Value=Math.Max(0,frameSlider.Value-1);};
  plus.Click+=delegate{frameSlider.Value=Math.Min(24,frameSlider.Value+1);};
  off.Click+=delegate{frameSlider.Value=0;};
  showFrame();
  panel.Children.Add(Hint("Sets the screen-edge frame thickness. Off removes the frame; tiled-window gaps follow its size."));
  panel.Children.Add(LockScreenControl());panel.Children.Add(BatchAppsView.Build());
  panel.Children.Add(source);showSource();
  // The wallpaper is read in the background; refresh the caption once it is known.
  var refresh=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};refresh.Tick+=delegate{showSource();};
  panel.Loaded+=delegate{refresh.Start();};panel.Unloaded+=delegate{refresh.Stop();};
  return panel;
 }
 internal static Button Button(string text,string name){return new Button{Content=text,Name=name,FontSize=12,Padding=new Thickness(8,7,8,7),BorderThickness=new Thickness(1),BorderBrush=B("Border"),Background=B("Raised"),Margin=new Thickness(0,0,5,0)};}
 /// <summary>The Configure tab: appearance, layout, window management and keybinds.</summary>
 public static StackPanel BuildAppearance(){var panel=new StackPanel{Margin=new Thickness(0,14,0,0)};panel.Children.Add(Appearance());panel.Children.Add(BuildLayout());panel.Children.Add(GlazeTab.Build());panel.Children.Add(IndicatorKeyControl.Build());return panel;}

 /// <summary>The Layout tab: which bar widgets show, and where.</summary>
 public static StackPanel BuildLayout(){
  var saved=RiceSettings.Load();var draft=saved.Clone();
  var panel=new StackPanel{Margin=new Thickness(0,14,0,0)};
  panel.Children.Add(new TextBlock{Text="Widgets & layout",FontSize=19,FontWeight=FontWeights.Medium});
  panel.Children.Add(new TextBlock{Text="Click a name to show or hide it. Arrows change its position.",FontSize=11,Foreground=B("Muted"),Margin=new Thickness(0,6,0,12),TextWrapping=TextWrapping.Wrap});
  var lanes=new Grid();lanes.ColumnDefinitions.Add(new ColumnDefinition());lanes.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(10)});lanes.ColumnDefinitions.Add(new ColumnDefinition());panel.Children.Add(lanes);
  var lists=new[]{new StackPanel(),new StackPanel()};
  for(int i=0;i<2;i++){var col=new StackPanel();Grid.SetColumn(col,i*2);lanes.Children.Add(col);col.Children.Add(new TextBlock{Text=i==0?"Left side":"Right side",FontWeight=FontWeights.Medium,FontSize=12,Margin=new Thickness(0,0,0,8)});col.Children.Add(new ScrollViewer{Content=lists[i],Height=258,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});}
  panel.Children.Add(new TextBlock{Text="Clock stays centered. Workspace apps keep their reserved space.\nEach side scrolls horizontally if the bar gets crowded.",FontSize=11,Foreground=B("Muted"),Margin=new Thickness(0,12,0,8),TextWrapping=TextWrapping.Wrap});
  var status=new TextBlock{Name="RiceStatus",Text="Changes are staged until Apply.",FontSize=11,Foreground=B("Muted"),Margin=new Thickness(0,0,0,10)};panel.Children.Add(status);
  var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(footer);
  var reset=Button("Reset layout","RiceReset");var discard=Button("Discard","RiceDiscard");var apply=Button("Apply","RiceApply");footer.Children.Add(reset);footer.Children.Add(discard);footer.Children.Add(apply);
  Action dirty=()=>{apply.IsEnabled=discard.IsEnabled=draft.Json()!=saved.Json();status.Text=apply.IsEnabled?"Unapplied changes":"Changes are staged until Apply.";};
  Action rebuild=null;rebuild=()=>{
   foreach(var list in lists)list.Children.Clear();
   for(int side=0;side<2;side++){bool right=side==1;var keys=right?draft.right:draft.left;
    foreach(var keyValue in keys){string key=keyValue;var row=new Grid{Margin=new Thickness(0,0,0,5)};row.ColumnDefinitions.Add(new ColumnDefinition());for(int j=0;j<3;j++)row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(23)});
     var label=Button("", "RiceWidget_"+key);label.Content=new TextBlock{Text=RiceSettings.Labels[key],TextTrimming=TextTrimming.CharacterEllipsis,FontSize=11};label.Padding=new Thickness(5,8,5,8);label.Margin=new Thickness(0,0,3,0);label.Background=B(draft.Enabled[key]?"Selected":"Raised");label.Foreground=B(draft.Enabled[key]?"Text":"Muted");label.ToolTip=RiceSettings.Labels[key]+(draft.Enabled[key]?" · visible":" · hidden");label.Click+=delegate{draft.Enabled[key]=!draft.Enabled[key];rebuild();dirty();};row.Children.Add(label);
     var up=Button("↑","RiceUp_"+key);var down=Button("↓","RiceDown_"+key);var move=Button(right?"←":"→","RiceMove_"+key);var buttons=new[]{up,down,move};
     for(int j=0;j<3;j++){buttons[j].Padding=new Thickness(0);buttons[j].Margin=new Thickness(1,0,0,0);buttons[j].BorderThickness=new Thickness(0);Grid.SetColumn(buttons[j],j+1);row.Children.Add(buttons[j]);}
     up.ToolTip="Move earlier";down.ToolTip="Move later";move.ToolTip=right?"Move to left side":"Move to right side";up.IsEnabled=keys.IndexOf(key)>0;down.IsEnabled=keys.IndexOf(key)<keys.Count-1;
     up.Click+=delegate{draft.Move(key,right,-1);rebuild();dirty();};down.Click+=delegate{draft.Move(key,right,1);rebuild();dirty();};move.Click+=delegate{draft.Move(key,!right,0);rebuild();dirty();};lists[side].Children.Add(row);
    }
   }
  };
  reset.Click+=delegate{draft.left=new List<string>(RiceSettings.Left);draft.right=new List<string>(RiceSettings.Right);rebuild();dirty();};
  discard.Click+=delegate{draft=saved.Clone();rebuild();dirty();};
  apply.Click+=delegate{try{draft.Save();saved=draft.Clone();dirty();status.Text="Applied to all bars";}catch(Exception ex){status.Text="Could not save: "+ex.Message;}};
  rebuild();dirty();return panel;
 }
}
