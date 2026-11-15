import { requestWorkspaceRules, workspaceRules } from '../../lib/rice-widgets';

// Turns the app assignment rules from workspaces.json on or off. The control center writes GlazeWM's
// config; this button only asks for it over the bar's bridge connection.
export default function WorkspaceRules() {
  const enabled = () => workspaceRules().enabled;
  const busy = () => workspaceRules().busy ?? false;
  const error = () => workspaceRules().error ? `Could not update app rules: ${workspaceRules().error} Click to retry.` : '';
  return <button type="button" aria-label="Automatic workspace assignments" aria-pressed={enabled() ?? false}
    disabled={busy()} onDblClick={event => event.stopPropagation()}
    onClick={() => requestWorkspaceRules(enabled() === undefined ? 'status' : enabled() ? 'off' : 'on')}
    title={error() || `App rules: ${enabled() === undefined ? 'checking' : enabled() ? 'ON' : 'OFF'} · Applies when an app has no other open window, including on other workspaces; tray-only apps can be placed`}
    style={{'--rules-idle-color': error() ? 'var(--nord-11)' : enabled() ? 'var(--nord-8)' : 'var(--muted)', opacity: busy() ? '.5' : '1'}}
    class="workspace-rules-button h-8 w-8 rounded-full flex items-center justify-center cursor-pointer">
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden="true" stroke="currentColor" stroke-width="1.35" stroke-linecap="round" stroke-linejoin="round">
      {/* A folded wayfinder: three destinations, one quiet route between them. */}
      <path d="M3.5 6.5 9 4l6 2.5L20.5 4v13.5L15 20l-6-2.5L3.5 20Z" />
      <path d="M9 4v5m0 5v3.5M15 6.5v3m0 5V20" opacity=".45" />
      <path d="M6 13c2.4-4 4.1 3 6.2-.5 1.3-2.2 2.8-1.8 5.2-2.8" opacity={enabled() ? '.9' : '.35'} />
      <circle cx="17.5" cy="9.5" r="1.3" fill={enabled() ? 'currentColor' : 'none'} />
    </svg>
  </button>;
}
