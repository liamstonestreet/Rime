using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

static class AppShortcutsChecks {
 [StructLayout(LayoutKind.Sequential)]struct Key {public Guid format;public uint id;}
 [StructLayout(LayoutKind.Explicit,Size=24)]struct Value {[FieldOffset(0)]public ushort type;[FieldOffset(8)]public IntPtr text;}
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Store {
  [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out Key key);[PreserveSig]int GetValue(ref Key key,out Value value);[PreserveSig]int SetValue(ref Key key,ref Value value);[PreserveSig]int Commit();
 }
 [DllImport("shell32.dll")]static extern int SHGetPropertyStoreForWindow(IntPtr window,ref Guid iid,out Store store);
 static void SetProperty(IntPtr window,uint id,string text){var iid=new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");Store store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window,ref iid,out store));var value=new Value{type=31,text=Marshal.StringToCoTaskMemUni(text)};try{var key=new Key{format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),id=id};Marshal.ThrowExceptionForHR(store.SetValue(ref key,ref value));Marshal.ThrowExceptionForHR(store.Commit());}finally{Marshal.FreeCoTaskMem(value.text);Marshal.ReleaseComObject(store);}}
 public static void Run(){
  var synthetic=new[]{new AppShortcuts.Link{Name="YouTube",Path=@"C:\Start Menu\YouTube.lnk"},new AppShortcuts.Link{Name="Spotify",Path=@"C:\Start Menu\Spotify.lnk"},new AppShortcuts.Link{Name="YouTube Music",Path=@"C:\Start Menu\YouTube Music.lnk"}};
  var merged=AppShortcuts.Merge(new[]{new AppEntry{Name="Spotify",AppID="spotify"}},synthetic);
  if(merged.Length!=3||merged.Single(a=>a.Name=="Spotify").AppID!="spotify"||merged.Single(a=>a.Name=="YouTube").Key!=synthetic[0].Path)throw new Exception("Shortcut merge lost deduplication/launch identity");
  var hits=SpotlightSearch.Quick("/apps youtube",merged,new SpotlightResult[0]);if(hits.Count(r=>r.Kind=="app")!=2||hits.Where(r=>r.Kind=="app").Select(r=>r.Key).Distinct().Count()!=2)throw new Exception("Web apps missing or sharing result identity");
  if(AppShortcuts.IconPath("missing.ico,0")!=null)throw new Exception("Missing icon resource accepted");
  var actual=AppShortcuts.Discover(true);var youtube=actual.FirstOrDefault(a=>a.Name.Equals("YouTube",StringComparison.OrdinalIgnoreCase));
  if(youtube!=null){var app=AppShortcuts.Merge(new AppEntry[0]).Single(a=>a.Name.Equals("YouTube",StringComparison.OrdinalIgnoreCase));AppCatalog.ValidateLaunch(app);if(!app.Executable.Equals(youtube.Path,StringComparison.OrdinalIgnoreCase))throw new Exception("YouTube must launch original shortcut, preserving profile/app arguments");if(!String.IsNullOrEmpty(youtube.Icon)){if(!(app.Icon is BitmapSource))throw new Exception("YouTube icon could not be decoded");var window=new System.Windows.Window();try{var handle=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();if(!String.IsNullOrEmpty(youtube.AppId)){SetProperty(handle,5,youtube.AppId);if(AppShortcuts.WindowIcon(handle)!=youtube.Icon)throw new Exception("Window app identity did not resolve shortcut icon");}SetProperty(handle,3,youtube.Icon);if(AppShortcuts.WindowIcon(handle)!=youtube.Icon)throw new Exception("Window relaunch icon did not resolve");}finally{window.Close();}}Console.WriteLine("PASS: installed YouTube shortcut discovered, launch target validated and local icon decoded. No app launched.");}
  Console.WriteLine("PASS: Start Menu fallback discovery/merge, distinct shortcut identities and Spotlight web-app search. No app launched.");
 }
}
