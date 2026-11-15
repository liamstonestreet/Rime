using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

/// <summary>
/// Hold-to-browse on one key (default Right Ctrl): pressing it opens
/// at once (like the Alt indicator), [ and ] step while it is held, releasing it commits, Esc cancels.
/// Watches the keyboard with a low-level hook on its own thread, since hooks must answer within a few
/// hundred milliseconds and the UI thread can be busy. Navigation, Esc and optional C-to-copy are swallowed only while
/// the key is held; any other combination (say Fn+Right, which GlazeWM reads as Alt+Right) passes
/// through and closes the strip straight away unless the user is already browsing.
/// </summary>
sealed class HoldKey:IDisposable {
 volatile bool disposed;bool started;
 const uint Marker=0x4E4F5244; // tags our own injected key so the hook ignores it
 int key,pendingKey;uint threadId;
 public Action Held,Released,Cancelled,CopySelected;public Action<int> Step,CategoryStep;
 HookProc proc;IntPtr hook;
 bool holding,stepped,shown,copyDown;

 public HoldKey(int virtualKey){key=pendingKey=virtualKey;}
 public void Rebind(int virtualKey){if(Interlocked.Exchange(ref pendingKey,virtualKey)==virtualKey)return;if(threadId!=0)PostThreadMessage(threadId,0x8001,IntPtr.Zero,IntPtr.Zero);}

 public void Start(){if(started||disposed)return;started=true;var thread=new Thread(Run){IsBackground=true,Name="Nord hold key"};thread.Start();}
 public void Dispose(){disposed=true;if(threadId!=0)PostThreadMessage(threadId,0x12,IntPtr.Zero,IntPtr.Zero);}
 void Run(){
  if(disposed)return;MSG initial;PeekMessage(out initial,IntPtr.Zero,0,0,0);threadId=GetCurrentThreadId();if(disposed)return;key=pendingKey;
  try{
  proc=Hook;hook=SetWindowsHookEx(13,proc,GetModuleHandle(null),0); // WH_KEYBOARD_LL
  MSG message;while(GetMessage(out message,IntPtr.Zero,0,0)>0){if(message.message==0x8001){if(shown)Post(Cancelled);holding=stepped=shown=false;key=pendingKey;continue;}TranslateMessage(ref message);DispatchMessage(ref message);}
  }finally{if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}threadId=0;}
 }

 IntPtr Hook(int code,IntPtr w,IntPtr l){
  if(!disposed&&code>=0){
   if(GameWindows.ForegroundGame!=IntPtr.Zero){if(holding){holding=shown=copyDown=false;Post(Cancelled);}return CallNextHookEx(hook,code,w,l);}
   var k=(KBDLLHOOKSTRUCT)Marshal.PtrToStructure(l,typeof(KBDLLHOOKSTRUCT));
   bool down=w==(IntPtr)0x100||w==(IntPtr)0x104,injected=(k.flags&0x10)!=0;
   if(!injected){
    if(k.vk==0x43&&copyDown&&!down){copyDown=false;return (IntPtr)1;}
    if(key!=0&&k.vk==key){
     if(down&&!holding){holding=true;stepped=false;shown=true;Post(Held);}
     else if(!down&&holding){holding=false;if(shown){Post(Released);if(k.vk==0xA4||k.vk==0xA5){Release(k.vk);return (IntPtr)1;}}}
    }else if(holding){
     if(k.vk==0x43&&shown&&CopySelected!=null){if(down&&!copyDown){copyDown=true;Post(CopySelected);}return (IntPtr)1;}
     if(k.vk==0x26||k.vk==0x28){if(down&&shown){stepped=true;int delta=k.vk==0x28?1:-1;Post(()=>{if(CategoryStep!=null)CategoryStep(delta);});}return (IntPtr)1;}
     if(k.vk==0xDB||k.vk==0xDD||k.vk==0x25||k.vk==0x27){if(down){if(!shown){shown=true;Post(Held);}stepped=true;int delta=k.vk==0xDD||k.vk==0x27?1:-1;Post(()=>{if(Step!=null)Step(delta);});}return (IntPtr)1;}
     if(k.vk==0x1B&&shown){if(down){shown=false;Post(Cancelled);}return (IntPtr)1;}
     if(down&&shown&&!stepped){shown=false;Post(Cancelled);} // a different shortcut: get out of its way
    }
   }
  }
  return CallNextHookEx(hook,code,w,l);
 }

 void Post(Action action){var app=Application.Current;if(!disposed&&action!=null&&app!=null&&!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke(new Action(()=>{if(!disposed)action();}));}

 /// <summary>
 /// Replays the key's release behind an unassigned key tap. Releasing Alt on its own opens the focused
 /// app's menu bar, and Windows saw a lone Alt (the [ ] presses were swallowed); the tap makes it a chord.
 /// </summary>
 static void Release(int vk){
  var inputs=new INPUT[3];
  for(int i=0;i<3;i++){inputs[i].type=1;inputs[i].ki.extra=(UIntPtr)Marker;}
  inputs[0].ki.vk=0xE8;inputs[1].ki.vk=0xE8;inputs[1].ki.flags=2;            // KEYEVENTF_KEYUP
  inputs[2].ki.vk=(ushort)vk;inputs[2].ki.flags=2|(vk==0xA5||vk==0xA3?1u:0u); // + EXTENDEDKEY for right-hand modifiers
  SendInput(3,inputs,Marshal.SizeOf(typeof(INPUT)));
 }

 [StructLayout(LayoutKind.Sequential)] struct KBDLLHOOKSTRUCT{public int vk,scan,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT{public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Explicit)] struct INPUT{[FieldOffset(0)]public uint type;[FieldOffset(8)]public KEYBDINPUT ki;[FieldOffset(8)]public MOUSEPAD pad;}
 [StructLayout(LayoutKind.Sequential)] struct MOUSEPAD{public int dx,dy;public uint data,flags,time;public UIntPtr extra;} // sizes the union like MOUSEINPUT
 [StructLayout(LayoutKind.Sequential)] struct MSG{public IntPtr hwnd;public uint message;public IntPtr w,l;public uint time;public int x,y;}
 delegate IntPtr HookProc(int code,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
 [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] static extern bool PeekMessage(out MSG message,IntPtr window,uint min,uint max,uint remove);
 [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern int GetMessage(out MSG message,IntPtr window,uint min,uint max);
 [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG message);
 [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG message);
 [DllImport("user32.dll")] static extern uint SendInput(uint count,INPUT[] inputs,int size);
}
