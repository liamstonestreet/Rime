// Identity is independent of the current visual column order.
enum ControlCenterTab {Menu=0,Audio=1,Configure=2,Focus=4}
sealed class ControlCenterTabDefinition {
 public readonly ControlCenterTab Id;public readonly string Title;
 public ControlCenterTabDefinition(ControlCenterTab id,string title){Id=id;Title=title;}
 public static readonly ControlCenterTabDefinition[] All={
  new ControlCenterTabDefinition(ControlCenterTab.Menu,"Menu"),new ControlCenterTabDefinition(ControlCenterTab.Audio,"Audio"),
  new ControlCenterTabDefinition(ControlCenterTab.Configure,"Configure"),new ControlCenterTabDefinition(ControlCenterTab.Focus,"Focus")};
 public static int Index(ControlCenterTab id){for(int i=0;i<All.Length;i++)if(All[i].Id==id)return i;throw new System.ArgumentOutOfRangeException("id");}
}
