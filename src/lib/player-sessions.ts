import type { MediaOutput, MediaSession } from "zebar";

export type PlayerSource = "spotify" | "helium";

export function matchesPlayer(sessionId: string, source: PlayerSource): boolean {
  // Zebar uses Windows' SourceAppUserModelId as sessionId.
  // Helium's observed ID is Helium.PYOTQSRM7YDPUMRDQRLDNSDQUI.
  // Match only Helium: never commandeer Edge/Chrome or another active player.
  return source === "spotify"
    ? /^spotify(?:\.exe$|$|ab\.|[.!])/i.test(sessionId)
    : /^helium(?:\.|!|$)/i.test(sessionId);
}

export function selectPlayerSession(
  output: MediaOutput | null | undefined,
  source: PlayerSource,
): MediaSession | undefined {
  const sessions = output?.allSessions ?? [];
  const candidates = sessions.filter(session =>
    matchesPlayer(session.sessionId, source) &&
    !!(session.title?.trim() || session.artist?.trim()) &&
    // Suppress browser thumbnail previews, retaining live streams with no end time.
    !(source === "helium" && session.endTime > 0 && session.endTime - session.startTime <= 10)
  );
  return candidates.find(session => session.isPlaying) ?? candidates[0];
}
