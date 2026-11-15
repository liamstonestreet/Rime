using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

// A small retained carousel, independent of wallpaper application and keyboard handling.
// Only nearby cards exist, even for libraries containing hundreds of images.
sealed class WallpaperVariants : Grid {
 sealed class Card {public int Index;public Border Border;public Image Image;public ScaleTransform Scale;public TranslateTransform Move;public int WidthRequested;}
 readonly string style;readonly List<string> images;readonly Action<string,Image,int> thumbnail;readonly Canvas rail=new Canvas();readonly Dictionary<int,Card> cards=new Dictionary<int,Card>();
 readonly TextBlock number=new TextBlock();readonly TranslateTransform numberSlide=new TranslateTransform();readonly TextBlock hint=new TextBlock();
 readonly double cardWidth,cardHeight,step,center,top;readonly int radius;int selected=-1,version;IEasingFunction easing;
 internal int SelectedIndex {get{return selected;}}
 internal int RetainedCards {get{return cards.Count;}}
 internal int[] VisibleIndices {get{return cards.Values.Where(c=>c.Border.Opacity>.01).Select(c=>c.Index).OrderBy(i=>i).ToArray();}}
 internal static int Relative(int index,int selected,int count){int result=(index-selected+count)%count;return result>count/2?result-count:result;}
 static Brush B(string role){return ControlCenterTheme.Current.Brush(role);}
 public WallpaperVariants(string style,List<string> images,Action<string,Image,int> thumbnail){
  this.style=style;this.images=images;this.thumbnail=thumbnail;ClipToBounds=true;
  if(style=="showcase"){Width=920;Height=292;cardWidth=440;cardHeight=248;step=340;center=240;top=22;radius=2;rail.Width=920;rail.Height=292;Children.Add(rail);}
  else{
   Width=900;Height=116;cardWidth=96;cardHeight=64;step=106;center=(752-cardWidth)/2;top=15;radius=4;
   number.FontSize=52;number.FontWeight=FontWeights.Light;number.Foreground=B("Accent");number.VerticalAlignment=VerticalAlignment.Top;number.HorizontalAlignment=HorizontalAlignment.Left;number.Margin=new Thickness(2,4,0,0);number.RenderTransform=numberSlide;Children.Add(number);
   Children.Add(new TextBlock{Text="SELECTED",FontSize=9,Foreground=B("Muted"),Margin=new Thickness(5,70,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top});
   rail.Width=752;rail.Height=90;rail.HorizontalAlignment=HorizontalAlignment.Right;rail.VerticalAlignment=VerticalAlignment.Top;rail.ClipToBounds=true;Children.Add(rail);
   hint.Text="A new view, one frame at a time";hint.FontSize=10;hint.Foreground=B("Muted");hint.HorizontalAlignment=HorizontalAlignment.Right;hint.VerticalAlignment=VerticalAlignment.Bottom;hint.Margin=new Thickness(0,0,4,0);Children.Add(hint);
  }
 }
 Card Make(int index,int relative){
  var image=new Image{Stretch=Stretch.UniformToFill};var face=new Grid{Clip=new RectangleGeometry(new Rect(0,0,cardWidth-4,cardHeight-4),style=="showcase"?12:6,style=="showcase"?12:6)};face.Children.Add(new Border{Background=B("Raised")});face.Children.Add(image);
  if(style=="showcase")face.Children.Add(new Border{Height=72,VerticalAlignment=VerticalAlignment.Bottom,Background=new LinearGradientBrush(Colors.Transparent,Color.FromArgb(125,0,0,0),90)});
  var badge=new TextBlock{Text=(index+1).ToString("00"),FontSize=style=="showcase"?13:9,FontWeight=FontWeights.Medium,Foreground=Brushes.White};face.Children.Add(new Border{Child=badge,Background=new SolidColorBrush(Color.FromArgb(110,30,35,43)),Padding=new Thickness(6,2,6,2),CornerRadius=new CornerRadius(4),Margin=new Thickness(style=="showcase"?12:5),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom});
  var card=new Card{Index=index,Image=image,Scale=new ScaleTransform(1,1,cardWidth/2,cardHeight/2),Move=new TranslateTransform(center+relative*step,top)};var transform=new TransformGroup();transform.Children.Add(card.Scale);transform.Children.Add(card.Move);
  card.Border=new Border{Width=cardWidth,Height=cardHeight,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(style=="showcase"?14:8),Child=face,RenderTransform=transform,Opacity=0};Roundness.Apply(card.Border,BarAppearance.Settings.radius/100.0);rail.Children.Add(card.Border);cards[index]=card;return card;
 }
 void Tween(DependencyObject target,DependencyProperty property,double value,double ms){DoubleAnimation animation=ms<=0?null:new DoubleAnimation(value,TimeSpan.FromMilliseconds(ms)){EasingFunction=easing};var element=target as UIElement;if(element!=null)element.BeginAnimation(property,animation);else ((Animatable)target).BeginAnimation(property,animation);if(ms<=0)target.SetValue(property,value);}
 bool Needed(int index){return selected>=0&&Math.Abs(Relative(index,selected,images.Count))<=radius;}
 void Remove(Card card){rail.Children.Remove(card.Border);cards.Remove(card.Index);WallpaperPicker.ReleaseThumbnail(card.Image);}
 void Cleanup(){foreach(var card in cards.Values.Where(c=>!Needed(c.Index)).ToArray())Remove(card);}
 public void Select(int index,bool animate){
  if(images.Count==0||index<0||index>=images.Count)return;int previous=selected;selected=index;int stamp=++version;double ms=animate&&previous>=0&&SystemParameters.ClientAreaAnimation?Motion.Ms(280):0;easing=Motion.Shift();
  for(int offset=-radius;offset<=radius;offset++){int candidate=((index+offset)%images.Count+images.Count)%images.Count;if(!cards.ContainsKey(candidate))Make(candidate,Relative(candidate,index,images.Count));}
  foreach(var card in cards.Values.OrderBy(c=>Math.Abs(Relative(c.Index,index,images.Count))).ToArray()){
   int relative=Relative(card.Index,index,images.Count);bool current=relative==0;bool visible=style=="showcase"?Math.Abs(relative)<=1:Math.Abs(relative)<=3;
   double scale=style=="showcase"?(current?1:.46):(current?1:.84);double opacity=visible?(current?1:style=="showcase"?.6:.52):0;
   Tween(card.Move,TranslateTransform.XProperty,center+relative*step,ms);Tween(card.Scale,ScaleTransform.ScaleXProperty,scale,ms);Tween(card.Scale,ScaleTransform.ScaleYProperty,scale,ms);Tween(card.Border,OpacityProperty,opacity,ms);card.Border.BorderBrush=current?B("Accent"):B("SoftEdge");Panel.SetZIndex(card.Border,current?2:1);
   int width=style=="showcase"?960:256;if(card.WidthRequested!=width){card.WidthRequested=width;thumbnail(images[card.Index],card.Image,width);}
  }
  // Keep the pool bounded during uninterrupted key-repeat; outgoing cards are disposable.
  foreach(var card in cards.Values.Where(c=>!Needed(c.Index)).ToArray().Take(Math.Max(0,cards.Count-(2*radius+5))))Remove(card);
  if(style=="filmstrip"){
   number.Text=(index+1).ToString("00");if(ms>0&&previous!=index){numberSlide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(Relative(index,previous,images.Count)>=0?7:-7,0,TimeSpan.FromMilliseconds(ms)){EasingFunction=easing});number.BeginAnimation(OpacityProperty,new DoubleAnimation(.4,1,TimeSpan.FromMilliseconds(ms)));}else{numberSlide.BeginAnimation(TranslateTransform.YProperty,null);numberSlide.Y=0;number.BeginAnimation(OpacityProperty,null);number.Opacity=1;}
  }
  if(ms<=0)Cleanup();else{var completion=new DoubleAnimation(1,TimeSpan.FromMilliseconds(ms));completion.Completed+=delegate{if(stamp==version)Cleanup();};BeginAnimation(OpacityProperty,completion);}
 }
 public void Settle(){if(selected>=0)Select(selected,false);BeginAnimation(OpacityProperty,null);Opacity=1;}
 public void ReleaseImages(){foreach(var card in cards.Values){WallpaperPicker.ReleaseThumbnail(card.Image);card.WidthRequested=0;}}
 public void SetHint(bool holding){hint.Text=holding?"Release to apply  \u00b7  Esc to cancel":"Applies when browsing stops  \u00b7  Esc to cancel";}
}
