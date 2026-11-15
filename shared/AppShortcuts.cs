using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

// Browser-installed apps may have valid Start Menu links without an AppsFolder
// entry. Launch their original link so the browser profile/app arguments survive.
static class AppShortcuts {
 internal sealed class Link {public string Name,Path,Icon,AppId;}
 static readonly object gate=new object();static DateTime sampled;static Link[] links=new Link[0];
 internal static Link Read(string path){object shell=null,shortcut=null;try{
  shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
  string icon=Convert.ToString(shortcut.GetType().InvokeMember("IconLocation",BindingFlags.GetProperty,null,shortcut,null));
  return new Link{Name=System.IO.Path.GetFileNameWithoutExtension(path),Path=path,Icon=IconPath(icon),AppId=ReadFileIdentity(path)};
 }catch{return null;}finally{if(shortcut!=null&&Marshal.IsComObject(shortcut))Marshal.ReleaseComObject(shortcut);if(shell!=null&&Marshal.IsComObject(shell))Marshal.ReleaseComObject(shell);}}
 internal static string IconPath(string resource){if(String.IsNullOrWhiteSpace(resource))return null;int comma=resource.LastIndexOf(',');int index;if(comma>=0&&Int32.TryParse(resource.Substring(comma+1),out index))resource=resource.Substring(0,comma);resource=Environment.ExpandEnvironmentVariables(resource.Trim().Trim('"'));return File.Exists(resource)?resource:null;}
 static IEnumerable<string> Files(string folder){var result=new List<string>();try{result.AddRange(Directory.GetFiles(folder,"*.lnk"));foreach(var sub in Directory.GetDirectories(folder)){if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)!=0||System.IO.Path.GetFileName(sub).Equals("Startup",StringComparison.OrdinalIgnoreCase))continue;result.AddRange(Files(sub));}}catch{}return result;}
 internal static Link[] Discover(bool refresh=false){lock(gate){if(!refresh&&DateTime.UtcNow-sampled<TimeSpan.FromSeconds(30))return links;var result=new List<Link>();foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)})foreach(var path in Files(root)){var link=Read(path);if(link!=null)result.Add(link);}links=result.ToArray();sampled=DateTime.UtcNow;return links;}}
 public static AppEntry[] Merge(AppEntry[] catalog,bool refresh=false){return Merge(catalog,Discover(refresh));}
 internal static AppEntry[] Merge(AppEntry[] catalog,Link[] shortcuts){var result=new List<AppEntry>((catalog??new AppEntry[0]).Where(a=>a!=null&&!String.IsNullOrWhiteSpace(a.Name)));var names=new HashSet<string>(result.Select(a=>a.Name),StringComparer.OrdinalIgnoreCase);foreach(var link in shortcuts)if(names.Add(link.Name))result.Add(new AppEntry{Name=link.Name,Executable=link.Path,WindowAppId=link.AppId});return result.ToArray();}
 public static string LinkIcon(string path){if(String.IsNullOrEmpty(path)||!System.IO.Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase))return null;return Discover().Where(l=>l.Path.Equals(path,StringComparison.OrdinalIgnoreCase)).Select(l=>l.Icon).FirstOrDefault()??(Read(path)??new Link()).Icon;}
 public static string WindowIcon(IntPtr window){string resource=ReadWindowProperty(window,3);var icon=IconPath(resource);if(icon!=null)return icon;string id=ReadWindowProperty(window,5);if(String.IsNullOrEmpty(id))return null;return Discover().Where(l=>String.Equals(l.AppId,id,StringComparison.OrdinalIgnoreCase)).Select(l=>l.Icon).FirstOrDefault(p=>p!=null);}
 public static string WindowIdentity(IntPtr window){return ReadWindowProperty(window,5);}
 public static AppEntry WindowApp(IntPtr window){string id=ReadWindowProperty(window,5);if(String.IsNullOrEmpty(id))return null;var link=Discover().FirstOrDefault(l=>String.Equals(l.AppId,id,StringComparison.OrdinalIgnoreCase));return link==null?null:new AppEntry{Name=link.Name,Executable=link.Path,WindowAppId=link.AppId};}
 static readonly Guid AppModel=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),StoreId=new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
 [StructLayout(LayoutKind.Sequential)]struct Key {public Guid format;public uint id;}
 [StructLayout(LayoutKind.Explicit,Size=24)]struct Value {[FieldOffset(0)]public ushort type;[FieldOffset(8)]public IntPtr text;}
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Store {
  [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out Key key);[PreserveSig]int GetValue(ref Key key,out Value value);[PreserveSig]int SetValue(ref Key key,ref Value value);[PreserveSig]int Commit();
 }
 [DllImport("shell32.dll")]static extern int SHGetPropertyStoreForWindow(IntPtr window,ref Guid iid,out Store store);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHGetPropertyStoreFromParsingName(string path,IntPtr context,uint flags,ref Guid iid,out Store store);
 [DllImport("ole32.dll")]static extern int PropVariantClear(ref Value value);
 static string Property(Store store,uint id){var key=new Key{format=AppModel,id=id};Value value;if(store.GetValue(ref key,out value)!=0)return null;try{return value.type==31?Marshal.PtrToStringUni(value.text):null;}finally{PropVariantClear(ref value);}}
 static string ReadWindowProperty(IntPtr window,uint id){Store store=null;try{var iid=StoreId;return SHGetPropertyStoreForWindow(window,ref iid,out store)==0?Property(store,id):null;}catch{return null;}finally{if(store!=null)Marshal.ReleaseComObject(store);}}
 static string ReadFileIdentity(string path){Store store=null;try{var iid=StoreId;return SHGetPropertyStoreFromParsingName(path,IntPtr.Zero,0,ref iid,out store)==0?Property(store,5):null;}catch{return null;}finally{if(store!=null)Marshal.ReleaseComObject(store);}}
}
