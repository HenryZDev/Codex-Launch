using System;
using System.Text.RegularExpressions;

namespace CodexLaunch {
 public struct PixelRect {
  public int Left,Top,Right,Bottom;
  public PixelRect(int x,int y,int width,int height) { Left=x;Top=y;Right=x+width;Bottom=y+height; }
  public int Width { get { return Right-Left; } }
  public int Height { get { return Bottom-Top; } }
  public bool IsUsable { get { return Width>=450&&Height>=280; } }
  public static PixelRect Interpolate(PixelRect from,PixelRect to,double progress) {
   double t=Math.Max(0,Math.Min(1,progress));
   var result=new PixelRect();
   result.Left=(int)Math.Round(from.Left+(to.Left-from.Left)*t);
   result.Top=(int)Math.Round(from.Top+(to.Top-from.Top)*t);
   result.Right=(int)Math.Round(from.Right+(to.Right-from.Right)*t);
   result.Bottom=(int)Math.Round(from.Bottom+(to.Bottom-from.Bottom)*t);
   return result;
  }
  public bool NearlyEquals(PixelRect other) {
   return Math.Abs(Left-other.Left)<=2&&Math.Abs(Top-other.Top)<=2
    &&Math.Abs(Right-other.Right)<=2&&Math.Abs(Bottom-other.Bottom)<=2;
  }
 }
 public static class WindowLayout {
  public static PixelRect Compact(PixelRect area,int logicalWidth,int logicalHeight,double dpiScale) {
   int width=Math.Max(1,Math.Min((int)Math.Round(logicalWidth*dpiScale),(int)(area.Width*.92)));
   int height=Math.Max(1,Math.Min((int)Math.Round(logicalHeight*dpiScale),(int)(area.Height*.90)));
   return new PixelRect(area.Left+(area.Width-width)/2,area.Top+(area.Height-height)/2,width,height);
  }
 }
 public sealed class LauncherSettings {
  public string ApplicationId="OpenAI.Codex_2p2nqsd0c76g0!App";
  public double MaxWaitSeconds=120;
  public double StableWindowSeconds=.75;
  public double TransitionSeconds=1.05;
  public double MinimumAnimationSeconds=2.5;
  public double ExpansionSeconds=.28;
  public bool ReducedMotion=false;
  public int WindowWidth=960,WindowHeight=600;
  public string PackageFamily { get { return ApplicationId.Split('!')[0]; } }
  public void Validate() {
   if(String.IsNullOrWhiteSpace(ApplicationId)||!Regex.IsMatch(ApplicationId,@"\A[A-Za-z0-9.-]+_[A-Za-z0-9]+![A-Za-z0-9._-]+\z"))
    throw new ArgumentException("ApplicationId must be a registered Windows package application identity.");
   Range(MaxWaitSeconds,10,600,"MaxWaitSeconds");Range(StableWindowSeconds,.1,10,"StableWindowSeconds");
   Range(TransitionSeconds,.15,5,"TransitionSeconds");
   Range(MinimumAnimationSeconds,0,10,"MinimumAnimationSeconds");
   Range(ExpansionSeconds,0,3,"ExpansionSeconds");
   if(WindowWidth<450||WindowWidth>3840||WindowHeight<280||WindowHeight>2160)
    throw new ArgumentException("WindowWidth and WindowHeight must fit a desktop window (450–3840 by 280–2160).");
  }
  static void Range(double value,double min,double max,string name) {
   if(Double.IsNaN(value)||Double.IsInfinity(value)||value<min||value>max)
    throw new ArgumentException(name+" must be between "+min+" and "+max+".");
  }
 }
 public static class PackageIdentity {
  public static bool Matches(string observed,string expected) {
   return !String.IsNullOrEmpty(observed)&&!String.IsNullOrEmpty(expected)
    &&String.Equals(observed,expected,StringComparison.OrdinalIgnoreCase);
  }
 }
 public sealed class WindowSample {
  public IntPtr Handle;
  public PixelRect Bounds;
  public bool Responsive;
  public bool Minimized;
  public WindowSample(IntPtr handle,PixelRect bounds,bool responsive) { Handle=handle;Bounds=bounds;Responsive=responsive; }
 }
 public static class WindowPolicy {
  public static bool KeepCover(IntPtr foreground,IntPtr host,IntPtr fader,IntPtr target,bool matchesApp,bool allowed) {
   if(foreground==IntPtr.Zero)return allowed;
   if(foreground==host||(fader!=IntPtr.Zero&&foreground==fader))return true;
   return allowed&&(matchesApp||(target!=IntPtr.Zero&&foreground==target));
  }
  public static WindowSample Choose(WindowSample current,WindowSample candidate,IntPtr preferred,IntPtr foreground) {
   if(candidate==null)return current;if(current==null)return candidate;
   if(preferred!=IntPtr.Zero){if(current.Handle==preferred)return current;if(candidate.Handle==preferred)return candidate;}
   if(current.Handle==foreground)return current;if(candidate.Handle==foreground)return candidate;
   if(current.Minimized!=candidate.Minimized)return current.Minimized?candidate:current;
   return (long)candidate.Bounds.Width*candidate.Bounds.Height>(long)current.Bounds.Width*current.Bounds.Height?candidate:current;
  }
  public static bool ShouldRestore(WindowSample sample,IntPtr host,IntPtr foreground,IntPtr restored,bool sawVisibleTarget) {
   return sample!=null&&sample.Handle!=IntPtr.Zero&&sample.Minimized&&!sawVisibleTarget&&sample.Handle!=restored
    &&(foreground==host||foreground==sample.Handle);
  }
 }
 public enum StartupPhase { Waiting,Aligning,Revealing,Finished }
 public sealed class StartupSession {
  readonly LauncherSettings settings;
  IntPtr stableHandle;
  PixelRect stableBounds;
  double stableSince,transitionStart,alignmentStart;
  WindowSample latest;
  public StartupPhase Phase { get; private set; }
  public WindowSample Target { get; private set; }
  public double Progress { get; private set; }
  public double AlignmentProgress { get; private set; }
  public string CloseReason { get; private set; }
  public StartupSession(LauncherSettings settings) { settings.Validate();this.settings=settings;Phase=StartupPhase.Waiting; }
  public void Advance(double seconds,WindowSample sample) {
   if(Phase==StartupPhase.Finished)return;
   latest=sample;
   if(seconds>=settings.MaxWaitSeconds){Target=sample;Finish("timeout");return;}
   bool usable=sample!=null&&sample.Handle!=IntPtr.Zero&&sample.Bounds.IsUsable&&sample.Responsive&&!sample.Minimized;
   if(Phase==StartupPhase.Aligning||Phase==StartupPhase.Revealing) {
    if(!usable||sample.Handle!=Target.Handle||!Target.Bounds.NearlyEquals(sample.Bounds)){Phase=StartupPhase.Waiting;Target=null;Progress=0;AlignmentProgress=0;stableHandle=IntPtr.Zero;}
    else {
     // Keep the accepted target as a fixed anchor for this whole phase.
     // Updating it each probe would allow slow 1px drift to accumulate.
     if(Phase==StartupPhase.Aligning) {
      double moving=settings.ReducedMotion?0:settings.ExpansionSeconds;
      AlignmentProgress=moving==0?1:Math.Max(0,Math.Min(1,(seconds-alignmentStart)/moving));
      // Paint the final-size opaque frame before introducing live app content.
      if(seconds-alignmentStart>=moving+.12){Phase=StartupPhase.Revealing;transitionStart=seconds;Progress=0;}
     } else {
      Progress=Math.Max(0,Math.Min(1,(seconds-transitionStart)/settings.TransitionSeconds));
      if(Progress>=1)Finish("handoff");
     }
     return;
    }
   }
   if(!usable){stableHandle=IntPtr.Zero;return;}
   if(sample.Handle!=stableHandle||!stableBounds.NearlyEquals(sample.Bounds)) {
    stableHandle=sample.Handle;stableBounds=sample.Bounds;stableSince=seconds;
   }
   if(seconds-stableSince>=settings.StableWindowSeconds&&seconds>=settings.MinimumAnimationSeconds) {
    Target=sample;Phase=StartupPhase.Aligning;alignmentStart=seconds;AlignmentProgress=0;Progress=0;
   }
  }
  public void Skip() { if(Phase!=StartupPhase.Finished){Target=latest;Finish("skipped");} }
  public void RestartStability() {
   if(Phase==StartupPhase.Finished)return;
   Phase=StartupPhase.Waiting;Target=null;Progress=0;AlignmentProgress=0;stableHandle=IntPtr.Zero;
  }
  void Finish(string reason){Phase=StartupPhase.Finished;CloseReason=reason;}
 }
}
