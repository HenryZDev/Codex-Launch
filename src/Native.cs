using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CodexLaunch {
 internal static class Native {
  internal delegate bool EnumCallback(IntPtr window,IntPtr data);
  [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; }
  [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size;public PixelRect Monitor,Work;public uint Flags; }
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr data);
  [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out PixelRect rect);
  [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref Point point);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out PixelRect rect);
  [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
  [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr window,int command);
  [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
  [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point point,uint flags);
  [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref PixelRect rect,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
  [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint processId);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetPackageFamilyName(IntPtr process,ref uint length,StringBuilder family);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);

  internal static PixelRect ClientBounds(IntPtr window) {
   PixelRect client;var point=new Point();
   if(!GetClientRect(window,out client)||!ClientToScreen(window,ref point))return new PixelRect();
   return new PixelRect(point.X,point.Y,client.Width,client.Height);
  }
  internal static PixelRect OuterBounds(IntPtr window) { PixelRect r;return GetWindowRect(window,out r)?r:new PixelRect(); }
  internal static bool Position(IntPtr window,PixelRect bounds) {
   return SetWindowPos(window,IntPtr.Zero,bounds.Left,bounds.Top,bounds.Width,bounds.Height,0x4|0x10);
  }
  internal static void MakeInputTransparent(IntPtr window) {
   // The artwork is visual only: clicks and activation belong to the host.
   SetWindowLongPtr(window,-20,new IntPtr(GetWindowLongPtr(window,-20).ToInt64()|0x20|0x8000000));
  }
  internal static void RoundCorners(IntPtr window) { int preference=2;DwmSetWindowAttribute(window,33,ref preference,4); }
  internal static bool Responsive(IntPtr window) {
   IntPtr result;return SendMessageTimeout(window,0,IntPtr.Zero,IntPtr.Zero,0x2,60,out result)!=IntPtr.Zero;
  }
  internal static bool ActivateExisting(WindowSample sample) {
   if(sample==null||sample.Handle==IntPtr.Zero||!IsWindow(sample.Handle))return false;
   // SW_RESTORE only for minimized windows; applying it to a maximized
   // window would unnecessarily change the user's current placement.
   if(IsIconic(sample.Handle))ShowWindowAsync(sample.Handle,9);
   SetForegroundWindow(sample.Handle);return true;
  }
  static string Family(uint pid) {
   IntPtr process=OpenProcess(0x1000,false,pid);if(process==IntPtr.Zero)return null;
   try { uint length=256;var value=new StringBuilder(256);return GetPackageFamilyName(process,ref length,value)==0?value.ToString():null; }
   finally { CloseHandle(process); }
  }
  internal static bool MatchesPackage(IntPtr window,string family) {
   if(window==IntPtr.Zero||!IsWindow(window))return false;
   uint pid;GetWindowThreadProcessId(window,out pid);return PackageIdentity.Matches(Family(pid),family);
  }
  internal static WindowSample FindTarget(string family,IntPtr preferred=default(IntPtr)) {
   var identity=new Dictionary<uint,bool>();WindowSample best=null;
   IntPtr foreground=GetForegroundWindow();
   EnumWindows(delegate(IntPtr window,IntPtr data) {
    if(!IsWindowVisible(window)||GetWindow(window,4)!=IntPtr.Zero||(GetWindowLongPtr(window,-20).ToInt64()&0x80)!=0)return true;
    int cloaked;if(DwmGetWindowAttribute(window,14,out cloaked,4)==0&&cloaked!=0)return true;
    uint pid;GetWindowThreadProcessId(window,out pid);bool matches;
    if(!identity.TryGetValue(pid,out matches)){matches=PackageIdentity.Matches(Family(pid),family);identity[pid]=matches;}
    if(!matches)return true;
    PixelRect bounds=ClientBounds(window);bool minimized=IsIconic(window);
    if(!minimized&&!bounds.IsUsable)return true;
    best=WindowPolicy.Choose(best,new WindowSample(window,bounds,false){Minimized=minimized},preferred,foreground);
    return true;
   },IntPtr.Zero);
   if(best!=null)best.Responsive=Responsive(best.Handle);
   return best;
  }
  internal static PixelRect InitialBounds(LauncherSettings settings,PixelRect? target=null) {
   IntPtr monitor;
   if(target.HasValue){PixelRect bounds=target.Value;monitor=MonitorFromRect(ref bounds,2);}
   else {Point cursor;GetCursorPos(out cursor);monitor=MonitorFromPoint(cursor,2);}
   var info=new MonitorInfo {Size=Marshal.SizeOf(typeof(MonitorInfo))};
   if(!GetMonitorInfo(monitor,ref info))return new PixelRect(100,100,settings.WindowWidth,settings.WindowHeight);
   uint dx=96,dy=96;try {if(GetDpiForMonitor(monitor,0,out dx,out dy)!=0)dx=96;}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
   return WindowLayout.Compact(info.Work,settings.WindowWidth,settings.WindowHeight,dx/96.0);
  }
  internal static PixelRect WorkArea() {
   Point cursor;GetCursorPos(out cursor);IntPtr monitor=MonitorFromPoint(cursor,2);
   var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};
   return GetMonitorInfo(monitor,ref info)?info.Work:new PixelRect(0,0,960,600);
  }
 }

 internal interface IWindowProbe:IDisposable {WindowSample Current {get;}}
 internal sealed class WindowProbe:IWindowProbe {
  readonly string family;
  readonly Timer timer;
  volatile WindowSample current;
  volatile bool disposed;
  int sampling;
  public WindowSample Current { get { return current; } }
  internal WindowProbe(string family,WindowSample initial) {this.family=family;current=initial;timer=new Timer(Sample,null,0,200);}
  void Sample(object unused) {
   if(disposed||Interlocked.Exchange(ref sampling,1)!=0)return;
   try {var previous=current;var next=Native.FindTarget(family,previous==null?IntPtr.Zero:previous.Handle);if(!disposed)current=next;}
   catch {if(!disposed)current=null;}
   finally {Interlocked.Exchange(ref sampling,0);}
  }
  public void Dispose(){disposed=true;timer.Dispose();}
 }

 internal sealed class LiveThumbnail:IDisposable {
  [StructLayout(LayoutKind.Sequential)] struct Properties {
   public uint Flags;public PixelRect Destination,Source;public byte Opacity;
   [MarshalAs(UnmanagedType.Bool)]public bool Visible;
   [MarshalAs(UnmanagedType.Bool)]public bool ClientOnly;
  }
  [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumbnail);
  [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref Properties properties);
  [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
  IntPtr thumbnail;
  readonly IntPtr source;
  internal int LastError {get;private set;}
  internal bool Connected {get{return thumbnail!=IntPtr.Zero;}}
  internal LiveThumbnail(IntPtr destination,IntPtr source){this.source=source;LastError=DwmRegisterThumbnail(destination,source,out thumbnail);if(LastError!=0)thumbnail=IntPtr.Zero;}
  internal bool Update(int width,int height,byte opacity) {
   if(thumbnail==IntPtr.Zero||!Native.IsWindow(source))return false;
   var props=new Properties {Flags=1|4|8|16,Destination=new PixelRect(0,0,width,height),Opacity=opacity,Visible=true,ClientOnly=true};
   LastError=DwmUpdateThumbnailProperties(thumbnail,ref props);return LastError==0;
  }
  public void Dispose(){if(thumbnail!=IntPtr.Zero){DwmUnregisterThumbnail(thumbnail);thumbnail=IntPtr.Zero;}}
 }
}
