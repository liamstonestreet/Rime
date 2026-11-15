using System;
using System.IO;
using System.Web.Script.Serialization;
public class FocusSettings {
 public int defaultMinutes=45;
 public int breakMinutes=10;
 public bool autoStartBreak=true;
 public string[] blockedProcesses={"steam"};
 public string[] hiddenProcesses={"steam","steamwebhelper"};
 public string[] disabledWorkspaces={"6"};
 public string fallbackWorkspace="1";
 public string edge="#315C45",background="#151816",accent="#81A990";
}
public class HiddenFocusWindow {public long handle;public uint pid;public string processKey;}
public class FocusState {
 public HiddenFocusWindow[] hiddenWindows=new HiddenFocusWindow[0];
 public string endsUtc;
 public FocusSettings settings=new FocusSettings();
 public string[] originals=new string[0],applied=new string[0],baseline=new string[0];
 public string phase,goal="",notice="";
 public bool paused,waiting;
 public double remainingSeconds,totalSeconds;
 public int completedSessions;
 public bool HasSession {get{return phase=="work"||phase=="break";}}
 public bool Running {get{return HasSession&&!paused&&!waiting;}}
 public double Remaining(DateTime now){DateTime end;return Running&&DateTime.TryParse(endsUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out end)?Math.Max(0,(end.ToUniversalTime()-now.ToUniversalTime()).TotalSeconds):Math.Max(0,remainingSeconds);}
 public bool Active {get {if(HasSession)return phase=="work"&&Running&&Remaining(DateTime.UtcNow)>0;DateTime end;return DateTime.TryParse(endsUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out end)&&end.ToUniversalTime()>DateTime.UtcNow;}}
 // Only an old session being recovered may use the former theme/config restrictions.
 public bool ThemeActive {get{return !HasSession&&Active;}}
 public static FocusState Read(){try{return new JavaScriptSerializer().Deserialize<FocusState>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"focus-state.json")))??new FocusState();}catch{return new FocusState();}}
}
