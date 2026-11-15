using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

sealed class WorkspaceLaunchWindow {public string Id,Process,Title,AppId;public long Handle;public DateTime StartedUtc;}
sealed class WorkspaceLaunchMove {public string WindowId,Workspace,RuleStamp;public string[] LaunchPeers;}

// Decides only on new window identities. Config edits, title changes and focus
// changes cannot reassign a known window. Managed windows across all workspaces
// define running instances; tray-only background processes do not block routing.
sealed class WorkspaceLaunchPolicy {
 WorkspaceLaunchWindow[] previous;
 sealed class Pending {public DateTime Created;public HashSet<string> Rules,Cohort;}
 readonly Dictionary<string,Pending> pending=new Dictionary<string,Pending>();
 internal static readonly TimeSpan IdentityAllowance=TimeSpan.FromSeconds(8);
 internal static string RuleKey(WorkspaceApp r){return String.Join("|",new[]{r.id,r.workspace,r.processPattern,r.titlePattern,r.windowAppId});}
 internal bool HasPending {get{return pending.Count>0;}}
 static string Name(string value){return Regex.Replace(value??"",@"\.exe$","",RegexOptions.IgnoreCase).ToLowerInvariant();}
 internal static bool Matches(WorkspaceApp rule,WorkspaceLaunchWindow window){return !String.IsNullOrEmpty(rule.windowAppId)?String.Equals(rule.windowAppId,window.AppId,StringComparison.OrdinalIgnoreCase)&&WorkspaceApp.Match(Name(window.Process),rule.processPattern):rule.Matches(Name(window.Process),window.Title);}
 public WorkspaceLaunchMove[] Observe(WorkspaceLaunchWindow[] windows,WorkspaceApp[] rules,bool enabled,DateTime now){
  if(previous==null){previous=windows.Where(w=>w.StartedUtc==default(DateTime)||now-w.StartedUtc<TimeSpan.Zero||now-w.StartedUtc>TimeSpan.FromMinutes(2)).ToArray();}
  var known=new HashSet<string>(previous.Select(w=>w.Id));var moves=new List<WorkspaceLaunchMove>();
  var alive=new HashSet<string>(windows.Select(w=>w.Id));foreach(var id in pending.Keys.ToArray())if(!alive.Contains(id)||now-pending[id].Created>IdentityAllowance)pending.Remove(id);
  if(enabled)foreach(var window in windows.Where(w=>!known.Contains(w.Id))){var candidates=rules.Where(r=>String.IsNullOrWhiteSpace(window.Process)||WorkspaceApp.Match(Name(window.Process),r.processPattern)).Select(RuleKey).ToArray();if(candidates.Length>0)pending[window.Id]=new Pending{Created=now,Rules=new HashSet<string>(candidates),Cohort=new HashSet<string>(windows.Where(w=>!known.Contains(w.Id)).Select(w=>w.Id))};}
  foreach(var window in windows.Where(w=>pending.ContainsKey(w.Id)).ToArray()){
   var rule=rules.Where(r=>pending[window.Id].Rules.Contains(RuleKey(r))&&Matches(r,window)).OrderByDescending(r=>!String.IsNullOrEmpty(r.windowAppId)).ThenByDescending(r=>!String.IsNullOrEmpty(r.titlePattern)).FirstOrDefault();if(rule==null)continue;var cohort=pending[window.Id].Cohort;pending.Remove(window.Id);
   bool other=windows.Any(w=>w.Id!=window.Id&&!cohort.Contains(w.Id)&&Matches(rule,w));
   bool first=!other;
   if(enabled&&first&&Regex.IsMatch(rule.workspace??"",@"^[A-Za-z0-9_-]+$")){Guid id;if(Guid.TryParse(window.Id,out id))moves.Add(new WorkspaceLaunchMove{WindowId=id.ToString(),Workspace=rule.workspace,RuleStamp=RuleKey(rule),LaunchPeers=windows.Where(w=>cohort.Contains(w.Id)&&Matches(rule,w)).Select(w=>w.Id).ToArray()});}
  }
  previous=windows;return moves.ToArray();
 }
}
