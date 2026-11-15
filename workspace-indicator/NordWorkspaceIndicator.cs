using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// A click-through, non-activating overlay. Reads a configurable hold-key state (Left Alt by default).
// All workspace data comes from GlazeWM's read-only localhost WebSocket API.
class NordWorkspaceIndicator : Window
{
    const int CardWidth = 68, CardStep = 76, CardHeight = 76;
    static readonly string[] Names = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
    // Card labels come from workspaces.json next to the exe (the single workspace definition file).
    static string[] LoadLabels()
    {
        var result = new string[Names.Length];
        try {
            var root = Dict(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspaces.json"))));
            foreach (var item in (IEnumerable)root["workspaces"]) {
                var ws = Dict(item); if (ws == null || !ws.ContainsKey("name")) continue;
                int index = Array.IndexOf(Names, Convert.ToString(ws["name"]));
                if (index >= 0 && ws.ContainsKey("label")) result[index] = Convert.ToString(ws["label"]);
            }
        } catch (Exception error) { Log("labels: " + error.Message); return null; }
        return result;
    }
    void ApplyLabels()
    {
        var next = LoadLabels(); if (next == null) return; // Keep the current labels while the file is invalid.
        for (int i = 0; i < Names.Length; i++) labels[i].Text = next[i] ?? "";
        if(variants!=null)variants.UpdateLabels(next);
    }
    IDisposable keyWatcher;
    FileSystemWatcher labelWatcher;
    System.Windows.Threading.DispatcherTimer labelDebounce;
    void WatchLabels()
    {
        labelDebounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        labelDebounce.Tick += delegate { labelDebounce.Stop(); ApplyLabels(); };
        labelWatcher = new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory, "workspaces.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
        FileSystemEventHandler changed = delegate { Dispatcher.BeginInvoke(new Action(() => { labelDebounce.Stop(); labelDebounce.Start(); })); };
        labelWatcher.Changed += changed; labelWatcher.Created += changed;
        labelWatcher.Renamed += delegate { Dispatcher.BeginInvoke(new Action(() => { labelDebounce.Stop(); labelDebounce.Start(); })); };
        labelWatcher.EnableRaisingEvents = true;
    }
    readonly Border panel = new Border();
    StackPanel classicContent;IndicatorVariants variants;string indicatorStyle="classic";int visibilityRevision;
    readonly BorderShimmer shimmer = new BorderShimmer();
    readonly Canvas track = new Canvas();
    readonly TranslateTransform markerMove = new TranslateTransform();
    readonly ScaleTransform panelScale = new ScaleTransform(.84, .72);
    readonly TextBlock[] numbers = new TextBlock[10];
    readonly TextBlock[] labels = new TextBlock[10];

    readonly AltMenuGuard altMenuGuard = new AltMenuGuard();
    readonly CancellationTokenSource stop = new CancellationTokenSource();
    readonly SemaphoreSlim refresh = new SemaphoreSlim(0, 1);
    volatile bool held;
    bool connected;
    bool visible;
    int selected = -1;
    string currentName;
    IntPtr hwnd;
    Native.RECT pinnedMonitor;
    DateTime lastGood = DateTime.MinValue;

    public NordWorkspaceIndicator()
    {
        Title = "Nord Workspace Indicator";
        Width = 820; Height = 134;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Focusable = false; IsHitTestVisible = false;
        UseLayoutRounding = true;
        var root = new Grid();
        root.Children.Add(panel); root.Children.Add(shimmer); Content = root;
        shimmer.RenderTransform = panelScale;
        shimmer.RenderTransformOrigin = new Point(.5,.5);
        panel.Margin = new Thickness(10);
        panel.Padding = new Thickness(20, 14, 20, 15);
        panel.CornerRadius = new CornerRadius(18); // designed radii; Round() scales them with the Rice tab
        panel.BorderThickness = new Thickness(3);
        panel.RenderTransform = panelScale;
        panel.RenderTransformOrigin = new Point(.5, .5);
        panel.Opacity = 0;
        var content = new StackPanel();classicContent=content; panel.Child = content;
        track.Width = CardStep * 9 + CardWidth; track.Height = CardHeight;
        content.Children.Add(track);
        var marker = new Border {
            Width = CardWidth, Height = CardHeight, CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1.5), RenderTransform = markerMove
        };
        track.Children.Add(marker);
        for (int i = 0; i < Names.Length; i++)
        {
            var cell = new Border { Width = CardWidth, Height = CardHeight, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1.5) };
            Canvas.SetLeft(cell, i * CardStep); track.Children.Add(cell);
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            cell.Child = stack;
            numbers[i] = Text(Names[i], 23, "#D8DEE9"); numbers[i].FontWeight = FontWeights.SemiBold;
            numbers[i].TextAlignment = TextAlignment.Center;
            stack.Children.Add(numbers[i]);
            labels[i] = Text("", 9, "#9BAEC2"); labels[i].TextAlignment = TextAlignment.Center;
            labels[i].Margin = new Thickness(0, 4, 0, 0); stack.Children.Add(labels[i]);
        }
        Paint(IndicatorPalette.For(new FocusState(), null)); Round();
        ThemeTransition.Began+=BeginTheme;ThemeTransition.Frame+=ThemeFrame;ThemeTransition.Listen();
        SourceInitialized += delegate {
            hwnd = new WindowInteropHelper(this).Handle;
            long style = Native.GetWindowLongPtr(hwnd, -20).ToInt64();
            Native.SetWindowLongPtr(hwnd, -20, new IntPtr(style | 0x08000000L | 0x80L | 0x20L));
            altMenuGuard.RegisterRawInput(hwnd);
            HwndSource.FromHwnd(hwnd).AddHook(delegate(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) {
                if (msg == 0x00FF) altMenuGuard.ReadRawInput(l); // WM_INPUT: independent of low-level hook consumers.
                if (msg == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
                return IntPtr.Zero;
            });
        };
        Closed += delegate { stop.Cancel(); altMenuGuard.Dispose(); if(keyWatcher!=null)keyWatcher.Dispose(); };
    }

    static SolidColorBrush Brush(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
    static TextBlock Text(string value, double size, string color) {
        return new TextBlock { Text = value, FontSize = size, Foreground = Brush(color), FontWeight = FontWeights.Bold };
    }
    // Rice tab motion: speed scales every duration; the ease defaults to a plain ease-out.
    static DoubleAnimation Animate(double to, int duration, IEasingFunction ease = null) {
        return new DoubleAnimation(to, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? Motion.Ms(duration) : 0)) {
            EasingFunction = ease ?? new CubicEase { EasingMode = EasingMode.EaseOut }
        };
    }
    void SelectWorkspace(string name, bool instant)
    {
        int index = Array.IndexOf(Names, name);
        if (index < 0 || index == selected) return;
        selected = index;
        if(variants!=null){variants.SetWorkspace(index,instant);return;}
        markerMove.BeginAnimation(TranslateTransform.XProperty, Animate(index * CardStep, instant ? 0 : 240, Motion.Shift()));
        PaintNumbers();
    }
    IndicatorPalette palette;
    IndicatorPalette themeFrom,themeTo;
    Brush previousFrost;
    void BeginTheme(){
        if(!visible)return;
        themeFrom=palette.Copy();themeTo=IndicatorPalette.For(FocusState.Read(),ThemeTransition.Current.to);
        var live=themeTo.Copy();BlendPalette(live,themeFrom,themeTo,ThemeTransition.Position);Paint(live);Frost(FocusState.Read());
    }
    static void BlendPalette(IndicatorPalette live,IndicatorPalette from,IndicatorPalette to,double t){
        foreach(var field in typeof(IndicatorPalette).GetFields())ThemeTransition.BlendBrush((Brush)field.GetValue(live),(Brush)field.GetValue(from),(Brush)field.GetValue(to),t);
    }
    void ThemeFrame(){if(!visible||themeTo==null)return;BlendPalette(palette,themeFrom,themeTo,ThemeTransition.Position);if(!ThemeTransition.Active){themeFrom=null;themeTo=null;}}
    void ConfigureStyle(string requested=null){
        string next=requested??AppearanceFile.StringValue("indicatorStyle","classic");if(next!="gallery"&&next!="ribbon")next="classic";
        if(next==indicatorStyle&&panel.Child!=null)return;if(variants!=null)variants.Settle();indicatorStyle=next;selected=-1;
        if(next=="classic"){variants=null;panel.Child=classicContent;Width=820;Height=134;panel.BorderThickness=new Thickness(3);}
        else{variants=new IndicatorVariants(next,Names,Array.ConvertAll(labels,label=>label.Text));panel.Child=variants;Width=next=="gallery"?790:600;Height=next=="gallery"?300:176;panel.BorderThickness=new Thickness(1);}
        if(!visible){panelScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);panelScale.BeginAnimation(ScaleTransform.ScaleYProperty,null);panelScale.ScaleX=next=="classic"?.84:.96;panelScale.ScaleY=next=="classic"?.72:.96;}
        if(palette!=null)Paint(palette);Round();
    }
    void Paint(IndicatorPalette p)
    {
        palette = p;
        panel.BorderBrush = p.PanelBorder; panel.Background = p.PanelBackground;
        if(variants!=null){variants.Apply(p);return;}
        foreach (UIElement child in track.Children) {
            var tile = child as Border; if (tile == null) continue;
            bool marker = tile.RenderTransform == markerMove;
            tile.Background = marker ? p.Marker : p.Tile; tile.BorderBrush = marker ? p.MarkerBorder : p.TileBorder;
        }
        PaintNumbers();
    }
    void PaintNumbers()
    {
        for (int i = 0; i < Names.Length; i++) {
            numbers[i].Foreground = i == selected ? palette.NumberOn : palette.Number;
            labels[i].Foreground = i == selected ? palette.LabelOn : palette.Label;
        }
    }
    void Reveal()
    {
        // Every open path (including workspace events and reconnects) must respect the bar-only style.
        if(AppearanceFile.StringValue("indicatorStyle","classic")=="zebar"){if(visible)Conceal();return;}
        if (altMenuGuard.Suppressed || GameWindows.ForegroundGame!=IntPtr.Zero || visible || !connected || currentName == null) return;
        var focus=FocusState.Read();
        Paint(IndicatorPalette.For(focus, WallpaperPalette.Load()));
        Frost(focus); Round();
        for(int i=0;i<Names.Length;i++){numbers[i].Opacity=labels[i].Opacity=focus.ThemeActive&&Array.IndexOf(focus.settings.disabledWorkspaces,Names[i])>=0?.25:1;}
        visible = true;
        if(ThemeTransition.Active){Paint(IndicatorPalette.For(focus,ThemeTransition.Current.from));BeginTheme();}
        visibilityRevision++;
        SelectWorkspace(currentName, true);
        AnimatePanel(true);
        panel.BeginAnimation(OpacityProperty, Animate(1, 170));
    }
    // The Rice tab's corner radius, for the panel, tiles and marker; the shimmer traces the same outline.
    void Round()
    {
        FontFamily = RiceFont.Current;
        Roundness.Apply(panel);
        shimmer.Radius = Math.Max(0, panel.CornerRadius.TopLeft - 1);
    }
    // Frosted glass under the panel's tint when the Rice tab's frost is on; lock-in stays opaque.
    void Frost(FocusState focus)
    {
        Native.RECT r; if (focus.ThemeActive || !Native.GetWindowRect(hwnd, out r)) return;
        double margin = 10 * (r.right - r.left) / Width;
        var frost = FrostFile.Behind(r.left + margin, r.top + margin, r.right - r.left - 2 * margin, r.bottom - r.top - 2 * margin);
        if (frost == null) return;
        previousFrost=ThemeTransition.Crossfade(previousFrost,frost);
        var tint = palette.PanelBackground; tint.Opacity = FrostFile.TintOpacity();
        panel.Background = FrostFile.Under(previousFrost, tint);
    }
    void Conceal()
    {
        if (!visible) return;
        visible = false;
        int version=++visibilityRevision;
        AnimatePanel(false);
        var fade=Animate(0,220);fade.Completed+=delegate{if(version!=visibilityRevision||visible)return;if(variants!=null)variants.Settle();previousFrost=null;themeFrom=null;themeTo=null;panel.Background=palette.PanelBackground;};panel.BeginAnimation(OpacityProperty,fade);
    }
    void AnimatePanel(bool opening)
    {
        // One monotonic expansion; reverse from the current size on quick releases.
        shimmer.Stop();
        int duration = opening ? 190 : 200;
        panelScale.BeginAnimation(ScaleTransform.ScaleXProperty, Animate(opening ? 1 : variants==null?.84:.96, duration, opening ? Motion.Arrive() : null));
        var vertical = Animate(opening ? 1 : variants==null?.72:.96, duration, opening ? Motion.Arrive() : null);
        if (opening && variants==null && SystemParameters.ClientAreaAnimation) {
            shimmer.Play();
        }
        panelScale.BeginAnimation(ScaleTransform.ScaleYProperty, vertical);
    }
    void PinToOriginMonitor()
    {
        // Capture the foreground monitor exactly once on the Alt-down edge.
        // Workspace changes never reposition the overlay until the next press.
        IntPtr monitor = Native.MonitorFromWindow(Native.GetForegroundWindow(), 2);
        var info = new Native.MONITORINFO(); info.cbSize = Marshal.SizeOf(info);
        if (!Native.GetMonitorInfo(monitor, ref info)) return;
        pinnedMonitor = info.rcMonitor;
        var transform = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
        int width = (int)Math.Round(Width * transform.M11);
        int height = (int)Math.Round(Height * transform.M22);
        int x = pinnedMonitor.left + (pinnedMonitor.right - pinnedMonitor.left - width) / 2;
        int y = pinnedMonitor.top + (pinnedMonitor.bottom - pinnedMonitor.top - height) / 2;
        Native.SetWindowPos(hwnd, new IntPtr(-1), x, y, width, height, 0x10 | 0x40);
    }
    public void Start()
    {
        // Left-Alt edges come from the keyboard hook (no polling while idle). The hook runs on this
        // thread; BeginInvoke keeps its callback short so Windows never drops the hook.
        ApplyLabels(); WatchLabels();
        stuckWatch = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        stuckWatch.Tick += delegate { WatchForStuckAlt(); };
        altMenuGuard.LeftAltChanged = down => Dispatcher.BeginInvoke(new Action(() => OnLeftAlt(down)));
        altMenuGuard.Start();
        keyWatcher=IndicatorKeys.Watch(settings=>altMenuGuard.ConfigureKey((uint)IndicatorKeys.Code(settings.workspace)));
        Microsoft.Win32.SystemEvents.PowerModeChanged += delegate(object sender, Microsoft.Win32.PowerModeChangedEventArgs e) {
            if(e.Mode==Microsoft.Win32.PowerModes.Resume) Dispatcher.BeginInvoke(new Action(ResetKeyboard));
        };
        Microsoft.Win32.SystemEvents.SessionSwitch += delegate(object sender, Microsoft.Win32.SessionSwitchEventArgs e) {
            if(e.Reason==Microsoft.Win32.SessionSwitchReason.SessionLock || e.Reason==Microsoft.Win32.SessionSwitchReason.SessionUnlock) Dispatcher.BeginInvoke(new Action(ResetKeyboard));
        };
        Show();
        ReadStep("start");
        Task.Run((Func<Task>)ReadWorkspaceLoop);
        Task.Run((Func<Task>)WatchWorkspaceEvents);
        watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        watchdog.Tick += delegate { Watchdog(); };
        watchdog.Start();
    }
    // Self-healing. The read loop is poked every 5 s, so it should never sit on one step for long.
    // If it does, or the process burns CPU while idle (both seen after GlazeWM restarts/reloads,
    // source not yet pinned down), log where each loop was and restart this helper cleanly.
    static volatile string readStep = "start", eventStep = "start";
    static long readStepTicks = DateTime.UtcNow.Ticks;
    static void ReadStep(string step) { readStep = step; Interlocked.Exchange(ref readStepTicks, DateTime.UtcNow.Ticks); }
    DispatcherTimer watchdog;
    TimeSpan lastCpu; DateTime lastCpuAt; int hotChecks;
    void Watchdog()
    {
        Poke();
        var now = DateTime.UtcNow;
        var stuck = now - new DateTime(Interlocked.Read(ref readStepTicks));
        var cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
        double load = lastCpuAt == default(DateTime) ? 0 : (cpu - lastCpu).TotalMilliseconds / Math.Max(1, (now - lastCpuAt).TotalMilliseconds);
        lastCpu = cpu; lastCpuAt = now;
        hotChecks = load > .5 && !held ? hotChecks + 1 : 0;
        string reason = stuck > TimeSpan.FromSeconds(20) ? "read loop stuck at '" + readStep + "' for " + (int)stuck.TotalSeconds + "s"
            : hotChecks >= 4 ? "idle but using " + (int)(load * 100) + "% CPU for 20s" : null;
        if (reason == null) return;
        Log("watchdog: " + reason + " (read=" + readStep + ", events=" + eventStep + ", workspace=" + currentName + "); restarting");
        Restart();
    }
    static void Restart()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location, "--restart") { UseShellExecute = false }); }
        catch (Exception error) { Log("watchdog: restart failed: " + error.Message); return; }
        Environment.Exit(0);
    }
    void ResetKeyboard() {
        OnLeftAlt(false);altMenuGuard.Dispose();altMenuGuard.Start();
        Poke();
    }
    void OnLeftAlt(bool down)
    {
        if(down&&(altMenuGuard.Suppressed||GameWindows.ForegroundGame!=IntPtr.Zero))return;
        if (down && !held) {
            held = true; heldSince = DateTime.UtcNow; altUpSince = DateTime.MaxValue;
            if(AppearanceFile.StringValue("indicatorStyle","classic")=="zebar"){BarIndicator(true);stuckWatch.Start();return;}
            ConfigureStyle(); PinToOriginMonitor(); Reveal();
            Poke(); stuckWatch.Start();
        }
        else if (!down && held) { held = false; BarIndicator(false);stuckWatch.Stop(); Conceal(); }
    }
    void BarIndicator(bool active){try{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"bar-indicator-state.txt"),active?"1":"0");}catch{}}
    // Safety net: if the release is missed anyway, hide once Windows has reported left Alt up for
    // 3 s. Windows' key state also flips on GlazeWM's injected Alt-up, so it can't be trusted sooner.
    DateTime heldSince, altUpSince = DateTime.MaxValue;
    System.Windows.Threading.DispatcherTimer stuckWatch;
    void WatchForStuckAlt()
    {
        if (!held) { stuckWatch.Stop(); return; }
        bool up = (Native.GetAsyncKeyState((int)altMenuGuard.TriggerKey) & 0x8000) == 0;
        if (!up) { altUpSince = DateTime.MaxValue; return; }
        if (altUpSince == DateTime.MaxValue) altUpSince = DateTime.UtcNow;
        if (DateTime.UtcNow - altUpSince < TimeSpan.FromSeconds(3)) return;
        Log("stuck Alt recovered after " + (int)(DateTime.UtcNow - heldSince).TotalSeconds + "s on workspace " + currentName + ": " + altMenuGuard.RecentEvents());
        altUpSince = DateTime.MaxValue; altMenuGuard.ForgetAlt(); OnLeftAlt(false);
    }
    // GlazeWM pushes focus/workspace events, so the focused workspace stays current without polling.
    // If subscribing fails, the read loop falls back to its old 500 ms refresh.
    volatile bool subscribed;
    void Poke() { try { if (refresh.CurrentCount == 0) refresh.Release(); } catch (SemaphoreFullException) { } }
    readonly SpinGuard watchGuard = new SpinGuard(), readGuard = new SpinGuard();
    // Small, capped log so a misbehaving GlazeWM connection leaves evidence instead of burning a core silently.
    static readonly object logGate = new object();
    static void Log(string message) {
        try { lock (logGate) {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "indicator.log");
            if (File.Exists(path) && new FileInfo(path).Length > 65536) File.Delete(path);
            File.AppendAllText(path, DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
        } } catch { }
    }
    async Task WatchWorkspaceEvents()
    {
        var buffer = new byte[16384];
        while (!stop.IsCancellationRequested) {
            try {
                using (var socket = new ClientWebSocket()) {
                    socket.Options.Proxy = null;
                    eventStep = "connect";
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token)) {
                        timeout.CancelAfter(2000);
                        await socket.ConnectAsync(new Uri("ws://127.0.0.1:6123"), timeout.Token);
                    }
                    eventStep = "subscribe";
                    var request = Encoding.UTF8.GetBytes("sub -e focus_changed workspace_activated");
                    await socket.SendAsync(new ArraySegment<byte>(request), WebSocketMessageType.Text, true, stop.Token);
                    while (socket.State == WebSocketState.Open) {
                        WebSocketReceiveResult part;
                        eventStep = "receive";
                        do {
                            part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stop.Token);
                            if (part.MessageType == WebSocketMessageType.Close) throw new IOException("GlazeWM disconnected");
                        } while (!part.EndOfMessage);
                        eventStep = "event";
                        subscribed = true;
                        if (watchGuard.Spinning()) throw new IOException("event stream spinning; last message: " + Encoding.UTF8.GetString(buffer, 0, Math.Min(part.Count, 160)));
                        Poke();
                    }
                }
            } catch (Exception error) {
                if (stop.IsCancellationRequested) return;
                Log("events: " + error.Message);
            }
            subscribed = false; eventStep = "retry";
            try { await Task.Delay(2000, stop.Token); } catch (OperationCanceledException) { return; }
        }
    }

    static Dictionary<string, object> Dict(object value) { return value as Dictionary<string, object>; }
    static string FocusedName(string json)
    {
        var root = Dict(new JavaScriptSerializer().DeserializeObject(json));
        if (root == null || !root.ContainsKey("data")) return null;
        var data = Dict(root["data"]);
        if (data == null || !data.ContainsKey("workspaces")) return null;
        foreach (var value in (IEnumerable)data["workspaces"]) {
            var workspace = Dict(value);
            if (workspace != null && workspace.ContainsKey("hasFocus") && (bool)workspace["hasFocus"])
                return Convert.ToString(workspace["name"]);
        }
        return null;
    }
    static async Task<string> Query(ClientWebSocket socket, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes("query workspaces");
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
        using (var stream = new MemoryStream()) {
            var buffer = new byte[8192];
            WebSocketReceiveResult part;
            do {
                part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (part.MessageType == WebSocketMessageType.Close) throw new IOException("GlazeWM disconnected");
                stream.Write(buffer, 0, part.Count);
                if (stream.Length > 4194304) throw new IOException("Unexpected response size");
            } while (!part.EndOfMessage);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
    async Task ReadWorkspaceLoop()
    {
        while (!stop.IsCancellationRequested) {
            try {
                using (var socket = new ClientWebSocket()) {
                    socket.Options.Proxy = null;
                    ReadStep("connect");
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token)) {
                        timeout.CancelAfter(2000);
                        await socket.ConnectAsync(new Uri("ws://127.0.0.1:6123"), timeout.Token);
                    }
                    while (socket.State == WebSocketState.Open && !stop.IsCancellationRequested) {
                        string json;
                        ReadStep("query");
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token)) {
                            timeout.CancelAfter(1500);
                            json = await Query(socket, timeout.Token);
                        }
                        ReadStep("parse");
                        string name = FocusedName(json);
                        ReadStep("dispatch");
                        if (name != null) await Dispatcher.InvokeAsync(delegate {
                            currentName = name; connected = true; lastGood = DateTime.UtcNow;
                            if (held && AppearanceFile.StringValue("indicatorStyle","classic")!="zebar") { Reveal(); SelectWorkspace(name, false); }
                        });
                        // Workspace events refresh immediately; held uses a 500 ms safety query. Idle waits up to 30 s,
                        // but the watchdog pokes every 5 s; 500 ms if the event subscription is unavailable.
                        ReadStep("wait");
                        await refresh.WaitAsync(held ? 500 : subscribed ? 30000 : 500, stop.Token);
                        if (readGuard.Spinning()) throw new IOException("query loop spinning; focused=" + (name ?? "none") + "; reply: " + json.Substring(0, Math.Min(json.Length, 160)));
                    }
                }
            } catch (Exception error) {
                if (stop.IsCancellationRequested) return;
                Log("query: " + error.Message);
            }
            if (stop.IsCancellationRequested) return;
            ReadStep("retry");
            await Dispatcher.InvokeAsync(delegate { connected = false; Conceal(); });
            try { await Task.Delay(2000, stop.Token); } catch (OperationCanceledException) { return; }
        }
    }

    void RenderPreview(string path,string style="classic")
    {
        ApplyLabels();ConfigureStyle(style); Paint(IndicatorPalette.For(FocusState.Read(), WallpaperPalette.Load())); panel.Opacity = 1; panelScale.ScaleX = 1; panelScale.ScaleY = 1;
        SelectWorkspace("3", true);
        var visual = (FrameworkElement)Content;
        visual.Measure(new Size(Width, Height)); visual.Arrange(new Rect(0, 0, Width, Height)); visual.UpdateLayout();
        // End animations before rendering a deterministic design preview.
        markerMove.BeginAnimation(TranslateTransform.XProperty, null); markerMove.X = 2 * CardStep;
        var bitmap = new RenderTargetBitmap((int)Width * 2, (int)Height * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) encoder.Save(file);
    }

    [STAThread]
    static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--check-styles"){try{IndicatorVariantChecks.Run();File.WriteAllText(args[1],"PASS: Gallery neighbor-only visibility/wraparound, centered geometry, rapid switch settling, Ribbon digit/rail cleanup, live labels and clipped layouts.");}catch(Exception e){File.WriteAllText(args[1],"FAIL: "+e);Environment.ExitCode=1;}return;}
        if(args.Length==3&&args[0]=="--render-style"){new NordWorkspaceIndicator().RenderPreview(args[2],args[1]);return;}
        if(args.Length==2 && args[0]=="--check-keys"){
            try{AltMenuGuard.CheckEdges();File.WriteAllText(args[1],"PASS: held Alt survives injected release/repeat; physical/raw release closes it; custom Right Ctrl and rebinding/disabled edges pass.");}
            catch(Exception e){File.WriteAllText(args[1],"FAIL: "+e);Environment.ExitCode=1;}return;
        }
        if (args.Length == 2 && args[0] == "--render") {
            new NordWorkspaceIndicator().RenderPreview(args[1]); return;
        }
        if (args.Length == 2 && args[0] == "--check") {
            try {
                if (FocusedName("{\"data\":{\"workspaces\":[{\"name\":\"0\",\"hasFocus\":true},{\"name\":\"3\",\"hasFocus\":false}]}}") != "0") throw new Exception("Workspace 0 parsing failed");
                using (var socket = new ClientWebSocket()) using (var timeout = new CancellationTokenSource(3000)) {
                    socket.Options.Proxy = null;
                    socket.ConnectAsync(new Uri("ws://127.0.0.1:6123"), timeout.Token).GetAwaiter().GetResult();
                    string name = FocusedName(Query(socket, timeout.Token).GetAwaiter().GetResult());
                    if (name == null || Array.IndexOf(Names, name) < 0) throw new Exception("No recognized focused workspace");
                    File.WriteAllText(args[1], "PASS: workspace 0 parsing; live GlazeWM WebSocket; focused workspace " + name);
                }
            } catch (Exception error) { File.WriteAllText(args[1], "FAIL: " + error); Environment.ExitCode = 1; }
            return;
        }
        // A watchdog restart starts the new copy before the old one exits, so it waits for the mutex.
        bool restart = args.Length == 1 && args[0] == "--restart";
        using (var mutex = new Mutex(false, "Local\\LiamNordWorkspaceIndicator")) {
            bool owned;
            try { owned = mutex.WaitOne(restart ? 10000 : 0); } catch (AbandonedMutexException) { owned = true; }
            if (!owned) return;
            if (restart) Log("watchdog: restarted");
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var window = new NordWorkspaceIndicator(); app.MainWindow = window;
            window.Start(); app.Run();
        }
    }
}

// Prevent Alt+digit and Alt+bracket workspace shortcuts from becoming a bare Alt menu action
// in the destination app. All physical keys otherwise pass through unchanged.
sealed class AltMenuGuard : IDisposable
{
    delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    readonly HookProc callback;
    IntPtr hook;
    bool leftAlt, workspaceChord, altDown;
    internal Func<bool> GameFocused=()=>GameWindows.ForegroundGame!=IntPtr.Zero;
    internal bool Suppressed {get;private set;}
    const uint Marker = 0x4E4F5244;
    public uint TriggerKey {get;private set;}
    public AltMenuGuard() { callback = Handle;TriggerKey=0xA4; }
    public void ConfigureKey(uint key){if(TriggerKey==key)return;bool wasDown=altDown;ForgetAlt();TriggerKey=key;if(wasDown)Raise(false);}
    // Raised on physical left-Alt press (not auto-repeat) and release.
    public Action<bool> LeftAltChanged;
    public void Start() {
        leftAlt = workspaceChord = altDown = false;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
    }
    public void Dispose() { if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
    IntPtr Handle(int code, IntPtr message, IntPtr data) {
        if(code>=0){
            var key=(KeyData)Marshal.PtrToStructure(data,typeof(KeyData));bool suppressed=Suppressed||GameFocused();
            ObserveAlt(key.vk,key.flags,key.extra.ToUInt64());
            // Preserve this pass-through gesture even after Alt+number changes foreground away from the game.
            if(suppressed||Suppressed){leftAlt=workspaceChord=false;return CallNextHookEx(hook,code,message,data);}
        }
        if (code >= 0) {
            var key = (KeyData)Marshal.PtrToStructure(data, typeof(KeyData));
            bool up = (key.flags & 0x80) != 0;
            // Physical edges only: GlazeWM injects Alt-up while the key is still held.
            // Raised before the menu guard below, which may swallow the Alt-up.
            ObserveAlt(key.vk,key.flags,key.extra.ToUInt64());
            if (key.extra.ToUInt64() != Marker && (key.flags & 0x10) == 0) {
                if (key.vk == 0xA4) {
                    if (!up) { if (!leftAlt) workspaceChord = false; leftAlt = true; }
                    else {
                        bool mask = leftAlt && workspaceChord;
                        leftAlt = false; workspaceChord = false;
                        if (mask) {
                            // An unassigned key cancels menu activation before the balanced
                            // Alt-up. Never swallow Alt-up if insertion fails (e.g. UIPI).
                            var inputs = new[] { Input(0xE8, 0), Input(0xE8, 2), Input(0xA4, 2) };
                            if (SendInput(3, inputs, Marshal.SizeOf(typeof(KeyboardInput))) == 3)
                                return new IntPtr(1);
                        }
                    }
                } else if (leftAlt && !up && IsWorkspaceKey(key.vk)
                    && (Native.GetAsyncKeyState(0x11) & 0x8000) == 0
                    && (Native.GetAsyncKeyState(0xA5) & 0x8000) == 0) workspaceChord = true;
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    // Raw keyboard input is delivered independently of WH_KEYBOARD_LL hooks. A hook in
    // another application may swallow Alt-up; the raw break still ends this gesture.
    public void RegisterRawInput(IntPtr window) {
        var devices = new[] { new RawDevice { page=1, usage=6, flags=0x100, target=window } };
        if(!RegisterRawInputDevices(devices,1,(uint)Marshal.SizeOf(typeof(RawDevice))))
            throw new System.ComponentModel.Win32Exception();
    }
    public void ReadRawInput(IntPtr input) {
        uint headerSize=(uint)Marshal.SizeOf(typeof(RawHeader)), size=0;
        if(GetRawInputData(input,0x10000003,IntPtr.Zero,ref size,headerSize)!=0 || size<headerSize+16 || size>4096) return;
        IntPtr buffer=Marshal.AllocHGlobal((int)size);
        try {
            if(GetRawInputData(input,0x10000003,buffer,ref size,headerSize)==uint.MaxValue) return;
            var header=(RawHeader)Marshal.PtrToStructure(buffer,typeof(RawHeader));
            // Injected keys (GlazeWM's focus-stealing Alt presses, SendInput) arrive with no device.
            // Counting them as physical could leave the gesture "held" after a real release.
            if(header.type!=1 || header.device==IntPtr.Zero) { Trace("raw injected "+(header.type==1?"key":"other")); return; }
            var key=(RawKeyboard)Marshal.PtrToStructure(IntPtr.Add(buffer,(int)headerSize),typeof(RawKeyboard));
            ObserveRawKey(key.vk,key.makeCode,key.flags);
        } finally { Marshal.FreeHGlobal(buffer); }
    }
    internal void ObserveRawAlt(ushort scan,ushort flags){ObserveRawKey(0,scan,flags);}
    internal void ObserveRawKey(ushort vk,ushort scan,ushort flags){
        uint resolved=vk;
        if(scan==0x38)resolved=(flags&2)!=0?0xA5u:0xA4u;
        else if(scan==0x1D)resolved=(flags&2)!=0?0xA3u:0xA2u;
        else if(scan==0x2A)resolved=0xA0;
        else if(scan==0x36)resolved=0xA1;
        if((flags&4)==0)ObserveAlt(resolved,(flags&1)!=0?0x80u:0u,0);
        if(resolved==0xA4&&(flags&1)!=0){leftAlt=false;workspaceChord=false;}
    }
    [StructLayout(LayoutKind.Sequential)] struct RawDevice { public ushort page,usage; public uint flags; public IntPtr target; }
    [StructLayout(LayoutKind.Sequential)] struct RawHeader { public uint type,size; public IntPtr device,param; }
    [StructLayout(LayoutKind.Sequential)] struct RawKeyboard { public ushort makeCode,flags,reserved,vk; public uint message,extra; }
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)] static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
    // Last few Alt events, written to the log if a gesture ever gets stuck.
    readonly System.Collections.Generic.Queue<string> recent=new System.Collections.Generic.Queue<string>();
    void Trace(string what){lock(recent){recent.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff")+" "+what);while(recent.Count>12)recent.Dequeue();}}
    public string RecentEvents(){lock(recent)return string.Join(" | ",recent.ToArray());}
    /// <summary>Forget a held Alt whose release was never seen.</summary>
    public void ForgetAlt(){altDown=false;leftAlt=false;workspaceChord=false;Suppressed=false;}
    internal void ObserveAlt(uint vk,uint flags,ulong extra){
        bool up=(flags&0x80)!=0;
        if(vk==TriggerKey) Trace((up?"up":"down")+" flags="+flags.ToString("x")+(extra==Marker?" ours":""));
        if(TriggerKey!=0 && vk==TriggerKey && extra!=Marker && (flags&0x10)==0 && up==altDown){
            altDown=!up;if(altDown){Suppressed=GameFocused();if(!Suppressed)Raise(true);}else{bool wasSuppressed=Suppressed;Suppressed=false;if(!wasSuppressed)Raise(false);}
        }
    }
    internal static void CheckEdges(){
        if(!IsWorkspaceKey(0xDB)||!IsWorkspaceKey(0xDD)||!IsWorkspaceKey(0x30)||IsWorkspaceKey(0x41))throw new Exception("Workspace menu guard must recognize digits and both bracket keys");
        var guard=new AltMenuGuard{GameFocused=()=>false};var edges=new System.Collections.Generic.List<bool>();guard.LeftAltChanged=edges.Add;
        guard.ObserveAlt(0xA4,0,0); // Physical press.
        guard.ObserveAlt(0xA4,0x90,0); // Workspace switch injects Alt-up.
        guard.ObserveAlt(0xA4,0,0); // Auto-repeat must not create a new gesture.
        guard.ObserveAlt(0xA5,0x80,0); // Right Alt is unrelated.
        if(edges.Count!=1 || !edges[0])throw new Exception("Held Alt was released by an injected event");
        guard.ObserveAlt(0xA4,0x80,0);
        if(edges.Count!=2 || edges[1])throw new Exception("Physical Alt release was lost");
        guard.ObserveAlt(0xA4,0,0); // Hook sees press, but another hook consumes release.
        guard.ObserveAlt(0xA4,0x90,0); // Glaze's synthetic release is still ignored.
        guard.ObserveRawAlt(0x38,3); // Right Alt break must not hide the indicator.
        if(edges.Count!=3 || !edges[2])throw new Exception("Right Alt ended left Alt gesture");
        guard.ObserveRawAlt(0x38,1); // Physical raw release recovers the missed hook event.
        guard.ObserveAlt(0xA4,0x80,0); // Late duplicate must not produce another edge.
        if(edges.Count!=4 || edges[3])throw new Exception("Raw Alt release did not recover swallowed hook event");
        guard.ObserveRawAlt(0x38,0);
        guard.ObserveAlt(0xA4,0,0);
        guard.ObserveRawAlt(0x38,1);
        if(edges.Count!=6 || !edges[4] || edges[5])throw new Exception("Raw/hook duplicate gesture was unbalanced");
        bool game=true;var gameGuard=new AltMenuGuard{GameFocused=()=>game};var gameEdges=new System.Collections.Generic.List<bool>();gameGuard.LeftAltChanged=gameEdges.Add;
        gameGuard.ObserveAlt(0xA4,0,0);game=false; // Alt+number switches to a normal workspace while Alt stays down.
        gameGuard.ObserveAlt(0xA4,0x90,0);gameGuard.ObserveRawAlt(0x38,0);gameGuard.ObserveAlt(0xA4,0,0);
        if(!gameGuard.Suppressed||gameEdges.Count!=0)throw new Exception("A game-started Alt hold must stay hidden after switching workspaces");
        gameGuard.ObserveRawAlt(0x38,1);if(gameGuard.Suppressed||gameEdges.Count!=0)throw new Exception("Game suppression must end only on physical release without emitting an indicator edge");
        gameGuard.ObserveAlt(0xA4,0,0);gameGuard.ObserveAlt(0xA4,0x80,0);if(gameEdges.Count!=2||!gameEdges[0]||gameEdges[1])throw new Exception("The next desktop Alt gesture must work normally");
        guard.ConfigureKey(0xA3);edges.Clear();guard.ObserveAlt(0xA4,0,0);guard.ObserveAlt(0xA3,0,0);guard.ObserveAlt(0xA3,0x90,0);guard.ObserveRawKey(0x11,0x1D,3);
        if(edges.Count!=2||!edges[0]||edges[1])throw new Exception("Custom right-Ctrl physical/raw edges failed");
        guard.ObserveAlt(0xA3,0,0);guard.ConfigureKey(0);guard.ObserveAlt(0xA3,0x80,0);if(edges.Count!=4||edges[3])throw new Exception("Rebinding a held key must close the old gesture");
    }
    void Raise(bool down) { var handler = LeftAltChanged; if (handler != null) try { handler(down); } catch { } }
    static bool IsWorkspaceKey(uint vk){return vk>=0x30&&vk<=0x39||vk==0xDB||vk==0xDD;}
    static KeyboardInput Input(ushort vk, uint flags) {
        return new KeyboardInput { type = 1, vk = vk, flags = flags, extra = new UIntPtr(Marker) };
    }
    [StructLayout(LayoutKind.Sequential)] struct KeyData { public uint vk, scan, flags, time; public UIntPtr extra; }
    // INPUT is 40 bytes on this application's x64 target; KEYBDINPUT starts at 8.
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct KeyboardInput {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public ushort vk;
        [FieldOffset(12)] public uint flags;
        [FieldOffset(24)] public UIntPtr extra;
    }
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int type, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, KeyboardInput[] inputs, int size);
}

/// <summary>The switcher's colors: lock-in's, else the wallpaper palette's (same roles as the bar), else Nord.</summary>
sealed class IndicatorPalette
{
    public Brush PanelBorder, PanelBackground, Marker, MarkerBorder, Tile, TileBorder, Number, NumberOn, Label, LabelOn;
    public IndicatorPalette Copy(){var result=new IndicatorPalette();foreach(var field in typeof(IndicatorPalette).GetFields())field.SetValue(result,((Brush)field.GetValue(this)).CloneCurrentValue());return result;}

    public static IndicatorPalette For(FocusState focus, Dictionary<string, string> wallpaper)
    {
        var p = new IndicatorPalette {
            PanelBorder = S("#6B86A3"), PanelBackground = new LinearGradientBrush(Color.FromArgb(246, 59, 70, 86), Color.FromArgb(248, 35, 42, 54), 90),
            Marker = S("#81A1C1"), MarkerBorder = S("#E5F4F8"), Tile = S("#102E3440"), TileBorder = S("#B46B86A3"),
            Number = S("#D8DEE9"), NumberOn = S("#24313D"), Label = S("#9BAEC2"), LabelOn = S("#354B5A")
        };
        if (focus.ThemeActive) {
            var s = focus.settings; var edge = (Color)ColorConverter.ConvertFromString(s.edge);
            p.PanelBorder = p.MarkerBorder = p.TileBorder = S(s.edge); p.PanelBackground = S(s.background); p.Marker = S(s.accent);
            p.Tile = new SolidColorBrush(Color.FromArgb(95, (byte)Math.Min(255, edge.R + 60), (byte)Math.Min(255, edge.G + 60), (byte)Math.Min(255, edge.B + 60)));
        } else if (wallpaper != null) {
            Func<string, Color> c = name => WallpaperPalette.Get(wallpaper, name, "#2E3440");
            Color accent = c("--nord-8"), shade = c("--shade"), base0 = c("--nord-0"), base1 = c("--nord-1");
            p.PanelBorder = new SolidColorBrush(c("--edge"));
            p.PanelBackground = new LinearGradientBrush(Color.FromArgb(246, base1.R, base1.G, base1.B), Color.FromArgb(248, base0.R, base0.G, base0.B), 90);
            p.Marker = new SolidColorBrush(accent); p.MarkerBorder = new SolidColorBrush(Mix(accent, Colors.White, .65));
            var edge = c("--edge");
            p.Tile = new SolidColorBrush(Color.FromArgb(0x10, base0.R, base0.G, base0.B)); p.TileBorder = new SolidColorBrush(Color.FromArgb(0xB4, edge.R, edge.G, edge.B));
            p.Number = new SolidColorBrush(c("--nord-4")); p.NumberOn = new SolidColorBrush(shade);
            p.Label = new SolidColorBrush(c("--muted")); p.LabelOn = new SolidColorBrush(Mix(shade, accent, .35));
        }
        return p;
    }
    static SolidColorBrush S(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    static Color Mix(Color a, Color b, double t) { return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t)); }
}

static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left, top, right, bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
}

// A short tapered light travels around the rounded outline once, after expansion.
// Only this small vector surface redraws during its 650ms animation.
class BorderShimmer : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        "Progress", typeof(double), typeof(BorderShimmer),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress { get { return (double)GetValue(ProgressProperty); } set { SetValue(ProgressProperty, value); } }
    public double Radius; // follows the panel's corner radius
    public BorderShimmer() { IsHitTestVisible = false; Visibility = Visibility.Hidden; }
    public void Play() {
        Visibility = Visibility.Visible;
        var travel = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(470)) { BeginTime = TimeSpan.FromMilliseconds(65) };
        travel.Completed += delegate { Visibility = Visibility.Hidden; };
        BeginAnimation(ProgressProperty, travel);
    }
    public void Stop() { BeginAnimation(ProgressProperty, null); Visibility = Visibility.Hidden; }
    protected override void OnRender(DrawingContext dc) {
        base.OnRender(dc);
        if (ActualWidth < 30 || ActualHeight < 30) return;
        var rect = new Rect(10.5, 10.5, ActualWidth - 21, ActualHeight - 21);
        double radius = Math.Min(Radius, Math.Min(rect.Width, rect.Height) / 2);
        double middle = rect.Left + rect.Width / 2;
        var figure = new PathFigure { StartPoint = new Point(middle,rect.Top), IsClosed = false };
        figure.Segments.Add(new LineSegment(new Point(rect.Right-radius,rect.Top),true));
        figure.Segments.Add(new ArcSegment(new Point(rect.Right,rect.Top+radius),new Size(radius,radius),0,false,SweepDirection.Clockwise,true));
        figure.Segments.Add(new LineSegment(new Point(rect.Right,rect.Bottom-radius),true));
        figure.Segments.Add(new ArcSegment(new Point(rect.Right-radius,rect.Bottom),new Size(radius,radius),0,false,SweepDirection.Clockwise,true));
        figure.Segments.Add(new LineSegment(new Point(middle,rect.Bottom),true));
        var outline = new PathGeometry(new[] { figure });
        double length = rect.Width - 2*radius + rect.Height - 2*radius + Math.PI*radius;
        double fadeStart = (rect.Width/2-radius + Math.PI*radius/2 + rect.Height-2*radius) / length;
        double fade = Math.Min(1, Math.Min(Progress / .08, (1 - Progress) / (1-fadeStart)));
        for (int i = 0; i < 40; i++) {
            double head = Progress - .085 + i * .085 / 40;
            double next = head + .085 / 40;
            Point a, b, tangent;
            if (next <= 0) continue;
            outline.GetPointAtFractionLength(Math.Max(0,head), out a, out tangent);
            outline.GetPointAtFractionLength(Math.Min(1,next), out b, out tangent);
            double strength = Math.Pow((i + 1) / 40.0, 1.8) * Math.Max(0, fade);
            // Tangent has both x/y components on curves, peaking halfway around a corner.
            double corner = 2 * Math.Abs(tangent.X * tangent.Y) / Math.Max(.0001, (tangent.X * tangent.X + tangent.Y * tangent.Y));
            var glow = new Pen(new SolidColorBrush(Color.FromArgb((byte)(90 * strength), 136, 192, 208)), 7 + 5 * corner);
            var core = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * strength), 225, 250, 255)), 2.3 + 2.4 * corner);
            glow.StartLineCap = glow.EndLineCap = PenLineCap.Round;
            core.StartLineCap = core.EndLineCap = PenLineCap.Round;
            dc.DrawLine(glow, a, b); dc.DrawLine(core, a, b);
        }
    }
}

// Detects a loop that keeps completing without waiting (over 200 passes within one second),
// so the caller can log it and reconnect instead of spinning a CPU core.
sealed class SpinGuard {
    long windowStart; int passes;
    public bool Spinning() {
        long now = Environment.TickCount;
        if (now - windowStart > 1000) { windowStart = now; passes = 0; }
        if (++passes <= 200) return false;
        passes = 0; windowStart = now; return true;
    }
}
