using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

static class PanelAnimationChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 public static void Run(){var owner=new Window();var surface=new Border{Height=300};var slide=new TranslateTransform();int paints=0;var animations=new PanelAnimationController(owner,surface,slide,force=>paints++);
  var frames=new FrameworkElement[]{new Border(),new Border(),new Border()};foreach(var page in frames){page.RenderTransform=new TranslateTransform();page.Width=500;page.Height=100;}var line=new Border{RenderTransform=new TranslateTransform()};
  animations.SwitchTab(frames,0,1,line,600,true,40);animations.SwitchTab(frames,1,2,line,600,true,40);animations.SwitchTab(frames,2,0,line,600,false,40);
  Pump();Check(frames[0].Visibility==Visibility.Visible&&frames[1].Visibility==Visibility.Collapsed&&frames[2].Visibility==Visibility.Collapsed,"Interrupted tab transitions left the wrong page visible");Check(frames[0].Opacity==1&&((TranslateTransform)frames[0].RenderTransform).X==0,"Incoming page retained an old clock");
  animations.ResizeUnderline(line,2,3,900);Check((double)line.GetAnimationBaseValue(FrameworkElement.WidthProperty)==300,"Underline width base did not follow resize");
  surface.BeginAnimation(FrameworkElement.HeightProperty,new DoubleAnimation(300,500,TimeSpan.FromSeconds(1)));animations.ResizeHeight(220,600,false,40,4);Check(surface.Height==220&&owner.Height==224,"Height replacement did not cancel prior clock");
  bool hidden=false;animations.DelayHide(()=>hidden=true);Check(animations.HasPendingHide,"Hide timer not owned");animations.AnimateVertical(0,40,EasingMode.EaseOut,null);Pump();Check(!hidden&&!animations.HasPendingHide,"Reopen failed to cancel pending hide");
  animations.DelayHide(()=>hidden=true);Pump();Check(hidden&&!animations.HasPendingHide,"Hide timer did not settle");
  hidden=false;animations.TrackShape(500);animations.DelayHide(()=>hidden=true);Check(animations.IsTracking,"Frame tracking did not start");animations.Dispose();int before=paints;Pump();Check(!hidden&&!animations.IsTracking&&!animations.HasPendingHide&&paints==before,"Disposal left a timer/frame callback alive");animations.TrackShape(100);animations.DelayHide(()=>hidden=true);Check(!animations.IsTracking&&!animations.HasPendingHide,"Disposed controller restarted");
  Console.WriteLine("PASS: extracted animation owner handles rapid interrupted tabs, underline width, height-clock replacement, reopen/hide cancellation and timer/frame disposal. Synthetic elements only; no window shown.");
 }
}
