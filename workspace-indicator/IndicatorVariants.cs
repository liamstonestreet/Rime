using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

// Retained, small visual trees. Motion exists only while the indicator is on screen.
sealed class IndicatorVariants : Grid {
 readonly string style;readonly string[] names;readonly Canvas canvas=new Canvas();
 readonly Border[] cards;readonly TextBlock[] digits,labels,roles;readonly TranslateTransform[] moves;readonly ScaleTransform[] scales;
 readonly TextBlock ribbonNumber=new TextBlock(),ribbonLabel=new TextBlock(),ribbonKicker=new TextBlock(),ribbonGhost=new TextBlock();
 readonly TranslateTransform numberSlide=new TranslateTransform(),ghostSlide=new TranslateTransform(),railSlide=new TranslateTransform();
 readonly Border railMarker=new Border();IndicatorPalette palette;int selected=-1;int transition;IEasingFunction shiftEase;
 internal string StyleName {get{return style;}}
 internal static int Relative(int index,int selected,int count){int value=(index-selected+count)%count;return value>count/2?value-count:value;}
 internal int[] VisibleNeighbors {get{return Enumerable.Range(0,names.Length).Where(i=>Math.Abs(Relative(i,selected,names.Length))<=1).ToArray();}}
 public IndicatorVariants(string style,string[] names,string[] workspaceLabels){
  this.style=style;this.names=names;Width=style=="gallery"?720:532;Height=style=="gallery"?244:118;ClipToBounds=true;Children.Add(canvas);canvas.Width=Width;canvas.Height=Height;
  cards=new Border[names.Length];digits=new TextBlock[names.Length];labels=new TextBlock[names.Length];roles=new TextBlock[names.Length];moves=new TranslateTransform[names.Length];scales=new ScaleTransform[names.Length];
  if(style=="gallery")BuildGallery();else BuildRibbon();UpdateLabels(workspaceLabels);
 }
 static TextBlock Text(double size){return new TextBlock{FontSize=size,TextAlignment=TextAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};}
 void BuildGallery(){
  for(int i=0;i<names.Length;i++){
   var body=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14)};roles[i]=Text(10);roles[i].Margin=new Thickness(0,0,0,14);body.Children.Add(roles[i]);digits[i]=Text(72);digits[i].Text=names[i];digits[i].FontWeight=FontWeights.Light;body.Children.Add(digits[i]);
   var accent=new Border{Width=28,Height=1,Margin=new Thickness(0,10,0,13),Tag="accent"};body.Children.Add(accent);labels[i]=Text(13);labels[i].MaxWidth=175;body.Children.Add(labels[i]);
   cards[i]=new Border{Width=210,Height=220,CornerRadius=new CornerRadius(18),BorderThickness=new Thickness(1),Child=body,Opacity=0};moves[i]=new TranslateTransform();scales[i]=new ScaleTransform(.78,.78,105,110);var transform=new TransformGroup();transform.Children.Add(scales[i]);transform.Children.Add(moves[i]);cards[i].RenderTransform=transform;canvas.Children.Add(cards[i]);
  }
 }
 void BuildRibbon(){
  ribbonKicker.Text="WORKSPACE";ribbonKicker.FontSize=9;ribbonKicker.Margin=new Thickness(112,12,0,0);canvas.Children.Add(ribbonKicker);
  var numberBox=new Grid{Width=90,Height=86,ClipToBounds=true};Canvas.SetTop(numberBox,3);canvas.Children.Add(numberBox);
  foreach(var text in new[]{ribbonGhost,ribbonNumber}){text.FontSize=62;text.FontWeight=FontWeights.Light;text.HorizontalAlignment=HorizontalAlignment.Center;text.VerticalAlignment=VerticalAlignment.Center;numberBox.Children.Add(text);}ribbonGhost.Opacity=0;ribbonNumber.RenderTransform=numberSlide;ribbonGhost.RenderTransform=ghostSlide;
  ribbonLabel.FontSize=23;ribbonLabel.FontWeight=FontWeights.Medium;ribbonLabel.MaxWidth=400;ribbonLabel.TextTrimming=TextTrimming.CharacterEllipsis;Canvas.SetLeft(ribbonLabel,110);Canvas.SetTop(ribbonLabel,31);canvas.Children.Add(ribbonLabel);
  var line=new Border{Width=400,Height=1,Opacity=.4,Tag="divider"};Canvas.SetLeft(line,112);Canvas.SetTop(line,74);canvas.Children.Add(line);
  for(int i=0;i<names.Length;i++){digits[i]=Text(11);digits[i].Text=names[i];digits[i].Width=28;Canvas.SetLeft(digits[i],112+i*40);Canvas.SetTop(digits[i],88);canvas.Children.Add(digits[i]);}
  railMarker.Width=28;railMarker.Height=2;railMarker.CornerRadius=new CornerRadius(1);railMarker.RenderTransform=railSlide;Canvas.SetTop(railMarker,113);Canvas.SetLeft(railMarker,112);canvas.Children.Add(railMarker);
 }
 public void SetLabels(string[] values){for(int i=0;i<names.Length;i++)if(labels[i]!=null)labels[i].Text=values!=null&&i<values.Length&&!String.IsNullOrWhiteSpace(values[i])?values[i]:"Workspace "+names[i];if(selected>=0)UpdateRibbonLabel(values);}
 string[] savedLabels;
 public void UpdateLabels(string[] values){savedLabels=values;SetLabels(values);}
 void UpdateRibbonLabel(string[] values){if(style!="ribbon"||selected<0)return;ribbonLabel.Text=values!=null&&selected<values.Length&&!String.IsNullOrWhiteSpace(values[selected])?values[selected]:"Workspace "+names[selected];}
 static double Ms(bool instant){return instant||!SystemParameters.ClientAreaAnimation?0:Motion.Ms(300);}
 DoubleAnimation Move(double to,double duration){return new DoubleAnimation(to,TimeSpan.FromMilliseconds(duration)){EasingFunction=shiftEase};}
 void Tween(DependencyObject target,DependencyProperty property,double value,double ms){DoubleAnimation animation=ms<=0?null:Move(value,ms);var element=target as UIElement;if(element!=null)element.BeginAnimation(property,animation);else ((Animatable)target).BeginAnimation(property,animation);if(ms<=0)target.SetValue(property,value);}
 public void SetWorkspace(int index,bool instant){
  if(index<0||index>=names.Length)return;int previous=selected;if(previous==index&&!instant)return;selected=index;double ms=Ms(instant||previous<0);transition++;shiftEase=Motion.Shift();
  if(style=="gallery"){
   for(int i=0;i<names.Length;i++){int relative=Relative(i,index,names.Length);bool current=relative==0,neighbor=Math.Abs(relative)==1;roles[i].Text=current?"CURRENT":relative<0?"PREVIOUS":"NEXT";
    Tween(moves[i],TranslateTransform.XProperty,255+relative*220,ms);Tween(moves[i],TranslateTransform.YProperty,12,ms);Tween(scales[i],ScaleTransform.ScaleXProperty,current?1:.78,ms);Tween(scales[i],ScaleTransform.ScaleYProperty,current?1:.78,ms);Tween(cards[i],OpacityProperty,current?1:neighbor?.58:0,ms);Panel.SetZIndex(cards[i],current?2:neighbor?1:0);
   }
  }else{
   if(previous!=index){int version=transition;double offset=previous<0?0:Relative(index,previous,names.Length)>=0?22:-22;ribbonGhost.Text=ribbonNumber.Text;ribbonNumber.Text=names[index];numberSlide.BeginAnimation(TranslateTransform.YProperty,null);ghostSlide.BeginAnimation(TranslateTransform.YProperty,null);ribbonGhost.BeginAnimation(OpacityProperty,null);ribbonNumber.BeginAnimation(OpacityProperty,null);
    var movement=new DoubleAnimation(offset,0,TimeSpan.FromMilliseconds(ms)){EasingFunction=shiftEase};movement.Completed+=delegate{if(version==transition)ribbonGhost.Text="";};numberSlide.BeginAnimation(TranslateTransform.YProperty,movement);ghostSlide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-offset,TimeSpan.FromMilliseconds(ms)){EasingFunction=shiftEase});ribbonNumber.BeginAnimation(OpacityProperty,new DoubleAnimation(ms==0?1:0,1,TimeSpan.FromMilliseconds(ms)));ribbonGhost.BeginAnimation(OpacityProperty,new DoubleAnimation(ms==0?0:1,0,TimeSpan.FromMilliseconds(ms)));
   }Tween(railSlide,TranslateTransform.XProperty,index*40,ms);UpdateRibbonLabel(savedLabels);if(ms<=0)ClearNumberMotion();
  }
  Paint();
 }
 public void Apply(IndicatorPalette palette){this.palette=palette;Paint();}
 void Paint(){if(palette==null)return;
  for(int i=0;i<names.Length;i++){
   bool current=i==selected;digits[i].Foreground=current?palette.Marker:palette.Number;
   if(style=="gallery"){roles[i].Foreground=palette.Label;labels[i].Foreground=current?palette.Number:palette.Label;var tint=ThemeTransition.Tinted(palette.Marker,current?.09:.025);cards[i].Background=tint;cards[i].BorderBrush=current?palette.Marker:palette.TileBorder;foreach(var line in ((StackPanel)cards[i].Child).Children.OfType<Border>())line.Background=current?palette.Marker:palette.TileBorder;}
   else digits[i].Opacity=current?1:.45;
  }
  if(style=="ribbon"){ribbonKicker.Foreground=palette.Label;ribbonLabel.Foreground=palette.Number;ribbonNumber.Foreground=ribbonGhost.Foreground=palette.Marker;railMarker.Background=palette.Marker;foreach(var line in canvas.Children.OfType<Border>().Where(b=>Equals(b.Tag,"divider")))line.Background=palette.TileBorder;}
 }
 // Release WPF clocks after the fade; no animations or rendering callbacks live while hidden.
 void ClearNumberMotion(){numberSlide.BeginAnimation(TranslateTransform.YProperty,null);numberSlide.Y=0;ghostSlide.BeginAnimation(TranslateTransform.YProperty,null);ghostSlide.Y=0;ribbonNumber.BeginAnimation(OpacityProperty,null);ribbonNumber.Opacity=1;ribbonGhost.BeginAnimation(OpacityProperty,null);ribbonGhost.Opacity=0;ribbonGhost.Text="";}
 public void Settle(){if(selected>=0)SetWorkspace(selected,true);if(style=="ribbon")ClearNumberMotion();}
}
