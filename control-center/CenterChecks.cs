using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
class CenterChecks {
 [STAThread] static void Main(){try{Run();}catch(Exception e){Console.Error.WriteLine("FAIL: "+e);Environment.ExitCode=1;}}
 static void Run(){
  DisplayCadence.Initialize();
  var previewSource=WallpaperSource.Current();if(previewSource!=null)WallpaperPalette.Save(WallpaperTheme.From(previewSource));
  BarAppearance.AutoSync=false;CalendarView.SourceFactory=()=>CalendarChecks.Fixture();
  File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rice-widgets.json"));
  var sample="window_effects:\n  focused_window:\n    border:\n      color: '#123456'\nkeybindings:\n  - commands: ['focus --workspace 6']\n    bindings: ['alt+6']\n  - commands: ['focus --workspace 60']\n";
  var focusState=new FocusState{endsUtc=DateTime.UtcNow.AddMinutes(1).ToString("o")};FocusMode.Validate(focusState.settings);
  var patched=FocusMode.Patch(sample,focusState);
  if(!patched.Contains("focus --workspace 1")||!patched.Contains("focus --workspace 60")||!patched.Contains("#315C45")||!focusState.Active)throw new Exception("Focus policy patch failed");
  if(FocusMode.Restore(patched+"# unrelated edit\n",focusState)!=sample+"# unrelated edit\n")throw new Exception("Focus restoration discarded edits");
  focusState.endsUtc=DateTime.UtcNow.AddSeconds(-1).ToString("o");if(focusState.Active)throw new Exception("Focus expiration failed");
  Console.WriteLine("PASS: focus config patch, exact workspace matching, restoration preserves edits, timer expiry.");
  var app=new Application();var monitorSource=new PanelPlacementChecks.Monitors{Bounds=System.Windows.Forms.Screen.PrimaryScreen.Bounds};var positionTarget=new PanelPlacementChecks.Target();var win=new NordControlCenter(monitorSource,positionTarget);
  AppWindowTintChecks.Run();AppServicesChecks.Run();AudioPeakSamplerChecks.Run();NotificationChecks.Run(win);PanelPlacementChecks.Run();PanelAnimationChecks.Run();
  var placement=(PanelPlacementController)typeof(NordControlCenter).GetField("placement",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win);
  ((CalendarView)typeof(NordControlCenter).GetField("calendar",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win)).ShowSnapshot(CalendarChecks.Fixture().Data);CalendarChecks.Run(win);
  FocusChecks.Run(win);
  WallpaperChecks.Run(win);
  WallpaperCategoryChecks.Run();
  WallpaperFadeChecks.Run();
  FocusBorderChecks.Run();
  ThemeTransitionChecks.Run();ThemeCoordinatorChecks.Run();
  QuickActionChecks.Run();
  TaskPinChecks.Run();WorkspaceLaunchChecks.Run();
  MediaVisualChecks.Run();
  LockScreenChecks.Run();
  BridgeOrderingChecks.Run();BridgeProtocolChecks.Run();
  AudioControllerChecks.Run();StereoBalanceChecks.Run();
  var basePalette=new ControlCenterPalette(new FocusState());var liftedPalette=new ControlCenterPalette(new FocusState());
  ControlCenterTheme.SurfaceContrast(liftedPalette,30);
  if(liftedPalette.Colors["Raised"]==basePalette.Colors["Raised"]||liftedPalette.Colors["Surface"]!=basePalette.Colors["Surface"]||liftedPalette.Colors["Accent"]!=basePalette.Colors["Accent"]||liftedPalette.Colors["Text"]!=basePalette.Colors["Text"])throw new Exception("Surface contrast must separate controls while preserving base, text and accent");
  Console.WriteLine("PASS: surface contrast separates controls while preserving palette hues and base/text roles.");
  FrameGlowChecks.Run();
  if(RiceFont.Names.Length>7||RiceFont.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=RiceFont.Names.Length)throw new Exception("Font suggestions must be unique and capped at seven");
  foreach(var name in RiceFont.Names){var data=RiceFont.WebData(name);if(data==null||!data.StartsWith("data:font/ttf;base64,"))throw new Exception("Archive font cannot be loaded: "+name);}
  Console.WriteLine("PASS: portable archive font loading; up to seven unique families: "+String.Join(", ",RiceFont.Names));
  var menuContents=(FrameworkElement[])typeof(NordControlCenter).GetField("contents",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win);
  var menuPanel=(StackPanel)menuContents[NordControlCenter.MenuTab];var audioPanel=(StackPanel)menuContents[NordControlCenter.AudioTab];
  if(menuContents.Length!=ControlCenterTabDefinition.All.Length||!(audioPanel.Children[0] is MediaCard)||menuPanel.Children.OfType<MediaCard>().Any()||menuPanel.Children.OfType<VolumeMixer>().Any()||audioPanel.Children.OfType<VolumeMixer>().Count()!=1)throw new Exception("Audio controls must live together in the Audio tab, with media first");
  var mic=audioPanel.Children.OfType<Grid>().SelectMany(g=>g.Children.OfType<MicControl>()).Single();mic.Show(new MicState{Id="test-input",Name="Desk microphone",Muted=false});if((string)mic.Content!="Mic on")throw new Exception("Mic on state missing");mic.Show(new MicState{Id="test-input",Name="Desk microphone",Muted=true});if((string)mic.Content!="Mic muted")throw new Exception("Mic mute state missing");mic.Show(new MicState{Id="test-input",Name="Desk microphone",Muted=false});
  var pin=(Button)typeof(NordControlCenter).GetField("pinCenter",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win);win.SetPinned(true);if((string)pin.Content!="Pinned")throw new Exception("Pin state not reflected");pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if((string)pin.Content!="Pin")throw new Exception("Unpin did not work");
  var states=new System.Collections.Generic.Dictionary<string,string>();if(!BarState.Changed(states,"audio","one")||BarState.Changed(states,"audio","one")||!BarState.Changed(states,"audio","two")||!BarState.Changed(states,"widgets","one"))throw new Exception("Shared status change suppression failed");
  var fontBefore=BarAppearance.FontSnapshot();var appearanceBefore=BarAppearance.Snapshot();int oldTransparency=BarAppearance.Settings.transparency;BarAppearance.Update(s=>s.transparency=(oldTransparency+1)%100);
  if(!Object.ReferenceEquals(fontBefore,BarAppearance.FontSnapshot())||BarAppearance.Snapshot().Contains("base64,")||BarAppearance.Snapshot()==appearanceBefore)throw new Exception("Ordinary appearance changes resend font data");BarAppearance.Update(s=>s.transparency=oldTransparency);
  Console.WriteLine("PASS: separate Audio tab with media first, independent mic state, pin/unpin, shared status change suppression and separate cached font channel.");
  if(ControlCenterTabDefinition.All.Length!=4||ControlCenterTabDefinition.All[NordControlCenter.RiceTab].Title!="Configure"||ControlCenterTabDefinition.All.Any(t=>t.Title=="Glaze"||t.Title=="Notifications"))throw new Exception("Configure tab rename/removal of separate Glaze tab failed");
  var rice=(StackPanel)typeof(NordControlCenter).GetMethod("BuildRice",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win,null);
  var appearance=(StackPanel)rice.Children[0];var fontRow=appearance.Children.OfType<Grid>().First(g=>g.Children.OfType<ComboBox>().Any());var fontPicker=fontRow.Children.OfType<ComboBox>().Single();
  if(rice.Children.Count!=4||!((StackPanel)rice.Children[1]).Children.OfType<TextBlock>().Any(t=>t.Text=="Widgets & layout")||!((StackPanel)rice.Children[2]).Children.OfType<TextBlock>().Any(t=>t.Text=="GlazeWM")||!((StackPanel)rice.Children[3]).Children.OfType<TextBlock>().Any(t=>t.Text=="Keybinds"))throw new Exception("Configure must contain Widgets & layout, then GlazeWM directly above Keybinds");
  var ribbonPanel=audioPanel.Children[1] as StackPanel;if(ribbonPanel==null||!ribbonPanel.Children.OfType<Grid>().SelectMany(g=>g.Children.OfType<StackPanel>()).SelectMany(p=>p.Children.OfType<Slider>()).Any(s=>s.Name=="AudioRibbonSensitivity")||!ribbonPanel.Children.OfType<Grid>().SelectMany(g=>g.Children.OfType<StackPanel>()).SelectMany(p=>p.Children.OfType<Slider>()).Any(s=>s.Name=="AudioPlayerHeight"))throw new Exception("Ribbon sensitivity/help must sit directly below media");
  Action drain=()=>{var pairFrame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>pairFrame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(pairFrame);};var pair=RiceLayout.Pair(new StackPanel(),new StackPanel());pair.Measure(new Size(680,100));pair.Arrange(new Rect(0,0,680,100));drain();if(Grid.GetColumn(pair.Children[1])!=2)throw new Exception("Wide slider pairing failed");pair.Measure(new Size(400,100));pair.Arrange(new Rect(0,0,400,100));drain();if(Grid.GetRow(pair.Children[1])!=1)throw new Exception("Narrow slider stacking failed");
  var typed=fontPicker.Text; fontPicker.ItemsSource=new[]{"stale"};fontRow.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(!((string[])fontPicker.ItemsSource).SequenceEqual(RiceFont.Names)||fontPicker.Text!=typed)throw new Exception("Refresh failed to rescan or preserve font text");
  Console.WriteLine("PASS: font refresh rescans archive and preserves current text.");
  var savedIndicatorStyle=BarAppearance.Settings.indicatorStyle;try{foreach(var name in AppearanceSettings.IndicatorStyles){var button=appearance.Children.OfType<Grid>().SelectMany(grid=>grid.Children.OfType<Button>()).Single(b=>b.Name=="RiceWorkspaceindicator_"+name);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(BarAppearance.Settings.indicatorStyle!=name||AppearanceSettings.Load().indicatorStyle!=name)throw new Exception("Workspace indicator selection did not persist");}}finally{BarAppearance.Update(s=>s.indicatorStyle=savedIndicatorStyle);}
  Console.WriteLine("PASS: Rice selector switches and persists Classic, Gallery and Ribbon workspace indicator styles.");
  var savedWallpaperStyle=BarAppearance.Settings.wallpaperIndicatorStyle;try{foreach(var name in AppearanceSettings.WallpaperIndicatorStyles){var button=appearance.Children.OfType<Grid>().SelectMany(grid=>grid.Children.OfType<Button>()).Single(b=>b.Name=="RiceWallpaperindicator_"+name);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(BarAppearance.Settings.wallpaperIndicatorStyle!=name||AppearanceSettings.Load().wallpaperIndicatorStyle!=name)throw new Exception("Wallpaper indicator selection did not persist");}}finally{BarAppearance.Update(s=>s.wallpaperIndicatorStyle=savedWallpaperStyle);}
  Console.WriteLine("PASS: Rice selector switches and persists Classic, Showcase and Filmstrip wallpaper indicator styles.");
  var layout=(StackPanel)typeof(NordControlCenter).GetMethod("BuildLayout",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win,null);
  // Preview of the Rice tab (checks\rice.png) for visual review.
  var frame=new Border{Child=rice,Width=430,Padding=new Thickness(16),Background=ControlCenterTheme.Current.Brush("Surface"),Resources=win.Resources};
  System.Windows.Documents.TextElement.SetForeground(frame,ControlCenterTheme.Current.Brush("Text"));System.Windows.Documents.TextElement.SetFontFamily(frame,win.FontFamily);
  frame.Measure(new Size(430,double.PositiveInfinity));frame.Arrange(new Rect(frame.DesiredSize));frame.UpdateLayout();
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)frame.ActualWidth*2,(int)frame.ActualHeight*2,192,192,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(frame);
  var ricePng=new System.Windows.Media.Imaging.PngBitmapEncoder();ricePng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rice.png")))ricePng.Save(file);
  // Same preview with the current wallpaper's palette (checks\rice-wallpaper.png).
  var wallpaperPath=WallpaperSource.Current();
  if(wallpaperPath!=null){
   BarAppearance.Update(s=>s.wallpaperColors=true);
   ControlCenterTheme.Current.Apply(new FocusState());frame.Background=ControlCenterTheme.Current.Brush("Surface");frame.UpdateLayout();
   var themed=new System.Windows.Media.Imaging.RenderTargetBitmap((int)frame.ActualWidth*2,(int)frame.ActualHeight*2,192,192,System.Windows.Media.PixelFormats.Pbgra32);themed.Render(frame);
   var themedPng=new System.Windows.Media.Imaging.PngBitmapEncoder();themedPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(themed));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rice-wallpaper.png")))themedPng.Save(file);
   BarAppearance.Update(s=>s.wallpaperColors=false);ControlCenterTheme.Current.Apply(new FocusState());
  }
  frame.Child=null;
  Func<DependencyObject,string,Button> find=null;find=(node,name)=>{var button=node as Button;if(button!=null&&button.Name==name)return button;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()){var found=find(child,name);if(found!=null)return found;}return null;};
  var apply=find(layout,"RiceApply");var first=find(layout,"RiceWidget_volume");
  if(apply==null||first==null||apply.IsEnabled)throw new Exception("Initial widget controls incorrect");
  first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rice-widgets.json");
  if(File.Exists(path)||!apply.IsEnabled)throw new Exception("Toggle applied before Apply");
  find(layout,"RiceMove_volume").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var actual=RiceSettings.Load();if(actual.Enabled["volume"]||!actual.left.Contains("volume")||actual.right.Contains("volume")||apply.IsEnabled)throw new Exception("Apply did not persist layout and visibility together");
  find(layout,"RiceWidget_volume").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  find(layout,"RiceDiscard").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if(apply.IsEnabled||RiceSettings.Load().Enabled["volume"])throw new Exception("Discard leaked a draft");
  find(layout,"RiceWidget_volume").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  find(layout,"RiceReset").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  actual=RiceSettings.Load();if(!actual.Enabled["volume"]||!actual.right.SequenceEqual(RiceSettings.Right))throw new Exception("Reset layout or re-enable failed");
  var repaired=RiceSettings.Parse("{\"volume\":false,\"layout\":{\"left\":[\"search\",\"search\",\"bad\"],\"right\":[\"search\",\"cpu\"]}}");
  if(repaired.left.Concat(repaired.right).Distinct().Count()!=RiceSettings.Keys.Length||repaired.left.Concat(repaired.right).Count()!=RiceSettings.Keys.Length||repaired.Enabled["volume"])throw new Exception("Layout validation/migration failed");
  Console.WriteLine("PASS: staged visibility + placement, atomic Apply, discard, reset, deduplication, legacy settings migration.");
  MediaBridge.Receive("{\"url\":\"https://open.spotify.com/track/test\",\"title\":\"Test song\"}");
  if(MediaBridge.Current("Test song")!="https://open.spotify.com/track/test"||MediaBridge.Current("Other song")!=null)throw new Exception("Track matching failed");
  MediaBridge.Receive("{\"url\":\"https://open.spotify.com/track/test\",\"title\":\"Test song\",\"album\":\"Test album\",\"playlist\":\"Test playlist\"}");
  var context=MediaBridge.Context("Test song");if(context[0]!="Test album"||context[1]!="Test playlist")throw new Exception("Spotify context failed");
  var surface=(Border)typeof(NordControlCenter).GetField("surface",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win);
  var tabs=(Grid)((StackPanel)surface.Child).Children[0];var menuTab=(Button)tabs.Children[0];var riceTab=(Button)tabs.Children[NordControlCenter.RiceTab];
  for(int i=0;i<10;i++){riceTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(win.Height!=win.RiceHeight+NordControlCenter.Edge||surface.Height!=win.RiceHeight)throw new Exception("Rice height incorrect");menuTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(win.Height!=win.MenuHeight+NordControlCenter.Edge||surface.Height!=win.MenuHeight)throw new Exception("Menu height incorrect");}
  surface.BeginAnimation(FrameworkElement.HeightProperty,new System.Windows.Media.Animation.DoubleAnimation(514,288,TimeSpan.FromSeconds(1)));riceTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  typeof(NordControlCenter).GetMethod("FitPanel",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win,new object[]{false});
  if(win.Height!=win.RiceHeight+NordControlCenter.Edge||surface.Height!=win.RiceHeight)throw new Exception("Interrupted transition/reopen height incorrect");
  foreach(var monitor in System.Windows.Forms.Screen.AllScreens)foreach(var scale in new[]{1.0,1.25,1.5,2.0}){
   monitorSource.Bounds=monitor.Bounds;placement.Place(monitor.Bounds.Left+20,monitor.Bounds.Top+20,scale,759);
   for(int tab=0;tab<menuContents.Length;tab++){var height=(double)typeof(NordControlCenter).GetMethod("Fit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win,new object[]{tab});if((height+NordControlCenter.Edge)*scale>monitor.Bounds.Height/2.0+.01)throw new Exception("Tab exceeds half-monitor height: "+monitor.DeviceName);}
  }
  monitorSource.Bounds=System.Windows.Forms.Screen.PrimaryScreen.Bounds;placement.Place(monitorSource.Bounds.Left+20,monitorSource.Bounds.Top+20,1,759);
  Console.WriteLine("PASS: all configured tabs fit half of each detected monitor at 100/125/150/200% scaling ("+System.Windows.Forms.Screen.AllScreens.Length+" monitors).");
  Console.WriteLine("PASS: repeated tab switching, separate target heights, interrupted animation reset.");
  if(PowerMenu.Actions.Length!=5||!File.Exists(PowerMenu.SaverPath()))throw new Exception("Power actions/screensaver configuration incorrect");
  var background=System.Windows.Media.Imaging.BitmapSource.Create(2,2,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[]{80,65,50,255,95,85,65,255,55,60,70,255,100,100,80,255},8);
  var frameEvent=typeof(ThemeTransition).GetField("Frame",BindingFlags.Static|BindingFlags.NonPublic);var previousFrames=frameEvent.GetValue(null) as Delegate;int frameListeners=previousFrames==null?0:previousFrames.GetInvocationList().Length;
  PowerActionChecks.Run();var powerActions=new PowerActionChecks.Fake();var powerOwner=new PowerMenuController(powerActions);var power=powerOwner.Create(powerOwner.Displays()[0],background,true);
  var powerRoot=(Grid)power.Content;var fit=(Viewbox)powerRoot.Children[3];var row=(StackPanel)fit.Child;
  var firstPowerRow=(StackPanel)row.Children[0];var secondPowerRow=(StackPanel)row.Children[1];if(row.Children.Count!=2||firstPowerRow.Children.Count!=3||secondPowerRow.Children.Count!=2||row.Children.Cast<StackPanel>().SelectMany(p=>p.Children.Cast<Button>()).Any(button=>button.Width!=button.Height)||System.Windows.Automation.AutomationProperties.GetName((Button)secondPowerRow.Children[0])!="Screensaver"||System.Windows.Automation.AutomationProperties.GetName((Button)secondPowerRow.Children[1])!="Lock")throw new Exception("Power menu must have three square actions above centered Screensaver/Lock");powerOwner.RunAction("Lock",true);if(powerActions.Calls.Count!=0)throw new Exception("Preview lock executed");powerOwner.RunAction("Lock",false);if(powerActions.Calls.Count!=1||powerActions.Calls[0]!="Lock")throw new Exception("Lock action not routed to injected adapter");
  ((Border)powerRoot.Children[2]).Opacity=1;fit.Opacity=1;powerRoot.Measure(new Size(1200,700));powerRoot.Arrange(new Rect(0,0,1200,700));powerRoot.UpdateLayout();var topCenter=firstPowerRow.TranslatePoint(new Point(firstPowerRow.ActualWidth/2,0),row).X;var bottomCenter=secondPowerRow.TranslatePoint(new Point(secondPowerRow.ActualWidth/2,0),row).X;if(Math.Abs(topCenter-bottomCenter)>.5)throw new Exception("Power menu second row is not centered");
  var render=new System.Windows.Media.Imaging.RenderTargetBitmap(1200,700,96,96,System.Windows.Media.PixelFormats.Pbgra32);render.Render(powerRoot);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(render));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"power-menu-preview.png")))png.Save(output);
  powerOwner.Dispose();bool powerClosed=false;try{powerOwner.Create(powerOwner.Displays()[0],background,true);}catch(ObjectDisposedException){powerClosed=true;}if(!powerClosed)throw new Exception("Disposed power owner created an overlay");powerOwner.RunAction("Lock",false);var remainingFrames=frameEvent.GetValue(null) as Delegate;if((remainingFrames==null?0:remainingFrames.GetInvocationList().Length)!=frameListeners||powerActions.Calls.Count!=1)throw new Exception("Power owner retained frame subscription or ran an action after disposal");
  Console.WriteLine("PASS: five square power actions in centered 3+2 rows, Screensaver path and mocked Lock dispatch; synthetic preview rendered. No real power or lock actions executed.");
  menuTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  typeof(NordControlCenter).GetMethod("UpdateClock",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win,null);
  var visual=(FrameworkElement)win.Content;visual.Measure(new Size(win.Width,win.MenuHeight+NordControlCenter.Edge));visual.Arrange(new Rect(0,0,win.Width,win.MenuHeight+NordControlCenter.Edge));visual.UpdateLayout();
  var centerImage=new System.Windows.Media.Imaging.RenderTargetBitmap((int)win.Width,(int)(win.MenuHeight+NordControlCenter.Edge),96,96,System.Windows.Media.PixelFormats.Pbgra32);centerImage.Render(visual);var centerPng=new System.Windows.Media.Imaging.PngBitmapEncoder();centerPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(centerImage));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"focus-control-preview.png")))centerPng.Save(output);
  var theme=ControlCenterTheme.Current;var originalText=theme.Color("Text");var originalSurface=theme.Color("Surface");
  var focusField=typeof(FocusMode).GetField("state",BindingFlags.NonPublic|BindingFlags.Static);var oldFocus=focusField.GetValue(null);
  var activeTheme=new FocusState{endsUtc=DateTime.UtcNow.AddMinutes(10).ToString("o"),settings=new FocusSettings()};
  focusField.SetValue(null,activeTheme);typeof(NordControlCenter).GetMethod("UpdateClock",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win,null);
  if(theme.Color("Surface")!=ControlCenterPalette.Parse(activeTheme.settings.background)||theme.Color("Border")!=ControlCenterPalette.Parse(activeTheme.settings.edge))throw new Exception("Focus palette settings not applied");
  visual.UpdateLayout();centerImage=new System.Windows.Media.Imaging.RenderTargetBitmap((int)win.Width,(int)(win.MenuHeight+NordControlCenter.Edge),96,96,System.Windows.Media.PixelFormats.Pbgra32);centerImage.Render(visual);centerPng=new System.Windows.Media.Imaging.PngBitmapEncoder();centerPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(centerImage));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"focus-theme-preview.png")))centerPng.Save(output);
  activeTheme.settings.accent="#C8A080";theme.Apply(activeTheme);if(theme.Color("Accent")!=ControlCenterPalette.Parse("#C8A080"))throw new Exception("Custom accent ignored");
  focusField.SetValue(null,oldFocus);theme.Apply(new FocusState());if(theme.Color("Surface")!=originalSurface||theme.Color("Text")!=originalText)throw new Exception("Nord theme restoration failed");
  Console.WriteLine("PASS: live palette mutation after template rendering, configured colors, custom accent, Nord restoration.");
  riceTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var rv=(FrameworkElement)win.Content;rv.Measure(new Size(win.Width,win.RiceHeight+NordControlCenter.Edge));rv.Arrange(new Rect(0,0,win.Width,win.RiceHeight+NordControlCenter.Edge));rv.UpdateLayout();
  var ri=new System.Windows.Media.Imaging.RenderTargetBitmap((int)win.Width,(int)(win.RiceHeight+NordControlCenter.Edge),96,96,System.Windows.Media.PixelFormats.Pbgra32);ri.Render(rv);var rp=new System.Windows.Media.Imaging.PngBitmapEncoder();rp.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(ri));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rice-layout-preview.png")))rp.Save(output);
  // Layout tab preview (checks\layout-tab.png): must fit its height without clipping the Apply row.
  var layoutTab=(Button)tabs.Children[NordControlCenter.RiceTab];layoutTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  rv.Measure(new Size(win.Width,win.RiceHeight+NordControlCenter.Edge));rv.Arrange(new Rect(0,0,win.Width,win.RiceHeight+NordControlCenter.Edge));rv.UpdateLayout();
  var li=new System.Windows.Media.Imaging.RenderTargetBitmap((int)win.Width,(int)(win.RiceHeight+NordControlCenter.Edge),96,96,System.Windows.Media.PixelFormats.Pbgra32);li.Render(rv);var lp=new System.Windows.Media.Imaging.PngBitmapEncoder();lp.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(li));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-tab.png")))lp.Save(output);
  // Glaze tab preview (checks\glaze-tab.png).
  ((Button)tabs.Children[NordControlCenter.RiceTab]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));double gh=win.Height;
  rv.Measure(new Size(win.Width,gh));rv.Arrange(new Rect(0,0,win.Width,gh));rv.UpdateLayout();
  var gi=new System.Windows.Media.Imaging.RenderTargetBitmap((int)win.Width,(int)gh,96,96,System.Windows.Media.PixelFormats.Pbgra32);gi.Render(rv);var gp=new System.Windows.Media.Imaging.PngBitmapEncoder();gp.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(gi));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"glaze-tab.png")))gp.Save(output);
  menuTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var stamp=DateTime.UtcNow;if(!MixerAudio.Retain(true,null,stamp)||!MixerAudio.Retain(false,stamp.AddSeconds(-30),stamp)||MixerAudio.Retain(false,stamp.AddMinutes(-3),stamp)||MixerAudio.Retain(false,null,stamp))throw new Exception("Mixer activity retention failed");
  var samples=new[]{new MixerApp{Key="spotify",Name="Spotify",Device="test-device",Sessions=new[]{"music-a","music-b"},Volume=68,Active=true},new MixerApp{Key="helium",Name="Helium",Device="test-device",Sessions=new[]{"browser"},Volume=32,Active=true},new MixerApp{Key="discord",Name="Discord",Device="test-device",Sessions=new[]{"call"},Volume=80,Muted=true}};
  var commands=new System.Collections.Generic.List<MixerChange>();var mixer=new VolumeMixer(()=>samples,command=>commands.Add(command));mixer.Resources=win.Resources;mixer.Render(samples);
  var mixerRows=(StackPanel)mixer.Children[1];var music=(Grid)mixerRows.Children[0];var musicSlider=music.Children.OfType<Slider>().Single();musicSlider.Value=60;musicSlider.Value=45;music.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  foreach(var command in mixer.TakeChanges())commands.Add(command);
  if(commands.Count!=1||commands[0].Volume!=45||commands[0].Muted!=true||commands[0].App.Device!="test-device"||commands[0].App.Sessions.Length!=2)throw new Exception("Mixer commands were not coalesced/scoped correctly");
  mixer.Render(samples.Take(1).ToArray());if(mixerRows.Children.Count!=1)throw new Exception("Ended mixer rows not removed");mixer.Render(new MixerApp[0]);if(((TextBlock)mixer.Children[2]).Visibility!=Visibility.Visible)throw new Exception("Mixer empty state missing");
  samples[0].Muted=false;mixer.Render(samples);var mixerFrame=new Border{Width=420,Padding=new Thickness(18),Background=theme.Brush("Surface"),Child=mixer,Resources=win.Resources};System.Windows.Documents.TextElement.SetForeground(mixerFrame,theme.Brush("Text"));System.Windows.Documents.TextElement.SetFontFamily(mixerFrame,win.FontFamily);mixerFrame.Measure(new Size(420,double.PositiveInfinity));mixerFrame.Arrange(new Rect(mixerFrame.DesiredSize));mixerFrame.UpdateLayout();
  var mi=new System.Windows.Media.Imaging.RenderTargetBitmap(420,(int)Math.Ceiling(mixerFrame.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);mi.Render(mixerFrame);var mp=new System.Windows.Media.Imaging.PngBitmapEncoder();mp.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(mi));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"volume-mixer.png")))mp.Save(output);
  Console.WriteLine("PASS: mixer activity retention, coalesced volume/mute, exact device/session targeting, removal and empty state. No live audio changes made.");
  // Synthetic audio preview: no endpoint objects or audio writes are used.
  typeof(AudioPage).GetField("updating",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(audioPanel,true);
  var outputPicker=(ComboBox)typeof(AudioPage).GetField("outputs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audioPanel);outputPicker.ItemsSource=new[]{new{FriendlyName="Headphones"}};outputPicker.SelectedIndex=0;
  var inputPicker=(ComboBox)typeof(AudioPage).GetField("inputs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audioPanel);inputPicker.ItemsSource=new[]{new{FriendlyName="Desk microphone"}};inputPicker.SelectedIndex=0;
  ((Slider)typeof(AudioPage).GetField("volume",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audioPanel)).Value=68;
  typeof(AudioPage).GetField("updating",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(audioPanel,false);
  audioPanel.Children.OfType<VolumeMixer>().Single().Render(samples);
  ((Button)tabs.Children[NordControlCenter.AudioTab]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  rv.Measure(new Size(win.Width,win.AudioHeight+NordControlCenter.Edge));rv.Arrange(new Rect(0,0,win.Width,win.AudioHeight+NordControlCenter.Edge));rv.UpdateLayout();
  var ai=new System.Windows.Media.Imaging.RenderTargetBitmap((int)win.Width,(int)(win.AudioHeight+NordControlCenter.Edge),96,96,System.Windows.Media.PixelFormats.Pbgra32);ai.Render(rv);var ap=new System.Windows.Media.Imaging.PngBitmapEncoder();ap.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(ai));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"audio-tab.png")))ap.Save(output);
  // Force overflow so this check exercises the scrollbar, even on large monitors.
  var checkPages=(FrameworkElement[])typeof(NordControlCenter).GetField("pages",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win);
  var checkContents=(FrameworkElement[])typeof(NordControlCenter).GetField("contents",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win);

  for(int tab=0;tab<menuContents.Length;tab++){
   ((Button)tabs.Children[tab]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   var viewer=(ScrollViewer)checkPages[tab];var oldHeight=checkContents[tab].Height;checkContents[tab].Height=100;viewer.Height=220;surface.Height=300;win.Height=304;
   rv.Measure(new Size(win.Width,304));rv.Arrange(new Rect(0,0,win.Width,304));rv.UpdateLayout();
   double restingWidth=checkContents[tab].ActualWidth;double restingX=checkContents[tab].TransformToAncestor(surface).Transform(new Point(0,0)).X;
   if(viewer.ScrollableHeight>0)throw new Exception("Non-overflow baseline should not scroll");
   checkContents[tab].Height=1200;
   rv.Measure(new Size(win.Width,304));rv.Arrange(new Rect(0,0,win.Width,304));rv.UpdateLayout();
   var scrollbar=viewer.Template.FindName("PART_VerticalScrollBar",viewer) as System.Windows.Controls.Primitives.ScrollBar;if(scrollbar==null||scrollbar.Visibility!=Visibility.Visible||scrollbar.ActualWidth<1||scrollbar.ActualHeight<1||viewer.ScrollableHeight<=0)throw new Exception("Overflow scrollbar missing in tab "+tab+": "+(scrollbar==null?"null":scrollbar.Visibility+" "+scrollbar.ActualWidth+"x"+scrollbar.ActualHeight)+" scroll="+viewer.ScrollableHeight+" viewport="+viewer.ViewportHeight+" extent="+viewer.ExtentHeight);
   var bounds=scrollbar.TransformToAncestor(surface).TransformBounds(new Rect(scrollbar.RenderSize));double inset=surface.ActualWidth-bounds.Right;
   if(Math.Abs(checkContents[tab].ActualWidth-restingWidth)>.1||Math.Abs(checkContents[tab].TransformToAncestor(surface).Transform(new Point(0,0)).X-restingX)>.1)throw new Exception("Scrollbar appearance shifts tab content");
   if(Math.Abs(scrollbar.ActualWidth-6)>.1)throw new Exception("Tab scrollbar should be six pixels wide: "+scrollbar.ActualWidth+" width="+scrollbar.Width+" min="+scrollbar.MinWidth);
   if(inset<6||inset>10)throw new Exception("Scrollbar clipped or outside right gutter: "+inset);
   for(DependencyObject ancestor=scrollbar;ancestor!=surface;ancestor=System.Windows.Media.VisualTreeHelper.GetParent(ancestor)){
    var element=ancestor as FrameworkElement;if(element!=null&&element.ClipToBounds){var clipped=scrollbar.TransformToAncestor(element).TransformBounds(new Rect(scrollbar.RenderSize));if(clipped.Right>element.ActualWidth+.1||clipped.Left<-.1)throw new Exception("Ancestor clips tab scrollbar");}
   }
   System.Windows.Controls.Primitives.ScrollBar.ScrollToVerticalOffsetCommand.Execute(80.0,viewer);rv.UpdateLayout();if(viewer.VerticalOffset<=0)throw new Exception("Tab scrollbar offset command cannot scroll");
   viewer.ScrollToEnd();rv.UpdateLayout();if(viewer.VerticalOffset<=0)throw new Exception("Tab scrollbar cannot scroll");
   if(tab==NordControlCenter.RiceTab){var si=new System.Windows.Media.Imaging.RenderTargetBitmap((int)win.Width,304,96,96,System.Windows.Media.PixelFormats.Pbgra32);si.Render(rv);var sp=new System.Windows.Media.Imaging.PngBitmapEncoder();sp.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(si));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"scrollbar-preview.png")))sp.Save(output);}
   checkContents[tab].Height=oldHeight;viewer.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;viewer.ScrollToHome();
  }
  Console.WriteLine("PASS: stable content width/position with and without overflow in all configured tabs; thin scrollbars stay inside clips, 8 px from the edge, with working thumb scrolling.");
  Console.WriteLine("PASS: context metadata; source routing; configurable widgets; changes staged; Apply persists; re-enable; exact Spotify title matching.");
 }
}
