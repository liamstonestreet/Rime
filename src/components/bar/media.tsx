import { packFile } from '../../lib/pack';
import { createEffect, createMemo, createSignal, onCleanup, onMount, Show } from "solid-js";
import { useProviders } from "../../lib/providers-context";
import { selectPlayerSession, type PlayerSource } from "../../lib/player-sessions";
import "./media.css";
import { shellExec } from "zebar";

function SourceIcon(props: { source: PlayerSource }) {
  return (
    <svg viewBox="0 0 24 24" class="player-source-icon" aria-hidden="true">
      <Show when={props.source === "spotify"} fallback={
        <>
          <circle cx="12" cy="12" r="3" fill="currentColor" />
          <ellipse cx="12" cy="12" rx="9" ry="4.5" fill="none" stroke="currentColor" stroke-width="1.6" transform="rotate(-35 12 12)" />
          <ellipse cx="12" cy="12" rx="9" ry="4.5" fill="none" stroke="currentColor" stroke-width="1.6" transform="rotate(55 12 12)" />
        </>
      }>
        <circle cx="12" cy="12" r="10" fill="currentColor" />
        <g fill="none" stroke="var(--bg, var(--nord-0))" stroke-linecap="round">
          <path d="M6 9c4-1.2 8-1 12 1" stroke-width="1.8" />
          <path d="M7 12c3.4-.9 6.6-.6 9.8 1" stroke-width="1.6" />
          <path d="M8 15c2.6-.6 5-.3 7.5.9" stroke-width="1.4" />
        </g>
      </Show>
    </svg>
  );
}

function SkipIcon(props: { next?: boolean }) {
  return <svg viewBox="0 0 24 24" aria-hidden="true" style={{ transform: props.next ? undefined : "rotate(180deg)" }}>
    <path d="m6 5 10 7-10 7Z" fill="currentColor" />
    <path d="M18 5v14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
  </svg>;
}

export function Player(props: { source: PlayerSource; label: string }) {
  const { media } = useProviders();
  const session = createMemo(() => selectPlayerSession(media(), props.source));
  const [busy, setBusy] = createSignal(false);
  const [opening, setOpening] = createSignal(false);
  const [error, setError] = createSignal("");
  const [position, setPosition] = createSignal(0);
  let sampledAt = performance.now();
  let sampledPosition = 0;
  let errorTimer: ReturnType<typeof setTimeout> | undefined;

  createEffect(() => {
    const current = session();
    sampledPosition = current?.position ?? 0;
    sampledAt = performance.now();
    setPosition(sampledPosition);
  });
  onMount(() => {
    const timer = setInterval(() => {
      const current = session();
      if (current?.isPlaying) {
        setPosition(sampledPosition + (performance.now() - sampledAt) / 1000);
      }
    }, 1000);
    onCleanup(() => clearInterval(timer));
  });
  onCleanup(() => clearTimeout(errorTimer));

  const progress = () => {
    const current = session();
    if (!current || current.endTime <= current.startTime) return 0;
    return Math.min(1, Math.max(0, (position() - current.startTime) / (current.endTime - current.startTime)));
  };
  const fullTitle = () => {
    const current = session();
    return current
      ? `${props.label} · ${current.title || "Untitled"}${current.artist ? ` — ${current.artist}` : ""}`
      : `${props.label} · No media session`;
  };
  const command = async (action: "previous" | "togglePlayPause" | "next") => {
    const current = session();
    const provider = media();
    if (!provider || !current || busy()) return;
    setBusy(true);
    setError("");
    try {
      // Always target this player's session, never Windows' current/default player.
      await provider[action]({ sessionId: current.sessionId });
    } catch (cause) {
      setError("Control unavailable");
      console.error(`${props.label} media control failed`, cause);
      clearTimeout(errorTimer);
      errorTimer = setTimeout(() => setError(""), 3000);
    } finally {
      setBusy(false);
    }
  };
  const openApp = async () => {
    if (opening()) return;
    setOpening(true);
    try {
      const result = await shellExec("powershell.exe", [
        "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden",
        "-ExecutionPolicy", "Bypass", "-File",
        packFile("open-player.ps1"),
        "-Player", props.source,
      ]);
      if (result.code !== 0) throw new Error(String(result.stderr));
    } catch (cause) {
      setError("Couldn't open app");
      console.error(`${props.label} launch failed`, cause);
      clearTimeout(errorTimer);
      errorTimer = setTimeout(() => setError(""), 3000);
    } finally {
      setOpening(false);
    }
  };

  return (
    <div
      class="nord-player"
      classList={{
        "has-session": !!session(),
        "is-playing": !!session()?.isPlaying,
        "has-error": !!error(),
      }}
      data-player={props.source}
      role="group"
      aria-label={`${props.label} player`}
      title={fullTitle()}
    >
      <button class="player-badge" type="button" aria-label={`Open ${props.label}`}
        title={`Open ${props.label}`} disabled={opening()} onClick={openApp}>
        <svg class="player-ring" viewBox="0 0 32 32" aria-hidden="true">
          <circle class="player-ring-track" cx="16" cy="16" r="14" />
          <circle class="player-ring-value" cx="16" cy="16" r="14" pathLength="100"
            stroke-dasharray={`${progress() * 100} 100`} />
        </svg>
        <SourceIcon source={props.source} />
      </button>
      <div class="player-content">
        <div class="player-caption">
          <span class="player-track">{error() || session()?.title || props.label}</span>
          <Show when={session()} fallback={<span class="player-detail">Nothing playing</span>}>
            <span class="player-detail">{session()?.artist || props.label}</span>
          </Show>
        </div>
        <div class="player-controls">
          <button class="player-skip" aria-label={`${props.label}: previous track`}
            disabled={!session() || busy()} onClick={() => command("previous")}>
            <SkipIcon />
          </button>
          <button class="player-toggle"
            aria-label={`${props.label}: ${session()?.isPlaying ? "pause" : "play"}`}
            disabled={!session() || busy()} onClick={() => command("togglePlayPause")}>
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path class="player-play-shape" d="m8 5 11 7-11 7Z" fill="currentColor" />
              <g class="player-pause-shape" fill="currentColor">
                <rect x="6" y="5" width="4" height="14" rx="1" />
                <rect x="14" y="5" width="4" height="14" rx="1" />
              </g>
            </svg>
          </button>
          <button class="player-skip" aria-label={`${props.label}: next track`}
            disabled={!session() || busy()} onClick={() => command("next")}>
            <SkipIcon next />
          </button>
        </div>
      </div>
      <span class="player-playing-mark" aria-hidden="true">
        <span /><span /><span />
      </span>
    </div>
  );
}
