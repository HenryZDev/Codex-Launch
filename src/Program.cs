using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

[assembly:AssemblyTitle("Codex Launch")]
[assembly:AssemblyProduct("Codex Launch")]
[assembly:AssemblyDescription("A quiet, independent startup animation for the installed Codex app")]
[assembly:AssemblyVersion("1.4.2.0")]
[assembly:AssemblyFileVersion("1.4.2.0")]

namespace CodexLaunch {
 internal sealed class Log {
  readonly string file;readonly object sync=new object();
  internal Log(string root){file=Path.Combine(root,"logs","launcher.log");}
  internal void Write(string text) {
   try {lock(sync){Directory.CreateDirectory(Path.GetDirectoryName(file));if(File.Exists(file)&&new FileInfo(file).Length>262144)File.WriteAllText(file,"");File.AppendAllText(file,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture)+" "+text+Environment.NewLine);}}
   catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }
 internal sealed class BootWindow:Window {
  readonly LauncherSettings settings;readonly StartupSession session;readonly Log log;
  readonly bool preview;readonly double previewSeconds;readonly WindowSample initial;
  readonly AnimationView view;readonly TextBlock status;readonly Button skip;
  readonly Grid grid;
  readonly Stopwatch clock=new Stopwatch();
  readonly DispatcherTimer interactionTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
  IntPtr host,restored,pausedTarget,restorePending,lastForeground;
  IWindowProbe probe;LiveThumbnail live;
  readonly IWindowProbe suppliedProbe;
  readonly Action<IntPtr,int> windowStateCommand;
  WindowFader fader;
  ArtworkWindow artwork;
  PixelRect initialBounds,revealFrom;
  StartupPhase lastPhase=StartupPhase.Waiting;
  bool closing,fallback,sawVisibleTarget,focusOnClose,rendering,launchQueued,initialized,paused,coverAllowed=true,foregroundIsApp;
  int renderedFrames,nestedFrames;
  double previousFrame,maxFrameGap;
  WindowSample closingTarget;
  string closeReason="dismissed";
  internal BootWindow(LauncherSettings settings,WindowSample initial,bool preview,double previewSeconds,Log log,IWindowProbe suppliedProbe=null,Action<IntPtr,int> windowStateCommand=null) {
   this.settings=settings;this.initial=initial;this.preview=preview;this.previewSeconds=previewSeconds;this.log=log;
   this.suppliedProbe=suppliedProbe;
   this.windowStateCommand=windowStateCommand??delegate(IntPtr target,int command){Native.ShowWindowAsync(target,command);};
   settings.ReducedMotion=settings.ReducedMotion||!SystemParameters.ClientAreaAnimation;
   if(settings.ReducedMotion){settings.TransitionSeconds=.25;settings.MinimumAnimationSeconds=0;}
   session=new StartupSession(settings);
   Title=preview?"Codex Launch — Preview":"Codex Launch";
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanMinimize;ShowInTaskbar=true;
   Background=new SolidColorBrush(Color.FromRgb(17,18,21));
   Width=settings.WindowWidth;Height=settings.WindowHeight;WindowStartupLocation=WindowStartupLocation.Manual;
   initialBounds=Native.InitialBounds(settings,initial!=null&&!initial.Minimized&&initial.Bounds.IsUsable?(PixelRect?)initial.Bounds:null);
   grid=new Grid{Background=Background};view=new AnimationView {ReducedMotion=settings.ReducedMotion};grid.Children.Add(view);
   status=new TextBlock {Text=preview?"Animation preview · Codex will not open":"Opening your workspace",FontFamily=new FontFamily("Segoe UI"),FontSize=11,
    Foreground=new SolidColorBrush(Color.FromRgb(133,139,148)),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,
    Margin=new Thickness(25,0,170,24),Opacity=0};grid.Children.Add(status);
   skip=new Button {Content=preview?"Close preview  Esc":"Skip animation  Esc",FontFamily=new FontFamily("Segoe UI"),FontSize=11,Style=SkipStyle(),
    Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(8,6,8,6),
    HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,20,18),Cursor=Cursors.Hand};
   skip.Click+=delegate {session.Skip();Finish("skipped");};grid.Children.Add(skip);
   if(preview)grid.Children.Add(new TextBlock {Text="ANIMATION PREVIEW",FontFamily=new FontFamily("Consolas"),FontSize=9,Foreground=new SolidColorBrush(Color.FromRgb(95,100,108)),Margin=new Thickness(20,16,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top});
   Content=grid;
   PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;session.Skip();Finish("skipped");}};
   SourceInitialized+=delegate {host=new WindowInteropHelper(this).Handle;Native.Position(host,initialBounds);Native.RoundCorners(host);};
   Activated+=delegate {if(!closing&&WindowState!=WindowState.Minimized){coverAllowed=true;SetCover(true);}};
   StateChanged+=delegate {if(initialized&&!closing)Render(null,EventArgs.Empty);};
   Loaded+=delegate {
    if(initialized)return;initialized=true;
    double utility=Math.Max(1,Math.Min(1.3,Math.Min(ActualWidth/960,ActualHeight/600)));
    status.FontSize=13*utility;skip.FontSize=13*utility;
    status.Margin=new Thickness(32*utility,0,200*utility,28*utility);
    skip.Margin=new Thickness(0,0,26*utility,22*utility);
    skip.Padding=new Thickness(10*utility,8*utility,10*utility,8*utility);
    clock.Start();CompositionTarget.Rendering+=Render;
    if(!preview)probe=suppliedProbe??new WindowProbe(settings.PackageFamily,initial);
    interactionTimer.Tick+=Render;interactionTimer.Start();
    log.Write(preview?"preview-started":"started; existing-window="+(initial!=null));
    log.Write("overlay-bounds="+RectText(Native.ClientBounds(host))+"; reduced-motion="+settings.ReducedMotion);
   };
   Closing+=delegate {
    if(closing)return;closing=true;
    closingTarget=session.Target??(probe==null?initial:probe.Current);
    IntPtr foreground=Native.GetForegroundWindow();focusOnClose=foreground==host||(fader!=null&&foreground==fader.Handle)||(closingTarget!=null&&foreground==closingTarget.Handle);
    log.Write(closeReason+"; elapsed="+clock.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture));
    log.Write("frame-stats; frames="+renderedFrames+"; nested-skipped="+nestedFrames+"; max-gap-ms="+(maxFrameGap*1000).ToString("F1",CultureInfo.InvariantCulture));
   };
   Closed+=delegate {
    CompositionTarget.Rendering-=Render;interactionTimer.Stop();if(probe!=null)probe.Dispose();if(live!=null)live.Dispose();if(artwork!=null)artwork.Dispose();if(fader!=null)fader.Dispose();clock.Stop();
    if(!preview&&focusOnClose&&closingTarget!=null&&!closingTarget.Minimized&&Native.IsWindow(closingTarget.Handle))Native.SetForegroundWindow(closingTarget.Handle);
   };
  }
  void Launch() {
   Task.Run(delegate {
    try {
     string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
     using(Process process=Process.Start(new ProcessStartInfo(shell,"shell:AppsFolder\\"+settings.ApplicationId){UseShellExecute=true})){}
     log.Write("app-launch-requested");
    } catch(Exception e) {
     log.Write("app-launch-failed: "+e.Message);
     if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(delegate {if(!closing){Finish("launch-failed");MessageBox.Show("Windows could not open Codex. Check ApplicationId in settings.json.\n\n"+e.Message,"Codex Launch",MessageBoxButton.OK,MessageBoxImage.Information);}}));
    }
   });
  }
  void Render(object sender,EventArgs args) {
   // Window resize, activation, and bitmap rendering can synchronously raise
   // Rendering again. A whole frame must finish before another mutates state.
   if(closing)return;if(rendering){nestedFrames++;return;}rendering=true;
   try {RenderFrame();}finally {rendering=false;}
  }
  void RenderFrame() {
   if(closing)return;double now=clock.Elapsed.TotalSeconds;
   if(renderedFrames>0)maxFrameGap=Math.Max(maxFrameGap,now-previousFrame);previousFrame=now;renderedFrames++;
   if(!preview&&initial==null&&!launchQueued){
    launchQueued=true;
    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(delegate {if(!closing&&(probe==null||probe.Current==null))Launch();}));
   }
   WindowSample sample=probe==null?initial:probe.Current;
   if(sample!=null&&!Native.IsWindow(sample.Handle))sample=null;
   if(sample!=null&&sample.Handle==restorePending&&!Native.IsIconic(sample.Handle))restorePending=IntPtr.Zero;
   // The user can use either taskbar button. Once the app has been visible,
   // follow its minimize request as well as a minimize of the overlay itself.
   if(!preview&&!paused&&sawVisibleTarget&&sample!=null&&sample.Handle!=restorePending&&sample.Minimized&&Native.IsIconic(sample.Handle)&&WindowState!=WindowState.Minimized)
    WindowState=WindowState.Minimized;
   if(WindowState==WindowState.Minimized||(fader!=null&&fader.Minimized)) {
    if(!paused) {
     paused=true;restorePending=IntPtr.Zero;clock.Stop();SetCover(false);session.RestartStability();lastPhase=StartupPhase.Waiting;
     if(live!=null){live.Dispose();live=null;}
     ReleaseArtwork();
     if(fader!=null){fader.Dispose();fader=null;ShowActivated=false;WindowState=WindowState.Minimized;Show();}
     view.Reveal=0;skip.Opacity=1;log.Write("user-minimized; animation-paused");
    }
    if(!preview&&sample!=null) {
     pausedTarget=sample.Handle;
     if(!Native.IsIconic(sample.Handle)){windowStateCommand(sample.Handle,7);log.Write("loading-window-minimized");}
    }
    return;
   }
   if(paused) {
    paused=false;coverAllowed=true;SetCover(true);clock.Start();previousFrame=now;
    // A minimize may still be queued on a busy app thread. Order the restore
    // after it even when IsIconic has not changed yet.
    if(pausedTarget!=IntPtr.Zero&&Native.IsWindow(pausedTarget)){restorePending=pausedTarget;windowStateCommand(pausedTarget,9);}
    pausedTarget=IntPtr.Zero;log.Write("user-restored; animation-resumed");
   }
   IntPtr foreground=Native.GetForegroundWindow();
   if(foreground!=lastForeground) {
    lastForeground=foreground;
    foregroundIsApp=foreground!=host&&(fader==null||foreground!=fader.Handle)&&Native.MatchesPackage(foreground,settings.PackageFamily);
   }
   coverAllowed=WindowPolicy.KeepCover(foreground,host,fader==null?IntPtr.Zero:fader.Handle,sample==null?IntPtr.Zero:sample.Handle,foregroundIsApp,coverAllowed);
   SetCover(coverAllowed);
   if(closing)return;
   view.Seconds=now;
   status.Opacity=settings.ReducedMotion?1:AnimationView.Ease((now-1.65)/.7);
   if(preview) {
    if(now>=previewSeconds){double progress=AnimationView.Clamp((now-previewSeconds)/settings.TransitionSeconds);EnsureFader();fader.SetOpacity(1-AnimationView.Ease(progress));if(progress>=1){Finish("preview-finished");return;}}
    view.InvalidateVisual();return;
   }
   if(WindowPolicy.ShouldRestore(sample,host,Native.GetForegroundWindow(),restored,sawVisibleTarget)){restored=sample.Handle;windowStateCommand(sample.Handle,9);log.Write("restoring-minimized-window");}
   if(sample!=null&&!sample.Minimized)sawVisibleTarget=true;
   if(closing)return;
   session.Advance(now,sample);
   if(session.Phase==StartupPhase.Finished){Finish(session.CloseReason);return;}
   if(session.Phase==StartupPhase.Aligning) {
    if(lastPhase!=StartupPhase.Aligning){revealFrom=Native.OuterBounds(host);EnsureArtwork();log.Write("aligning; from="+RectText(revealFrom)+"; to="+RectText(session.Target.Bounds));}
    PixelRect aligned=PixelRect.Interpolate(revealFrom,session.Target.Bounds,AnimationView.Ease(session.AlignmentProgress));
    artwork.Move(aligned);
    if(!Native.ClientBounds(host).NearlyEquals(aligned))Native.Position(host,aligned);
    if(closing)return;
    view.Reveal=0;
   } else if(session.Phase==StartupPhase.Revealing) {
    if(lastPhase!=StartupPhase.Revealing) {
     Native.Position(host,session.Target.Bounds);if(closing)return;
     if(artwork!=null)artwork.Move(session.Target.Bounds);
     if(live!=null)live.Dispose();live=new LiveThumbnail(host,session.Target.Handle);fallback=!live.Connected;
     log.Write((fallback?"handoff-fallback; dwm="+live.LastError:"live-window-connected")+"; overlay="+RectText(Native.ClientBounds(host))+"; target="+RectText(session.Target.Bounds));
    }
    double p=session.Progress,fade=AnimationView.Ease(p);
    if(!fallback) {
     PixelRect client=Native.ClientBounds(host);
     if(!live.Update(client.Width,client.Height,(byte)Math.Round(fade*255))){fallback=true;log.Write("dwm-update-fallback; code="+live.LastError);live.Dispose();}
    }
    if(fallback){EnsureFader();fader.Move(session.Target.Bounds);fader.SetOpacity(1-fade);}
    view.Reveal=fade;status.Opacity*=1-AnimationView.Ease(p*2);skip.Opacity=1-AnimationView.Ease(p*2);
   } else {
    if(lastPhase==StartupPhase.Revealing||lastPhase==StartupPhase.Aligning){if(live!=null){live.Dispose();live=null;}if(fader!=null){bool focused=Native.GetForegroundWindow()==fader.Handle;ReleaseArtwork();fader.Dispose();fader=null;ShowActivated=focused;Show();}view.Reveal=0;skip.Opacity=1;log.Write("target-changed; waiting-again");}
    status.Text=now>45?"Codex is taking longer than usual · Esc to skip":now>18?"Still opening your workspace":"Opening your workspace";
   }
   lastPhase=session.Phase;view.InvalidateVisual();
  }
  internal void Finish(string reason) {
   if(closing)return;closeReason=reason;Close();
  }
  void EnsureArtwork() {
   if(artwork!=null)return;
   PixelRect opening=Native.ClientBounds(host);
   grid.Children.Remove(view);
   artwork=new ArtworkWindow(this,view,opening);artwork.SetCover(Topmost);artwork.Show();
  }
  void ReleaseArtwork() {
   if(artwork==null)return;
   artwork.Content=null;artwork.Dispose();artwork=null;
   view.TransparentBackground=false;grid.Children.Insert(0,view);
  }
  void EnsureFader() {
   if(fader!=null)return;
   if(live!=null){live.Dispose();live=null;}
   fader=new WindowFader(grid,Native.OuterBounds(host),Native.GetForegroundWindow()==host,delegate {Finish("dismissed");});
   fader.SetCover(Topmost);
   if(artwork!=null)fader.Own(artwork);
   Hide();
  }
  void SetCover(bool cover) {
   if(Topmost!=cover){Topmost=cover;log.Write(cover?"cover-enabled":"cover-yielded");}
   if(fader!=null)fader.SetCover(cover);
   if(artwork!=null)artwork.SetCover(cover);
  }
  static Style SkipStyle() {
   var content=new FrameworkElementFactory(typeof(ContentPresenter));
   content.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);
   content.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);
   var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);
   border.SetBinding(Border.PaddingProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});border.AppendChild(content);
   var style=new Style(typeof(Button));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Button)){VisualTree=border}));
   style.Setters.Add(new Setter(Control.ForegroundProperty,new SolidColorBrush(Color.FromRgb(143,149,159))));
   style.Setters.Add(new Setter(Control.FocusVisualStyleProperty,null));
   foreach(var property in new[]{UIElement.IsMouseOverProperty,UIElement.IsKeyboardFocusedProperty}) {
    var trigger=new Trigger{Property=property,Value=true};trigger.Setters.Add(new Setter(Control.ForegroundProperty,new SolidColorBrush(Color.FromRgb(220,224,230))));style.Triggers.Add(trigger);
   }
   return style;
  }
  static string RectText(PixelRect r){return r.Left+","+r.Top+" "+r.Width+"x"+r.Height;}
 }

 internal static class Program {
  [STAThread] public static int Main(string[] args) {
   string root=AppDomain.CurrentDomain.BaseDirectory;var log=new Log(root);
   try {
    string settingsFile=Path.Combine(root,"settings.json");
    var settings=File.Exists(settingsFile)?new JavaScriptSerializer().Deserialize<LauncherSettings>(File.ReadAllText(settingsFile)):new LauncherSettings();
    if(settings==null)throw new ArgumentException("settings.json is empty.");settings.Validate();
    bool preview=false,diagnose=false;double seconds=30;
    for(int i=0;i<args.Length;i++) {
     if(args[i]=="--preview")preview=true;
     else if(args[i]=="--diagnose")diagnose=true;
     else if(args[i]=="--seconds"&&i+1<args.Length){if(!Double.TryParse(args[++i],NumberStyles.Float,CultureInfo.InvariantCulture,out seconds)||Double.IsNaN(seconds)||Double.IsInfinity(seconds)||seconds<1||seconds>600)throw new ArgumentException("--seconds must be between 1 and 600.");}
     else throw new ArgumentException("Use Codex Launch.exe, --preview [--seconds 30], or --diagnose.");
    }
    WindowSample initial=preview?null:Native.FindTarget(settings.PackageFamily);
    if(diagnose) {
     var data=new { applicationId=settings.ApplicationId,packageFamily=settings.PackageFamily,windowFound=initial!=null,responsive=initial!=null&&initial.Responsive,
      minimized=initial!=null&&initial.Minimized,bounds=initial==null?(object)null:new {x=initial.Bounds.Left,y=initial.Bounds.Top,width=initial.Bounds.Width,height=initial.Bounds.Height} };
     File.WriteAllText(Path.Combine(root,"diagnostics.json"),new JavaScriptSerializer().Serialize(data));return initial!=null?0:1;
    }
    bool owns;using(var mutex=new Mutex(true,"Local\\CodexLaunch."+settings.PackageFamily+(preview?".Preview":""),out owns)) {
     if(!owns){log.Write("duplicate-launch-ignored");return 0;}
     try {
      if(!preview) {
       // Recheck after acquiring the launch mutex: a window may have
       // appeared since the first lookup. Never construct an overlay here.
       initial=Native.FindTarget(settings.PackageFamily);
       if(Native.ActivateExisting(initial)){log.Write("existing-window-activated; animation-skipped");return 0;}
      }
      var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
      var window=new BootWindow(settings,initial,preview,seconds,log);app.MainWindow=window;
      app.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e){log.Write("ui-error: "+e.Exception);e.Handled=true;window.Finish("error");};
      return app.Run(window);
     } finally {mutex.ReleaseMutex();}
    }
   } catch(Exception e) {log.Write("startup-error: "+e);MessageBox.Show("Codex Launch could not start.\n\n"+e.Message,"Codex Launch",MessageBoxButton.OK,MessageBoxImage.Information);return 2;}
  }
 }
}
