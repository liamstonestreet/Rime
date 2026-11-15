using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

// UI-thread owner of CC animation clocks, completion revisions and temporary frame
// tracking. The window supplies geometry/render actions rather than service state.
sealed class PanelAnimationController:IDisposable {
 readonly Window owner;readonly Border surface;readonly TranslateTransform slide;readonly Action<bool> updateShape;
 int heightRevision,tabRevision,visibilityRevision;bool tracking,disposed;DateTime shapeUntil;DispatcherTimer hideDelay;
 FrameworkElement[] pages;Border underline;
 public PanelAnimationController(Window owner,Border surface,TranslateTransform slide,Action<bool> updateShape){this.owner=owner;this.surface=surface;this.slide=slide;this.updateShape=updateShape;}
 internal bool IsTracking {get{return tracking;}}
 internal bool HasPendingHide {get{return hideDelay!=null;}}
 DoubleAnimation Ease(double to,TimeSpan time){return DisplayCadence.At(owner,new DoubleAnimation(to,time){EasingFunction=Motion.Shift()});}
 public void TrackShape(double ms){if(disposed)return;var until=DateTime.UtcNow.AddMilliseconds(ms+50);if(until>shapeUntil)shapeUntil=until;updateShape(false);if(!tracking){tracking=true;CompositionTarget.Rendering+=ShapeFrame;}}
 void ShapeFrame(object sender,EventArgs e){updateShape(false);if(DateTime.UtcNow>shapeUntil){StopTracking();updateShape(true);}}
 void StopTracking(){if(tracking){CompositionTarget.Rendering-=ShapeFrame;tracking=false;}}
 public void ResizeHeight(double target,double maxHeight,bool animate,double ms,double edge){
  if(disposed)return;
  double start=Math.Min(surface.Height,maxHeight);int revision=++heightRevision;
  surface.BeginAnimation(FrameworkElement.HeightProperty,null);surface.Height=target;
  if(!animate||!owner.IsVisible||ms==0||Math.Abs(start-target)<.1){owner.Height=target+edge;updateShape(false);return;}
  owner.Height=Math.Max(start,target)+edge;
  var motion=DisplayCadence.At(owner,new DoubleAnimation(start,target,TimeSpan.FromMilliseconds(ms)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop});
  motion.Completed+=delegate{if(disposed||revision!=heightRevision)return;surface.BeginAnimation(FrameworkElement.HeightProperty,null);surface.Height=target;owner.Height=target+edge;};
  surface.BeginAnimation(FrameworkElement.HeightProperty,motion);TrackShape(ms);
 }
 public void FreezeHeight(double edge){if(disposed)return;double current=surface.Height;++heightRevision;surface.BeginAnimation(FrameworkElement.HeightProperty,null);surface.Height=current;owner.Height=current+edge;}
 public void ResizeUnderline(Border line,int selected,int count,double width){if(disposed)return;underline=line;double target=width/count;var time=Motion.Time(140);double previous=Double.IsNaN(line.Width)?target:line.Width;line.BeginAnimation(FrameworkElement.WidthProperty,null);line.Width=target;line.BeginAnimation(FrameworkElement.WidthProperty,DisplayCadence.At(owner,new DoubleAnimation(previous,target,time){EasingFunction=Motion.Shift()}));var offset=(TranslateTransform)line.RenderTransform;offset.BeginAnimation(TranslateTransform.XProperty,DisplayCadence.At(owner,new DoubleAnimation(Double.IsNaN(offset.X)?selected*target:offset.X,selected*target,time){EasingFunction=Motion.Shift()}));}
 public void SwitchTab(FrameworkElement[] frames,int from,int to,Border line,double rowWidth,bool animate,double ms){
  if(disposed)return;
  pages=frames;underline=line;int revision=++tabRevision;var time=TimeSpan.FromMilliseconds(animate?ms:0);
  double width=Math.Max(1,frames[to].ActualWidth>0?frames[to].ActualWidth:frames[from].ActualWidth),direction=to>from?1:-1;
  for(int j=0;j<frames.Length;j++)if(j!=to&&j!=from){Move(frames[j],0,1,TimeSpan.Zero,null);frames[j].Visibility=Visibility.Collapsed;}
  var incoming=frames[to];incoming.Visibility=Visibility.Visible;
  // Remove a previous incoming/outgoing clock before setting its new base value.
  Move(incoming,direction*width,0,TimeSpan.Zero,null);Move(incoming,0,1,time,null);
  var outgoing=frames[from];Move(outgoing,-direction*width,0,time,()=>{if(!disposed&&revision==tabRevision){outgoing.Visibility=Visibility.Collapsed;Move(outgoing,0,1,TimeSpan.Zero,null);}});
  var offset=(TranslateTransform)line.RenderTransform;offset.BeginAnimation(TranslateTransform.XProperty,animate?Ease(to*rowWidth/frames.Length,time):null);if(!animate)offset.X=to*rowWidth/frames.Length;
 }
 void Move(FrameworkElement page,double x,double opacity,TimeSpan time,Action done){var offset=(TranslateTransform)page.RenderTransform;
  if(time==TimeSpan.Zero){offset.BeginAnimation(TranslateTransform.XProperty,null);offset.X=x;page.BeginAnimation(UIElement.OpacityProperty,null);page.Opacity=opacity;if(done!=null)done();return;}
  var move=Ease(x,time);if(done!=null)move.Completed+=delegate{if(!disposed)done();};offset.BeginAnimation(TranslateTransform.XProperty,move);page.BeginAnimation(UIElement.OpacityProperty,Ease(opacity,time));
 }
 public void AnimateVertical(double target,double ms,EasingMode easing,Action done){
  if(disposed)return;
  CancelHideDelay();int revision=++visibilityRevision;
  bool fade=BarAppearance.Settings.centerAnimation=="fade",hide=target<0;
  owner.BeginAnimation(UIElement.OpacityProperty,null);
  if(fade){slide.BeginAnimation(TranslateTransform.YProperty,null);slide.Y=0;updateShape(true);
   var opacity=DisplayCadence.At(owner,new DoubleAnimation(hide?1:0,hide?0:1,Motion.Time(ms)));
   opacity.Completed+=delegate{if(!disposed&&revision==visibilityRevision&&done!=null)done();};owner.BeginAnimation(UIElement.OpacityProperty,opacity);return;
  }
  owner.Opacity=1;
  var motion=DisplayCadence.At(owner,new DoubleAnimation(slide.Y,target,Motion.Time(ms)){EasingFunction=new CubicEase{EasingMode=easing}});
  motion.Completed+=delegate{if(!disposed&&revision==visibilityRevision&&done!=null)done();};slide.BeginAnimation(TranslateTransform.YProperty,motion);TrackShape(Motion.Ms(ms));
 }
 public void DelayHide(Action done){if(disposed)return;CancelHideDelay();int revision=visibilityRevision;hideDelay=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};hideDelay.Tick+=delegate{CancelHideDelay();if(!disposed&&revision==visibilityRevision)done();};hideDelay.Start();}
 void CancelHideDelay(){if(hideDelay!=null){hideDelay.Stop();hideDelay=null;}}
 public void Dispose(){if(disposed)return;disposed=true;++heightRevision;++tabRevision;++visibilityRevision;CancelHideDelay();StopTracking();surface.BeginAnimation(FrameworkElement.HeightProperty,null);slide.BeginAnimation(TranslateTransform.YProperty,null);
  if(pages!=null)foreach(var page in pages)Move(page,0,1,TimeSpan.Zero,null);
  if(underline!=null){underline.BeginAnimation(FrameworkElement.WidthProperty,null);((TranslateTransform)underline.RenderTransform).BeginAnimation(TranslateTransform.XProperty,null);}
 }
}
