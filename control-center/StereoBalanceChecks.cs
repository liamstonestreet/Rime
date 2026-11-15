using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
static class StereoBalanceChecks {
 static Button Find(DependencyObject parent){var button=parent as Button;if(button!=null&&button.Name=="AudioResetBalance")return button;foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>()){var found=Find(child);if(found!=null)return found;}return null;}
 static Slider FindSlider(DependencyObject parent){var slider=parent as Slider;if(slider!=null&&slider.Name=="AudioBalance")return slider;foreach(var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>()){var found=FindSlider(child);if(found!=null)return found;}return null;}
 public static void Run(){float master=.61f;var channels=new[]{.61f,0f};int writes=0;Action<int,float> write=(i,v)=>{channels[i]=v;master=Math.Max(channels[0],channels[1]);};
  var actual=StereoBalance.Apply(0,()=>master,(i,v)=>{writes++;write(i,v);},i=>channels[i]);if(actual!=0||Math.Abs(channels[0]-.61f)>.0001||channels[0]!=channels[1]||writes!=2)throw new Exception("Balance Reset must write equal channel levels using live master volume");
  StereoBalance.Apply(1,()=>master,write,i=>channels[i]);if(channels[0]!=0||Math.Abs(channels[1]-.61f)>.0001)throw new Exception("Right pan failed");StereoBalance.Apply(0,()=>master,write,i=>channels[i]);if(channels[0]!=channels[1])throw new Exception("Reset must restore both channels after hard panning");
  var endpoint=new AudioControllerChecks.Endpoint();var controller=new AudioController(endpoint);controller.AcceptDiscovery(endpoint.ReadDevices(),0,false);
  using(var page=new AudioPage(controller)){
   var slider=FindSlider(page);if(slider==null)throw new Exception("Missing balance slider");var button=Find(page);if(button==null)throw new Exception("Missing Reset button");
   button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));int before=endpoint.Writes;button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(endpoint.Writes!=before+2||endpoint.Channels[0]!=endpoint.Channels[1])throw new Exception("Centered reset skipped real endpoint command");
   typeof(AudioPage).GetField("updating",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(page,true);before=endpoint.Writes;button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(endpoint.Writes!=before+2||Math.Abs(endpoint.Master-.61f)>.0001)throw new Exception("Refresh guard blocked reset or changed loudness");
  }
  Console.WriteLine("PASS: balance Reset explicitly writes both channels, retains current loudness, restores hard pan and works when slider is already centered or refreshing. Mock endpoint only; no live audio writes.");
 }
}
