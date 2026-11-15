using System;

// Pure session transitions, with an explicit clock for deterministic checks.
static class FocusSession {
 internal static FocusState Start(FocusSettings settings,string goal,DateTime now){var state=new FocusState{settings=settings,goal=(goal??"").Trim()};Phase(state,"work",now,false);return state;}
 internal static void Phase(FocusState state,string phase,DateTime now,bool wait){state.phase=phase;state.paused=false;state.waiting=wait;state.remainingSeconds=state.totalSeconds=(phase=="break"?state.settings.breakMinutes:state.settings.defaultMinutes)*60.0;state.endsUtc=wait?null:now.AddSeconds(state.remainingSeconds).ToString("o");}
 internal static bool Advance(FocusState state,DateTime now){
  if(!state.Running||state.Remaining(now)>0)return false;
  if(state.phase=="work"){state.completedSessions++;Phase(state,"break",now,!state.settings.autoStartBreak);state.notice="Work complete. Time for a break.";}
  else{Phase(state,"work",now,true);state.notice="Break complete. Ready when you are.";}return true;
 }
 internal static void Pause(FocusState state,DateTime now){if(!state.Running)return;state.remainingSeconds=state.Remaining(now);state.endsUtc=null;state.paused=true;state.notice="Paused. Your hidden windows are restored.";}
 internal static void Resume(FocusState state,DateTime now){if(!state.HasSession||state.Running)return;state.paused=state.waiting=false;state.endsUtc=now.AddSeconds(state.remainingSeconds).ToString("o");state.notice="";}
 internal static void Extend(FocusState state,DateTime now){if(!state.HasSession)return;state.remainingSeconds=state.Remaining(now)+300;state.totalSeconds+=300;if(state.Running)state.endsUtc=now.AddSeconds(state.remainingSeconds).ToString("o");}
}
