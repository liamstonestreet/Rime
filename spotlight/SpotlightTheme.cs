using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;

static class SpotlightTheme {
 sealed class Tone:INotifyPropertyChanged {
  public Color Original,From,To;public string Role;Color value;
  public Color Value {get{return value;}set{this.value=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Value"));}}
  public event PropertyChangedEventHandler PropertyChanged;
 }
 static readonly Dictionary<string,Tone> tones=new Dictionary<string,Tone>();
 static readonly Dictionary<string,SolidColorBrush> brushes=new Dictionary<string,SolidColorBrush>();
 public static SolidColorBrush Brush(string hex){
  SolidColorBrush brush;if(brushes.TryGetValue(hex,out brush))return brush;
  var c=(Color)ColorConverter.ConvertFromString(hex);string key=hex.ToUpperInvariant(),role;
  switch(key){case "#D8DEE9":role="--nord-4";break;case "#ECEFF4":role="--nord-6";break;case "#88C0D0":case "#507A9CAB":case "#306D8997":role="--nord-8";break;case "#96A7BA":role="--muted-2";break;case "#4C566A":role="--nord-3";break;case "#507F9BA8":case "#465465":role="--edge";break;default:role="--nord-0";break;}
  var tone=new Tone{Original=c,From=c,To=c,Role=role,Value=c};tones[hex]=tone;
  brush=new SolidColorBrush();BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding("Value"){Source=tone});brushes[hex]=brush;return brush;
 }
 public static void Start(){Brush("#2E3440");ThemeTransition.Began+=Begin;ThemeTransition.Frame+=Paint;ThemeTransition.Listen();if(ThemeTransition.Current==null)Set(WallpaperPalette.Load());}
 static void Set(Dictionary<string,string> palette){foreach(var tone in tones.Values){tone.From=tone.Value;var c=WallpaperPalette.Get(palette,tone.Role,tone.Original.ToString());tone.To=Color.FromArgb(tone.Original.A,c.R,c.G,c.B);}Paint();}
 static void Begin(){Set(ThemeTransition.Current.to);}
 static void Paint(){foreach(var tone in tones.Values)tone.Value=ThemeTransition.Mix(tone.From,tone.To,ThemeTransition.Position);}
}
