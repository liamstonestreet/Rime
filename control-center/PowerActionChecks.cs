using System;
using System.Collections.Generic;
using System.Linq;

static class PowerActionChecks {
 static void CheckColorOwnership(){
  using(var controller=new PowerMenuController(new Fake())){
   var get=typeof(PowerMenuController).GetMethod("B",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
   System.Windows.Media.Brush previous=null;
   for(int pass=0;pass<3;pass++){
    Exception failure=null;System.Windows.Media.Brush current=null;
    var thread=new System.Threading.Thread(()=>{try{current=(System.Windows.Media.Brush)get.Invoke(controller,new object[]{"#88C0D0"});if(!current.Dispatcher.CheckAccess())throw new Exception("Power brush belongs to another thread");}catch(Exception error){failure=error;}});
    thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.Start();thread.Join();
    if(failure!=null)throw new Exception("Repeated power menu thread ownership regression",failure);
    if(Object.ReferenceEquals(previous,current))throw new Exception("Power menu reused a brush across UI threads");previous=current;
   }
  }
  Console.WriteLine("PASS: repeated power-menu STA sessions rebuild thread-owned colors. No windows shown or power actions executed.");
 } internal sealed class Fake:IPowerActions {public readonly List<string> Calls=new List<string>();public void Execute(string action){Calls.Add(action);}}
 public static void Run(){CheckColorOwnership();var fake=new Fake();var dispatcher=new PowerActionDispatcher(fake);foreach(string action in PowerMenu.Actions)dispatcher.Execute(action,true);dispatcher.Execute(null,false);if(fake.Calls.Count!=0)throw new Exception("Preview/dismiss executed a power action");foreach(string action in PowerMenu.Actions)dispatcher.Execute(action,false);if(!fake.Calls.SequenceEqual(PowerMenu.Actions))throw new Exception("Power action mapping changed");bool rejected=false;try{dispatcher.Execute("invalid",false);}catch(ArgumentException){rejected=true;}if(!rejected||fake.Calls.Count!=5)throw new Exception("Invalid power action reached native adapter");Console.WriteLine("PASS: injected shutdown/restart/sleep/screensaver/lock routing, preview/dismiss suppression and invalid action rejection. Fake actions only.");}
}
