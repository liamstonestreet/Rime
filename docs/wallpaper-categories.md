# Wallpaper categories

Create folders inside the wallpaper library configured in `wallpapers.json`. Each top-level folder becomes a category; images nested farther down belong to that top-level category. Adding, renaming or removing a folder is detected the next time the indicator opens.

- **All** combines every supported image across categories and the library root.
- **Uncategorized** contains images directly in the root, when any exist.
- Empty folders are omitted. JPG, JPEG, PNG and BMP retain existing support. Directory junctions/symlinks below the library root are skipped to prevent loops.

Hold the configured wallpaper indicator key (Right Ctrl by default). **Up/Down** cycles categories; **Left/Right** or **[ / ]** browses images. Release the hold key to apply the selected image, or Escape to cancel. Opening and releasing without browsing never changes the wallpaper.

The category chips are also clickable without activating the window. The chip row scrolls with the mouse wheel; keyboard navigation brings the selected chip into view. The header shows the current category and image count. The same controls work in Classic, Showcase and Filmstrip.

Last category and per-category selected image paths persist in `wallpaper-browser.json` beside the helpers, scoped to the library root. Reopening resumes the remembered position; selecting a missing category falls back to All. Cancelling a wallpaper change still remembers browsing preferences. Browser-state writes are debounced; closing the indicator flushes them. A root/library change starts a fresh browsing state.

The existing Win+[ / Win+] timed-cycling shortcuts still work in the selected category. The indicator reuses its library snapshot while open, so image/category steps do not rescan the filesystem. Thumbnail requests remain bounded and old category previews are released when switching.

Images are not moved or renamed. Existing wallpaper fades, synchronized theme transitions and optional blurred lock-screen updates run only when a selection is applied.
