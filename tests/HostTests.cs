using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using CodexLaunch;

static class HostTests {
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr handle);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr handle,int index);
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,uint message,IntPtr w,IntPtr l);
 static void Pump(double seconds) {
  var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};
  timer.Tick+=delegate {timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
 }
 static void MeasureGlyphs(Drawing drawing,Matrix parent,ref Rect bounds) {
  var group=drawing as DrawingGroup;
  if(group!=null){Matrix transform=Matrix.Multiply(group.Transform==null?Matrix.Identity:group.Transform.Value,parent);foreach(Drawing child in group.Children)MeasureGlyphs(child,transform,ref bounds);}
  var glyph=drawing as GlyphRunDrawing;if(glyph!=null){Rect rect=glyph.Bounds;rect.Transform(parent);bounds.Union(rect);}
 }
 static void ExpansionKeepsTypography() {
  var overlay=new BootWindow(new LauncherSettings{ReducedMotion=true},null,true,300,
   new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"typography-check")));
  try {
   overlay.Show();Pump(.1);IntPtr host=new WindowInteropHelper(overlay).Handle;
   var grid=(Grid)overlay.Content;var artwork=(AnimationView)grid.Children[0];
   var status=(TextBlock)grid.Children[1];var skip=(Button)grid.Children[2];
   Rect before=Rect.Empty;MeasureGlyphs(VisualTreeHelper.GetDrawing(artwork),Matrix.Identity,ref before);
   if(before.IsEmpty)throw new Exception("Native text did not render before the expansion fixture.");
   double statusSize=status.FontSize,skipSize=skip.FontSize;PixelRect opening=Native.ClientBounds(host);
   Native.Position(host,new PixelRect(opening.Left-100,opening.Top-100,opening.Width+700,opening.Height+400));Pump(.15);
   Rect after=Rect.Empty;MeasureGlyphs(VisualTreeHelper.GetDrawing(artwork),Matrix.Identity,ref after);
   if(Math.Abs(before.Width-after.Width)>.1||Math.Abs(before.Height-after.Height)>.1)throw new Exception("Expansion scaled the rendered wordmark or greeting.");
   if(status.FontSize!=statusSize||skip.FontSize!=skipSize)throw new Exception("Expansion scaled the status or Skip font.");
   Console.WriteLine("PASS Native window expansion preserves rendered typography and control font sizes");
  }finally {overlay.Close();}
 }
 static void ExpansionDoesNotResizeArtworkSurface() {
  var source=new Window{Title="Disposable artwork continuity fixture",Width=1450,Height=900,Left=80,Top=60,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};BootWindow overlay=null;
  try {
   source.Show();Pump(.1);IntPtr sourceHandle=new WindowInteropHelper(source).Handle;
   overlay=new BootWindow(new LauncherSettings{MinimumAnimationSeconds=2.5,StableWindowSeconds=.1,ExpansionSeconds=.6,TransitionSeconds=.6},
    new WindowSample(sourceHandle,Native.ClientBounds(sourceHandle),true),false,30,
    new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"artwork-continuity-check")),new FixtureProbe(sourceHandle));
   overlay.Show();Pump(2.35);var artwork=(AnimationView)((Grid)overlay.Content).Children[0];
   double width=artwork.ActualWidth,height=artwork.ActualHeight;int resizes=0;
   SizeChangedEventHandler changed=delegate {if(Math.Abs(artwork.ActualWidth-width)>.1||Math.Abs(artwork.ActualHeight-height)>.1)resizes++;};
   artwork.SizeChanged+=changed;
   Rect glyphs=Rect.Empty;MeasureGlyphs(VisualTreeHelper.GetDrawing(artwork),Matrix.Identity,ref glyphs);
   var dpi=VisualTreeHelper.GetDpi(artwork);
   PixelRect opening=Native.ClientBounds(new WindowInteropHelper(overlay).Handle);
   double offsetX=(glyphs.Left+glyphs.Width/2)*dpi.DpiScaleX-opening.Width/2.0;
   double offsetY=(glyphs.Top+glyphs.Height/2)*dpi.DpiScaleY-opening.Height/2.0;
   Pump(.4);
   PixelRect moving=Native.ClientBounds(new WindowInteropHelper(overlay).Handle);
   if(moving.NearlyEquals(opening))throw new Exception("Artwork fixture did not exercise native background expansion.");
   if(resizes!=0)throw new Exception("Native expansion resized the artwork's rendering surface "+resizes+" times; it can redraw off-center between window moves.");
   for(int i=0;i<10;i++) {
    Pump(.025);Window surface=Window.GetWindow(artwork);
    PixelRect artBounds=Native.ClientBounds(new WindowInteropHelper(surface).Handle),background=Native.ClientBounds(new WindowInteropHelper(overlay).Handle);
    Rect current=Rect.Empty;MeasureGlyphs(VisualTreeHelper.GetDrawing(artwork),Matrix.Identity,ref current);
    double x=artBounds.Left+(current.Left+current.Width/2)*dpi.DpiScaleX-background.Left-background.Width/2.0;
    double y=artBounds.Top+(current.Top+current.Height/2)*dpi.DpiScaleY-background.Top-background.Height/2.0;
    if(Math.Abs(x-offsetX)>1.5||Math.Abs(y-offsetY)>1.5)throw new Exception("Rendered greeting jumped relative to the expanding background center.");
   }
   Pump(.9);artwork.SizeChanged-=changed;
   if(overlay.IsVisible)throw new Exception("Stable artwork layer prevented natural handoff.");
   foreach(Window candidate in Application.Current.Windows)if(candidate!=source&&candidate.IsVisible)throw new Exception("Handoff left an artwork layer visible.");
   Console.WriteLine("PASS Background expands without resizing the artwork surface or leaving a floating layer");
  }finally {if(overlay!=null&&overlay.IsVisible)overlay.Close();source.Close();}
 }
 static void ArtworkLayerSupportsFallback() {
  var owner=new Window{Width=960,Height=600,ShowInTaskbar=false,Content=new Grid{Background=Brushes.Black}};
  ArtworkWindow layer=null;
  try {
   owner.Show();Pump(.1);IntPtr host=new WindowInteropHelper(owner).Handle;
   var view=new AnimationView{ReducedMotion=true};
   layer=new ArtworkWindow(owner,view,Native.ClientBounds(host));layer.Show();Pump(.1);
   IntPtr handle=new WindowInteropHelper(layer).Handle;
   long style=GetWindowLongPtr(handle,-20).ToInt64();
   if((style&0x8000020)!=0x8000020||layer.ShowInTaskbar||Native.GetForegroundWindow()==handle)throw new Exception("Artwork layer can intercept mouse input, activation, or the taskbar.");
   var bitmap=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
   if(pixel[3]!=0)throw new Exception("Artwork layer paints an opaque background over the live app.");
   using(var fade=new WindowFader((FrameworkElement)owner.Content,Native.ClientBounds(host),false,delegate{})) {
    fade.Own(layer);owner.Hide();Pump(.1);
    if(!layer.IsVisible||!Native.IsWindow(handle))throw new Exception("Fallback hid the artwork with its original owner.");
    fade.SetOpacity(.5);view.Reveal=.5;view.InvalidateVisual();Pump(.1);
   }
   if(layer.IsVisible||Native.IsWindow(handle))throw new Exception("Fallback left its owned artwork window alive after closing.");
   Console.WriteLine("PASS Artwork layer is transparent, noninteractive, and follows fallback ownership and cleanup");
  }finally {if(layer!=null)layer.Dispose();owner.Close();}
 }
 sealed class FixtureProbe:IWindowProbe {
  readonly IntPtr handle;
  internal FixtureProbe(IntPtr handle){this.handle=handle;}
  public WindowSample Current {get{return Native.IsWindow(handle)?new WindowSample(handle,Native.ClientBounds(handle),true){Minimized=IsIconic(handle)}:null;}}
  public void Dispose(){}
 }
 static void MinimizeAndCoverage() {
  var source=new Window{Title="Disposable loading app",Width=1000,Height=700,ShowInTaskbar=false};
  var other=new Window{Title="Disposable unrelated app",Width=600,Height=400,ShowInTaskbar=false,ShowActivated=false};
  BootWindow overlay=null;
  try {
   source.Show();other.Show();Pump(.1);
   IntPtr handle=new WindowInteropHelper(source).Handle;
   overlay=new BootWindow(new LauncherSettings{MinimumAnimationSeconds=10},new WindowSample(handle,Native.ClientBounds(handle),true),false,30,
    new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"interaction-check")),new FixtureProbe(handle));
   overlay.Show();Pump(.2);IntPtr host=new WindowInteropHelper(overlay).Handle;
   if((GetWindowLongPtr(host,-16).ToInt64()&0x20000)==0)throw new Exception("Overlay has no Windows minimize capability for the taskbar.");
   SendMessage(host,0x112,new IntPtr(0xF020),IntPtr.Zero);Pump(.3);
   if(!IsIconic(host)||!IsIconic(handle))throw new Exception("Taskbar minimize did not keep both overlay and loading app minimized.");
   Native.ShowWindowAsync(handle,9);source.Activate();Pump(.3);
   if(!IsIconic(host)||!IsIconic(handle))throw new Exception("App activation undid the user's minimize request.");
   Console.WriteLine("PASS Taskbar minimize hides both windows and survives app activation");
   Native.ShowWindowAsync(host,9);Pump(.05);overlay.Activate();Pump(.25);
   if(IsIconic(host)||IsIconic(handle))throw new Exception("Restoring the overlay did not restore its loading app.");
   Console.WriteLine("PASS Restore resumes both the animation and the loading app");
   // Window.Activate can activate a window within a background process while
   // Windows refuses global foreground focus. Do not mistake a correct yield
   // to the user's foreground app for a coverage defect in that environment.
   if(Native.GetForegroundWindow()!=host&&Native.GetForegroundWindow()!=handle) {
    Console.WriteLine("SKIP Three native foreground checks: Windows did not grant foreground focus to the fixture");
   } else {
    if(!overlay.Topmost)throw new Exception("Active overlay is not protected against startup window activation.");
    Console.WriteLine("PASS Active restored animation retains coverage");
    other.Activate();Pump(.3);
    if(Native.GetForegroundWindow()==new WindowInteropHelper(other).Handle) {
     if(overlay.Topmost)throw new Exception("Overlay stayed above an unrelated foreground app.");
     Console.WriteLine("PASS Overlay yields its cover when switching to another app");
    } else Console.WriteLine("SKIP Foreground yield check: Windows rejected unrelated fixture activation");
    overlay.Activate();Pump(.15);source.Activate();Pump(.2);
    if(Native.GetForegroundWindow()==host||Native.GetForegroundWindow()==handle) {
     if(!overlay.Topmost||IsIconic(handle))throw new Exception("Startup coverage minimized the source app or was lost during activation.");
     Console.WriteLine("PASS Returning to the animation covers app activation without minimizing the app");
    } else Console.WriteLine("SKIP Foreground return check: Windows rejected fixture activation");
   }
   SendMessage(handle,0x112,new IntPtr(0xF020),IntPtr.Zero);Pump(.3);
   if(!IsIconic(host)||!IsIconic(handle))throw new Exception("Minimizing the loading app left the overlay covering the desktop.");
   Native.ShowWindowAsync(host,9);Pump(.3);
   if(IsIconic(host)||IsIconic(handle))throw new Exception("Restoring after an app minimize did not restore both windows.");
   Console.WriteLine("PASS Minimizing the loading app also minimizes its animation");
  }finally {if(overlay!=null)overlay.Close();source.Close();other.Close();}
 }
 static void MinimizeDuringReveal() {
  var source=new Window{Title="Disposable reveal fixture",Width=1000,Height=700,ShowInTaskbar=false};BootWindow overlay=null;
  try {
   source.Show();Pump(.1);IntPtr sourceHandle=new WindowInteropHelper(source).Handle;
   overlay=new BootWindow(new LauncherSettings{MinimumAnimationSeconds=0,StableWindowSeconds=.1,TransitionSeconds=2},
    new WindowSample(sourceHandle,Native.ClientBounds(sourceHandle),true),false,30,
    new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reveal-minimize-check")),new FixtureProbe(sourceHandle));
   overlay.Show();Pump(1.3);IntPtr host=new WindowInteropHelper(overlay).Handle;
   SendMessage(host,0x112,new IntPtr(0xF020),IntPtr.Zero);Pump(2.3);
   if(!IsIconic(host)||!IsIconic(sourceHandle))throw new Exception("Minimizing during the reveal did not keep both windows minimized.");
   Native.ShowWindowAsync(host,9);Pump(.35);
   if(!overlay.IsVisible||IsIconic(host)||IsIconic(sourceHandle))throw new Exception("Paused reveal did not resume as a visible animation.");
   Pump(3.2);
   if(overlay.IsVisible)throw new Exception("Resumed reveal did not hand off naturally.");
   Console.WriteLine("PASS Minimizing mid-reveal pauses the handoff and resumes cleanly");
  }finally {if(overlay!=null&&overlay.IsVisible)overlay.Close();source.Close();}
 }
 static void CompactExpansionPreservesApp() {
  var source=new Window{Title="Disposable compact expansion fixture",Width=1450,Height=900,Left=80,Top=60,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};BootWindow overlay=null;
  try {
   source.Show();Pump(.1);IntPtr sourceHandle=new WindowInteropHelper(source).Handle;PixelRect target=Native.ClientBounds(sourceHandle),finalOverlay=new PixelRect();
   var settings=new LauncherSettings{MinimumAnimationSeconds=0,StableWindowSeconds=.1,ExpansionSeconds=.85,TransitionSeconds=.35};
   overlay=new BootWindow(settings,new WindowSample(sourceHandle,target,true),false,30,
    new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"compact-expansion-check")),new FixtureProbe(sourceHandle));
   overlay.Closing+=delegate {finalOverlay=Native.ClientBounds(new WindowInteropHelper(overlay).Handle);};
   overlay.Show();IntPtr host=new WindowInteropHelper(overlay).Handle;PixelRect opening=Native.ClientBounds(host);
   if(opening.Width>=target.Width||opening.Height>=target.Height)throw new Exception("Opening splash did not remain compact relative to the app.");
   Pump(.5);PixelRect middle=Native.ClientBounds(host);
   if(middle.Width<=opening.Width||middle.Width>=target.Width||middle.Height<=opening.Height||middle.Height>=target.Height)throw new Exception("Splash jumped to final bounds instead of expanding smoothly.");
   if(!Native.ClientBounds(sourceHandle).NearlyEquals(target))throw new Exception("Expansion moved or resized the app.");
   Pump(1.3);
   if(overlay.IsVisible||!finalOverlay.NearlyEquals(target))throw new Exception("Compact expansion did not hand off at exactly matching bounds.");
   if(!Native.ClientBounds(sourceHandle).NearlyEquals(target))throw new Exception("Handoff changed the app's placement.");
   Console.WriteLine("PASS Compact opening expands through intermediate bounds and crossfades without moving the app");
  }finally {if(overlay!=null&&overlay.IsVisible)overlay.Close();source.Close();}
 }
 static void DelayedMinimizeRestore() {
  var source=new Window{Title="Disposable delayed command fixture",Width=1000,Height=700,ShowInTaskbar=false,ShowActivated=false};BootWindow overlay=null;
  var commands=new List<int>();
  try {
   source.Show();Pump(.1);IntPtr sourceHandle=new WindowInteropHelper(source).Handle;
   overlay=new BootWindow(new LauncherSettings{MinimumAnimationSeconds=10},new WindowSample(sourceHandle,Native.ClientBounds(sourceHandle),true),false,30,
    new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"delayed-restore-check")),new FixtureProbe(sourceHandle),delegate(IntPtr target,int command){if(target!=sourceHandle)throw new Exception("State command targeted the wrong window.");commands.Add(command);});
   overlay.Show();Pump(.2);
   IntPtr host=new WindowInteropHelper(overlay).Handle;SendMessage(host,0x112,new IntPtr(0xF020),IntPtr.Zero);Pump(.05);
   if(commands.Count==0||commands[commands.Count-1]!=7||IsIconic(sourceHandle))throw new Exception("Fixture did not defer the app minimize at the native command boundary.");
   Native.ShowWindowAsync(host,9);Pump(.1);
   if(commands[commands.Count-1]!=9)throw new Exception("User restore did not enqueue a restore after the outstanding minimize command.");
   foreach(int command in commands)Native.ShowWindowAsync(sourceHandle,command);Pump(.5);
   if(IsIconic(host)||IsIconic(sourceHandle))throw new Exception("A delayed app minimize undid a newer user restore request.");
   Console.WriteLine("PASS Quick restore wins over a queued minimize on a busy app UI thread");
  }finally {if(overlay!=null&&overlay.IsVisible)overlay.Close();source.Close();}
 }
 static void MinimizeFallbackFade() {
  var overlay=new BootWindow(new LauncherSettings{TransitionSeconds=2},null,true,1,
   new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fallback-minimize-check")));
  try {
   overlay.Show();Pump(1.3);Window fade=null;
   foreach(Window candidate in Application.Current.Windows)if(candidate!=overlay&&candidate.Title=="Codex Launch")fade=candidate;
   if(fade==null)throw new Exception("Preview did not enter its per-pixel fade.");
   SendMessage(new WindowInteropHelper(fade).Handle,0x112,new IntPtr(0xF020),IntPtr.Zero);Pump(.3);
   IntPtr host=new WindowInteropHelper(overlay).Handle;
   if(!IsIconic(host)||fade.IsVisible)throw new Exception("Fallback minimize did not transfer to the paused taskbar window.");
   Native.ShowWindowAsync(host,9);Pump(.35);
   if(IsIconic(host))throw new Exception("Fallback minimize could not be restored.");
   Pump(2.1);
   if(overlay.IsVisible)throw new Exception("Restored fallback fade did not finish.");
   Console.WriteLine("PASS Fallback fade can minimize, restore, and finish without orphan windows");
  }finally {if(overlay.IsVisible)overlay.Close();}
 }
 static void ResizeAndActivationHandoff() {
  var source=new Window {Title="Disposable startup fixture",Width=1450,Height=950,Left=40,Top=40,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};
  BootWindow overlay=null;var pulse=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(60)};
  var deadline=new DispatcherTimer{Interval=TimeSpan.FromSeconds(8)};bool timedOut=false;PixelRect finalOverlay=new PixelRect(),finalSource=new PixelRect();
  try {
   source.Show();Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
   IntPtr sourceHandle=new WindowInteropHelper(source).Handle;
   var initial=new WindowSample(sourceHandle,new PixelRect(100,80,960,600),true);
   overlay=new BootWindow(new LauncherSettings{MinimumAnimationSeconds=0,StableWindowSeconds=.1},initial,false,30,
    new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resize-check")),new FixtureProbe(sourceHandle));
   var loop=new DispatcherFrame();var time=Stopwatch.StartNew();int ticks=0;
   overlay.Closing+=delegate {finalOverlay=Native.ClientBounds(new WindowInteropHelper(overlay).Handle);finalSource=Native.ClientBounds(sourceHandle);};
   overlay.Closed+=delegate {loop.Continue=false;};
   pulse.Tick+=delegate {if(time.Elapsed.TotalSeconds>.7&&time.Elapsed.TotalSeconds<1.3){source.Left=40+(ticks%2)*30;source.Width=1450+(ticks++%2)*50;source.Activate();}else if(time.Elapsed.TotalSeconds>=1.3){source.WindowState=WindowState.Maximized;pulse.Stop();}};
   deadline.Tick+=delegate {timedOut=true;overlay.Close();loop.Continue=false;};
   overlay.Show();pulse.Start();deadline.Start();Dispatcher.PushFrame(loop);
   if(timedOut)throw new Exception("Resizing startup fixture left the overlay running.");
   if(!finalOverlay.NearlyEquals(finalSource))throw new Exception("Handoff did not finish at the app's client bounds.");
   if(ticks<2)throw new Exception("Startup resize/activation scenario did not run.");
   if(source.WindowState!=WindowState.Maximized)throw new Exception("Delayed maximization scenario did not run.");
   Console.WriteLine("PASS Startup movement, resize, activation, and delayed maximization finish an aligned handoff");
  }finally {pulse.Stop();deadline.Stop();if(overlay!=null&&overlay.IsVisible)overlay.Close();source.Close();}
 }
 [STAThread] public static int Main() {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};BootWindow window=null;
  try {
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"window-state.json"),"{\"Left\":100,\"Top\":80,\"Width\":900,\"Height\":600}");
   var coldInitial=new WindowSample(new IntPtr(123),new PixelRect(100,80,900,600),true){Minimized=true};
   window=new BootWindow(new LauncherSettings(),coldInitial,false,300,new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"host-check")),new FixtureProbe(IntPtr.Zero));
   window.Show();Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
   var coldBounds=Native.ClientBounds(new WindowInteropHelper(window).Handle);
   var compactBounds=Native.InitialBounds(new LauncherSettings());window.Close();
   if(!coldBounds.NearlyEquals(compactBounds))throw new Exception("Cold-start overlay did not open at compact centered bounds.");
   Console.WriteLine("PASS Cold start ignores stale placement and opens a compact centered splash");
   var expected=new PixelRect(100,80,1400,900);
   var initial=new WindowSample(new IntPtr(123),expected,true);
   window=new BootWindow(new LauncherSettings(),initial,true,300,new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"host-check")));
   window.Show();Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
   var observed=Native.ClientBounds(new WindowInteropHelper(window).Handle);
   window.Close();
   if(observed.NearlyEquals(expected))throw new Exception("Preview incorrectly opened at the full target size.");
   Console.WriteLine("PASS Preview keeps its compact opening even with an existing target sample");
   window=new BootWindow(new LauncherSettings(),null,true,300,new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"host-check")));
   window.Show();Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
   window.Close();
   if(window.IsVisible)throw new Exception("Normal window close left a visible overlay.");
   Console.WriteLine("PASS Normal window close completes without recursive Close");
   ResizeAndActivationHandoff();
   CompactExpansionPreservesApp();
   ExpansionKeepsTypography();
   ExpansionDoesNotResizeArtworkSurface();
   ArtworkLayerSupportsFallback();
   DelayedMinimizeRestore();
   MinimizeAndCoverage();
   MinimizeDuringReveal();
   MinimizeFallbackFade();
   window=new BootWindow(new LauncherSettings(),null,true,1,new Log(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"host-check")));
   app.ShutdownMode=ShutdownMode.OnMainWindowClose;app.MainWindow=window;app.Run(window);
   if(window.IsVisible)throw new Exception("Preview fade left its main window visible.");
   Console.WriteLine("PASS Preview fades and shuts down both overlay windows");return 0;
  } catch(Exception e) {Console.WriteLine("FAIL "+e.GetType().Name+": "+e.Message);return 1;}
  finally {if(window!=null&&window.IsVisible)window.Close();if(!app.Dispatcher.HasShutdownStarted)app.Shutdown();}
 }
}
