using System;
static class PowerMenu {
 public static readonly string[] Actions={"Shutdown","Restart","Sleep","Screensaver","Lock"};
 static readonly PowerMenuController controller=new PowerMenuController(new WindowsPowerActions());
 public static bool IsOpen {get{return controller.IsOpen;}}
 public static void Open(int x,int y,bool test=false){controller.Open(x,y,test);}
 public static void Run(string[] args){controller.Run(args);}
 public static string SaverPath(){return WindowsPowerActions.SaverPath();}
 public static void Stop(){controller.Dispose();}
}
