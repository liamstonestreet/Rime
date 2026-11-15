using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

sealed class NotificationsView:StackPanel,IDisposable {
 sealed class SourceChoice {public string FriendlyName {get;private set;}public SourceChoice(string name){FriendlyName=name;}public override string ToString(){return FriendlyName;}}
 string SelectedSource(){var choice=source.SelectedItem as SourceChoice;return choice==null?"All apps":choice.FriendlyName;}
 readonly NotificationInbox inbox;readonly TextBox search=new TextBox{Name="NotificationSearch",Margin=new Thickness(0,8,0,8),Padding=new Thickness(9,6,9,6),ToolTip="Search app, title and message"};
 readonly ComboBox source=new ComboBox{MinWidth=110,MaxWidth=200,Margin=new Thickness(8,0,0,0)};readonly ListBox list=new ListBox{Name="NotificationList",BorderThickness=new Thickness(0),Background=Brushes.Transparent,Height=300,HorizontalContentAlignment=HorizontalAlignment.Stretch};
 readonly TextBlock count=new TextBlock{FontSize=11},status=new TextBlock{FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)},empty=new TextBlock{FontSize=13,Margin=new Thickness(10,18,10,18),Text="You're all caught up."};
 readonly ObservableCollection<NotificationItem> cards=new ObservableCollection<NotificationItem>();
 readonly HashSet<string> leaving=new HashSet<string>(),entered=new HashSet<string>();bool waitingRender;
 readonly List<Button> filters=new List<Button>();readonly DispatcherTimer debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};string filter="All";bool disposed,rendering;string viewKey;int renderRevision;NotificationItem[] shown=new NotificationItem[0];int queued;
 static Brush B(string name){return ControlCenterTheme.Current.Brush(name);}
 static Button Small(string text,string name){return new Button{Content=text,Name=name,FontSize=11,Style=ActionStyle(),ToolTip=text};}
 static Style ActionStyle(){return (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
 <Setter Property='Foreground' Value='{DynamicResource ThemeMuted}'/><Setter Property='Background' Value='Transparent'/><Setter Property='Padding' Value='10,6'/><Setter Property='Margin' Value='0,0,6,6'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Cursor' Value='Hand'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='Surface' Background='{TemplateBinding Background}' CornerRadius='7' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers>
 <Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='{DynamicResource ThemeText}'/><Setter TargetName='Surface' Property='Background' Value='{DynamicResource ThemeSelected}'/><Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName='Surface' Storyboard.TargetProperty='Opacity' From='0.65' To='1' Duration='0:0:0.14'/></Storyboard></BeginStoryboard></Trigger.EnterActions></Trigger>
 <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='Background' Value='{DynamicResource ThemeSelected}'/><Setter Property='Foreground' Value='{DynamicResource ThemeText}'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Opacity' Value='0.65'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
 </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");}

 internal void FitList(double availableHeight,double width,bool fill=false){
  // Header, search, filters, actions and footer stay fixed. Only the list owns scrolling.
  double fixedHeight=Margin.Top+Margin.Bottom;foreach(UIElement child in Children){if(child==list)continue;child.Measure(new Size(Math.Max(1,width),Double.PositiveInfinity));fixedHeight+=child.DesiredSize.Height;}
  list.Height=Math.Max(0,Math.Min(fill?Double.MaxValue:360,availableHeight-fixedHeight));
 }
 internal NotificationItem[] VisibleItems {get{return shown.Select(i=>i.Copy()).ToArray();}}
 public NotificationsView():this(NotificationCenter.Current){}
 internal NotificationsView(NotificationInbox inbox){this.inbox=inbox;Margin=new Thickness(0,12,0,0);Resources["ShowNotificationAction"]=new BooleanToVisibilityConverter();Resources[typeof(Button)]=ActionStyle();Resources["NotificationLogo"]=new NotificationLogoConverter();
  var heading=new DockPanel();var title=new TextBlock{Text="Notifications",FontSize=24,FontWeight=FontWeights.SemiBold};count.Foreground=B("Muted");count.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(count,Dock.Right);heading.Children.Add(count);heading.Children.Add(title);Children.Add(heading);status.Name="NotificationsConnectionStatus";status.Foreground=B("Muted");status.Margin=new Thickness(0,6,0,14);Children.Add(status);
  search.Background=B("Raised");search.Foreground=B("Text");search.CaretBrush=B("Text");search.BorderBrush=B("Border");search.BorderThickness=new Thickness(0);System.Windows.Automation.AutomationProperties.SetName(search,"Search notifications");search.Padding=new Thickness(13,10,13,10);Children.Add(new TextBlock{Text="Find a message",FontSize=11,Foreground=B("Muted")});Children.Add(search);search.TextChanged+=delegate{debounce.Stop();debounce.Start();};debounce.Tick+=delegate{debounce.Stop();Render();};
  var tabs=new WrapPanel{Margin=new Thickness(0,0,0,8)};foreach(var name in new[]{"All","Unread","Pinned","History"}){string value=name;var button=Small(name,"Notifications"+name);button.Click+=delegate{filter=value;Render();};filters.Add(button);tabs.Children.Add(button);}source.SelectionChanged+=delegate{if(!rendering)Render();};tabs.Children.Add(source);Children.Add(tabs);
  var actions=new WrapPanel{Margin=new Thickness(0,0,0,8)};Button read=Small("Mark all read","NotificationsMarkRead"),clear=Small("Clear view","NotificationsArchive"),connect=Small("Connect Windows","NotificationsConnect"),settings=Small("Access settings","NotificationsSettings");read.Click+=delegate{inbox.Change(shown.Select(i=>i.Id).ToArray(),"mark-read");};clear.Click+=delegate{inbox.Change(shown.Select(i=>i.Id).ToArray(),"archive");};connect.Click+=delegate{
   if(!NotificationCenter.LaunchCompanion())MessageBox.Show(NotificationCenter.Current.SourceStatus+"\n\nFrom the WindowsRice repository, run:\n\npowershell -ExecutionPolicy Bypass -File .\\nord-custom\\install.ps1 -Notifications\n\nThen return here, click Connect Windows, and choose Allow Windows access. Windows will ask for notification permission.","Connect Windows notifications",MessageBoxButton.OK,MessageBoxImage.Information);
   Render();
  };settings.Click+=delegate{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:privacy-notifications"){UseShellExecute=true});}catch{}};actions.Children.Add(read);actions.Children.Add(clear);actions.Children.Add(connect);actions.Children.Add(settings);Children.Add(actions);
  list.Foreground=B("Text");list.SetValue(VirtualizingPanel.ScrollUnitProperty,ScrollUnit.Pixel);list.SetValue(ScrollViewer.CanContentScrollProperty,true);list.SetValue(VirtualizingPanel.IsVirtualizingProperty,true);list.SetValue(VirtualizingPanel.VirtualizationModeProperty,VirtualizationMode.Recycling);list.SetValue(VirtualizingPanel.IsVirtualizingWhenGroupingProperty,true);ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
  list.GroupStyle.Add(new GroupStyle{HeaderTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding Name}' FontWeight='SemiBold' FontSize='12' Margin='0,8,0,7'/></DataTemplate>")});
  list.ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse(@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:local='clr-namespace:;assembly=__ASSEMBLY__'><DataTemplate.Resources><local:NotificationLogoConverter x:Key='NotificationLogo'/><BooleanToVisibilityConverter x:Key='ShowNotificationAction'/></DataTemplate.Resources>
 <Border x:Name='Card' Background='{DynamicResource ThemeRaised}' BorderBrush='Transparent' BorderThickness='1' CornerRadius='14' Padding='16,14,12,10' Margin='0,0,0,10'>
 <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='52'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
 <Border Width='38' Height='38' CornerRadius='10' Background='{DynamicResource ThemeSelected}' VerticalAlignment='Top' HorizontalAlignment='Left' Padding='5' ToolTip='{Binding Source}'><Grid>
 <TextBlock x:Name='AppFallback' Text='&#x25A3;' Foreground='{DynamicResource ThemeAccent}' FontSize='24' HorizontalAlignment='Center' VerticalAlignment='Center' Visibility='Collapsed'/>
 <Image x:Name='AppLogo' Source='{Binding AppIcon,Converter={StaticResource NotificationLogo}}' Stretch='Uniform' RenderOptions.BitmapScalingMode='HighQuality'/>
 </Grid></Border> <StackPanel Grid.Column='1'><DockPanel Margin='0,0,2,6'><StackPanel DockPanel.Dock='Right' Orientation='Horizontal'><Ellipse x:Name='UnreadDot' Width='5' Height='5' Fill='{DynamicResource ThemeAccent}' Margin='10,0,7,0'/><TextBlock Text='{Binding TimeLabel}' FontSize='10' Foreground='{DynamicResource ThemeMuted}' VerticalAlignment='Center'/></StackPanel><TextBlock Text='{Binding Source}' FontSize='10' FontWeight='Medium' Foreground='{DynamicResource ThemeMuted}'/></DockPanel>
 <TextBlock Text='{Binding Title}' FontSize='15' FontWeight='SemiBold' Foreground='{DynamicResource ThemeText}' TextWrapping='Wrap' Margin='0,0,0,6'/>
 <TextBlock Text='{Binding Body}' FontSize='12' LineHeight='19' Foreground='{DynamicResource ThemeText}' Opacity='0.85' TextWrapping='Wrap' Margin='0,0,0,12'/>
 <WrapPanel Margin='-10,0,0,0'><Button Content='Open' Tag='open' FontSize='10' Visibility='{Binding CanOpen,Converter={StaticResource ShowNotificationAction}}'/><Button Content='Copy' Tag='copy' FontSize='10'/><Button Content='{Binding ReadLabel}' Tag='read' FontSize='10'/><Button Content='{Binding PinLabel}' Tag='pin' FontSize='10'/><Button Content='Archive' Tag='archive' FontSize='10'/><Button Content='Restore' Tag='restore' FontSize='10' Visibility='{Binding Archived,Converter={StaticResource ShowNotificationAction}}'/><Button Content='Dismiss' ToolTip='Dismiss this notification in Windows' Tag='dismiss' FontSize='10' Visibility='{Binding NativeActive,Converter={StaticResource ShowNotificationAction}}'/></WrapPanel>
 </StackPanel><Viewbox Width='16' Height='16' HorizontalAlignment='Left' VerticalAlignment='Bottom' Margin='6,0,0,8' Visibility='{Binding Pinned,Converter={StaticResource ShowNotificationAction}}' ToolTip='Pinned notification' IsHitTestVisible='False'><Path Data='M 8,2 L 16,2 M 10,2 L 10,9 L 6,13 L 6,15 L 18,15 L 18,13 L 14,9 L 14,2 M 12,15 L 12,23' Stroke='{DynamicResource ThemeAccent}' StrokeThickness='2' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round'/></Viewbox></Grid></Border><DataTemplate.Triggers><DataTrigger Binding='{Binding IsKeyboardFocusWithin,RelativeSource={RelativeSource AncestorType={x:Type ListBoxItem}}}' Value='True'><Setter TargetName='Card' Property='BorderBrush' Value='{DynamicResource ThemeAccent}'/></DataTrigger><DataTrigger Binding='{Binding HasIcon}' Value='False'><Setter TargetName='AppLogo' Property='Visibility' Value='Collapsed'/><Setter TargetName='AppFallback' Property='Visibility' Value='Visible'/></DataTrigger><DataTrigger Binding='{Binding Read}' Value='True'><Setter TargetName='UnreadDot' Property='Visibility' Value='Collapsed'/></DataTrigger><DataTrigger Binding='{Binding Pinned}' Value='True'><Setter TargetName='Card' Property='BorderBrush' Value='{DynamicResource ThemeAccent}'/><Setter TargetName='Card' Property='BorderThickness' Value='1'/></DataTrigger></DataTemplate.Triggers></DataTemplate>".Replace("__ASSEMBLY__",typeof(NotificationsView).Assembly.GetName().Name));
  list.Resources[typeof(Button)]=ActionStyle();
  var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));itemStyle.Setters.Add(new Setter(Control.ForegroundProperty,B("Text")));itemStyle.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));itemStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><ContentPresenter/></ControlTemplate>")));itemStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty,null));itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));itemStyle.Setters.Add(new EventSetter(FrameworkElement.LoadedEvent,new RoutedEventHandler(CardLoaded)));list.ItemContainerStyle=itemStyle;
  var collection=new ListCollectionView(cards);collection.GroupDescriptions.Add(new PropertyGroupDescription("Source"));list.ItemsSource=collection;
  list.AddHandler(Button.ClickEvent,new RoutedEventHandler(ItemAction));Children.Add(list);Children.Add(empty);Children.Add(RiceLayout.Hint("Read, pin and archive affect this inbox only. Dismiss in Windows removes that specific native notification. History stays encrypted on this PC for up to 7 days / 500 items; pinned items are retained within that limit."));
  inbox.Changed+=Changed;IsVisibleChanged+=delegate{if(IsVisible)Render();else debounce.Stop();};Unloaded+=delegate{debounce.Stop();};Render();
 }
 void Changed(){if(disposed||Dispatcher.HasShutdownStarted||System.Threading.Interlocked.Exchange(ref queued,1)!=0)return;Dispatcher.BeginInvoke(new Action(()=>{System.Threading.Interlocked.Exchange(ref queued,0);if(!disposed&&IsVisible)Render();}));}
 internal static NotificationItem[] Filter(NotificationItem[] items,string filter,string query,string app){query=(query??"").Trim();return items.Where(i=>filter=="History"?i.Archived:filter=="Pinned"?i.Pinned:!i.Archived).Where(i=>filter!="Unread"||!i.Read).Where(i=>filter!="Pinned"||i.Pinned).Where(i=>String.IsNullOrEmpty(app)||app=="All apps"||i.Source==app).Where(i=>query.Length==0||(i.Source+" "+i.Title+" "+i.Body).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).OrderByDescending(i=>i.CreatedUtc).ToArray();}
 static ScrollViewer Scroll(DependencyObject root){if(root is ScrollViewer)return (ScrollViewer)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=Scroll(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}return null;}
 internal void Render(){if(disposed)return;if(leaving.Count>0){waitingRender=true;return;}var scrolling=Scroll(list);double offset=scrolling==null?0:scrolling.VerticalOffset;string selectedId=(list.SelectedItem as NotificationItem)==null?null:((NotificationItem)list.SelectedItem).Id;rendering=true;try{var all=inbox.Snapshot();string selected=SelectedSource();var choices=new[]{"All apps"}.Concat(all.Select(i=>i.Source).Distinct().OrderBy(n=>n)).Select(n=>new SourceChoice(n)).ToArray();source.ItemsSource=choices;source.DisplayMemberPath="FriendlyName";source.SelectedItem=choices.FirstOrDefault(c=>c.FriendlyName==selected)??choices[0];shown=Filter(all,filter,search.Text,SelectedSource());count.Text=inbox.Unread+" unread";status.Text=inbox.SourceStatus+(inbox.StorageError==null?"":"\n"+inbox.StorageError);foreach(var button in filters)button.Background=B((string)button.Content==filter?"Selected":"Raised");var nextKey=filter+"|"+search.Text+"|"+SelectedSource();bool preserve=nextKey==viewKey;viewKey=nextKey;int version=++renderRevision;Reconcile(shown,preserve);if(leaving.Count>0)waitingRender=true;list.SelectedItem=shown.FirstOrDefault(i=>i.Id==selectedId);if(preserve&&offset>0)Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(!disposed&&version==renderRevision){var current=Scroll(list);if(current!=null)current.ScrollToVerticalOffset(offset);}}));list.Visibility=shown.Length>0||leaving.Count>0?Visibility.Visible:Visibility.Collapsed;empty.Visibility=shown.Length==0&&leaving.Count==0?Visibility.Visible:Visibility.Collapsed;empty.Text=search.Text.Length>0?"No matching notifications.":filter=="History"?"No archived notifications.":inbox.SourceStatus=="Windows notifications connected"?"You're all caught up.":"No notifications yet. Check the connection status above.";}finally{rendering=false;}}
 internal static bool SameCard(NotificationItem a,NotificationItem b){return a.Id==b.Id&&a.Read==b.Read&&a.Pinned==b.Pinned&&a.Archived==b.Archived&&a.NativeActive==b.NativeActive&&a.Title==b.Title&&a.Body==b.Body&&a.Source==b.Source&&a.AppIcon==b.AppIcon&&a.AppId==b.AppId&&a.CreatedUtc==b.CreatedUtc;}
 bool Animate {get{return !disposed&&IsVisible&&SystemParameters.ClientAreaAnimation&&PresentationSource.FromVisual(this)!=null;}}
 static DoubleAnimation Motion(double from,double to,int ms){return new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(ms)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};}
 void CardLoaded(object sender,RoutedEventArgs args){var card=sender as ListBoxItem;var item=card==null?null:card.DataContext as NotificationItem;if(item==null)return;
  card.BeginAnimation(HeightProperty,null);card.Height=Double.NaN;card.IsHitTestVisible=true;card.Opacity=1;card.RenderTransform=new TranslateTransform();
  if(!entered.Add(item.Id)||!Animate)return;card.BeginAnimation(OpacityProperty,Motion(0,1,220));((TranslateTransform)card.RenderTransform).BeginAnimation(TranslateTransform.YProperty,Motion(12,0,260));
 }
 void Reconcile(NotificationItem[] next,bool preserve){
  if(!preserve){cards.Clear();foreach(var item in next)cards.Add(item);if(Animate)list.BeginAnimation(OpacityProperty,Motion(0.5,1,160));return;}
  var wanted=new HashSet<string>(next.Select(i=>i.Id));
  foreach(var item in cards.Where(i=>!wanted.Contains(i.Id)).ToArray()){
   var container=list.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
   if(!Animate||container==null||container.ActualHeight<=0){cards.Remove(item);entered.Remove(item.Id);continue;}
   leaving.Add(item.Id);container.IsHitTestVisible=false;var transform=new TranslateTransform();container.RenderTransform=transform;
   container.BeginAnimation(OpacityProperty,Motion(1,0,160));transform.BeginAnimation(TranslateTransform.XProperty,Motion(0,36,200));
   var collapse=Motion(container.ActualHeight,0,180);collapse.BeginTime=TimeSpan.FromMilliseconds(90);
   collapse.Completed+=delegate{if(disposed)return;cards.Remove(item);leaving.Remove(item.Id);entered.Remove(item.Id);if(leaving.Count==0&&waitingRender){waitingRender=false;Render();}};
   container.BeginAnimation(HeightProperty,collapse);
  }
  for(int i=0;i<next.Length;i++){var existing=cards.FirstOrDefault(c=>c.Id==next[i].Id);if(existing==null)cards.Insert(Math.Min(i,cards.Count),next[i]);else if(!SameCard(existing,next[i]))cards[cards.IndexOf(existing)]=next[i];}
 }
 void ItemAction(object sender,RoutedEventArgs args){var button=args.OriginalSource as Button;if(button==null)return;var item=button.DataContext as NotificationItem;if(item==null)return;args.Handled=true;try{switch(Convert.ToString(button.Tag)){case "copy":Clipboard.SetText(item.Title+"\n"+item.Body);break;case "open":NotificationCenter.OpenApp(item.AppId);break;case "dismiss":NotificationCenter.Dismiss(item);break;default:inbox.Change(new[]{item.Id},Convert.ToString(button.Tag));break;}}catch{status.Text="That action could not be completed. Try again.";}}
 public void Dispose(){if(disposed)return;disposed=true;leaving.Clear();entered.Clear();cards.Clear();inbox.Changed-=Changed;debounce.Stop();list.ItemsSource=null;shown=new NotificationItem[0];}
}

// Only bounded embedded PNGs from the companion: never load a remote URL or arbitrary file.
public sealed class NotificationLogoConverter:IValueConverter {
 readonly Dictionary<string,BitmapSource> cache=new Dictionary<string,BitmapSource>();
 public object Convert(object value,Type target,object parameter,CultureInfo culture){var data=value as string;if(String.IsNullOrEmpty(data)||data.Length>32768)return null;BitmapSource known;if(cache.TryGetValue(data,out known))return known;try{
  var bytes=System.Convert.FromBase64String(data);if(bytes.Length<8||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)return null;
  using(var stream=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=128;image.StreamSource=stream;image.EndInit();image.Freeze();var logo=TrimLogo(image);if(cache.Count<128)cache[data]=logo;return logo;}
 }catch{return null;}}
 // Windows logos often include generous transparent margins within the image.
 // Fit the visible artwork, rather than that invisible canvas, into the badge.
 internal static BitmapSource TrimLogo(BitmapSource image){
  var rgba=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);int w=rgba.PixelWidth,h=rgba.PixelHeight,stride=w*4;var pixels=new byte[stride*h];rgba.CopyPixels(pixels,stride,0);
  int left=w,top=h,right=-1,bottom=-1;
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[y*stride+x*4+3]>8){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
  if(right<left||bottom<top)return image;
  var cropped=new CroppedBitmap(image,new Int32Rect(left,top,right-left+1,bottom-top+1));cropped.Freeze();return cropped;
 } public object ConvertBack(object value,Type target,object parameter,CultureInfo culture){throw new NotSupportedException();}
}
