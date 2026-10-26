using System;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodexLaunch {
 internal sealed class AnimationView:FrameworkElement {
  internal double Seconds,Reveal;
  internal bool ReducedMotion;
  internal bool TransparentBackground;
  readonly Pen[] trails=new Pen[65];
  readonly SolidColorBrush[] particles=new SolidColorBrush[65];
  readonly SolidColorBrush background=Brush(17,18,21),white=Brush(240,242,245),muted=Brush(169,175,184);
  readonly RadialGradientBrush halo;
  readonly Typeface typeface=new Typeface(new FontFamily("Segoe UI Variable, Segoe UI"),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
  readonly Pen logo;
  FormattedText wordmark,greeting;
  double textDpi;
  double referenceWidth,referenceHeight;
  static SolidColorBrush Brush(byte r,byte g,byte b){var brush=new SolidColorBrush(Color.FromRgb(r,g,b));brush.Freeze();return brush;}
  internal AnimationView() {
   IsHitTestVisible=false;
   for(int i=0;i<trails.Length;i++) {
    var color=Color.FromArgb((byte)i,198,217,243);var brush=new SolidColorBrush(color);brush.Freeze();
    trails[i]=new Pen(brush,.8);trails[i].Freeze();particles[i]=brush;
   }
   logo=new Pen(white,6.44){StartLineCap=PenLineCap.Square,EndLineCap=PenLineCap.Square,LineJoin=PenLineJoin.Miter};logo.Freeze();
   halo=new RadialGradientBrush();halo.GradientStops.Add(new GradientStop(Color.FromArgb(12,180,205,235),0));
   halo.GradientStops.Add(new GradientStop(Color.FromArgb(0,180,205,235),1));halo.Freeze();
   RenderOptions.SetEdgeMode(this,EdgeMode.Unspecified);
  }
  internal static double Clamp(double value){return Math.Max(0,Math.Min(1,value));}
  internal static double Ease(double value){value=Clamp(value);return value*value*(3-2*value);}
  static double Out(double value){return 1-Math.Pow(1-Clamp(value),3);}
  void EnsureText() {
   double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
   if(wordmark!=null&&Math.Abs(textDpi-dpi)<.01)return;textDpi=dpi;
   wordmark=new FormattedText("Codex",CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,typeface,25,white,dpi);
   greeting=new FormattedText("Bring ideas to life.",CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,typeface,13,muted,dpi);
  }
  protected override void OnRender(DrawingContext dc) {
   base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;
   EnsureText();if(!TransparentBackground)dc.DrawRectangle(background,null,new Rect(0,0,w,h));
   if(referenceWidth==0){referenceWidth=w;referenceHeight=h;}
   double t=ReducedMotion?3:Seconds,cx=w*.5,cy=h*.5-referenceHeight*.04;
   // Size the composition once for the compact opening. Expanding the host
   // reveals more background; it must not zoom or reflow the typography.
   double unit=Math.Max(.65,Math.Min(2.4,1.25*Math.Min(referenceWidth/960,referenceHeight/600)));
   dc.PushOpacity(1-Ease(Reveal));
   dc.PushTransform(new TranslateTransform(cx,cy));dc.PushTransform(new ScaleTransform(unit,unit));
   cx=0;cy=0;
   dc.PushTransform(new TranslateTransform(0,-12));
   dc.DrawEllipse(halo,null,new Point(),155,155);
   double settle=Ease((t-.15)/2.3),quiet=1-.38*Ease((t-12)/18);
   for(int k=0;k<3;k++) {
    double angle=t*.21+k*Math.PI*2/3,rx=185-59*settle+k*14,ry=57-16*settle+k*7;
    double tilt=.35*Math.Sin(angle),amplitude=(1-settle)*21;
    for(int i=0;i<90;i++) {
     double u=i/90.0*Math.PI*2,next=(i+1)/90.0*Math.PI*2;
     double glint=Math.Pow((Math.Sin(u-t*.65+k)+1)/2,9);
     int alpha=(int)Math.Round((.016+.105*glint)*Out(t/1.1)*quiet*255);
     dc.DrawLine(trails[Math.Max(0,Math.Min(64,alpha))],Orbit(u,rx,ry,tilt,amplitude,t),Orbit(next,rx,ry,tilt,amplitude,t));
    }
   }
   if(t<2.5&&!ReducedMotion)for(int i=0;i<34;i++) {
    double seed=i*2.399963,travel=Out((t-i*.011)/1.9),radius=(1-travel)*(120+(i%7)*19)+43;
    double x=Math.Cos(seed+travel*.2)*radius,y=Math.Sin(seed+travel*.2)*radius*.57;
    int alpha=(int)Math.Round((1-travel)*Out(t/.25)*.42*255);
    dc.DrawRectangle(particles[Math.Min(64,Math.Max(0,alpha))],null,new Rect(x,y,1.1,1.1));
   }
   dc.Pop();
   double breathe=ReducedMotion?1:1+Math.Sin(t*1.15)*.006*(1-Ease((t-12)/18)*.6);
   dc.PushTransform(new ScaleTransform(breathe,breathe,cx,cy));
   Point p1=new Point(cx-27.6,cy-41.94),p2=new Point(cx-2.76,cy-21.7),p3=new Point(cx-27.6,cy-1.46);
   double line=ReducedMotion?1:Out((t-.3)/1.35);
   DrawProgress(dc,p1,p2,Clamp(line*2));
   if(line>.5)DrawProgress(dc,p2,p3,Clamp(line*2-1));
   double underline=ReducedMotion?1:Out((t-.92)/.65);
   DrawProgress(dc,new Point(cx+6.44,cy-1.46),new Point(cx+30.36,cy-1.46),underline);
   dc.Pop();
   dc.PushOpacity(ReducedMotion?1:Ease((t-1)/.8));
   dc.DrawText(wordmark,new Point(cx-wordmark.Width/2,cy+28.5));dc.Pop();
   double welcome=ReducedMotion?1:Ease((t-1.5)/.75);
   dc.PushOpacity(welcome);dc.DrawText(greeting,new Point(cx-greeting.Width/2,cy+82+(ReducedMotion?0:4*(1-welcome))));dc.Pop();
   dc.Pop();dc.Pop();dc.Pop();
  }
  void DrawProgress(DrawingContext dc,Point a,Point b,double progress) {
   if(progress<=0)return;dc.DrawLine(logo,a,new Point(a.X+(b.X-a.X)*progress,a.Y+(b.Y-a.Y)*progress));
  }
  static Point Orbit(double u,double rx,double ry,double tilt,double amplitude,double time) {
   double x=Math.Cos(u)*rx,y=Math.Sin(u)*ry;
   return new Point(x*Math.Cos(tilt)-y*Math.Sin(tilt),x*Math.Sin(tilt)+y*Math.Cos(tilt)+Math.Sin(u*3+time)*amplitude);
  }
 }
 // Keep the animated composition on a surface which never resizes. Native
 // host resizing can otherwise present an intermediate retained frame before
 // WPF has re-centered its drawing. Move this surface as a whole instead.
 internal sealed class ArtworkWindow:Window,IDisposable {
  readonly int width,height;
  IntPtr handle;
  bool closed;
  internal ArtworkWindow(Window owner,AnimationView view,PixelRect opening) {
   width=opening.Width;height=opening.Height;
   Owner=owner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
   AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;
   ShowActivated=false;Focusable=false;IsHitTestVisible=false;
   WindowStartupLocation=WindowStartupLocation.Manual;
   Width=owner.ActualWidth;Height=owner.ActualHeight;
   view.TransparentBackground=true;Content=view;
   SourceInitialized+=delegate {handle=new WindowInteropHelper(this).Handle;Native.MakeInputTransparent(handle);Native.Position(handle,opening);};
   Closed+=delegate {closed=true;};
  }
  internal void Move(PixelRect background) {
   var bounds=new PixelRect(background.Left+(background.Width-width)/2,background.Top+(background.Height-height)/2,width,height);
   PixelRect current=Native.ClientBounds(handle);
   if(current.Left!=bounds.Left||current.Top!=bounds.Top)Native.Position(handle,bounds);
  }
  internal void SetCover(bool cover){if(!closed&&Topmost!=cover)Topmost=cover;}
  public void Dispose(){if(!closed)Close();}
 }
}
