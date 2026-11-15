using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

/// <summary>One row in a <see cref="NordMenu"/>.</summary>
sealed class NordMenuItem {
 public string Label; public string Hint; public bool Danger; public Action Run;
 public NordMenuItem(string label,Action run,string hint=null,bool danger=false){Label=label;Run=run;Hint=hint;Danger=danger;}
}

/// <summary>
/// A Nord-themed right-click menu for the bar. WPF's ContextMenu only dismisses on click-away when its
/// app has focus, and the control center deliberately never takes focus, so menus used to linger.
/// This one never activates (full-screen apps and GlazeWM are untouched) and closes itself on any
/// click outside it, on Escape, after a choice, or when another menu opens. Only one is open at a time.
/// </summary>
static class NordMenu {
 static Window open;
 public static void Stop(){var window=open;open=null;if(window!=null)window.Close();}

 /// <summary>Shows a menu at the cursor. Call on the UI thread.</summary>
 public static void Show(string header,IList<NordMenuItem> items,int? anchorX=null,int? anchorY=null){
  Close();
  var w=new Window{Title="Nord Menu",SizeToContent=SizeToContent.WidthAndHeight,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,
   Topmost=true,ShowInTaskbar=false,ShowActivated=false,Focusable=false,Opacity=0,
   FontFamily=RiceFont.Current,Foreground=B("Text"),FontSize=12.5};
  w.Content=Build(header,items,()=>w.Close());Roundness.Apply(w.Content as DependencyObject,BarAppearance.Settings.radius/100.0);
  w.SourceInitialized+=delegate{var hwnd=new WindowInteropHelper(w).Handle;SetWindowLongPtr(hwnd,-20,new IntPtr(GetWindowLongPtr(hwnd,-20).ToInt64()|0x08000000L|0x80L));};
  POINT cursor;GetCursorPos(out cursor);if(anchorX.HasValue&&anchorY.HasValue){cursor.x=anchorX.Value;cursor.y=anchorY.Value;}
  bool wasDown=true; // The right-click that opened the menu is still held; wait for a fresh press.
  var watch=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
  watch.Tick+=delegate{
   if((GetAsyncKeyState(0x1B)&0x8000)!=0){w.Close();return;}
   bool down=(GetAsyncKeyState(1)&0x8000)!=0||(GetAsyncKeyState(2)&0x8000)!=0||(GetAsyncKeyState(4)&0x8000)!=0;
   if(down&&!wasDown){POINT p;GetCursorPos(out p);RECT box;GetWindowRect(new WindowInteropHelper(w).Handle,out box);
    if(p.x<box.left||p.x>=box.right||p.y<box.top||p.y>=box.bottom)w.Close();}
   wasDown=down;
  };
  w.Closed+=delegate{watch.Stop();if(open==w)open=null;};
  open=w;
  w.Show();
  // Move onto the cursor's monitor first so WPF settles its DPI, then place and clamp the real size.
  var handle=new WindowInteropHelper(w).Handle;
  SetWindowPos(handle,new IntPtr(-1),cursor.x,cursor.y,0,0,0x0001|0x0010);
  w.UpdateLayout();
  RECT size;GetWindowRect(handle,out size);int width=size.right-size.left,height=size.bottom-size.top;
  var area=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.x,cursor.y)).Bounds;
  int x=Math.Max(area.Left+4,Math.Min(cursor.x,area.Right-width-4));
  int y=cursor.y+8+height<=area.Bottom-4?cursor.y+8:Math.Max(area.Top+4,cursor.y-8-height);
  SetWindowPos(handle,new IntPtr(-1),x,y,0,0,0x0001|0x0010);
  w.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,Motion.Time(110)));
  watch.Start();
 }

 public static void Close(){var w=open;open=null;if(w!=null)w.Close();}

 /// <summary>Appends to menu.log beside the helper (capped), so menu failures leave a trace.</summary>
 public static void Log(string message){
  try{var path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"menu.log");
   if(System.IO.File.Exists(path)&&new System.IO.FileInfo(path).Length>65536)System.IO.File.Delete(path);
   System.IO.File.AppendAllText(path,DateTime.Now.ToString("s")+" "+message+Environment.NewLine);}catch{}
 }

 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}

 static FrameworkElement Build(string header,IList<NordMenuItem> items,Action close){
  var stack=new StackPanel{MinWidth=168};
  if(!String.IsNullOrWhiteSpace(header))
   stack.Children.Add(new TextBlock{Text=header,Foreground=B("Muted"),FontSize=11,Margin=new Thickness(10,4,10,6),TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=240});
  foreach(var item in items){
   var entry=item;
   var label=new TextBlock{Text=entry.Label,Foreground=entry.Run==null?B("Muted"):entry.Danger?B("Error"):B("Text"),VerticalAlignment=VerticalAlignment.Center};
   var row=new Border{Child=label,Padding=new Thickness(10,7,10,7),CornerRadius=new CornerRadius(8),Background=Brushes.Transparent,Cursor=entry.Run==null?Cursors.Arrow:Cursors.Hand,ToolTip=entry.Hint};
   row.MouseEnter+=delegate{if(entry.Run!=null)row.Background=entry.Danger?new SolidColorBrush(Color.FromArgb(0x38,0xBF,0x61,0x6A)):B("Hover");};
   row.MouseLeave+=delegate{row.Background=Brushes.Transparent;};
   row.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){e.Handled=true;close();if(entry.Run!=null)entry.Run();};
   stack.Children.Add(row);
  }
  return new Border{Child=stack,Padding=new Thickness(5),CornerRadius=new CornerRadius(12),Background=B("Surface"),BorderBrush=B("Border"),BorderThickness=new Thickness(1),
   Margin=new Thickness(8),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=2,Opacity=.35,Color=Colors.Black}};
 }

 struct POINT{public int x,y;} struct RECT{public int left,top,right,bottom;}
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out RECT rect);
 [DllImport("user32.dll")]static extern bool GetCursorPos(out POINT point);
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")]static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
 [DllImport("user32.dll")]static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
}
