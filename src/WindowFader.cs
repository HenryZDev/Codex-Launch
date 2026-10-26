using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexLaunch {
 // A separate per-pixel window is used only for the brief non-DWM fade.
 // The normal animation/DWM host retains its efficient opaque rendering mode.
 internal sealed class WindowFader:IDisposable {
  readonly Window window;
  bool disposed;
  internal IntPtr Handle {get;private set;}
  internal bool Minimized {get{return !disposed&&window.WindowState==WindowState.Minimized;}}
  internal WindowFader(FrameworkElement visual,PixelRect bounds,bool activate,Action dismissed) {
   DpiScale dpi=VisualTreeHelper.GetDpi(visual);
   int width=Math.Max(1,(int)Math.Ceiling(visual.ActualWidth*dpi.DpiScaleX));
   int height=Math.Max(1,(int)Math.Ceiling(visual.ActualHeight*dpi.DpiScaleY));
   Button original=null;var panel=visual as Panel;
   if(panel!=null)foreach(UIElement child in panel.Children)if(child is Button){original=(Button)child;break;}
   var image=new RenderTargetBitmap(width,height,96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);
   double opacity=original==null?0:original.Opacity;
   try {if(original!=null)original.Opacity=0;image.Render(visual);}
   finally {if(original!=null)original.Opacity=opacity;}
   image.Freeze();
   var content=new Grid();content.Children.Add(new Image {Source=image,Stretch=Stretch.Fill});
   if(original!=null) {
    var skip=new Button{Content=original.Content,Style=original.Style,FontFamily=original.FontFamily,FontSize=original.FontSize,
     HorizontalAlignment=original.HorizontalAlignment,VerticalAlignment=original.VerticalAlignment,Margin=original.Margin,Padding=original.Padding,
     Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand,Opacity=opacity};
    skip.Click+=delegate {dismissed();};content.Children.Add(skip);
   }
   window=new Window {Title="Codex Launch",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.CanMinimize,
    AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=true,ShowActivated=activate,
    Width=Math.Max(1,visual.ActualWidth),Height=Math.Max(1,visual.ActualHeight),WindowStartupLocation=WindowStartupLocation.Manual,
    Content=content};
   window.SourceInitialized+=delegate {Handle=new WindowInteropHelper(window).Handle;Native.Position(Handle,bounds);};
   window.PreviewKeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;dismissed();}};
   window.Closed+=delegate {if(!disposed)window.Dispatcher.BeginInvoke(new Action(dismissed));};
   window.Show();
  }
  internal void Move(PixelRect bounds){if(!disposed)Native.Position(Handle,bounds);}
  internal void Own(Window child){if(!disposed)child.Owner=window;}
  internal void BringForward(){if(!disposed)window.Activate();}
  internal void SetCover(bool cover){if(!disposed&&window.Topmost!=cover)window.Topmost=cover;}
  internal void SetOpacity(double opacity){if(!disposed)window.Opacity=Math.Max(0,Math.Min(1,opacity));}
  public void Dispose(){if(disposed)return;disposed=true;if(window.IsVisible)window.Close();}
 }
}
