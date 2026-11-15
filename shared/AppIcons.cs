using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
static class AppIcons {
    static readonly System.Collections.Generic.Dictionary<string, ImageSource> cache = new System.Collections.Generic.Dictionary<string, ImageSource>();
    static readonly System.Collections.Generic.Queue<string> order = new System.Collections.Generic.Queue<string>();
    internal static int CacheCount {get{lock(cache)return cache.Count;}}
    static void Remember(string key,ImageSource image){if(!cache.ContainsKey(key)){while(cache.Count>=512)cache.Remove(order.Dequeue());order.Enqueue(key);}cache[key]=image;}
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Info {
        public IntPtr icon; public int index; public uint attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string display;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=80)] public string type;
    }
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint flags, out uint attrs);
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attrs, ref Info info, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    public static ImageSource Get(string id) { lock(cache) return Load(id); }
    public static ImageSource GetExecutable(string path) {lock(cache){
        if(String.IsNullOrEmpty(path))return null;var key="exe:"+path;ImageSource image;if(cache.TryGetValue(key,out image))return image;
        image=null;try{using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(path)){if(icon!=null){image=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(24,24));image.Freeze();}}}catch{}Remember(key,image);return image;
    }}
    static ImageSource Load(string id) {
        if (String.IsNullOrEmpty(id)) return null;
        ImageSource image; if(cache.TryGetValue(id, out image)) return image;
        IntPtr pidl=IntPtr.Zero; var info=new Info(); uint attrs;
        try {
            if(SHParseDisplayName("shell:AppsFolder\\"+id,IntPtr.Zero,out pidl,0,out attrs)==0) {
                SHGetFileInfo(pidl,0,ref info,(uint)Marshal.SizeOf(typeof(Info)),0x100|0x8);
                if(info.icon!=IntPtr.Zero) { image=Imaging.CreateBitmapSourceFromHIcon(info.icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(24,24)); image.Freeze(); }
            }
        } catch { image=null; }
        finally { if(info.icon!=IntPtr.Zero) DestroyIcon(info.icon); if(pidl!=IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
        Remember(id,image); return image;
    }
}
