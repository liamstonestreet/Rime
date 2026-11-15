using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Automation;

class NordControlCenter : Window,IDisposable {
 static readonly string Request=ControlCenterRequests.Position;
 EventWaitHandle signal;bool interacting,disposed,hasClosed;
 internal bool HasClosed {get{return hasClosed;}}Action appearanceChanged,frostRendered;Action<string> hoverAction,pinAction,focusAction;


 readonly List<string[]> anchors=new List<string[]>();
 // Anchor files are named center-anchor-<barX>_<barY>.txt (the bar window's physical origin), so a
 // re-registering bar overwrites its own file. Entries are the 7 file values plus that key.
 // Older files keyed by the clock position (some with wrong second-monitor coordinates) are removed.
 static readonly System.Text.RegularExpressions.Regex AnchorKey=new System.Text.RegularExpressions.Regex(@"^-?\d{1,6}_-?\d{1,6}$");
 void LoadAnchors(){RemoveBarBorder();anchors.Clear();foreach(var f in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory,"center-anchor-*.txt")){try{var key=Path.GetFileNameWithoutExtension(f).Substring(14);if(!AnchorKey.IsMatch(key)){File.Delete(f);continue;}var v=File.ReadAllText(f).Split(' ');if(v.Length==7)anchors.Add(v.Concat(new[]{key}).ToArray());}catch{}}}
 // The clock/outer HWND can round up (63 for 62 drawable pixels at 125%). Use the drawable edge so the
 // control center's outline lands on the same pixel rows as the bar's bottom line. Measured at each
 // open (not at registration, when a restarting bar's window may still be mid-resize). Returns a copy.
 string[] SnapToBar(string[] v){RemoveBarBorder();v=(string[])v.Clone();var c=System.Globalization.CultureInfo.InvariantCulture;double cx=(double.Parse(v[3],c)+double.Parse(v[5],c))/2,top=double.Parse(v[4],c),bottom=double.Parse(v[6],c);
  foreach(var r in bars)if(cx>=r.left&&cx<r.right&&top>=r.top&&top<r.bottom){var drawable=ScreenFrame.DrawableBarBounds(new System.Drawing.Rectangle(r.left,r.top,r.right-r.left,r.bottom-r.top),double.Parse(v[2],c));v[1]=v[6]=drawable.Bottom.ToString(c);break;}return v;}
 // Bar-edge outline: Edge is the band above the panel (1 transparent + 3 line). 4 DIPs is a whole
 // number of physical pixels at 100/125/150/175%, so the window top never lands between pixels.
 public const double Edge=4,Wing=24;
 // Line = floor(3 x scale) physical pixels, in DIPs. Chromium snaps the bar's line to whole pixels
 // (3 rows at 125%, not 3.75), so a fractional WPF stroke would add a faint extra row at the joint.
 double Line=3;System.Windows.Shapes.Path shell;PathFigure figure;
 void SetLine(double scale){double v=Math.Floor(3*scale+1e-6)/scale;if(Math.Abs(v-Line)<1e-6)return;Line=v;shell.StrokeThickness=v;UpdateShape(true);}
 readonly List<WindowRect> bars=new List<WindowRect>();
 // One shape draws the line, the wing fillets and the panel outline and fill, so the line follows
 // the panel edge as it moves. Its points change only while an animation runs; idle cost is zero.
 readonly LineSegment flatL=new LineSegment(),sideL=new LineSegment(),floor=new LineSegment(),sideR=new LineSegment(),flatR=new LineSegment(),coverR=new LineSegment{IsStroked=false},coverL=new LineSegment{IsStroked=false};
 readonly ArcSegment wingL=new ArcSegment{SweepDirection=SweepDirection.Clockwise},cornerL=new ArcSegment{SweepDirection=SweepDirection.Counterclockwise},cornerR=new ArcSegment{SweepDirection=SweepDirection.Counterclockwise},wingR=new ArcSegment{SweepDirection=SweepDirection.Clockwise};
 double shapeDepth=-1;
 static System.Windows.Point P(double x,double y){return new System.Windows.Point(x,y);}
 UIElement BuildShell(){
  // Nothing is painted above the line except across the opening. An opaque fill reaches the line's top
  // there to hide the bar's own line; a see-through one stops at the bar's bottom, because the bar
  // hides its line under the panel (ShowGap) and a second translucent layer would show as a strip.
  // (Window shadows can tint the bar, so a wider cover strip would show.)
  figure=new PathFigure{StartPoint=P(0,Edge-Line/2),IsClosed=false,IsFilled=true};
  foreach(var s in new PathSegment[]{flatL,wingL,sideL,cornerL,floor,cornerR,sideR,wingR,flatR,coverR,coverL})figure.Segments.Add(s);
  UpdateShape(true);
  return shell=new System.Windows.Shapes.Path{Data=new PathGeometry(new[]{figure}),Fill=B("Panel"),Stroke=B("Edge"),StrokeThickness=Line,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
 }
 // Depth h = visible panel height. Flat line at h=0; fillets and corners grow as it opens. The corners
 // follow the Rice tab's corner radius (24 at 100%); the wing fillets too, but no wider than the panel's
 // inset from the window edge (Wing), which they would otherwise overrun.
 double roundness=1;
 void UpdateShape(bool force=false){
  double full=surface.Height,h=Math.Max(0,Math.Min(full,full+slide.Y));
  if(!force&&Math.Abs(h-shapeDepth)<.05)return;shapeDepth=h;
  var frame=PanelFrameGeometry.Calculate(Width,h,Edge,Line,Wing,roundness,ControlCenterTheme.Current.Color("Panel").A<255);
  figure.StartPoint=frame.Start;flatL.Point=frame.FlatLeft;wingL.Point=frame.WingLeft;wingL.Size=frame.WingSize;sideL.Point=frame.SideLeft;cornerL.Point=frame.CornerLeft;cornerL.Size=frame.CornerSize;
  floor.Point=frame.Floor;cornerR.Point=frame.CornerRight;cornerR.Size=frame.CornerSize;sideR.Point=frame.SideRight;wingR.Point=frame.WingRight;wingR.Size=frame.WingSize;flatR.Point=frame.FlatRight;
  coverR.Point=frame.CoverRight;coverL.Point=frame.CoverLeft;
 }
 /// <summary>Applies the corner radius to the outline and every rounded element inside.</summary>
 void ApplyRoundness(){roundness=BarAppearance.Settings.radius/100.0;Roundness.Apply(this,roundness);UpdateShape(true);ApplyEdgeStyle();}
 FrameworkElement glow; // EdgeGlow.Inside: shines into the panel only, like the bar's
 /// <summary>
 /// The bar's edge style on the outline, which continues the bar's own line: flat edge color, an
 /// accent-to-partner gradient spanning the whole monitor (so it lines up with the bar's), a soft glow,
 /// or none. Lock-in keeps its flat line.
 /// </summary>
 void ApplyEdgeStyle(){
  var style=FocusMode.State.ThemeActive?"flat":BarAppearance.Settings.edgeStyle;Brush stroke=B("Edge");
  if(style=="gradient"){
   var monitor=placement.Monitor.Bounds;var theme=ControlCenterTheme.Current;
   stroke=theme.Gradient("Accent","Partner",
    P((monitor.Left-placeX)/placeScale,0),P((monitor.Right-placeX)/placeScale,0));
  }
  shell.Stroke=style=="none"?Brushes.Transparent:stroke;
  EdgeGlow.SetStroke(glow,stroke,Line);glow.Visibility=style=="glow"?Visibility.Visible:Visibility.Collapsed;
 }
 readonly StackPanel menu=new StackPanel();readonly AudioPage audio=new AudioPage(new WindowsAudioEndpointService());
 readonly TextBlock clock=new TextBlock();readonly CalendarView calendar=new CalendarView();
 readonly Button pinCenter=new Button{Name="MenuPin"};bool pinned;string currentAnchor;
 readonly TranslateTransform slide=new TranslateTransform();
 Border surface;
 // Panel heights per tab, measured from their content (Fit); exposed for the checks.
 internal static readonly int MenuTab=ControlCenterTabDefinition.Index(ControlCenterTab.Menu),AudioTab=ControlCenterTabDefinition.Index(ControlCenterTab.Audio),RiceTab=ControlCenterTabDefinition.Index(ControlCenterTab.Configure),FocusTab=ControlCenterTabDefinition.Index(ControlCenterTab.Focus);
 ControlCenterTab selected=ControlCenterTab.Menu;
 int selectedTab {get{return ControlCenterTabDefinition.Index(selected);}}
 readonly StackPanel focusPage=new StackPanel();
 public double MenuHeight {get{return Fit(MenuTab);}} public double AudioHeight {get{return Fit(AudioTab);}} public double RiceHeight {get{return Fit(RiceTab);}}
 double tabTransitionMs=220;
 // Include the top edge in the half-monitor limit; monitor bounds and anchors are physical pixels.
 internal static double MonitorPanelLimit(int pixelHeight,double scale,int percent=50){return PanelPlacementController.HeightLimit(pixelHeight,scale,percent,Edge);}
 double maxHeight {get{return placement.MaxHeight(BarAppearance.Settings.centerMaxHeight);}}
 void LoadTabLayout(){try{var file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"control-center-layout.json");var settings=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,double>>(File.ReadAllText(file));if(settings.ContainsKey("transitionMs"))tabTransitionMs=Math.Max(0,Math.Min(1000,settings["transitionMs"]));}catch{}}
 /// <summary>
 /// Panel height for tab i: the tab row plus the page measured at the panel's width, so it fits snugly,
 /// but never above maxHeight; a taller page gets a capped frame and scrolls.
 /// </summary>
 double Fit(int i){
  if(i==FocusTab&&focusPage.Children.Count==0)focusPage.Children.Add(new FocusView());
  var available=new Size(Math.Max(1,Width-2*Wing-surface.Padding.Left-surface.Padding.Right),double.PositiveInfinity);
  tabsRow.Measure(available);contents[i].Measure(new Size(Math.Max(1,available.Width-16),available.Height));
  double chrome=surface.Padding.Top+surface.Padding.Bottom+tabsRow.DesiredSize.Height;
  double page=Math.Min(Math.Ceiling(contents[i].DesiredSize.Height),Math.Max(0,maxHeight-chrome));
  pages[i].Height=page;return Math.Ceiling(chrome+page);
 }
 DateTime lastClock, dismissed;
 [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;}
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);
 double anchorLeft,anchorTop,anchorRight,anchorBottom;
 DateTime outsideSince=DateTime.MinValue;
 [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int left,top,right,bottom; }
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out WindowRect r);
 delegate bool WindowVisitor(IntPtr hwnd,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumWindows(WindowVisitor visitor,IntPtr data);
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref uint value,int size);
 void RemoveBarBorder(){bars.Clear();var ids=new HashSet<int>(System.Diagnostics.Process.GetProcessesByName("zebar").Select(p=>p.Id));EnumWindows(delegate(IntPtr h,IntPtr d){uint pid;GetWindowThreadProcessId(h,out pid);if(ids.Contains((int)pid)){uint none=0xfffffffe;DwmSetWindowAttribute(h,34,ref none,4);WindowRect r;if(IsWindowVisible(h)&&GetWindowRect(h,out r)&&r.bottom-r.top>30)bars.Add(r);}return true;},IntPtr.Zero);}
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 void ReadPosition(string[] position=null){try{
  var values=position??File.ReadAllText(Request).Split(' ');var c=System.Globalization.CultureInfo.InvariantCulture;
  double scale=double.Parse(values[2],c);Place(double.Parse(values[0],c),double.Parse(values[1],c),scale);SetLine(scale);
  if(values.Length>=7){anchorLeft=double.Parse(values[3],c);anchorTop=double.Parse(values[4],c);anchorRight=double.Parse(values[5],c);anchorBottom=double.Parse(values[6],c);}
 }catch{} }
 readonly PanelPlacementController placement;PanelAnimationController animations;
 int placeX {get{return placement.X;}}int placeY {get{return placement.Y;}}
 double placeScale {get{return placement.Scale;}}bool placed {get{return placement.IsPlaced;}}
 internal static double CenterWidth(int requested,int monitorWidth,double scale){return PanelPlacementController.ConstrainWidth(requested,monitorWidth,scale);}
 void Place(double centerX,double bottom,double scale){placement.Place(centerX,bottom,scale,BarAppearance.Settings.centerWidth);}
 void ApplyCenterWidth(){if(!placement.ApplyWidth(BarAppearance.Settings.centerWidth))return;if(IsVisible){FitPanel(false);UpdateShape(true);PaintBackdrop();ShowGap(true);}}
 void KeepPlace(){placement.KeepPosition();}
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flags);
 // True only when the window under the cursor is a Zebar bar, i.e. the clock is actually visible
 // (not covered by a full-screen video or game).
 static bool OverBar(Point p){try{var top=GetAncestor(WindowFromPoint(p),2);uint pid;GetWindowThreadProcessId(top,out pid);using(var process=System.Diagnostics.Process.GetProcessById((int)pid))return process.ProcessName=="zebar";}catch{return false;}}
 void CheckHover(){
  if(GameWindows.ForegroundGame!=IntPtr.Zero){if(IsVisible)Conceal();return;}
  if(calendar.ModalActive){outsideSince=DateTime.MinValue;return;}
  if(FocusMode.Transitioning){outsideSince=DateTime.MinValue;return;}
  if(PowerMenu.IsOpen){if(IsVisible)Conceal();return;}
  Point p;if(!GetCursorPos(out p))return;
  if(!IsVisible||closing){foreach(var v in anchors){double l=double.Parse(v[3]),t=double.Parse(v[4]),rr=double.Parse(v[5]),b=double.Parse(v[6]);if(p.x>=l&&p.x<=rr&&p.y>=t&&p.y<=b&&OverBar(p)){currentAnchor=v[7];ReadPosition(SnapToBar(v));Reveal();return;}}return;}
  WindowRect r;if(!GetWindowRect(new System.Windows.Interop.WindowInteropHelper(this).Handle,out r))return;
  double visibleBottom=r.top+(Edge+surface.ActualHeight)*(r.bottom-r.top)/Math.Max(1,Height);
  bool insidePanel=p.x>=r.left&&p.x<=r.right&&p.y>=r.top&&p.y<=visibleBottom;
  bool insideAnchor=p.x>=anchorLeft-4&&p.x<=anchorRight+4&&p.y>=anchorTop&&p.y<=anchorBottom+5;
  if(insidePanel||insideAnchor||audio.DevicePickerOpen||RiceLayout.FontPickerOpen){outsideSince=DateTime.MinValue;return;}
  if((GetAsyncKeyState(1)&0x8000)!=0||(GetAsyncKeyState(2)&0x8000)!=0){Conceal();return;}
  if(pinned){outsideSince=DateTime.MinValue;return;}
  if(outsideSince==DateTime.MinValue)outsideSince=DateTime.UtcNow;
  else if(DateTime.UtcNow-outsideSince>TimeSpan.FromMilliseconds(130))Conceal();
 }
 bool closing;
 static Brush B(string s) {return ControlCenterTheme.Current.Brush(s);}
 public NordControlCenter():this(new WindowsPanelMonitorSource(),null){}
 internal NordControlCenter(IPanelMonitorSource monitors,IPanelPositionTarget target) {
  placement=new PanelPlacementController(monitors,target??new WindowsPanelPositionTarget(this),Edge);
  LoadTabLayout();Title="Nord Control Center"; Width=BarAppearance.Settings.centerWidth; Height=400+Edge; LocationChanged+=delegate{KeepPlace();}; SizeChanged+=delegate{KeepPlace();}; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
  AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false; ShowActivated=false; FontFamily=RiceFont.Current; FontSize=14; Foreground=B("Text");
  var panel=new Border { Background=Brushes.Transparent,CornerRadius=new CornerRadius(0,0,24,24),Padding=new Thickness(24,8,8,24), BorderBrush=B("SoftEdge"),BorderThickness=new Thickness(0),ClipToBounds=true };
  panel.VerticalAlignment=VerticalAlignment.Top;panel.Height=400;surface=panel;panel.RenderTransform=slide;panel.CacheMode=new BitmapCache();animations=new PanelAnimationController(this,panel,slide,force=>UpdateShape(force));
  // The shell (fill + outline, replaces the old fixed wings) sits behind; the panel content slides in a
  // host clipped below the line band so it never paints over the bar edge.
  var root=new Grid {ClipToBounds=true};var shellShape=BuildShell();backdrop=new CenterBackdrop(shell.Data);root.Children.Add(backdrop.Layer);root.Children.Add(shellShape);glow=EdgeGlow.Inside(shell.Data,null,Line,shell.Data);glow.Visibility=Visibility.Collapsed;root.Children.Add(glow);grain=new System.Windows.Shapes.Rectangle{IsHitTestVisible=false,Clip=shell.Data};root.Children.Add(grain);var host=new Grid {Margin=new Thickness(Wing,Edge,Wing,0),VerticalAlignment=VerticalAlignment.Top,ClipToBounds=true};host.Children.Add(panel);root.Children.Add(host);Content=root;
  Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Load(System.Xml.XmlReader.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"control-center-styles.xaml")));ScrollChrome.Install(Resources);ControlCenterTheme.Current.Install(Resources);appearanceChanged=()=>{ApplyCenterWidth();if(IsVisible){FitPanel(false);ApplyRoundness();}};
  frostRendered=()=>{if(IsVisible)PaintBackdrop();};
   var content=new StackPanel();panel.Child=content;
  // Tabs: the panel fits the selected page (Fit), up to maxHeight, beyond which the page scrolls. Pages
  // share one cell and slide sideways on a switch, under a single accent underline that glides along.
  var definitions=ControlCenterTabDefinition.All;contents=definitions.Select(tab=>BuildPage(tab.Id)).ToArray();pages=new FrameworkElement[contents.Length];
  var tabs=new Grid{Margin=new Thickness(0,0,16,0)};tabsRow=tabs;content.Children.Add(tabs);var pager=new Grid();content.Children.Add(pager);
  for(int i=0;i<pages.Length;i++){
   int index=i;tabs.ColumnDefinitions.Add(new ColumnDefinition());
   var tab=new Button{Content=definitions[i].Title,Padding=new Thickness(0,8,0,8),Background=Brushes.Transparent,Foreground=Foreground,BorderThickness=new Thickness(0,0,0,2),BorderBrush=Brushes.Transparent,Cursor=System.Windows.Input.Cursors.Hand};
   Grid.SetColumn(tab,i);tabs.Children.Add(tab);tab.Click+=delegate{SelectTab(definitions[index].Id);};
   // Each page sits in a scroll frame (the frame is what slides); content that changes size re-fits the panel.
   // A fixed 6px scrollbar gutter plus 10px padding keeps page width stable and aligned with tabs.
   pages[i]=new ScrollViewer{Content=contents[i],Style=(Style)FindResource("RiceScrollViewer"),Padding=new Thickness(0,0,10,0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,VerticalAlignment=VerticalAlignment.Top,RenderTransform=new TranslateTransform()};
   if(i>0)pages[i].Visibility=Visibility.Collapsed;pager.Children.Add(pages[i]);
   contents[i].SizeChanged+=(sender,e)=>{if(index==selectedTab&&IsVisible&&!closing&&Math.Abs(e.NewSize.Height-e.PreviousSize.Height)>.5&&!refitQueued){refitQueued=true;Dispatcher.BeginInvoke(new Action(()=>{refitQueued=false;if(!disposed)FitPanel(true);}));}};
  }
  underline=new Border{Height=2,Background=B("Accent"),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,RenderTransform=new TranslateTransform(),IsHitTestVisible=false};
  Grid.SetColumnSpan(underline,pages.Length);tabs.Children.Add(underline);
  tabs.SizeChanged+=delegate{ResizeUnderline();};
  clock.FontSize=18;clock.FontWeight=FontWeights.Medium;clock.Margin=new Thickness(0,12,0,12);
  var clockRow=new Grid();clockRow.ColumnDefinitions.Add(new ColumnDefinition());clockRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});clockRow.Children.Add(clock);
  pinCenter.FontSize=11;pinCenter.Padding=new Thickness(10,5,10,5);pinCenter.BorderThickness=new Thickness(0);pinCenter.VerticalAlignment=VerticalAlignment.Center;pinCenter.Click+=delegate{SetPinned(!pinned);};Grid.SetColumn(pinCenter,1);clockRow.Children.Add(pinCenter);menu.Children.Add(clockRow);SetPinned(false);
  menu.Children.Add(calendar);
  PreviewKeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape) Conceal();};
  // Programmatic WM focus changes are not a request to dismiss a hover panel.
  // CheckHover still closes immediately on an outside click, or after leaving.
  Deactivated+=delegate {if(IsVisible)CheckHover();};
  hoverTimer=new DispatcherTimer {Interval=Fast};
  hoverTimer.Tick+=delegate {if(disposed)return;if(signal!=null&&signal.WaitOne(0))LoadAnchors(); CheckHover(); if(IsVisible && DateTime.UtcNow-lastClock>TimeSpan.FromSeconds(1)){lastClock=DateTime.UtcNow;UpdateClock();if(selected==ControlCenterTab.Audio){audio.Tick();}} if(IsVisible&&!closing&&selected==ControlCenterTab.Audio)audio.RefreshIfDue(10);Pace();};hoverAction=HoverAnchor;pinAction=PinAnchor;focusAction=OpenFocusAnchor;Closed+=delegate{hasClosed=true;Dispose();};
 }
 // Open: poll at 60 Hz to detect leaving. Hidden: the bar sends a hover event, so polling is only a
 // fallback: 1 Hz while a bar is connected, 10 Hz if not (helper restarted, bar reconnecting).
 DispatcherTimer hoverTimer; static readonly TimeSpan Fast=TimeSpan.FromMilliseconds(16);
 void Pace(){var want=IsVisible?Fast:TimeSpan.FromMilliseconds(MediaBridge.BarAlive?1000:100);if(hoverTimer.Interval!=want)hoverTimer.Interval=want;}
 void HoverAnchor(string key){Point p;if(PowerMenu.IsOpen||(IsVisible&&!closing)||!GetCursorPos(out p)||!OverBar(p))return;foreach(var v in anchors)if(v[7]==key){currentAnchor=v[7];ReadPosition(SnapToBar(v));Reveal();return;}}
 void FitPanel(bool animate){if(disposed)return;animations.ResizeHeight(Fit(selectedTab),maxHeight,animate,TabMs,Edge);}
 void ResizeUnderline(){if(disposed)return;animations.ResizeUnderline(underline,selectedTab,pages.Length,tabsRow.ActualWidth);}
 FrameworkElement[] pages,contents;Grid tabsRow;Border underline;bool refitQueued;

 /// <summary>
 /// Switches tabs: the old page slides out one way while the new one slides in from the other (by tab
 /// order) with a light fade, the underline glides to the new tab, and the height follows (FitPanel),
 /// all on the same timing. Instant while hidden or with transitions off.
 /// </summary>
 // Retained index entry point for existing characterization checks; UI uses stable IDs.
 void ShowTab(int index){if(index<0||index>=ControlCenterTabDefinition.All.Length)throw new ArgumentOutOfRangeException("index");SelectTab(ControlCenterTabDefinition.All[index].Id);}
 void SelectTab(ControlCenterTab id){
  int index=ControlCenterTabDefinition.Index(id),from=selectedTab;if(id==selected)return;selected=id;
  animations.SwitchTab(pages,from,index,underline,tabsRow.ActualWidth,IsVisible&&TabMs>0,TabMs);
  FitPanel(true);if(id==ControlCenterTab.Audio&&IsVisible){audio.Tick();audio.RefreshIfDue(2);}
 }
 double TabMs {get{return Motion.Ms(tabTransitionMs);}}
 void FreezeTabHeight(){animations.FreezeHeight(Edge);}
 FrameworkElement BuildPage(ControlCenterTab id){switch(id){case ControlCenterTab.Menu:return menu;case ControlCenterTab.Audio:return audio;case ControlCenterTab.Configure:return BuildRice();case ControlCenterTab.Focus:return focusPage;default:throw new ArgumentOutOfRangeException("id");}}

 StackPanel BuildRice(){return RiceLayout.BuildAppearance();}
 StackPanel BuildLayout(){return RiceLayout.BuildLayout();}

 internal void SetPinned(bool value){pinned=value;pinCenter.Content=value?"Pinned":"Pin";pinCenter.ToolTip=value?"Unpin control center":"Keep control center open";pinCenter.Foreground=B(value?"Accent":"Muted");pinCenter.Background=B(value?"Selected":"Raised");}
 void OpenFocusAnchor(string key){foreach(var anchor in anchors)if(anchor[7]==key){currentAnchor=key;ReadPosition(SnapToBar(anchor));SetPinned(true);Reveal();SelectTab(ControlCenterTab.Focus);return;}}
 void PinAnchor(string key){foreach(var anchor in anchors)if(anchor[7]==key){if(IsVisible&&!closing&&currentAnchor==key){SetPinned(!pinned);return;}currentAnchor=key;ReadPosition(SnapToBar(anchor));SetPinned(true);Reveal();return;}}
 void UpdateClock(){ControlCenterTheme.Current.Apply(FocusMode.State);if(backdrop!=null&&IsVisible)PaintBackdrop();if(grain!=null)grain.Opacity=FocusMode.State.ThemeActive?0:BarAppearance.GrainOpacity;var now=DateTime.Now;clock.Text=now.ToString("h:mm tt  \u00b7  MMM d");calendar.RefreshToday();}

 void Reveal(){if(GameWindows.ForegroundGame!=IntPtr.Zero)return;closing=false;outsideSince=DateTime.MinValue;UpdateClock();FitPanel(false);double start=IsVisible?slide.Y:-surface.Height;slide.BeginAnimation(TranslateTransform.YProperty,null);slide.Y=start;UpdateShape(true);if(grainScale!=placeScale){grain.Fill=Grain.Brush(placeScale);grain.CacheMode=new BitmapCache(placeScale);grainScale=placeScale;}if(!IsVisible)ApplyRoundness();if(!IsVisible)PaintBackdrop();Show();KeepPlace();ShowGap(true);Pace();
 // Refresh audio after opening has settled; all vertical clocks have one owner.
 animations.AnimateVertical(0,180,EasingMode.EaseOut,()=>{if(!closing&&IsVisible&&selected==ControlCenterTab.Audio){audio.RefreshMedia();audio.RefreshIfDue(2);}});}
 void Conceal(){if(closing||!IsVisible)return;SetPinned(false);closing=true;FreezeTabHeight();dismissed=DateTime.UtcNow;
 // Restore the bar line before hiding the panel to keep the join covered.
 animations.AnimateVertical(-surface.Height,150,EasingMode.EaseIn,()=>{if(!closing)return;ShowGap(false);animations.DelayHide(()=>{if(!closing)return;Hide();backdrop.Hide();slide.BeginAnimation(TranslateTransform.YProperty,null);closing=false;});});}
 CenterBackdrop backdrop;System.Windows.Shapes.Rectangle grain;double grainScale;
 // Frost only shows through a see-through panel; lock-in is opaque. Sized for the taller tab, so a
 // tab switch while open stays covered.
 void PaintBackdrop(){
  if(!placed||ControlCenterTheme.Current.Color("Panel").A==255){backdrop.Hide();return;}
  backdrop.Paint(placeX,placeY,Width,maxHeight+Edge,placeScale);
 }
 // Tells the bars to hide their bottom line under this window (physical pixels), or to restore it.
 void ShowGap(bool open){
  WindowRect r;
  if(open&&GetWindowRect(new System.Windows.Interop.WindowInteropHelper(this).Handle,out r))MediaBridge.Broadcast(String.Format("{{\"centerGap\":{{\"left\":{0},\"right\":{1},\"top\":{2}}}}}",r.left+1,r.right-1,r.top));
  else MediaBridge.Broadcast("{\"centerGap\":null}");
 }
 static void Tray(){var task=AutomationElement.RootElement.FindFirst(TreeScope.Children,new PropertyCondition(AutomationElement.ClassNameProperty,"Shell_TrayWnd"));if(task==null)return;foreach(AutomationElement item in task.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button))){var name=item.Current.Name; if(name.IndexOf("hidden icon",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("Show hidden",StringComparison.OrdinalIgnoreCase)>=0){object pattern;if(item.TryGetCurrentPattern(InvokePattern.Pattern,out pattern))((InvokePattern)pattern).Invoke();return;}}}
 internal void Open(){Reveal();}
 internal void StartInteraction(EventWaitHandle requestSignal){if(disposed||interacting)return;interacting=true;signal=requestSignal;BarAppearance.Changed+=appearanceChanged;FrostMaterial.Rendered+=frostRendered;MediaBridge.Hover=hoverAction;MediaBridge.Pin=pinAction;MediaBridge.OpenFocus=focusAction;LoadAnchors();hoverTimer.Start();Dispatcher.BeginInvoke(new Action(()=>{if(!disposed)audio.RefreshIfDue(0);}));}
 internal void StopInteraction(){audio.Dispose();calendar.Dispose();animations.Dispose();foreach(var child in focusPage.Children){var owner=child as IDisposable;if(owner!=null)owner.Dispose();}if(!interacting)return;interacting=false;hoverTimer.Stop();signal=null;BarAppearance.Changed-=appearanceChanged;FrostMaterial.Rendered-=frostRendered;if(MediaBridge.Hover==hoverAction)MediaBridge.Hover=null;if(MediaBridge.Pin==pinAction)MediaBridge.Pin=null;if(MediaBridge.OpenFocus==focusAction)MediaBridge.OpenFocus=null;ShowGap(false);}
 public void Dispose(){if(disposed)return;disposed=true;StopInteraction();hoverTimer.Stop();audio.Dispose();calendar.Dispose();animations.Dispose();backdrop.Hide();}

}
// Entry point. Runs before any WPF type loads so the helper is per-monitor DPI aware (PMv2) and WPF
// rescales per monitor: the panel renders crisp at the right size on a 100% screen next to a 125% one.
// The switch must be set explicitly: a csc-built exe has no target-framework attribute, so WPF would
// otherwise default to the legacy "do not scale for DPI changes" behaviour.
static class Program {
 [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 [STAThread] static void Main(string[] args){
  try{SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{}
  AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges",false);
  Start(args);
 }
 // Kept separate and un-inlined: Main must not mention any WPF type, or the JIT loads PresentationCore
 // while compiling Main and its module initializer makes the process system-DPI aware first.
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static void Start(string[] args){ControlCenterApp.Run(args);}
}
