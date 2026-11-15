# Wallpaper indicator styles

Choose **Control Center > Configure > Wallpaper indicator**:

- **Classic**: the original five-thumbnail sliding strip.
- **Showcase**: a large cover preview with smaller neighboring wallpapers. Cards glide, scale and fade as you browse.
- **Filmstrip**: a compact seven-thumbnail rail with a large selection number and a subtle browsing hint.

The selection persists in `bar-appearance.json` as `wallpaperIndicatorStyle` (`classic`, `showcase`, `filmstrip`). It applies on the next indicator open, without restarting the helper. Classic remains the default. All styles use the shared font, theme, wallpaper palette, frost, corner radius and motion preferences. The content scales down to fit the active monitor's work area, including at higher DPI. Long file names truncate rather than making the panel wider.

Hold the configured wallpaper indicator key (**Right Ctrl** by default), then press **[ / ]** to browse. Release applies the chosen wallpaper with the existing desktop fade. Escape cancels. Releasing without browsing, or returning to the original wallpaper before releasing, closes without applying anything. If the current wallpaper is outside the folder, simply opening and releasing still leaves it unchanged. Tap shortcuts (**Win+[ / Win+]**) retain the existing delayed apply behavior.

Showcase and Filmstrip wrap at the library ends and keep only nearby preview cards, with a bounded outgoing-card pool during key repeat. They do not build full-size previews for the entire folder. Thumbnail decoding stays off the UI thread and serialized; Showcase requests the current image first. The shared thumbnail cache is keyed by file/time/size and bounded to approximately 32 MB (plus images still used by visible controls). Showcase uses 960px previews; Filmstrip uses 256px previews. Existing full-screen wallpaper fade preparation remains separate.

There are no new keyboard hooks, idle polling loops or endless animations. Selection transitions obey Rice's animation speed/style and Windows' animation preference; motion settles after hiding. The existing hold-key and wallpaper-folder settings are unchanged.

The desktop fade creates each monitor cover with managed WinForms opacity already zero, before native handle creation or Show. After painting, a compositor flush precedes theme preparation. Once all images, palette and frost are ready, desktop and shell share one scheduled 700 ms fade. Another compositor flush confirms opaque covers before the real Windows wallpaper changes. A short covered settling interval and final compositor flush precede closing the covers. Appearance polling waits until the Windows commit finishes. Border colors are applied without a GlazeWM reload, avoiding workspace/cursor detours.

Desktop changes are serialized. Superseded queued choices are skipped, and a single preparation worker skips obsolete queued decodes. In-use bitmap buffers remain pinned until every fade window closes, with explicit disposal afterward. There is no forced full garbage collection on each wallpaper switch. Failure cleanup closes/disposes the covers and releases the transition gate.

Native component checks use generated landscape fixtures and injected apply callbacks. They cover all three styles, persistence, release/cancel behavior, unknown-current safety, wraparound, zero/one/two-image folders, rapid navigation, bounded card pools and fitting constrained space. No real wallpaper is changed by those checks. Design previews are written into `control-center/checks/wallpaper-{style}.png`.

Fade checks additionally inspect a tiny off-screen native cover's first-show alpha and exercise multi-monitor fade/commit ordering, pending-choice coalescing, preparation cancellation, active bitmap lifetime and failure recovery through injected backends. They never call the real wallpaper-setting API.
