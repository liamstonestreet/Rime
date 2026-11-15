using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

static class SpotlightChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Number(string text,double expected){Check(Math.Abs(SpotlightCalculator.Evaluate(text)-expected)<Math.Max(1,Math.Abs(expected))*1e-10,"Calculator failed: "+text);}
 static void Invalid(string text){try{SpotlightCalculator.Evaluate(text);}catch(FormatException){return;}throw new Exception("Invalid calculation accepted: "+text);}
 public static void Run(){AppShortcutsChecks.Run();WorkspaceBatchChecks.Run();
  Number("2+3*4",14);Number("(2+3)*4",20);Number("2^3^2",512);Number("-2^2",-4);Number("2^-2",.25);Number("20% of 150",30);Number("sqrt(144)+abs(-8)",20);Number("cos(pi)",-1);Number("2pi",2*Math.PI);Number("2(3+4)",14);Number("5!",120);Number("1e3/4",250);Number("max(2, min(5,3))",3);Number("0.1+0.2",.3);
  Invalid("1/0");Invalid("sqrt(-1)");Invalid("1e999");Invalid("9^999");Invalid("2 +");Invalid("System.Diagnostics.Process.Start(\"calc\")");Invalid("(-1)!");
  string answer;Check(SpotlightCalculator.Convert("5 km to m",out answer)&&answer=="5000 m","Length conversion");Check(SpotlightCalculator.Convert("32 f to c",out answer)&&answer=="0 c","Temperature conversion");Check(SpotlightCalculator.Convert("1 GiB to MiB",out answer)&&answer=="1024 MiB","Binary byte conversion");Check(SpotlightCalculator.Convert("2 h to min",out answer)&&answer=="120 min","Time conversion");Check(SpotlightCalculator.Format(.1+.2)=="0.3","Result formatting");
  Console.WriteLine("PASS: precedence, powers, percentages, scientific notation, functions, implicit multiplication, factorials, invalid inputs and unit conversions.");
  var apps=new[]{new AppEntry{Name="Spotify",AppID="test-spotify"},new AppEntry{Name="Visual Studio Code",AppID="test-code"}};var windows=new[]{SpotlightSearch.Result("Notes - editor","Editor","window",Guid.NewGuid().ToString())};
  Check(SpotlightSearch.Quick("spot",apps,windows)[0].App==apps[0],"App search");Check(SpotlightSearch.Quick("/apps studio",apps,windows).Single().App==apps[1],"App slash command");Check(SpotlightSearch.Quick("sqrt(144)",apps,windows)[0].Value=="12","Automatic calculator");Check(SpotlightSearch.Quick("/calc 1/0",apps,windows)[0].Kind=="hint","Calculation error row");Check(SpotlightSearch.Quick("/calc 1e999",apps,windows)[0].Kind=="hint","Large number error row");
  Check(SpotlightSearch.Quick("/openall",apps,windows).Single().Kind=="openall"&&SpotlightSearch.Quick("/closeall",apps,windows).Single().Kind=="closeall"&&SpotlightSearch.Quick("/launch",apps,windows).Single().Kind=="openall","Batch command discovery and legacy launch alias");Check(SpotlightSearch.Quick("/help",apps,windows).Length>=15,"Command discovery");Check(SpotlightSearch.Quick("/cal",apps,windows)[0].Value=="/calc ","Command completion");Check(SpotlightSearch.Quick("/windows notes",apps,windows).Single().Name=="Notes - editor","Window search");Check(SpotlightSearch.Quick("/settings sound",apps,windows).Single().Value=="ms-settings:sound","Settings shortcut");Check(SpotlightSearch.Quick("/copy hello",apps,windows)[0].Value=="hello","Copy command");Check(SpotlightSearch.Quick("example.com",apps,windows)[0].Value=="https://example.com","URL routing");Check(SpotlightSearch.Quick("/whoami",apps,windows).Length==0,"Unknown commands should not execute");Check(SpotlightSearch.Web("a & b").Value.EndsWith("a%20%26%20b"),"Web query escaping");
  string term;bool folders;Check(SpotlightSearch.FileQuery("/folders project",out term,out folders)&&folders&&term=="project","Folder routing");Check(SpotlightSearch.FileQuery("2026-report",out term,out folders),"Numbered file names");Check(!SpotlightSearch.FileQuery("/settings",out term,out folders),"Commands must not scan files");
  Check(NordSpotlight.Score("Spotify","spot")==80&&NordSpotlight.Score("Visual Studio Code","studio code")==60,"Existing app matching changed");
  Console.WriteLine("PASS: app ranking, command discovery/completion, math/error rows, files/folders/windows routing, settings, URLs and escaped web searches.");
  var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"search-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var document=Path.Combine(root,"report with spaces.txt");File.WriteAllText(document,"fixture");Directory.CreateDirectory(Path.Combine(root,"Projects"));Directory.CreateDirectory(Path.Combine(root,"node_modules"));File.WriteAllText(Path.Combine(root,"node_modules","hidden-dependency.txt"),"fixture");
   var scanned=SpotlightFiles.CheckScan(root);Check(scanned.Any(e=>e.Path==document)&&scanned.Any(e=>e.Folder&&e.Path.EndsWith("Projects"))&&!scanned.Any(e=>e.Path.Contains("node_modules")),"Bounded scanner skips dependency trees");
   Check(SpotlightFiles.Search(document,false,CancellationToken.None).GetAwaiter().GetResult().Single().Value==document,"Direct paths with spaces");
   var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{SpotlightFiles.Search(document,false,cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Cancelled file query was accepted");
  }finally{if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
  Console.WriteLine("PASS: actual file scanning, dependency exclusions, direct paths and cancellation. No apps, windows, settings or clipboard actions executed.");
  var preview=new NordSpotlight();var query=(TextBox)typeof(NordSpotlight).GetField("query",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(preview);query.Text="/help";
  var visual=(FrameworkElement)preview.Content;visual.Measure(new Size(720,double.PositiveInfinity));visual.Arrange(new Rect(visual.DesiredSize));visual.UpdateLayout();
  var list=(ListBox)typeof(NordSpotlight).GetField("results",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(preview);
  var viewer=(ScrollViewer)list.Template.FindName("PART_ScrollViewer",list);var scrollbar=(System.Windows.Controls.Primitives.ScrollBar)viewer.Template.FindName("PART_VerticalScrollBar",viewer);
  Check(scrollbar.Visibility==Visibility.Visible&&Math.Abs(scrollbar.ActualWidth-6)<.1,"Spotlight must use the shared six-pixel scrollbar");
  var presenter=(FrameworkElement)viewer.Template.FindName("PART_ScrollContentPresenter",viewer);var width=presenter.ActualWidth;
  query.Text="/calc 2+2";visual.Measure(new Size(720,double.PositiveInfinity));visual.Arrange(new Rect(visual.DesiredSize));visual.UpdateLayout();Check(Math.Abs(presenter.ActualWidth-width)<.1,"Spotlight scrollbar must not change result width");
  Console.WriteLine("PASS: shared slim scrollbar and stable result width with and without overflow.");
 }
}
