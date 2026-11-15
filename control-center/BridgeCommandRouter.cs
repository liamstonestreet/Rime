using System;

enum BridgeCommandKind {Unknown,NotificationSync,NotificationOpen,Metadata,Ping,Subscribe,Widgets,Status,Appearance,WorkspacePins,FocusStatus,VpnStatus,VpnOpen,DiscordMute,DiscordStatus,DiscordClose,QuickAction,DiscordMenu,CenterPin,CenterHover,FocusOpen,TrayOpen,TrayWait,TrayClose,TaskOrder,TaskContext,WorkspaceRules,PinLaunch,Audio,TaskAction,TaskIcon,Power,PowerPreview}
sealed class BridgeCommand {
 public readonly BridgeCommandKind Kind;public readonly string Raw,Value;
 BridgeCommand(BridgeCommandKind kind,string raw,string value){Kind=kind;Raw=raw;Value=value;}
 static readonly string[] prefixes={"notification-sync:","notifications-open:","quick-action:","quick-discord-menu:","center-pin:","center-hover:","focus-open:","tray-open:","tray-wait:","tray-close:","task-order:","task-context:","workspace-rules:","pin-launch:","audio-","task-focus:","task-menu:","task-icon:","power-preview:","power:"};
 static readonly BridgeCommandKind[] kinds={BridgeCommandKind.NotificationSync,BridgeCommandKind.NotificationOpen,BridgeCommandKind.QuickAction,BridgeCommandKind.DiscordMenu,BridgeCommandKind.CenterPin,BridgeCommandKind.CenterHover,BridgeCommandKind.FocusOpen,BridgeCommandKind.TrayOpen,BridgeCommandKind.TrayWait,BridgeCommandKind.TrayClose,BridgeCommandKind.TaskOrder,BridgeCommandKind.TaskContext,BridgeCommandKind.WorkspaceRules,BridgeCommandKind.PinLaunch,BridgeCommandKind.Audio,BridgeCommandKind.TaskAction,BridgeCommandKind.TaskAction,BridgeCommandKind.TaskIcon,BridgeCommandKind.PowerPreview,BridgeCommandKind.Power};
 public static BridgeCommand Parse(string text){text=text??"";BridgeCommandKind kind;
  switch(text){case "ping":kind=BridgeCommandKind.Ping;break;case "subscribe":kind=BridgeCommandKind.Subscribe;break;case "widgets":kind=BridgeCommandKind.Widgets;break;case "status":kind=BridgeCommandKind.Status;break;case "appearance":kind=BridgeCommandKind.Appearance;break;case "workspace-pins":kind=BridgeCommandKind.WorkspacePins;break;case "focus-status":kind=BridgeCommandKind.FocusStatus;break;case "vpn-status":kind=BridgeCommandKind.VpnStatus;break;case "vpn-open":kind=BridgeCommandKind.VpnOpen;break;case "quick-discord-mute":kind=BridgeCommandKind.DiscordMute;break;case "quick-discord-status":kind=BridgeCommandKind.DiscordStatus;break;case "quick-discord-close":kind=BridgeCommandKind.DiscordClose;break;
   default:for(int i=0;i<prefixes.Length;i++)if(text.StartsWith(prefixes[i],StringComparison.Ordinal))return new BridgeCommand(kinds[i],text,text.Substring(prefixes[i].Length));return new BridgeCommand(text.TrimStart().StartsWith("{",StringComparison.Ordinal)?BridgeCommandKind.Metadata:BridgeCommandKind.Unknown,text,text);
  }return new BridgeCommand(kind,text,"");
 }
 public bool Coordinates(out int x,out int y){var parts=Value.Split(':');x=y=0;return parts.Length==2&&Int32.TryParse(parts[0],out x)&&Int32.TryParse(parts[1],out y);}
}
// Dispatch is injectable; parser/order tests cannot invoke desktop actions.
sealed class BridgeCommandRouter {
 readonly Func<BridgeCommand,string> execute;readonly Action<string> log;
 public BridgeCommandRouter(Func<BridgeCommand,string> execute,Action<string> log){this.execute=execute;this.log=log;}
 public string Receive(string payload){var command=BridgeCommand.Parse(payload);if(command.Kind==BridgeCommandKind.Unknown)return "{\"ok\":false}";try{return execute(command)??"{}";}catch(Exception ex){log("bridge command "+command.Kind+": "+ex.GetType().Name);return "{\"ok\":false}";}}
}
