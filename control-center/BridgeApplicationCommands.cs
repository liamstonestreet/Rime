using System;
using System.IO;

// Application adapters, separate from transport and command parsing. UI actions
// are posted rather than synchronously waiting on the dispatcher.
static class BridgeApplicationCommands {
 static void Post(Action action){System.Windows.Application.Current.Dispatcher.BeginInvoke(action);}
 public static string Execute(BridgeCommand command){string value=command.Value;switch(command.Kind){
  case BridgeCommandKind.NotificationSync:return NotificationCenter.Receive(value);
  case BridgeCommandKind.NotificationOpen:var notifications=MediaBridge.OpenNotifications;if(notifications!=null)Post(()=>notifications(value));return "{}";
  case BridgeCommandKind.Ping:case BridgeCommandKind.Subscribe:MediaBridge.Heartbeat();return "{}";
  case BridgeCommandKind.Widgets:MediaBridge.Heartbeat();var file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rice-widgets.json");return File.Exists(file)?File.ReadAllText(file):"{}";
  case BridgeCommandKind.Status:return MediaBridge.Metadata.Snapshot();
  case BridgeCommandKind.Metadata:return MediaBridge.Metadata.Receive(command.Raw);
  case BridgeCommandKind.Appearance:return BarAppearance.Snapshot();
  case BridgeCommandKind.WorkspacePins:return WorkspacePins.Snapshot();
  case BridgeCommandKind.FocusStatus:return FocusMode.Snapshot();
  case BridgeCommandKind.VpnStatus:return VpnStatus.Read();case BridgeCommandKind.VpnOpen:return VpnStatus.Open();
  case BridgeCommandKind.DiscordMute:return QuickActions.Voice("mute");case BridgeCommandKind.DiscordStatus:return QuickActions.Voice("status");case BridgeCommandKind.DiscordClose:return QuickActions.Close();
  case BridgeCommandKind.QuickAction:return QuickActions.Receive(value);case BridgeCommandKind.DiscordMenu:return QuickActions.Open(value);
  case BridgeCommandKind.CenterPin:var pin=MediaBridge.Pin;if(pin!=null)Post(()=>pin(value));return "{}";
  case BridgeCommandKind.CenterHover:var hover=MediaBridge.Hover;if(hover!=null)Post(()=>hover(value));return "{\"ok\":true}";
  case BridgeCommandKind.FocusOpen:var focus=MediaBridge.OpenFocus;if(focus!=null)Post(()=>focus(value));return "{}";
  case BridgeCommandKind.TrayOpen:return TrayPopup.Open(value);case BridgeCommandKind.TrayWait:return TrayPopup.Wait(value);case BridgeCommandKind.TrayClose:return TrayPopup.Close(value);
  case BridgeCommandKind.TaskOrder:return TaskPins.Order(value);case BridgeCommandKind.TaskContext:return TaskPins.Receive(value);
  case BridgeCommandKind.WorkspaceRules:return WorkspaceConfig.Receive(value);case BridgeCommandKind.PinLaunch:return WorkspacePins.Launch(value);
  case BridgeCommandKind.Audio:return BarAudio.Receive(command.Raw);case BridgeCommandKind.TaskAction:return TaskActions.Receive(command.Raw);case BridgeCommandKind.TaskIcon:return TaskIcons.Get(value);
  case BridgeCommandKind.Power:case BridgeCommandKind.PowerPreview:int x,y;if(!command.Coordinates(out x,out y))return "{\"ok\":false}";Post(()=>PowerMenu.Open(x,y,command.Kind==BridgeCommandKind.PowerPreview));return "{\"ok\":true}";
  default:return "{\"ok\":false}";
 }}
 public static string[] Initial(){var state=BarState.Initial();var frames=new string[state.Length+4];frames[0]=BarAppearance.FontSnapshot();frames[1]=BarAppearance.Snapshot();frames[2]=NotificationCenter.Badge();frames[3]=NotificationPopup.GapSnapshot();Array.Copy(state,0,frames,4,state.Length);return frames;}
}
