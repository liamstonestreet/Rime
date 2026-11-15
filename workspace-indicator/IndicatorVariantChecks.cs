using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

static class IndicatorVariantChecks {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 internal static void Run(){
  var names=new[]{"1","2","3","4","5","6","7","8","9","0"};var labels=new[]{"Home","Study","Design","Code","Music","Games","Notes","Reading","Tools","Archive"};
  var gallery=new IndicatorVariants("gallery",names,labels);gallery.Apply(IndicatorPalette.For(new FocusState(),null));var cardField=typeof(IndicatorVariants).GetField("cards",BindingFlags.NonPublic|BindingFlags.Instance);var cards=(Border[])cardField.GetValue(gallery);
  for(int index=0;index<names.Length;index++){gallery.SetWorkspace(index,true);var visible=gallery.VisibleNeighbors;Check(visible.Length==3&&visible.Contains(index)&&visible.Contains((index+9)%10)&&visible.Contains((index+1)%10),"Gallery must show exactly the current workspace and immediate neighbors, including wraparound");Check(cards.Count(c=>c.Opacity>.01)==3,"Gallery must hide all non-neighbor cards");}
  gallery.SetWorkspace(0,false);gallery.SetWorkspace(9,false);gallery.SetWorkspace(1,false);gallery.Settle();Check(cards[1].Opacity==1&&cards.Count(c=>c.Opacity>.01)==3,"Rapid changes must settle at the newest workspace");
  var move=(TransformGroup)cards[1].RenderTransform;Check(Math.Abs(((TranslateTransform)move.Children[1]).X-255)<.01&&((ScaleTransform)move.Children[0]).ScaleX==1,"Gallery active card must return to its centered resting position");
  var ribbon=new IndicatorVariants("ribbon",names,labels);ribbon.Apply(IndicatorPalette.For(new FocusState(),null));ribbon.SetWorkspace(2,true);ribbon.SetWorkspace(8,false);ribbon.SetWorkspace(0,false);ribbon.Settle();
  var number=(TextBlock)typeof(IndicatorVariants).GetField("ribbonNumber",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ribbon);var ghost=(TextBlock)typeof(IndicatorVariants).GetField("ribbonGhost",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ribbon);var label=(TextBlock)typeof(IndicatorVariants).GetField("ribbonLabel",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ribbon);var rail=(TranslateTransform)typeof(IndicatorVariants).GetField("railSlide",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ribbon);
  Check(number.Text=="1"&&number.Opacity==1&&ghost.Opacity==0&&ghost.Text==""&&rail.X==0,"Ribbon must settle without stale digits or a stale marker");Check(label.Text=="Home","Ribbon must use configured workspace labels");labels[0]="Updated home";ribbon.UpdateLabels(labels);Check(label.Text=="Updated home","Workspace label edits must reach variant views");
  foreach(var view in new[]{gallery,ribbon}){view.Measure(new Size(view.Width,view.Height));view.Arrange(new Rect(0,0,view.Width,view.Height));view.UpdateLayout();Check(view.ClipToBounds&&view.ActualWidth==view.Width&&view.ActualHeight==view.Height,"Variant geometry must remain inside its viewport");}
 }
}
