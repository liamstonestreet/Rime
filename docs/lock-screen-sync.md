# Wallpaper → lock screen

After a successful desktop wallpaper commit, the rice prepares a separate blurred JPEG and passes it to Windows' [LockScreen.SetImageFileAsync API](https://learn.microsoft.com/en-us/uwp/api/windows.system.userprofile.lockscreen.setimagefileasync). The x64 helper uses the documented API, without registry policy overrides or administrator-only changes.

In **Rice → Lock screen**, **Sync wallpaper** enables/disables future updates. **Lock-screen blur** ranges from 0–100%, default 45%. The setting saves with appearance preferences. Adjusting blur re-applies the current wallpaper after a short debounce; zero applies no blur. Disabling sync keeps the last applied lock-screen picture.

The desktop wallpaper file is never edited. Background processing starts after the desktop commit, so the wallpaper/theme fade does not wait for image processing or the Windows call. Requests are serialized and obsolete queued choices are skipped. The cache retains two alternating JPEGs in `%LOCALAPPDATA%\NordRice\LockScreen`; images are decoded to a maximum longest side of 1920 pixels to bound memory/processing.

Rice shows preparation, completion, or failure status. Windows personalization restrictions may reject the request; errors also go to `wallpaper.log`, while desktop wallpaper switching continues normally. If Windows keeps replacing the image through Spotlight or a slideshow, review **Settings → Personalization → Lock screen** and use a Picture background.

Checks use synthetic images and an injected setter. They verify blur, original-file preservation, rapid-request coalescing, disabled sync, and visible rejection. They do not change the real lock screen. Actual Windows application is checked when an installed helper next changes wallpaper.

The JPEGs contain personal wallpaper imagery: regenerate them locally rather than including the cache in a portable preferences export.
