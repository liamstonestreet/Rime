using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

static class NotificationChecks {
 sealed class Store:INotificationStore {public NotificationItem[] Items=new NotificationItem[0];public bool Fail;public int Writes;public NotificationItem[] Load(){return Items;}public void Save(NotificationItem[] items){if(Fail)throw new IOException("fixture");Items=items;Writes++;}}
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static NotificationItem Native(int id,DateTime now,string app="Calendar"){return new NotificationItem{Id="windows:"+app+":"+id+":"+now.Ticks,Provider="windows",Source=app,Title="Meeting "+id,Body="Design review at 3 PM",AppId="Fixture_abc!App",NativeId=(uint)id,CreatedUtc=now};}
 public static void Run(Window owner){
  var widget=RiceSettings.Parse("{\"layout\":{\"left\":[\"workspaces\"],\"right\":[\"power\"]}}");Check(widget.left[0]=="notifications"&&widget.Enabled["notifications"],"Older layouts must gain the notification bell once at the left");widget.Move("notifications",true,0);widget.Enabled["notifications"]=false;var roundTrip=RiceSettings.Parse(widget.Json());Check(!roundTrip.Enabled["notifications"]&&roundTrip.right.Count(k=>k=="notifications")==1&&!roundTrip.left.Contains("notifications"),"Bell visibility and lane placement must persist");
  uint mods,key;Check(NotificationShortcut.Parse("win+shift+n",out mods,out key)&&mods==12&&key==0x4E,"Default notification shortcut parsing failed");Check(NotificationShortcut.Parse("",out mods,out key)&&key==0,"Blank shortcut must disable it");Check(!NotificationShortcut.Parse("n",out mods,out key)&&!NotificationShortcut.Parse("win+bad+n",out mods,out key)&&!NotificationShortcut.Parse("ctrl+ctrl+n",out mods,out key),"Invalid shortcut accepted");
  foreach(var screen in new[]{new System.Drawing.Rectangle(0,0,1920,1080),new System.Drawing.Rectangle(-2560,-200,2560,1440)})foreach(double scale in new[]{1.0,1.25,1.5,2.0}){int bar=screen.Top+(int)Math.Floor(50*scale);var strip=NotificationStripGeometry.Bounds(screen,bar);Check(strip.Left==screen.Left&&strip.Top==bar&&strip.Bottom==screen.Bottom,"Notification strip must fill screen height below Zebar");var shape=NotificationStripGeometry.Outline(strip.Height/scale,480,3,16);Check(!shape.Bounds.IsEmpty&&shape.Bounds.Right<=480&&shape.Bounds.Bottom<=strip.Height/scale,"Strip geometry escaped its host");}
  NotificationAnchor anchor;Check(NotificationAnchor.Parse("hover,100,10,128,40,1250,62",out anchor)&&!anchor.Pin&&anchor.BarBottom==62,"Bell anchor parsing failed");
  foreach(var bad in new[]{"hover,0,0,1,1,0","hover,0,0,9999,30,1000","pin,0,0,20,30,1000,10","garbage","hover,0,0,20,30,1000,200000"})Check(!NotificationAnchor.Parse(bad,out anchor),"Invalid popup anchor accepted");
  foreach(var bounds in new[]{new System.Drawing.Rectangle(0,0,1920,1080),new System.Drawing.Rectangle(-2560,-200,2560,1440)})foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0}){
   anchor=new NotificationAnchor{Left=bounds.Left+bounds.Width/2-15,Right=bounds.Left+bounds.Width/2+15,Top=bounds.Top+5,Bottom=bounds.Top+40,BarBottom=bounds.Top+65,Scale=scale};var point=NotificationAnchor.Place(anchor,bounds,600,300);
   Check(Math.Abs(point.X+600*scale/2-(anchor.Left+anchor.Right)/2)<=1&&point.Y>=anchor.BarBottom,"Popup must center on bell below the bar across DPI/origins");
   anchor.Left=bounds.Right-35;anchor.Right=bounds.Right-5;point=NotificationAnchor.Place(anchor,bounds,600,300);Check(point.X>=bounds.Left&&point.X+600*scale<=bounds.Right&&point.Y+300*scale<=bounds.Bottom,"Edge bell must clamp inside its monitor");
  }
  foreach(var bounds in new[]{new System.Drawing.Rectangle(0,0,1920,1080),new System.Drawing.Rectangle(-2560,-200,2560,1440)})foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0})foreach(int thickness in new[]{0,1,13,24}){
   int bar=bounds.Top+(int)Math.Floor(50*scale);var point=NotificationDrawerGeometry.Place(bounds,bar,scale,thickness,400);double line=Math.Floor(3*scale)/scale;
   Check(Math.Abs(point.X+(NotificationDrawerGeometry.Edge-line/2)*scale-(bounds.Left+(thickness-line/2)*scale))<=.51,"Drawer join must align with the screen-frame stroke across DPI/frame widths");
   Check(point.Y>=bar&&point.Y+400*scale<=bounds.Bottom,"Drawer must stay below the bar and inside its monitor");
   foreach(double depth in new[]{0.0,12.0,300.0,600.0})foreach(double radius in new[]{0.0,1.0,2.0}){var shape=NotificationDrawerGeometry.Outline(400,depth,line,radius,true);Check(!shape.Bounds.IsEmpty&&!Double.IsNaN(shape.Bounds.Width)&&shape.Bounds.Right<=NotificationDrawerGeometry.Edge+depth+.1,"Animated drawer outline escaped its fixed host");}
  }
  var preview=new DrawingVisual();using(var drawing=preview.RenderOpen()){
   drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(24,27,36)),null,new Rect(0,0,720,620));
   var tint=new SolidColorBrush(Color.FromRgb(47,52,66));var accent=new SolidColorBrush(Color.FromRgb(136,192,208));drawing.DrawRectangle(tint,null,new Rect(0,0,13,620));drawing.DrawLine(new Pen(accent,3),new Point(11.5,0),new Point(11.5,80));drawing.DrawLine(new Pen(accent,3),new Point(11.5,540),new Point(11.5,620));
   drawing.PushTransform(new TranslateTransform(9,80));drawing.DrawGeometry(tint,new Pen(accent,3),NotificationDrawerGeometry.Outline(460,600,3,1,true));drawing.Pop();
  }
  var drawerPreview=new RenderTargetBitmap(720,620,96,96,PixelFormats.Pbgra32);drawerPreview.Render(preview);var drawerPng=new PngBitmapEncoder();drawerPng.Frames.Add(BitmapFrame.Create(drawerPreview));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"notification-drawer-preview.png")))drawerPng.Save(file);
  Console.WriteLine("PASS: left drawer reuses CC outline, aligns frame joins across five DPI scales/four frame widths, and stays inside both monitor origins through closed/partial/open geometry. Synthetic rendering only.");
  foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0})foreach(int frame in new[]{1,11,24}){
   var screen=new System.Drawing.Rectangle(-2560,-100,2560,1440);int bar=screen.Top+(int)Math.Floor(50*scale);var host=NotificationStripGeometry.AttachedBounds(screen,bar,scale,frame);double line=Math.Floor(3*scale)/scale;
   Check(Math.Abs(host.Top+(NotificationStripGeometry.Join-line/2)*scale-(bar-line*scale/2))<.01,"Notification top join must match Zebar stroke");
   Check(Math.Abs(host.Top+(host.Height/scale-frame+line/2)*scale-(screen.Bottom-frame*scale+line*scale/2))<.01,"Notification bottom join must match the frame's inner stroke");
   foreach(double depth in new[]{5.0,100.0,480.0})foreach(double radius in new[]{0.0,16.0,32.0}){var shape=NotificationStripGeometry.AttachedOutline(host.Height/scale,depth,line,radius,frame);Check(!shape.Bounds.IsEmpty&&shape.Bounds.Right<=depth+24.01&&shape.Bounds.Bottom<=host.Height/scale+line/2+.01,"Attachment wings escaped animated host");}
  }
  Check(NotificationStripGeometry.AttachedOutline(800,0,3,16,11)==Geometry.Empty,"Closed strip must not leave a second outline");
  foreach(double stroke in new[]{2.0,2.4,3.0}){
   var edge=(PathGeometry)NotificationStripGeometry.AttachedOutline(800,480,stroke,16,11);
   var fill=(PathGeometry)NotificationStripGeometry.AttachedOutline(800,480,0,16,11);
   var edgeArc=(ArcSegment)edge.Figures[0].Segments[1];var fillArc=(ArcSegment)fill.Figures[0].Segments[1];
   Check(Math.Abs(edgeArc.Size.Width-edgeArc.Size.Height)<.001&&Math.Abs(edgeArc.Size.Width-fillArc.Size.Width-stroke/2)<.001,"Join must be circular with a concentric border offset");
   Check(Math.Abs(edgeArc.Point.X+edgeArc.Size.Width-fillArc.Point.X-fillArc.Size.Width)<.001&&Math.Abs(edgeArc.Point.Y-fillArc.Point.Y)<.001,"Material and border arcs must share a center");
  }
  var fallback=(PathGeometry)NotificationStripGeometry.AttachedOutline(800,480,3,16,0);Check(fallback.Figures[0].Segments.Count(segment=>segment.IsStroked)==1,"Frame-off fallback should have only a straight right outline");
  int previewFrame=BarAppearance.Settings.frame;BarAppearance.Update(s=>s.frame=11);
  using(var previewInbox=new NotificationInbox(new Store(),()=>DateTime.UtcNow)){
   previewInbox.Start();previewInbox.AddShell("drawer-preview","Test notification","A message in the assembled notification drawer. This fixture contains no real notification data.");
  using(var popup=new NotificationPopupWindow(previewInbox)){
   var root=popup.Preview(new System.Drawing.Rectangle(0,0,1920,1080),1);double width=root.ActualWidth,height=root.ActualHeight;
   for(int i=0;i<5;i++){root=popup.Preview(new System.Drawing.Rectangle(0,0,1920,1080),1);Check(Math.Abs(root.ActualWidth-width)<.1&&Math.Abs(root.ActualHeight-height)<.1,"Assembled drawer layout must settle without size feedback");}
   var image=new RenderTargetBitmap((int)Math.Ceiling(width),(int)Math.Ceiling(height),96,96,PixelFormats.Pbgra32);image.Render(root);var uiPng=new PngBitmapEncoder();uiPng.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"notification-drawer-ui-preview.png")))uiPng.Save(file);
   var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);int painted=0;for(int pixel=0;pixel<pixels.Length;pixel+=4)if(pixels[pixel+3]>0&&(pixels[pixel]>0||pixels[pixel+1]>0||pixels[pixel+2]>0))painted++;Check(painted>image.PixelWidth*image.PixelHeight/2,"Assembled drawer must paint its themed content rather than a black surface");
  }}
  BarAppearance.Update(s=>s.frame=previewFrame);
  Check(ControlCenterTabDefinition.All.All(t=>t.Title!="Notifications"),"Notifications must not remain a CC tab");
var now=new DateTime(2026,11,5,18,0,0,DateTimeKind.Utc);var store=new Store();var inbox=new NotificationInbox(store,()=>now);inbox.Start();var first=Native(1,now);var second=Native(2,now.AddMinutes(-1),"Messenger");inbox.SyncWindows(new[]{first,second},"Windows notifications connected");Check(inbox.Unread==2,"New notifications missing from badge");inbox.Change(new[]{first.Id},"read");inbox.Change(new[]{first.Id},"pin");inbox.SyncWindows(new[]{first,second},"Windows notifications connected");Check(inbox.Snapshot().Length==2&&inbox.Unread==1&&inbox.Snapshot().Single(i=>i.Id==first.Id).Pinned,"Reconnect lost local state or duplicated notifications");
  var copy=inbox.Snapshot();copy[0].Body="mutated";Check(inbox.Snapshot()[0].Body!="mutated","Inbox leaked mutable state");Check(NotificationsView.Filter(inbox.Snapshot(),"Unread","design","Messenger").Single().Id==second.Id,"Notification filters are incorrect");inbox.Change(new[]{second.Id},"archive");Check(NotificationsView.Filter(inbox.Snapshot(),"History","","All apps").Length==1&&inbox.Unread==0,"Archive must be local and update the badge");inbox.Change(new[]{second.Id},"restore");Check(!inbox.Snapshot().Single(i=>i.Id==second.Id).Archived,"History restore failed");
  inbox.SyncWindows(new[]{second},"Windows notifications connected");Check(inbox.Snapshot().Single(i=>i.Id==first.Id).Archived&&!inbox.Snapshot().Single(i=>i.Id==first.Id).NativeActive,"Native dismissal must synchronize to local history");inbox.SetStatus("Permission revoked");Check(inbox.Snapshot().Length==2&&!inbox.Snapshot().Any(i=>i.NativeActive),"Permission revocation must preserve history and disable native actions");
  try{inbox.SyncWindows(new[]{new NotificationItem{Id="bad"}},"bad");throw new Exception("Invalid notification accepted");}catch(ArgumentException){}Check(inbox.Snapshot().Length==2,"Invalid batch changed the inbox");
  inbox.AddShell("timer-one","Work complete","Take a break.");inbox.AddShell("timer-one","Work complete","Take a break.");Check(inbox.Snapshot().Count(i=>i.Provider=="shell")==1,"Shell timer events must deduplicate");
  var view=new NotificationsView(inbox){Width=680,Resources=owner.Resources};view.SetValue(System.Windows.Documents.TextElement.ForegroundProperty,ControlCenterTheme.Current.Brush("Text"));view.Measure(new Size(680,double.PositiveInfinity));view.Arrange(new Rect(0,0,680,view.DesiredSize.Height));view.UpdateLayout();Check(view.VisibleItems.Length==2,"Notification view rendered the wrong items");var png=new PngBitmapEncoder();var bitmap=new RenderTargetBitmap(680,(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(view);png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"notifications-preview.png")))png.Save(file);var list=view.Children.OfType<ListBox>().Single();var collection=list.ItemsSource;
  Check(first.Initials=="M1"&&new NotificationItem{Title="  "}.Initials=="?","Sender initial fallback failed");
  Check(NotificationsView.SameCard(second,second.Copy()),"Unchanged snapshots must retain cards");var edited=second.Copy();edited.Body="Changed";Check(!NotificationsView.SameCard(second,edited),"Message edits must refresh cards");
  var logo=new NotificationLogoConverter();Check(logo.Convert("https://example.com/avatar.png",null,null,null)==null&&logo.Convert(new string('A',32769),null,null,null)==null,"Avatars must never load remote or oversized data");
  using(var logoInbox=new NotificationInbox(new Store(),()=>now)){
   logoInbox.Start();var old=Native(71,now);var unrelated=Native(72,now);unrelated.AppId="Different_abc!App";
   logoInbox.SyncWindows(new[]{old,unrelated},"Windows notifications connected");logoInbox.Change(new[]{old.Id},"pin");
   var fresh=Native(73,now);fresh.AppIcon="iVBORw0KGgo=";logoInbox.SyncWindows(new[]{fresh},"Windows notifications connected");
   var recovered=logoInbox.Snapshot().Single(i=>i.Id==old.Id);Check(recovered.AppIcon==fresh.AppIcon&&recovered.Pinned&&recovered.Archived,"Historical same-app logo recovery must preserve pin/history state");
   Check(!logoInbox.Snapshot().Single(i=>i.Id==unrelated.Id).HasIcon,"Logo recovery must not cross app identities");
   fresh.AppIcon=null;logoInbox.SyncWindows(new[]{fresh},"Windows notifications connected");Check(logoInbox.Snapshot().Single(i=>i.Id==fresh.Id).HasIcon,"A transient missing logo must not erase a known icon");
  }
  inbox.Change(new[]{second.Id},"archive");view.Render();Check(Object.ReferenceEquals(collection,list.ItemsSource)&&view.VisibleItems.Length==1,"Removal must reconcile the existing collection");inbox.Change(new[]{second.Id},"restore");view.Render();Check(view.VisibleItems.Length==2,"Restored cards missing");
  foreach(double available in new[]{380.0,480.0,600.0}){
   view.FitList(available,680);view.Measure(new Size(680,Double.PositiveInfinity));view.Arrange(new Rect(0,0,680,view.DesiredSize.Height));view.UpdateLayout();
   Check(view.DesiredSize.Height<=available+1&&list.Height>=0&&list.Height<=360,"Fixed notification chrome plus list must fit the available popup height");
  }
  view.Dispose();view.Dispose();
  inbox.Dispose();Check(store.Writes>0,"Pending history must flush at shutdown");var restarted=new NotificationInbox(store,()=>now);restarted.Start();Check(restarted.Snapshot().Length==3&&restarted.Snapshot().Single(i=>i.Id==first.Id).Pinned,"History did not survive restart");restarted.Dispose();
  var bounded=new NotificationInbox(new Store(),()=>now);bounded.Start();for(int i=0;i<530;i++)bounded.AddShell("bounded-"+i,"Update","Body");Check(bounded.Snapshot().Length==500,"Notification retention is unbounded");bounded.Dispose();
  var failed=new NotificationInbox(new Store{Fail=true},()=>now);failed.Start();failed.AddShell("failure","Test","Sensitive text");failed.Dispose();Check(failed.StorageError!=null,"Encryption/storage failure must be visible");
  var vault=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"notification-store-check-"+Guid.NewGuid().ToString("N"),"history.bin");try{var protectedStore=new ProtectedNotificationStore(vault);try{protectedStore.Save(new[]{first});Check(protectedStore.Load().Single().Id==first.Id&&!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(vault)).Contains("Design review"),"History encryption round-trip failed");}catch(System.Security.Cryptography.CryptographicException){Check(!File.Exists(vault),"DPAPI failure must never persist plaintext");Console.WriteLine("SKIP: notification DPAPI round-trip requires a loaded user profile; verified no plaintext fallback.");}}finally{if(File.Exists(vault))File.Delete(vault);var directory=Path.GetDirectoryName(vault);if(Directory.Exists(directory))Directory.Delete(directory);}
  Check(BridgeCommand.Parse("notification-sync:{}").Kind==BridgeCommandKind.NotificationSync&&BridgeCommand.Parse("notifications-open:0_0").Kind==BridgeCommandKind.NotificationOpen,"Notification bridge routing failed");Check(!NotificationCenter.ValidAppId("bad\" --command")&&NotificationCenter.ValidAppId("Package_abc!App"),"Unsafe app activation accepted");
  Console.WriteLine("PASS notification inbox: deduplication, badge/read/pin/archive/history, source revocation, filtering, bounded retention, shutdown flush, storage failure, lazy view disposal and bridge parsing. Fake source/store only; no real notifications read or dismissed.");
 }
}
