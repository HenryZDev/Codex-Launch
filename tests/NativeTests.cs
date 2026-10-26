using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using CodexLaunch;

static class NativeTests {
 static Button FindButton(DependencyObject root) {
  var button=root as Button;if(button!=null)return button;
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindButton(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}
  return null;
 }
 [STAThread] public static int Main() {
  Window source=null,destination=null;
  try {
   source=new Window { Title="ChatGPT",Width=640,Height=400,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.CenterScreen };
   var content=new Border {Background=Brushes.DarkSlateBlue};
   destination=new Window { Title="Codex Launch — integration check",Content=content,Width=640,Height=400,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.CenterScreen };
   source.Show();destination.Show();Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
   IntPtr a=new WindowInteropHelper(source).Handle,b=new WindowInteropHelper(destination).Handle;
   if(!Native.ClientBounds(a).IsUsable)throw new Exception("Native client bounds were not usable.");
   if(Native.FindTarget("A.Nonexistent.Package_publisher")!=null)throw new Exception("A matching title was mistaken for a package identity.");
   using(var live=new LiveThumbnail(b,a)) {
    if(!live.Connected)throw new Exception("DWM registration failed: "+live.LastError);
    PixelRect bounds=Native.ClientBounds(b);
    if(!live.Update(bounds.Width,bounds.Height,128))throw new Exception("DWM update failed: "+live.LastError);
    Console.WriteLine("PASS DWM live client-area thumbnail registered and updated");
    source.Close();source=null;
    if(live.Update(bounds.Width,bounds.Height,255))throw new Exception("Destroyed source was still considered usable.");
    Console.WriteLine("PASS Destroyed source detected before handoff");
   }
   Console.WriteLine("PASS Unrelated same-title window rejected");
   using(var fade=new WindowFader(content,Native.ClientBounds(b),false,delegate{})) {
    fade.SetOpacity(.375);Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
    var fadeSource=HwndSource.FromHwnd(fade.Handle);
    if(fadeSource==null||!fadeSource.CompositionTarget.UsesPerPixelOpacity)throw new Exception("Fallback did not enable actual per-pixel window transparency.");
   }
   Console.WriteLine("PASS Fallback uses a real per-pixel transparency window");
   var controls=new Grid();controls.Children.Add(new Button{Content="Skip animation  Esc",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(16)});
   destination.Content=controls;Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));bool dismissed=false;
   using(var fade=new WindowFader(controls,Native.ClientBounds(b),false,delegate {dismissed=true;})) {
    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
    var button=FindButton(HwndSource.FromHwnd(fade.Handle).RootVisual);
    if(button==null)throw new Exception("Fallback fade displayed a snapshot of Skip without a live dismiss button.");
    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(!dismissed)throw new Exception("Clicking Skip during fallback did not dismiss the launcher.");
   }
   Console.WriteLine("PASS Skip remains a live clickable control during the fallback fade");
   PixelRect before=Native.OuterBounds(b);
   if(!Native.ActivateExisting(new WindowSample(b,Native.ClientBounds(b),true)))throw new Exception("Existing app activation was rejected.");
   Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
   if(!Native.OuterBounds(b).NearlyEquals(before))throw new Exception("Activating an existing normal window changed its placement.");
   Console.WriteLine("PASS Existing normal window is activated without resizing");
   destination.WindowState=WindowState.Maximized;Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
   if(!Native.ActivateExisting(new WindowSample(b,Native.ClientBounds(b),true))||destination.WindowState!=WindowState.Maximized)throw new Exception("Activating an existing maximized window restored its old size.");
   Console.WriteLine("PASS Existing maximized window stays maximized");
   destination.WindowState=WindowState.Minimized;Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
   if(!Native.ActivateExisting(new WindowSample(b,Native.ClientBounds(b),true){Minimized=true}))throw new Exception("Existing minimized window was rejected.");
   var wait=System.Diagnostics.Stopwatch.StartNew();
   while(destination.WindowState==WindowState.Minimized&&wait.Elapsed.TotalSeconds<2){Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));System.Threading.Thread.Sleep(10);}
   if(destination.WindowState!=WindowState.Maximized)throw new Exception("Minimized window was not restored to its previous maximized state.");
   Console.WriteLine("PASS Existing minimized window restores its previous maximized state");
   if(Native.ActivateExisting(null)||Native.ActivateExisting(new WindowSample(IntPtr.Zero,new PixelRect(),true)))throw new Exception("Absent window incorrectly suppressed a cold launch.");
   Console.WriteLine("PASS Missing window leaves the cold-launch path available");
   Console.WriteLine("RESULT: 9 native checks passed");return 0;
  } catch(Exception e) { Console.WriteLine("FAIL "+e);return 1; }
  finally { if(destination!=null)destination.Close();if(source!=null)source.Close(); }
 }
}
