# Memory and efficiency pass

`checks/memory-snapshot.ps1` records private committed bytes, resident working set, thread and handle counts. It follows only Zebar's descendants plus the known rice helpers; unrelated apps' WebViews are excluded. Toolhelp avoids an administrator/WMI requirement. Working sets include shared pages and should not be interpreted as unique RAM usage by summing them.

Capture after installing, opening/closing the usual overlays, switching a few wallpapers and allowing the shell to settle:

```powershell
powershell -ExecutionPolicy Bypass -File .\nord-custom\checks\memory-snapshot.ps1 -OutFile .\nord-custom\checks\memory-after-optimization.json
```

The historical pre-update baseline measured roughly 1.24 GB private committed memory for the rice tree at capture time, including approximately 461 MB for CC, 145 MB for the workspace indicator and 134 MB for Spotlight. Its machine-specific snapshot was removed during repository cleanup. This was a point-in-time baseline, not a controlled leak measurement.

Changes:

- Removed two full-monitor bitmap caches per screen-frame pane. The thin frame still uses the same geometry and live/frost brushes, without rasterizing whole monitors for each layer. Grain is omitted when its opacity is zero. Removed the redundant CC backdrop bitmap cache too.
- Cancelled/unused full-resolution wallpaper preparations are retired and their GDI bitmaps disposed. In-use fade buffers remain pinned until the fade closes. Closing during a queued apply does not discard that pending apply's preparation prematurely.
- Wallpaper thumbnails are bounded to 12 MB/48 entries, down from 32 MB/96. Classic decodes only nearby previews; all styles release hidden image references and reload from cache on reopening. Removed cards cancel pending image delivery.
- Serialized thumbnail work awaits its semaphore asynchronously, avoiding blocked thread-pool workers during rapid browsing. Cancelled/obsolete requests are skipped and cannot repopulate hidden previews.
- Frost's per-monitor source cache uses weak references. Hidden workspace/Spotlight/wallpaper overlays drop their frost brush references after closing. The current visible screen frames still retain the images they need.
- Native appearance JSON is deserialized once per file revision, rather than once per font/motion lookup. Repeated lookups reuse the parsed settings.

No periodic forced garbage collections, working-set trimming, hardware-acceleration changes or loss of animations were introduced. A smaller cache budget and fewer retained graphics allocations do not guarantee that Windows immediately shrinks a running process's working set. Compare fresh installs under the same interactions. Larger future savings would require consolidating the separate WPF helpers or changes to Zebar's WebView process model; those are separate architectural changes.
