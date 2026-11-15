using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

// Small expression grammar; no scripting engine, shell, network or dynamic code evaluation.
static class SpotlightCalculator {
 public static double Evaluate(string expression){
  if(String.IsNullOrWhiteSpace(expression)||expression.Length>160)throw new FormatException("Enter a calculation");
  expression=Regex.Replace(expression,@"([\d.]+)\s*%\s+of\s+", "($1/100)*",RegexOptions.IgnoreCase);
  var parser=new Parser(expression.Replace('\u00d7','*').Replace('\u00f7','/').Replace("\u03c0","pi"));
  double value=parser.Parse();if(Double.IsInfinity(value)||Double.IsNaN(value))throw new FormatException("Result is outside the supported range");return value;
 }
 public static string Format(double value){return (Math.Abs(value)<1e-13?0:value).ToString("G12",CultureInfo.InvariantCulture);}
 sealed class Parser {
  readonly string text;int at,depth;
  public Parser(string text){this.text=text;}
  void Space(){while(at<text.Length&&Char.IsWhiteSpace(text[at]))at++;}
  bool Take(char c){Space();if(at<text.Length&&text[at]==c){at++;return true;}return false;}
  public double Parse(){double value=Sum();Space();if(at!=text.Length)throw new FormatException("Unexpected input near "+text.Substring(at));return value;}
  double Sum(){double value=Product();while(true){if(Take('+'))value+=Product();else if(Take('-'))value-=Product();else return value;}}
  double Product(){double value=Unary();while(true){if(Take('*'))value*=Unary();else if(Take('/')){double divisor=Unary();if(divisor==0)throw new FormatException("Cannot divide by zero");value/=divisor;}else{Space();if(at<text.Length&&(text[at]=='('||Char.IsLetter(text[at])))value*=Unary();else return value;}}}
  double Unary(){if(Take('+'))return Unary();if(Take('-'))return -Unary();return Power();}
  double Power(){double value=Primary();while(true){if(Take('%'))value/=100;else if(Take('!')){if(value<0||value>170||value!=Math.Floor(value))throw new FormatException("Factorial needs a whole number from 0 to 170");double factorial=1;for(int i=2;i<=value;i++)factorial*=i;value=factorial;}else break;}return Take('^')?Math.Pow(value,Unary()):value;}
  double Primary(){
   if(++depth>32)throw new FormatException("Expression is too deeply nested");
   try{
    if(Take('(')){double value=Sum();if(!Take(')'))throw new FormatException("Missing closing parenthesis");return value;}
    Space();var number=Regex.Match(text.Substring(at),@"^(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?");
    if(number.Success){at+=number.Length;try{return Double.Parse(number.Value,CultureInfo.InvariantCulture);}catch(OverflowException){throw new FormatException("Number is too large");}}
    int start=at;while(at<text.Length&&Char.IsLetter(text[at]))at++;var name=text.Substring(start,at-start).ToLowerInvariant();
    if(name=="pi")return Math.PI;if(name=="e")return Math.E;
    if(name.Length==0)throw new FormatException("Expected a number");
    if(!Take('('))throw new FormatException("Unknown constant: "+name);
    var args=new List<double>{Sum()};while(Take(','))args.Add(Sum());if(!Take(')'))throw new FormatException("Missing closing parenthesis");
    if(args.Count==1){double x=args[0];switch(name){case "sqrt":if(x<0)throw new FormatException("Square root needs a non-negative number");return Math.Sqrt(x);case "abs":return Math.Abs(x);case "round":return Math.Round(x,MidpointRounding.AwayFromZero);case "floor":return Math.Floor(x);case "ceil":return Math.Ceiling(x);case "sin":return Math.Sin(x);case "cos":return Math.Cos(x);case "tan":return Math.Tan(x);case "ln":return Math.Log(x);case "log":return Math.Log10(x);case "exp":return Math.Exp(x);}}
    if(args.Count==2){switch(name){case "min":return Math.Min(args[0],args[1]);case "max":return Math.Max(args[0],args[1]);case "pow":return Math.Pow(args[0],args[1]);}}
    throw new FormatException("Unknown function or argument count: "+name);
   }finally{depth--;}
  }
 }
 sealed class Unit {public string Group;public double Factor,Offset;public Unit(string group,double factor,double offset=0){Group=group;Factor=factor;Offset=offset;}}
 static readonly Dictionary<string,Unit> units=new Dictionary<string,Unit>(StringComparer.OrdinalIgnoreCase){
  {"mm",new Unit("length",.001)},{"cm",new Unit("length",.01)},{"m",new Unit("length",1)},{"km",new Unit("length",1000)},{"in",new Unit("length",.0254)},{"inch",new Unit("length",.0254)},{"ft",new Unit("length",.3048)},{"yd",new Unit("length",.9144)},{"mi",new Unit("length",1609.344)},
  {"mg",new Unit("mass",.001)},{"g",new Unit("mass",1)},{"kg",new Unit("mass",1000)},{"oz",new Unit("mass",28.349523125)},{"lb",new Unit("mass",453.59237)},{"lbs",new Unit("mass",453.59237)},
  {"ms",new Unit("time",.001)},{"s",new Unit("time",1)},{"sec",new Unit("time",1)},{"min",new Unit("time",60)},{"h",new Unit("time",3600)},{"hr",new Unit("time",3600)},{"day",new Unit("time",86400)},{"days",new Unit("time",86400)},
  {"b",new Unit("data",1)},{"kb",new Unit("data",1e3)},{"mb",new Unit("data",1e6)},{"gb",new Unit("data",1e9)},{"tb",new Unit("data",1e12)},{"kib",new Unit("data",1024)},{"mib",new Unit("data",1048576)},{"gib",new Unit("data",1073741824)},
  {"c",new Unit("temperature",1)},{"f",new Unit("temperature",5.0/9,-32*5.0/9)},{"k",new Unit("temperature",1,-273.15)},
  {"deg",new Unit("angle",Math.PI/180)},{"rad",new Unit("angle",1)}
 };
 static SpotlightCalculator(){foreach(var pair in new[]{new[]{"meters","m"},new[]{"kilometers","km"},new[]{"miles","mi"},new[]{"feet","ft"},new[]{"foot","ft"},new[]{"inches","in"},new[]{"pounds","lb"},new[]{"seconds","s"},new[]{"minutes","min"},new[]{"hours","h"},new[]{"celsius","c"},new[]{"fahrenheit","f"}})units[pair[0]]=units[pair[1]];}
 public static bool Convert(string text,out string answer){
  answer=null;var match=Regex.Match(text.Replace("\u00b0",""),@"^(.+?)\s*([a-z]+)\s+(?:to|in)\s+([a-z]+)$",RegexOptions.IgnoreCase);if(!match.Success)return false;
  Unit from,to;if(!units.TryGetValue(match.Groups[2].Value,out from)||!units.TryGetValue(match.Groups[3].Value,out to))throw new FormatException("Unknown unit");
  if(from.Group!=to.Group)throw new FormatException("These units measure different things");
  double value=(Evaluate(match.Groups[1].Value)*from.Factor+from.Offset-to.Offset)/to.Factor;
  answer=Format(value)+" "+match.Groups[3].Value;return true;
 }
}
