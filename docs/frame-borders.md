# Fractional-DPI border thickness

At 125% a 50-DIP bar can report a 63-pixel outer HWND while WebView paints only 62 complete pixels. Using the rounded-up height for edge placement clipped one stroke pixel, making the bar look thinner than CC. Bar metadata, CC placement and the screen-frame join now share the drawable bottom, clamped to `floor(50 * monitorScale)`. Browser edge placement also defensively clamps oversized host bounds.

The offscreen CSS edge layer uses an integral physical-pixel extent while retaining the designed corner radius. This avoids resampling-induced thinning at arbitrary radii/fractional scales.

`node checks/frame-glow/check-thickness.mjs` renders floor/ceil host-height cases at 100/125/150/175/200%. Native `CenterChecks` verifies alpha coverage equals the CC stroke's full physical-pixel width and checks the mixed-monitor anchors. This supplements the existing corner/glow-join tests.

The horizontal Zebar segment is rendered separately as a pixel-aligned SVG rectangle with crisp edges. Keeping it in the transformed corner texture softened its solid core at fractional DPI, even when summed alpha coverage was correct. The regression check now requires the exact number and bottom alignment of fully opaque pixel rows, not just equivalent alpha coverage. Native CC and screen-frame strokes are unchanged by this rendering correction.

Wallpaper transitions keep the native screen frame above the temporary wallpaper fade covers, while remaining below application windows. Both cover creation and the frame placement repair use that ordering; the two surfaces no longer compete for the same desktop slot. Frame frost and tint continue to use ThemeTransition's shared clock.

Window borders use the final palette immediately, rather than interpolating DWM border colors while GlazeWM retains an older cached palette. A changed palette is persisted once and GlazeWM is refreshed through the workspace/cursor-preserving reload helper. Stale persistence jobs are rejected. This is one native refresh per changed palette, not one per animation frame.
