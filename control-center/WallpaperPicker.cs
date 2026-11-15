using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

/// <summary>
/// The wallpaper strip: numbered thumbnails 1..N that slide so the selection sits in the middle. It
/// opens on the current wallpaper and glides to the next one, follows every further press, and once
/// the presses stop it applies the choice (with <see cref="DesktopWallpaper"/>'s crossfade) and fades.
/// Never takes focus; uses the control center's theme, so it follows lock-in and wallpaper colors.
/// </summary>
sealed class WallpaperPicker : Window {
 const double CardWidth=200,CardHeight=112,Gap=14,Visible=5,Dim=.86;
 static readonly TimeSpan Settle=TimeSpan.FromMilliseconds(900);
 readonly Dictionary<string,BitmapSource> thumbnails=new Dictionary<string,BitmapSource>();
 readonly Queue<string> thumbnailOrder=new Queue<string>();long thumbnailBytes;
 readonly System.Threading.SemaphoreSlim decodeOne=new System.Threading.SemaphoreSlim(1);
 sealed class ThumbnailTicket {public volatile bool Cancelled;}
 internal static void ReleaseThumbnail(Image image){var ticket=image.Tag as ThumbnailTicket;if(ticket!=null)ticket.Cancelled=true;image.Tag=null;image.Source=null;}
 void ReleasePreviews(){
  if(variants!=null)variants.ReleaseImages();
  foreach(var card in cards){var face=card.Child as Grid;if(face!=null)foreach(var image in face.Children.OfType<Image>())ReleaseThumbnail(image);}
 }

 readonly StackPanel strip=new StackPanel{Orientation=Orientation.Horizontal};
 readonly StackPanel body=new StackPanel{Width=ViewWidth};
 readonly TranslateTransform scroll=new TranslateTransform();
 readonly Grid viewportHost=new Grid();readonly Canvas classicViewport;readonly Viewbox fit=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly};WallpaperVariants variants;string style="classic";int fadeRevision;
 internal Action<string,Image,int> LoadThumbnail;
 internal Action<string> PrepareWallpaper=path=>{DesktopWallpaper.Prepare(path);};
 internal Func<string,Task> ApplyWallpaper=DesktopWallpaper.Apply;
 internal Func<string,Task> CopyWallpaper=WallpaperImageClipboard.Copy;bool copying;
 readonly TextBlock name=new TextBlock{FontSize=15,FontWeight=FontWeights.Medium,TextTrimming=TextTrimming.CharacterEllipsis};
 readonly TextBlock count=new TextBlock{FontSize=13,VerticalAlignment=VerticalAlignment.Center};
 readonly DispatcherTimer settle=new DispatcherTimer{Interval=Settle};
 readonly List<Border> cards=new List<Border>();
 List<string> images=new List<string>();
 int index=-1;
 List<WallpaperCategory> categories=new List<WallpaperCategory>();int categoryIndex;WallpaperBrowserState browserState;string originalWallpaper;bool resumePosition;
 readonly StackPanel categoryStrip=new StackPanel{Orientation=Orientation.Horizontal};readonly ScrollViewer categoryScroll=new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,0,0,12),Height=34};
 readonly TextBlock navigationHint=new TextBlock{FontSize=10,Margin=new Thickness(4,10,0,0)};
 readonly DispatcherTimer saveBrowser=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};
 internal string BrowserStatePath=Path.Combine(AppCatalog.Folder,"wallpaper-browser.json");
 internal Func<string> LibraryFolder=()=>WallpaperSettings.Load().ImageFolder();
 static readonly ControlTemplate categoryTemplate=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border x:Name='Face' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' CornerRadius='8'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Face' Property='Background' Value='{DynamicResource ThemeHover}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
 internal bool IsBrowsing {get{return open;}}
 void Interactive(bool enabled){var hwnd=new WindowInteropHelper(this).Handle;if(hwnd==IntPtr.Zero)return;long flags=GetWindowLongPtr(hwnd,-20).ToInt64();SetWindowLongPtr(hwnd,-20,new IntPtr(enabled?flags&~0x20L:flags|0x20L));}
 internal string CategoryName {get{return categories.Count==0?"All":categories[categoryIndex].Name;}}
 internal string SelectedWallpaper {get{return index>=0&&index<images.Count?images[index]:null;}}
 void Remember(){if(browserState==null||categories.Count==0)return;browserState.category=categories[categoryIndex].Key;if(SelectedWallpaper!=null)browserState.positions[browserState.category]=SelectedWallpaper;saveBrowser.Stop();saveBrowser.Start();}
 void SaveBrowser(){saveBrowser.Stop();if(browserState!=null)try{browserStore.Save(BrowserStatePath,browserState);}catch(Exception ex){WallpaperSwitcher.Log("wallpaper browser state: "+ex.Message);}}
 void ConfigureLibrary(List<string> list){var root=Path.GetFullPath(LibraryFolder());var scanned=library.Scan(root);var available=new HashSet<string>(list,StringComparer.OrdinalIgnoreCase);foreach(var group in scanned)group.Images=group.Images.Where(available.Contains).ToList();scanned.RemoveAll(g=>g.Key!="all"&&g.Images.Count==0);scanned[0].Images=new List<string>(list);categories=scanned;if(browserState==null||!String.Equals(browserState.root,root,StringComparison.OrdinalIgnoreCase))browserState=browserStore.Load(BrowserStatePath,root);categoryIndex=categories.FindIndex(g=>g.Key==browserState.category);if(categoryIndex<0)categoryIndex=0;RenderCategories();}
 void RenderCategories(){categoryStrip.Children.Clear();for(int i=0;i<categories.Count;i++){int choice=i;var group=categories[i];var button=new Button{Template=categoryTemplate,Content=group.Name+"  "+group.Images.Count,FontSize=11,Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,0,5,0),BorderThickness=new Thickness(0),Background=B(i==categoryIndex?"Selected":"Raised"),Foreground=B(i==categoryIndex?"Text":"Muted"),Focusable=false,ToolTip="Browse "+group.Name};button.Click+=delegate{ChooseCategory(choice);};categoryStrip.Children.Add(button);}if(categoryIndex<categoryStrip.Children.Count){var selected=(FrameworkElement)categoryStrip.Children[categoryIndex];Dispatcher.BeginInvoke(new Action(()=>{if(selected.IsLoaded)selected.BringIntoView();}),DispatcherPriority.Loaded);}}
 public void ChangeCategory(int delta){if(!open||categories.Count<2)return;ChooseCategory((categoryIndex+delta%categories.Count+categories.Count)%categories.Count);}
 internal void ChooseCategory(int choice){if(choice<0||choice>=categories.Count||choice==categoryIndex)return;Remember();categoryIndex=choice;string previous=originalWallpaper;var group=categories[choice];string saved=null;browserState.positions.TryGetValue(group.Key,out saved);Fill(group.Images,saved??previous);originalWallpaper=previous;opened=images.FindIndex(p=>SamePath(p,previous));hasStepped=true;resumePosition=true;RenderCategories();Select(true);Remember();settle.Stop();if(!holding)settle.Start();Center();}

 static double ViewWidth {get{return Visible*CardWidth+(Visible-1)*Gap;}}

 readonly IWallpaperLibrary library;readonly WallpaperBrowserStateStore browserStore=new WallpaperBrowserStateStore();volatile bool pickerDisposed;
 public WallpaperPicker():this(new WallpaperLibraryService()){}
 internal WallpaperPicker(IWallpaperLibrary library){this.library=library;LoadThumbnail=Thumbnail;
  Title="Nord Wallpapers";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
  Topmost=true;ShowInTaskbar=false;ShowActivated=false;Focusable=false;SizeToContent=SizeToContent.WidthAndHeight;Opacity=0;
  FontFamily=RiceFont.Current;Foreground=B("Text");ControlCenterTheme.Current.Install(Resources);
  count.Foreground=B("Muted");
  var viewport=classicViewport=new Canvas{Width=ViewWidth,Height=CardHeight+8,ClipToBounds=true};
  strip.RenderTransform=scroll;Canvas.SetTop(strip,4);viewport.Children.Add(strip);
  var header=new DockPanel{Margin=new Thickness(4,0,4,12),LastChildFill=true};
  DockPanel.SetDock(count,Dock.Right);header.Children.Add(count);header.Children.Add(name);
  categoryScroll.Content=categoryStrip;categoryScroll.PreviewMouseWheel+=(sender,e)=>{categoryScroll.ScrollToHorizontalOffset(categoryScroll.HorizontalOffset-e.Delta);e.Handled=true;};body.Children.Add(categoryScroll);viewportHost.Children.Add(viewport);body.Children.Add(header);body.Children.Add(viewportHost);navigationHint.Foreground=B("Muted");body.Children.Add(navigationHint);fit.Child=body;
  // Frosted glass under the theme tint (Panel: Surface at the bar's transparency, opaque in lock-in).
  frame=new Border{Margin=new Thickness(18),CornerRadius=new CornerRadius(20),BorderBrush=B("Edge"),BorderThickness=new Thickness(1.5),
   Child=new Border{Child=fit,Padding=new Thickness(20,16,20,18),CornerRadius=new CornerRadius(19),Background=B("Panel")},
   Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=24,ShadowDepth=3,Opacity=.4,Color=Colors.Black}};
  Content=frame;
  SourceInitialized+=delegate{var h=new WindowInteropHelper(this).Handle;SetWindowLongPtr(h,-20,new IntPtr(GetWindowLongPtr(h,-20).ToInt64()|0x08000000L|0x80L|0x20L));};
  settle.Tick+=delegate{settle.Stop();Apply();};saveBrowser.Tick+=delegate{SaveBrowser();};
  // A DPI change resizes the window in pixels without changing its DIP size, so recentre after both.
  SizeChanged+=delegate{Center();};
  DpiChanged+=delegate{Dispatcher.BeginInvoke(new Action(Center),DispatcherPriority.Background);};
  Action frostRendered=()=>{if(open)Frost();};FrostMaterial.Rendered+=frostRendered;
  Closed+=delegate{pickerDisposed=true;open=false;++fadeRevision;SaveBrowser();settle.Stop();saveBrowser.Stop();FrostMaterial.Rendered-=frostRendered;ReleasePreviews();lock(thumbnails){thumbnails.Clear();thumbnailOrder.Clear();thumbnailBytes=0;}};
 }

 readonly Border frame;
 bool open,holding,hasStepped;int applying;int opened=-1; // showing; hold-to-browse in progress; the card the strip opened on

 /// <summary>
 /// Builds the strip and shows the (still transparent) window ahead of time, like the Alt indicator,
 /// so opening is only a fade: no layout, window creation or first render when the key goes down.
 /// </summary>
 public void Warm(List<string> list,string current){ConfigureLibrary(list);string saved=null;browserState.positions.TryGetValue(categories[categoryIndex].Key,out saved);Fill(categories[categoryIndex].Images,saved??current);Opacity=0;Show();}

 /// <summary>Hold mode: opens on the current wallpaper without moving; <see cref="Release"/> commits.</summary>
 public void Peek(List<string> list,string current){holding=true;settle.Stop();if(open){if(variants!=null)variants.SetHint(true);Raise();}else Open(list,current);}

 /// <summary>Moves the selection, opening on the current wallpaper first. Outside hold mode the choice applies once the presses stop.</summary>
 public void Step(List<string> list,string current,int delta){
  if(!open)Open(list,current);else Raise();
  if(images.Count==0)return;index=index<0||opened<0&&!hasStepped&&!resumePosition?(delta>0?0:images.Count-1):((index+delta)%images.Count+images.Count)%images.Count;hasStepped=true;
  Select(true);
  settle.Stop();if(!holding)settle.Start();
 }

 /// <summary>Hold key released: applies the selection if it moved, otherwise just closes.</summary>
 public void Release(){holding=false;if(!open)return;if(hasStepped&&SelectedWallpaper!=null&&!SamePath(SelectedWallpaper,originalWallpaper))Apply();else FadeOut();}
 internal async Task CopySelected(){
  var selected=SelectedWallpaper;if(pickerDisposed||!open||selected==null||copying)return;
  copying=true;navigationHint.Text="Copying image...";
  try{await CopyWallpaper(selected);if(!pickerDisposed&&open)navigationHint.Text="Image copied  \u00b7  C to copy highlighted image";}
  catch(Exception ex){WallpaperSwitcher.Log("copy selected wallpaper: "+ex.Message);if(!pickerDisposed&&open)navigationHint.Text="Could not copy image  \u00b7  Press C to retry";}
  finally{copying=false;}
 }
 public void Cancel(){holding=false;settle.Stop();if(open)FadeOut(90);}

 void Build(){
  strip.Children.Clear();cards.Clear();
  viewportHost.Children.Clear();
  if(style!="classic"){variants=new WallpaperVariants(style,images,LoadThumbnail);body.Width=viewportHost.Width=variants.Width;viewportHost.Height=variants.Height;viewportHost.Children.Add(variants);return;}
  variants=null;body.Width=viewportHost.Width=ViewWidth;viewportHost.Height=CardHeight+8;viewportHost.Children.Add(classicViewport);
  for(int i=0;i<images.Count;i++){
   var picture=new Image{Stretch=Stretch.UniformToFill};
   var face=new Grid{Clip=new RectangleGeometry(new Rect(0,0,CardWidth-6,CardHeight-6),9,9)};
   face.Children.Add(new Border{Background=B("Raised")});face.Children.Add(picture);
   face.Children.Add(new Border{CornerRadius=new CornerRadius(7),Background=B("Surface"),Padding=new Thickness(8,2,8,3),Margin=new Thickness(7),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,
    Child=new TextBlock{Text=(i+1).ToString(),FontSize=12,FontWeight=FontWeights.SemiBold}});
   var card=new Border{Width=CardWidth,Height=CardHeight,Margin=new Thickness(0,0,i<images.Count-1?Gap:0,0),CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(3),BorderBrush=B("SoftEdge"),
    Child=face,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(Dim,Dim),Opacity=.5};
   strip.Children.Add(card);cards.Add(card);
  }
 }

 /// <summary>Slides the selected card to the middle and lifts it; the rest shrink and dim.</summary>
 void Select(bool animate){
  if(index<0||index>=images.Count)return;int ms=animate&&SystemParameters.ClientAreaAnimation?(int)Motion.Ms(260):0;
  if(variants!=null){variants.Select(index,animate);variants.SetHint(holding);}else{
  Animate(scroll,TranslateTransform.XProperty,ViewWidth/2-(index*(CardWidth+Gap)+CardWidth/2),ms);
  for(int i=0;i<cards.Count;i++){
   var face=cards[i].Child as Grid;var preview=face==null?null:face.Children.OfType<Image>().FirstOrDefault();
   if(preview!=null){if(Math.Abs(i-index)<=3){if(preview.Source==null&&preview.Tag==null)LoadThumbnail(images[i],preview,400);}else ReleaseThumbnail(preview);}
   bool on=i==index;var scale=(ScaleTransform)cards[i].RenderTransform;
   Animate(scale,ScaleTransform.ScaleXProperty,on?1:Dim,ms);Animate(scale,ScaleTransform.ScaleYProperty,on?1:Dim,ms);
   Animate(cards[i],OpacityProperty,on?1:.5,ms);cards[i].BorderBrush=on?B("Accent"):B("SoftEdge"); // like the Alt indicator's tiles
  }
  }
  // The badge already shows the number; a header name only adds something for non-numbered files.
  long numbered;var file=Path.GetFileNameWithoutExtension(images[index]);
  name.Text=Int64.TryParse(file,out numbered)?"Wallpaper":file;count.Text=CategoryName+" \u00b7 "+(index+1)+" / "+images.Count;Remember();
  if(hasStepped&&!SamePath(images[index],originalWallpaper))PrepareWallpaper(images[index]); // decode ahead so applying it can fade in at once
 }

 /// <summary>Builds the strip on the current wallpaper, centres it on the monitor under the cursor and fades it in.</summary>
 void Open(List<string> list,string current){
  navigationHint.Text="\u2191 / \u2193 categories   \u00b7   \u2190 / \u2192 or [ / ] images   \u00b7   "+(holding?"Release to apply":"Applies when browsing stops")+"   \u00b7   C to copy image   \u00b7   Esc to cancel";
  ControlCenterTheme.Current.Apply(FocusMode.State);
  ConfigureLibrary(list);string saved=null;browserState.positions.TryGetValue(categories[categoryIndex].Key,out saved);resumePosition=saved!=null&&categories[categoryIndex].Images.Any(p=>SamePath(p,saved));Fill(categories[categoryIndex].Images,resumePosition?saved:current);originalWallpaper=current;opened=images.FindIndex(p=>SamePath(p,current));open=true;fadeRevision++;Roundness.Apply(this,BarAppearance.Settings.radius/100.0);
  POINT cursor;GetCursorPos(out cursor);
  var monitor=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.x,cursor.y)).WorkingArea;
  if(!IsVisible)Show();Interactive(true);
  // On another monitor, land there first so WPF adopts its DPI; the window then resizes, and Center follows.
  if(monitor!=area){area=monitor;SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),cursor.x,cursor.y,0,0,0x0001|0x0010);Dispatcher.BeginInvoke(new Action(Center),DispatcherPriority.Background);}
  Center();
  BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation?Motion.Ms(110):0)));
 }
 /// <summary>Rebuilds the cards only when the folder changed, and selects the current wallpaper.</summary>
 void Fill(List<string> list,string current,string requestedStyle=null){
  string next=requestedStyle??BarAppearance.Settings.wallpaperIndicatorStyle;if(!AppearanceSettings.WallpaperIndicatorStyles.Contains(next))next="classic";
  if(next!=style||!list.SequenceEqual(images,StringComparer.OrdinalIgnoreCase)){ReleasePreviews();style=next;images=new List<string>(list);Build();}
  name.FontSize=style=="showcase"?18:style=="filmstrip"?16:15;
  originalWallpaper=current;hasStepped=false;opened=images.FindIndex(p=>SamePath(p,current));index=opened>=0?opened:images.Count>0?0:-1;if(index>=0)Select(false);else{name.Text="No wallpapers";count.Text=CategoryName+" \u00b7 0 / 0";}
 }
 System.Drawing.Rectangle area;
 /// <summary>Keeps the window centred on its monitor's work area, from its real size in pixels.</summary>
 void Center(){
  if(!IsVisible)return;
  if(area.Width>0){var dpi=VisualTreeHelper.GetDpi(this);double width=Math.Max(160,area.Width/dpi.DpiScaleX-96),height=Math.Max(100,area.Height/dpi.DpiScaleY-96);if(Math.Abs(fit.MaxWidth-width)>.1||Math.Abs(fit.MaxHeight-height)>.1){fit.MaxWidth=width;fit.MaxHeight=height;Dispatcher.BeginInvoke(new Action(Center),DispatcherPriority.Background);return;}}
  var handle=new WindowInteropHelper(this).Handle;RECT r;if(!GetWindowRect(handle,out r))return;
  int x=area.Left+(area.Width-(r.right-r.left))/2,y=area.Top+(area.Height-(r.bottom-r.top))/2;
  if(r.left!=x||r.top!=y)SetWindowPos(handle,new IntPtr(-1),x,y,0,0,0x0001|0x0010);
  Dispatcher.BeginInvoke(new Action(Frost),DispatcherPriority.Loaded);
 }
 /// <summary>Shows the frost that lies under the panel's spot on screen (none when frost is off).</summary>
 void Frost(){
  if(!open||!IsVisible||frame.ActualWidth==0)return;
  var origin=frame.PointToScreen(new Point(0,0));var dpi=VisualTreeHelper.GetDpi(frame);
  frame.Background=ThemeTransition.Crossfade(frame.Background,FrostFile.Behind(origin.X,origin.Y,frame.ActualWidth*dpi.DpiScaleX,frame.ActualHeight*dpi.DpiScaleY));
 }

 async void Apply(){
  if(index<0||index>=images.Count)return;
  var chosen=images[index];opened=index;applying++;FadeOut();
  try{await ApplyWallpaper(chosen);}catch(Exception e){WallpaperSwitcher.Log("apply failed: "+e.Message);}finally{applying--;if(applying==0&&!open)DesktopWallpaper.DiscardUnused();}
 }
 void Raise(){fadeRevision++;BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation?Motion.Ms(100):0)));}
 /// <summary>Fades to transparent; the window stays up (click-through) so the next open is instant.</summary>
 void FadeOut(int ms=220){SaveBrowser();Interactive(false);open=false;settle.Stop();int version=++fadeRevision;var fade=new DoubleAnimation(0,TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation?Motion.Ms(ms):0));fade.Completed+=delegate{if(version==fadeRevision&&!open){if(variants!=null)variants.Settle();frame.Background=null;ReleasePreviews();if(applying==0)DesktopWallpaper.DiscardUnused();}};BeginAnimation(OpacityProperty,fade);}

 /// <summary>Thumbnails decode off the UI thread at card size and are kept for the session.</summary>
 void Thumbnail(string path,Image target,int width){
  var previous=target.Tag as ThumbnailTicket;if(previous!=null)previous.Cancelled=true;
  var ticket=new ThumbnailTicket();target.Tag=ticket;
  string key;try{key=path+"|"+File.GetLastWriteTimeUtc(path).Ticks+"|"+width;}catch{return;}
  BitmapSource cached;lock(thumbnails)if(thumbnails.TryGetValue(key,out cached)){target.Source=cached;return;}
  var ui=target.Dispatcher;
  Task.Run(async()=>{
   // One at a time: the first time a wallpaper is seen, its downsized copy is made from the whole photo.
   await decodeOne.WaitAsync().ConfigureAwait(false);
   try{if(ticket.Cancelled)return null;lock(thumbnails){BitmapSource found;if(thumbnails.TryGetValue(key,out found))return found;}var b=ScaledDecode.Load(path,width);if(ticket.Cancelled)return null;lock(thumbnails){if(pickerDisposed||ticket.Cancelled)return null;thumbnails[key]=b;thumbnailOrder.Enqueue(key);thumbnailBytes+=(long)b.PixelWidth*b.PixelHeight*4;while((thumbnailBytes>12*1024*1024||thumbnails.Count>48)&&thumbnailOrder.Count>0){var oldest=thumbnailOrder.Dequeue();BitmapSource old;if(thumbnails.TryGetValue(oldest,out old)){thumbnails.Remove(oldest);thumbnailBytes-=(long)old.PixelWidth*old.PixelHeight*4;}}}return b;}catch{return null;}finally{decodeOne.Release();}
  }).ContinueWith(t=>{if(t.Result!=null&&!pickerDisposed&&!ticket.Cancelled&&!ui.HasShutdownStarted)ui.BeginInvoke(new Action(()=>{if(!ticket.Cancelled&&ReferenceEquals(target.Tag,ticket))target.Source=t.Result;}));});
 }

 static void Animate(IAnimatable target,DependencyProperty property,double to,int ms){
  target.BeginAnimation(property,ms==0?null:new DoubleAnimation(to,TimeSpan.FromMilliseconds(ms)){EasingFunction=Motion.Arrive()});
  if(ms==0)((DependencyObject)target).SetValue(property,to);
 }
 static bool SamePath(string a,string b){
  if(a==null||b==null)return false;
  try{return String.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);}catch{return false;}
 }
 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}

 struct POINT{public int x,y;} struct RECT{public int left,top,right,bottom;}
 [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rect);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
 [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
}

// Decode the original at full resolution off the UI thread. Publish on the STA
// dispatcher using persistent bitmap and PNG formats for image-paste clients.
static class WallpaperImageClipboard {
 internal static BitmapSource Load(string path){
  using(var input=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
   var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();return bitmap;
  }
 }
 public static async Task Copy(string path){
  var payload=await Task.Run(()=>{var bitmap=Load(path);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()){encoder.Save(stream);return Tuple.Create(bitmap,stream.ToArray());}});
  using(var png=new MemoryStream(payload.Item2)){
   var data=new DataObject();data.SetData(DataFormats.Bitmap,payload.Item1);data.SetData("PNG",png);
   for(int attempt=0;;attempt++){try{png.Position=0;Clipboard.SetDataObject(data,true);return;}catch(System.Runtime.InteropServices.COMException){if(attempt>=3)throw;}await Task.Delay(60);}
  }
 }
}
