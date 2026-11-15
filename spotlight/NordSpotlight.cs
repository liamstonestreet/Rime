using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Web.Script.Serialization;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

class SearchRequest { public int id {get;set;} public string source {get;set;} public string action {get;set;} public string query {get;set;} public double x {get;set;} public double y {get;set;} public double width {get;set;} public double scale {get;set;} }
class NordSpotlight : Window {
    static readonly string RequestFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"spotlight-requests");
    readonly Grid header = new Grid { Margin = new Thickness(4,2,4,14) };
    bool dropdown; IntPtr barWindow;
    Border surface;
    readonly Poof poof = new Poof();
    static readonly DependencyProperty FramePulseProperty=DependencyProperty.Register("FramePulse",typeof(double),typeof(NordSpotlight),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
    bool motionActive;
    TimeSpan lastRenderTime=TimeSpan.MinValue;
    int motionFrames;
    double lastMotionFps;
    readonly Stopwatch motionClock = new Stopwatch();
    bool closing;
    double reveal = 1, motionFrom, expandedHeight;
    // Rice tab animation speed; never zero, since the motion clock divides by these.
    static double OpenMs { get { return Math.Max(1, Motion.Ms(150)); } }
    static double CloseMs { get { return Math.Max(1, Motion.Ms(130)); } }
    DateTime catalogTime;
    int filterCount, catalogReads, requestCount;
    int glassResult;
    AppUsageTracker usageTracker;
    readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
    readonly TextBox query = new TextBox();
    readonly ListBox results = new ListBox();
    readonly TextBlock status = new TextBlock();
    AppEntry[] apps = new AppEntry[0];
    SpotlightResult[] windows=new SpotlightResult[0];CancellationTokenSource filtering;
    int queryVersion,windowVersion;bool refreshing,readingCatalog,readingWindows;string requestSource;
    readonly string catalog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "spotlight-apps.json");
    static SolidColorBrush Color(string hex) { return SpotlightTheme.Brush(hex); }
    public NordSpotlight() {
        Title = "Nord Spotlight"; Width = 720; SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false;
        Resources["ThemeTrack"]=Color("#4C566A");ScrollChrome.Install(Resources);
        FontFamily = RiceFont.Current; FontSize = 14; Foreground = Color("#D8DEE9");
        var border = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(16),
            BorderThickness = new Thickness(1), BorderBrush = Color("#507F9BA8"),
            Background = Color("#20273038") };
        surface = border;
        var root = new Grid(); root.Children.Add(border); root.Children.Add(poof); Content = root;
        frostHost = root;
        SizeChanged += delegate { Frost(); }; LocationChanged += delegate { Frost(); };

        var stack = new StackPanel(); border.Child = stack;

        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.Children.Add(new TextBlock { Text = "⌕", FontSize = 24, Foreground = Color("#88C0D0"), VerticalAlignment = VerticalAlignment.Center });
        query.Background = Brushes.Transparent; query.Foreground = Color("#ECEFF4"); query.CaretBrush = Color("#88C0D0");
        query.BorderThickness = new Thickness(0); query.FontSize = 18; query.Padding = new Thickness(2,7,2,7);
        System.Windows.Automation.AutomationProperties.SetName(query, "Search apps, files, windows and commands");
        var hint = new TextBlock { Text = "Search anything, or type /", FontSize = 18, Foreground = Color("#96A7BA"), Margin = new Thickness(2,7,2,7), IsHitTestVisible = false };
        Grid.SetColumn(hint,1); header.Children.Add(hint);
        query.TextChanged += delegate { hint.Visibility = query.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
        Grid.SetColumn(query,1); header.Children.Add(query); stack.Children.Add(header);
        stack.Children.Add(new Border { Height = 1, Background = Color("#465465"), Margin = new Thickness(0,0,0,8) });
        results.Background = Brushes.Transparent; results.BorderThickness = new Thickness(0); results.Foreground = Foreground;
        results.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBox'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><ScrollViewer x:Name='PART_ScrollViewer' Style='{DynamicResource RiceScrollViewer}' CanContentScroll='True' HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto'><ItemsPresenter/></ScrollViewer></Border></ControlTemplate>");
        results.MaxHeight = 340;ScrollViewer.SetHorizontalScrollBarVisibility(results,ScrollBarVisibility.Disabled);
        var row = new FrameworkElementFactory(typeof(DockPanel));
        var badge = new FrameworkElementFactory(typeof(Grid));badge.SetValue(FrameworkElement.WidthProperty,24.0);badge.SetValue(FrameworkElement.HeightProperty,24.0);badge.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,12,0));badge.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);badge.SetValue(DockPanel.DockProperty,Dock.Left);
        var symbol=new FrameworkElementFactory(typeof(TextBlock));symbol.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Symbol"));symbol.SetValue(TextBlock.ForegroundProperty,Color("#88C0D0"));symbol.SetValue(TextBlock.FontSizeProperty,18.0);symbol.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);symbol.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);badge.AppendChild(symbol);
        var icon = new FrameworkElementFactory(typeof(Image));
        icon.SetValue(FrameworkElement.WidthProperty, 24.0); icon.SetValue(FrameworkElement.HeightProperty, 24.0);
        icon.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding("Icon"));badge.AppendChild(icon);row.AppendChild(badge);
        var text=new FrameworkElementFactory(typeof(StackPanel));text.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
        name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));text.AppendChild(name);
        var detail=new FrameworkElementFactory(typeof(TextBlock));detail.SetValue(TextBlock.FontSizeProperty,11.0);detail.SetValue(TextBlock.ForegroundProperty,Color("#96A7BA"));detail.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);detail.SetValue(FrameworkElement.MarginProperty,new Thickness(0,3,0,0));detail.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Detail"));text.AppendChild(detail);row.AppendChild(text);
        results.ItemTemplate = new DataTemplate { VisualTree = row };
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,10,12,10)));
        style.Setters.Add(new Setter(Control.ForegroundProperty,Color("#D8DEE9")));
        var template = new ControlTemplate(typeof(ListBoxItem));
        var itemBorder = new FrameworkElementFactory(typeof(Border)); itemBorder.Name = "pill";
        itemBorder.SetValue(Border.CornerRadiusProperty,new CornerRadius(9));
        itemBorder.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.MarginProperty,new Thickness(12,10,12,10));
        itemBorder.AppendChild(presenter); template.VisualTree = itemBorder;
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Border.BackgroundProperty,Color("#507A9CAB"),"pill")); template.Triggers.Add(selected);
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty,Color("#306D8997"),"pill")); template.Triggers.Add(hover);
        style.Setters.Add(new Setter(Control.TemplateProperty,template)); results.ItemContainerStyle = style;
        stack.Children.Add(results);
        status.FontSize = 11; status.Foreground = Color("#96A7BA"); status.Margin = new Thickness(4,12,4,0); stack.Children.Add(status);
        query.TextChanged += delegate { Filter(); };
        PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) { Conceal(); e.Handled = true; }
            else if (e.Key == Key.Enter) { if((Keyboard.Modifiers&ModifierKeys.Control)!=0)OpenWeb();else Launch(); e.Handled = true; }
            else if(e.Key==Key.Tab){CompleteSelected();e.Handled=true;}
            else if(e.Key==Key.C&&(Keyboard.Modifiers&ModifierKeys.Control)!=0&&query.SelectionLength==0){CopySelected();e.Handled=true;}
            else if (e.Key == Key.Down || e.Key == Key.Up) {
                if (results.Items.Count > 0) { results.SelectedIndex = Math.Max(0,Math.Min(results.Items.Count-1,results.SelectedIndex+(e.Key == Key.Down ? 1 : -1))); results.ScrollIntoView(results.SelectedItem); }
                e.Handled = true;
            }
        };
        results.PreviewMouseLeftButtonUp += delegate { if (results.SelectedItem != null) Launch(); };
        Deactivated += delegate { if (!dropdown) Conceal(); };
        SpotlightTheme.Start();ThemeTransition.Began+=delegate{if(IsVisible)Frost();};ThemeTransition.Frame+=delegate{if(IsVisible)GlassTint();};
        SourceInitialized += delegate {
            var hwnd = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(hwnd);
            src.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
            var margins = new MARGINS { left=-1,right=-1,top=-1,bottom=-1 };
            DwmExtendFrameIntoClientArea(hwnd,ref margins);
            // Windows draws the acrylic as a full rectangle and ignores window regions, so let DWM round
            // the window itself (DWMWCP_ROUND, 8 DIP); the panel's outline uses the same radius.
            int corner=2; DwmSetWindowAttribute(hwnd,33,ref corner,4);
            int noBorder=unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(hwnd,34,ref noBorder,4);
            // Accent acrylic stays blurred for the non-activating dropdown too.
            var accent=new ACCENT { state=4,flags=0,color=unchecked((int)0xAA383028) };
            IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ACCENT)));
            try {
                Marshal.StructureToPtr(accent,memory,false);
                var data=new COMPOSITION { attribute=19,data=memory,size=Marshal.SizeOf(typeof(ACCENT)) };
                glassResult=SetWindowCompositionAttribute(hwnd,ref data) ? 0 : -1;
            } finally { Marshal.FreeHGlobal(memory); }

            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(delegate(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) {
                if (dropdown && msg == 0x21) { handled = true; return new IntPtr(3); }
                return IntPtr.Zero;
            });
        };
    }
    // App search is a separate provider: slash-command routing can be added here
    // without interpreting user input as a shell command.
    internal static int Score(string name, string text) {
        if (text.Length == 0) return 1;
        if (name.Equals(text,StringComparison.OrdinalIgnoreCase)) return 100;
        if (name.StartsWith(text,StringComparison.OrdinalIgnoreCase)) return 80;
        if (name.IndexOf(text,StringComparison.OrdinalIgnoreCase) >= 0) return 60;
        return text.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).All(t => name.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0) ? 40 : 0;
    }
    internal static AppEntry[] RankApps(AppEntry[] catalog,string text){return catalog.Select(a=>new{App=a,Rank=Score(a.Name,text)}).Where(a=>a.Rank>0).OrderByDescending(a=>AppUsage.Last(a.App.Key)).ThenByDescending(a=>a.Rank).ThenBy(a=>a.App.Name).Take(7).Select(a=>a.App).ToArray();}
    void ShowResults(SpotlightResult[] matches,bool preserve=false){
        var selected=preserve?results.SelectedItem as SpotlightResult:null;results.ItemsSource=matches;
        int index=selected==null?-1:Array.FindIndex(matches,m=>m.Key==selected.Key);results.SelectedIndex=index>=0?index:matches.Length>0?0:-1;
        status.Text=matches.Length==0?"No matching results":matches[0].Kind=="copy"?"Enter copies result    Ctrl+C copy    Esc close":"Up / Down choose    Enter open    Tab complete    Ctrl+C copy";
    }
    async void Filter(bool preserve=false) {
        filterCount++;int version=++queryVersion;if(filtering!=null){filtering.Cancel();filtering.Dispose();}filtering=new CancellationTokenSource();var token=filtering.Token;
        string text=query.Text.Trim();var quick=SpotlightSearch.Quick(text,apps,windows);ShowResults(quick,preserve);
        string term;bool folders;
        if(quick.Length==1&&(quick[0].Kind=="copy"||quick[0].Kind=="hint")||!SpotlightSearch.FileQuery(text,out term,out folders))return;
        try{
            if(quick.Length==0)status.Text="Searching Desktop, Documents and Downloads...";
            await Task.Delay(160,token);var files=await SpotlightFiles.Search(term,folders,token);
            if(version==queryVersion&&!closing&&!token.IsCancellationRequested)ShowResults(SpotlightSearch.MergeFiles(quick,files),true);
        }catch(OperationCanceledException){}catch{if(version==queryVersion)status.Text="File search is unavailable; apps and commands still work.";}
    }
    void CompleteQuery(string value){
        query.Text=value;query.CaretIndex=query.Text.Length;
        if(dropdown)SearchBridge.Broadcast(serializer.Serialize(new{completion=true,query=value,source=requestSource}));
        else query.Focus();
    }
    void CompleteSelected(){var selected=results.SelectedItem as SpotlightResult;if(selected!=null&&selected.Kind=="query")CompleteQuery(selected.Value);}
    void CopySelected(){var selected=results.SelectedItem as SpotlightResult;if(selected==null||String.IsNullOrEmpty(selected.Value))return;try{Clipboard.SetText(selected.Kind=="app"&&!String.IsNullOrEmpty(selected.App.Executable)?selected.App.Executable:selected.Value);status.Text="Copied to clipboard";}catch{status.Text="Clipboard is busy. Try again.";}}
    void OpenWeb(){if(query.Text.Trim().Length==0)return;try{Process.Start(new ProcessStartInfo(SpotlightSearch.Web(query.Text.Trim()).Value){UseShellExecute=true});Conceal();}catch{status.Text="Could not open your browser.";}}
    async void Launch() {
        var item=results.SelectedItem as SpotlightResult;if(item==null||item.Kind=="hint")return;
        try{
            switch(item.Kind){
                case "query":CompleteQuery(item.Value);return;
                case "copy":CopySelected();return;
                case "app":AppCatalog.Launch(item.App);Conceal();return;
                case "window":await Task.Run(()=>SpotlightWindows.Focus(item));Conceal();return;
                case "path":if(!File.Exists(item.Value)&&!Directory.Exists(item.Value))throw new FileNotFoundException();Process.Start(new ProcessStartInfo(item.Value){UseShellExecute=true});Conceal();return;
                case "uri":Process.Start(new ProcessStartInfo(item.Value){UseShellExecute=true});Conceal();return;
                case "recycle":Process.Start(new ProcessStartInfo("explorer.exe","shell:RecycleBinFolder"){UseShellExecute=true});Conceal();return;
                case "lock":Conceal();SpotlightWindows.Lock();return;
                case "refresh":await RefreshSearch();return;
                case "openall":case "closeall":status.Text=item.Kind=="closeall"?"Force-closing workspace apps...":"Opening configured apps...";status.Text=await WorkspaceBatch.Run(item.Kind=="closeall");return;
            }
        }catch(Exception e){status.Text=e is FileNotFoundException?"This file or folder moved. Try /refresh.":"Could not complete this action. "+e.Message;}
    }
    async Task RefreshSearch(){
        if(refreshing){status.Text="Refreshing search...";return;}refreshing=true;status.Text="Refreshing installed apps...";
        try{
            var updated=await Task.Run(()=>SpotlightWindows.RefreshApps());
            var text=serializer.Serialize(updated.Select(app=>new{app.Name,app.AppID,app.Executable}).ToArray());File.WriteAllText(catalog+".tmp",text);if(File.Exists(catalog))File.Replace(catalog+".tmp",catalog,null);else File.Move(catalog+".tmp",catalog);
            apps=updated;catalogTime=File.GetLastWriteTimeUtc(catalog);SpotlightFiles.Invalidate();ReadWindows();status.Text="Search refreshed. "+apps.Length+" installed apps.";
        }catch{status.Text="Could not refresh installed apps; keeping the existing catalog.";}finally{refreshing=false;}
    }
    async void ReadWindows(){if(readingWindows)return;readingWindows=true;int version=++windowVersion;try{var found=await Task.Run(()=>SpotlightWindows.Read());if(version!=windowVersion)return;windows=found;if(IsVisible&&!closing)Filter(true);}catch{}finally{readingWindows=false;}}
    async void ReadCatalog() {
        ReadWindows();if(readingCatalog||refreshing)return;readingCatalog=true;
        var previousStamp=catalogTime;bool empty=apps.Length==0;
        try {
            var loaded=await Task.Run(()=>{
                AppUsage.Refresh();var stamp=File.GetLastWriteTimeUtc(catalog);
                if(!empty&&stamp==previousStamp)return null;
                AppEntry[] catalogEntries;
                try{catalogEntries=new JavaScriptSerializer().Deserialize<AppEntry[]>(File.ReadAllText(catalog))??new AppEntry[0];}
                catch{if(!empty)throw;catalogEntries=new AppEntry[0];}
                var entries=AppShortcuts.Merge(catalogEntries).Where(app=>!String.IsNullOrWhiteSpace(app.AppID)||!String.IsNullOrWhiteSpace(app.Executable)).ToArray();
                return Tuple.Create(stamp,entries);
            });
            // An explicit refresh may have installed a newer catalog while this read was in flight.
            if(loaded!=null&&!refreshing&&catalogTime==previousStamp){apps=loaded.Item2;catalogTime=loaded.Item1;catalogReads++;if(IsVisible&&!closing)Filter(true);}
        } catch { /* Keep the last good catalog if the installer is replacing it. */ }
        finally{readingCatalog=false;}
    }
    void ProcessRequests() {
        foreach (var file in Directory.GetFiles(RequestFolder,"*.json").OrderBy(f => f)) {
            try { var request = new JavaScriptSerializer().Deserialize<SearchRequest>(File.ReadAllText(file)); File.Delete(file); Apply(request); }
            catch { }
        }
    }
    void Apply(SearchRequest request) {
        if (request == null) return;
        requestCount++;if(request.source!=null)requestSource=request.source;
        if (request.query != null && request.query.Length > 160) request.query = request.query.Substring(0,160);
        if (request.action == "diagnostics") return;
        if ((request.action == "launch" || request.action == "up" || request.action == "down" || request.action == "complete" || request.action == "copy" || request.action == "web") && dropdown && request.query != null && request.query != query.Text) query.Text=request.query;
        // The hot path is purely in-memory. Do not reposition or reload on typing.
        if (request.action == "update" && dropdown && IsVisible && !closing) {
            if (query.Text != (request.query ?? "")) query.Text=request.query ?? "";
            return;
        }
        if (request.action == "hide-dropdown") { if (dropdown) Conceal(); return; }
        // Sent when the control center opens: close the dropdown or the floating window.
        if (request.action == "hide") { if (IsVisible && !closing) Conceal(); return; }
        if(request.action=="complete"){CompleteSelected();return;}if(request.action=="copy"){CopySelected();return;}if(request.action=="web"){OpenWeb();return;}
        if (request.action == "launch") { if (IsVisible) Launch(); return; }
        if (request.action == "up" || request.action == "down") {
            if (results.Items.Count > 0) { results.SelectedIndex = Math.Max(0,Math.Min(results.Items.Count-1,results.SelectedIndex+(request.action == "down" ? 1 : -1))); results.ScrollIntoView(results.SelectedItem); } return;
        }
        if (request.action != "update" && request.action != "floating") return;
        bool wasVisible = IsVisible && !closing;
        dropdown = request.action == "update";
        if (!wasVisible || apps.Length == 0) ReadCatalog();
        var handle = new WindowInteropHelper(this).EnsureHandle();
        long style = GetWindowLongPtr(handle,-20).ToInt64();
        SetWindowLongPtr(handle,-20,new IntPtr(dropdown ? style | 0x08000000L : style & ~0x08000000L));
        ShowActivated = !dropdown;
        var source = HwndSource.FromHwnd(handle);
        double scale = source.CompositionTarget.TransformToDevice.M11;
        var foreground = GetForegroundWindow();
        uint pid; GetWindowThreadProcessId(foreground,out pid);
        try { if (Process.GetProcessById((int)pid).ProcessName == "zebar") barWindow = foreground; } catch { }
        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = dropdown && barWindow != IntPtr.Zero ? System.Windows.Forms.Screen.FromHandle(barWindow) : System.Windows.Forms.Screen.FromHandle(foreground);
        var area = screen.WorkingArea;
        header.Visibility = dropdown ? Visibility.Collapsed : Visibility.Visible;
        Width = dropdown ? Math.Min(520,area.Width/scale-24) : Math.Min(720,area.Width/scale-24);
        if (dropdown) {
            RECT bounds; GetWindowRect(barWindow,out bounds);
            double webScale = Math.Max(1,Math.Min(4,request.scale));
            Left = Math.Max(screen.Bounds.Left/scale+4,Math.Min((bounds.left+(request.x+request.width/2)*webScale)/scale-Width/2,screen.Bounds.Right/scale-Width-4));
            Top = (bounds.top+request.y*webScale)/scale+4;
        } else {
            Left = (area.Left+area.Width/2)/scale-Width/2;
            Top = (area.Top+area.Height*.23)/scale;
        }
        string text = request.query ?? ""; if (text.Length > 160) text = text.Substring(0,160);
        if (query.Text != text) query.Text = text;
        else if (!wasVisible || !dropdown) Filter();
        if (!wasVisible) Reveal();
        if (!dropdown) {
            Activate();
            Dispatcher.BeginInvoke(new Action(delegate { query.Focus(); Keyboard.Focus(query); query.CaretIndex = query.Text.Length; }),System.Windows.Threading.DispatcherPriority.Input);
        }

    }
    void FreezePanelHeight() {
        var root=(FrameworkElement)Content;
        root.Height=double.NaN;
        root.Measure(new Size(Width,double.PositiveInfinity));
        expandedHeight=Math.Max(1,root.DesiredSize.Height);
        root.Height=expandedHeight;
        root.VerticalAlignment=VerticalAlignment.Top;
        SizeToContent=SizeToContent.Manual;
    }
    readonly Grid frostHost;
    Brush previousFrost;
    void GlassTint(){
        var handle=new WindowInteropHelper(this).Handle;if(handle==IntPtr.Zero)return;
        var c=Color("#2E3440").Color;
        var accent=new ACCENT{state=4,flags=0,color=unchecked((int)(0xAA000000u|((uint)c.B<<16)|((uint)c.G<<8)|c.R))};
        IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ACCENT)));
        try{Marshal.StructureToPtr(accent,memory,false);var data=new COMPOSITION{attribute=19,data=memory,size=Marshal.SizeOf(typeof(ACCENT))};SetWindowCompositionAttribute(handle,ref data);}finally{Marshal.FreeHGlobal(memory);}
    }
    // Frosted glass under a Nord tint when the Rice tab's frost is on (the accent blur shows otherwise);
    // lock-in keeps the plain look.
    void Frost() {
        if(!IsVisible)return;
        GlassTint();
        var handle = new WindowInteropHelper(this).Handle;
        var frost = handle == IntPtr.Zero || FocusState.Read().ThemeActive ? null : FrostFile.Behind(handle);
        previousFrost=ThemeTransition.Crossfade(previousFrost,frost);
        var tint = Color("#2E3440");tint.Opacity=FrostFile.TintOpacity();
        frostHost.Background = frost == null ? null : FrostFile.Under(previousFrost, tint);
    }
    // Windows clips the window's corners itself (at most 8 px), so the border follows the setting up to that.
    void Round() {
        double scale = Math.Min(1, Roundness.Scale);
        Roundness.Apply(this, scale);
        var hwnd = new WindowInteropHelper(this).Handle; if (hwnd == IntPtr.Zero) return;
        int corner = scale <= 0 ? 1 : scale < .75 ? 3 : 2; // do not round, small, round
        DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
    }
    void Reveal() {
        if(GameWindows.ForegroundGame!=IntPtr.Zero)return;
        StopMotion();
        bool reopening=IsVisible;
        closing=false; poof.Progress=-1;
        if (!reopening) {
            FreezePanelHeight(); reveal=0; Opacity=0; surface.Opacity=1;
            Height=expandedHeight; // Full geometry stays fixed throughout the fade.
        }
        Show(); Frost(); Round();
        if (!SystemParameters.ClientAreaAnimation) { CompleteOpen(); return; }
        motionFrom=reveal; motionClock.Restart(); StartMotion();
    }
    void Conceal() {
        if (!IsVisible || closing) return;
        bool opening=motionActive;
        if(filtering!=null)filtering.Cancel();
        StopMotion(); closing=true;
        if (!opening) { FreezePanelHeight(); reveal=1; }
        if (!SystemParameters.ClientAreaAnimation) { FinishClose(); return; }
        motionFrom=reveal; motionClock.Restart(); StartMotion();
    }
    void StartMotion() {
        motionActive=true; motionFrames=0; lastRenderTime=TimeSpan.MinValue;
        var pulse=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(closing ? CloseMs : OpenMs));
        Timeline.SetDesiredFrameRate(pulse,144);
        BeginAnimation(FramePulseProperty,pulse);
        CompositionTarget.Rendering += OnMotionFrame;
    }
    void StopMotion() {
        if(motionActive && motionClock.Elapsed.TotalSeconds>0) lastMotionFps=motionFrames/motionClock.Elapsed.TotalSeconds;
        motionActive=false;
        CompositionTarget.Rendering -= OnMotionFrame;
        BeginAnimation(FramePulseProperty,null);
    }
    void OnMotionFrame(object sender,EventArgs args) {
        var rendering=(RenderingEventArgs)args;
        if(rendering.RenderingTime==lastRenderTime) return;
        lastRenderTime=rendering.RenderingTime; motionFrames++;
        AnimateFrame();
    }
    void AnimateFrame() {
        double t=Math.Min(1,motionClock.Elapsed.TotalMilliseconds/(closing ? CloseMs : OpenMs));
        double ease=1-Math.Pow(1-t,3);
        reveal=closing ? motionFrom*(1-ease) : motionFrom+(1-motionFrom)*ease;
        Opacity=reveal;
        poof.Progress=-1;
        if (t>=1) {
            StopMotion();
            if (closing) FinishClose(); else CompleteOpen();
        }
    }
    void CompleteOpen() {
        reveal=1; Opacity=1; surface.Opacity=1;
        Height=expandedHeight;
        ((FrameworkElement)Content).Height=double.NaN;
        SizeToContent=SizeToContent.Height;
    }
    void FinishClose() {
        StopMotion(); Hide(); closing=false; reveal=0; poof.Progress=-1;
        previousFrost=null;frostHost.Background=null;
        ((FrameworkElement)Content).Height=double.NaN;
        SizeToContent=SizeToContent.Height;
    }
    void ResizeReveal(double amount) {
        double puffRoom=closing ? 38*Math.Sin(Math.PI*Math.Min(1,motionClock.Elapsed.TotalMilliseconds/CloseMs)) : 1;
        // Resize the real HWND through WPF, so DWM's acrylic bottom edge follows.
        // The content keeps its full measured height and is simply cropped by the
        // native bounds; result rows never compress/reflow during the animation.
        Height=Math.Max(Math.Max(1,puffRoom),expandedHeight*amount);
    }
    int NativeHeight() {
        RECT bounds; GetWindowRect(new WindowInteropHelper(this).Handle,out bounds);
        return bounds.bottom-bounds.top;
    }
    [STAThread] static void Main(string[] args) {
        if ((args.Length == 2||args.Length==3) && (args[0] == "--render" || args[0] == "--render-dropdown")) {
            var preview = new NordSpotlight();
            if (args[0] == "--render-dropdown") { preview.header.Visibility = Visibility.Collapsed; preview.Width = 520; }
            preview.apps = new[] { new AppEntry { Name = "Spotify", AppID = "preview" }, new AppEntry { Name = "Helium", AppID = "preview" }, new AppEntry { Name = "Obsidian", AppID = "preview" } };
            if(args.Length==3)preview.query.Text=args[2];
            preview.Filter();
            var visual = (FrameworkElement)preview.Content;
            visual.Measure(new Size(preview.Width,600)); visual.Arrange(new Rect(0,0,preview.Width,visual.DesiredSize.Height)); visual.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)preview.Width*2,(int)Math.Ceiling(visual.ActualHeight*2),192,192,PixelFormats.Pbgra32);
            bitmap.Render(visual); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(args[1])) png.Save(file);
            return;
        }
        if(args.Length==1&&args[0]=="--check"){try{SpotlightChecks.Run();}catch(Exception e){Console.Error.WriteLine("FAIL: "+e.Message);Environment.ExitCode=1;}return;}

        bool first;
        Directory.CreateDirectory(RequestFolder);
        if (!args.Contains("--background")) {
            SearchRequest request = new SearchRequest { action = "floating", query = "" };
            if (args.Length == 2 && args[0] == "--request") {
                try { request = new JavaScriptSerializer().Deserialize<SearchRequest>(Encoding.UTF8.GetString(Convert.FromBase64String(args[1]))); } catch { return; }
            }
            string file = Path.Combine(RequestFolder,DateTime.UtcNow.Ticks.ToString("D19")+"-"+Guid.NewGuid().ToString("N"));
            File.WriteAllText(file+".tmp",new JavaScriptSerializer().Serialize(request)); File.Move(file+".tmp",file+".json");
        }
        using (var mutex = new Mutex(true,"Local\\LiamNordSpotlight",out first))
        using (var signal = new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\LiamNordSpotlightShow")) {
            if (!first) {
                foreach (var process in Process.GetProcessesByName("NordSpotlight")) {
                    if (process.Id != Process.GetCurrentProcess().Id) AllowSetForegroundWindow((uint)process.Id);
                }
                signal.Set(); return;
            }
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var window = new NordSpotlight();
            window.Dispatcher.BeginInvoke(new Action(window.ReadCatalog),System.Windows.Threading.DispatcherPriority.Background);
            window.usageTracker=new AppUsageTracker(()=>window.apps,window.Dispatcher);
            app.Exit+=delegate{window.usageTracker.Dispose();};
            new WindowInteropHelper(window).EnsureHandle();
            // Pay font/template/layout initialization once during background startup,
            // so the first click doesn't have to create all seven result rows.
            window.Filter();
            var warmVisual=(FrameworkElement)window.Content;
            warmVisual.Measure(new Size(720,600));
            warmVisual.Arrange(new Rect(0,0,720,warmVisual.DesiredSize.Height));
            warmVisual.UpdateLayout();
            var warmBitmap=new RenderTargetBitmap(720,(int)Math.Ceiling(warmVisual.ActualHeight),96,96,PixelFormats.Pbgra32);
            warmBitmap.Render(warmVisual);
            var bridge = new SearchBridge(delegate(string json) {
                return window.Dispatcher.Invoke(new Func<string>(delegate {
                    var watch=Stopwatch.StartNew();
                    try {
                        var request=window.serializer.Deserialize<SearchRequest>(json);window.Apply(request);
                        return window.serializer.Serialize(new { ok=true,id=request.id,action=request.action,query=window.query.Text,ms=watch.Elapsed.TotalMilliseconds,requests=window.requestCount,filters=window.filterCount,catalogReads=window.catalogReads,pid=Process.GetCurrentProcess().Id,glass=window.glassResult,visible=window.IsVisible,nativeHeight=window.NativeHeight(),animationFps=window.lastMotionFps });
                    } catch { return "{\"ok\":false}"; }
                }));
            });
            bridge.Start();
            var listener = new Thread(delegate() { while (true) { signal.WaitOne(); window.Dispatcher.BeginInvoke(new Action(window.ProcessRequests)); } });
            listener.IsBackground = true; listener.Start();
            if (!args.Contains("--background")) window.ProcessRequests();
            app.Run();
        }
    }
    [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int left,right,top,bottom; }
    [StructLayout(LayoutKind.Sequential)] struct ACCENT { public int state,flags,color,animation; }
    [StructLayout(LayoutKind.Sequential)] struct COMPOSITION { public int attribute; public IntPtr data; public int size; }
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref MARGINS margins);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowCompositionAttribute(IntPtr h,ref COMPOSITION data);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int left,top,right,bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle,out RECT rect);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle,out uint pid);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint processId);
}

// Three soft, low-opacity wisps. No particle loop, bitmap blur or background work.
class Poof : FrameworkElement {
    public static readonly DependencyProperty ProgressProperty=DependencyProperty.Register("Progress",typeof(double),typeof(Poof),new FrameworkPropertyMetadata(-1.0,FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress { get { return (double)GetValue(ProgressProperty); } set { SetValue(ProgressProperty,value); } }
    public Poof() { IsHitTestVisible=false; }
    protected override void OnRender(DrawingContext dc) {
        double t=Progress; if(t<0 || t>=1) return;
        double opacity=Math.Sin(Math.PI*t)*.34;
        var brush=new RadialGradientBrush(System.Windows.Media.Color.FromArgb((byte)(255*opacity),216,235,239),System.Windows.Media.Colors.Transparent);
        double x=ActualWidth/2, y=24-t*9, spread=8+t*17;
        dc.DrawEllipse(brush,null,new Point(x-spread,y+3),13+t*6,7+t*3);
        dc.DrawEllipse(brush,null,new Point(x,y-2),17+t*7,10+t*3);
        dc.DrawEllipse(brush,null,new Point(x+spread,y+3),13+t*6,7+t*3);
    }
}

