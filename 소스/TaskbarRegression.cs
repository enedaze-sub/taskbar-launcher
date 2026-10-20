using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DeckLauncher;

internal static class TaskbarRegression
{
    private static bool MintIcon(BitmapSource? source)
    {
        if(source==null)return false;
        var image=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);
        return Enumerable.Range(0,pixels.Length/4).Count(i=>pixels[4*i]>140 && pixels[4*i+1]>160 && pixels[4*i+2]<150)>pixels.Length/64;
    }
    internal static void Run(string output,Action<bool,string> check)
    {
        var store=new Store(Path.Combine(output,"taskbar-test"));var lib=new Library();
        var popup=new LauncherWindow(store,lib,true){ShowActivated=false,Left=-32000,Top=-32000};
        popup.Show();popup.UpdateLayout();popup.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        check(popup.ShowInTaskbar && popup.IsVisible,"The launcher popup itself owns its taskbar entry");
        string exe=Path.Combine(AppContext.BaseDirectory,"DeckLauncher.exe");
        check(Native.TaskbarProperty(popup,2)=="\""+exe+"\"" && Native.TaskbarProperty(popup,5)==TaskbarPinning.AppId,"Popup pinning preserves the relaunch command and application identity");
        check(Native.TaskbarProperty(popup,3)==exe+",-32512","Taskbar icon metadata uses the actual negative resource ID");
        check(MintIcon(Native.WindowIcon(popup,true)),"The native small window icon contains the mint launcher artwork, not a generic document");
        check(MintIcon(Native.WindowIcon(popup,false)),"The native large window icon contains the mint launcher artwork");
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Native.WindowIcon(popup,true)!));using(var file=File.Create(Path.Combine(output,"native-taskbar-icon.png")))encoder.Save(file);
        var pin=FindButtons(popup).FirstOrDefault(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="작업표시줄 고정 / 해제");
        check(pin!=null && pin.Width==24,"A small accessible pin-management button is present in the launcher");
        popup.Hide();check(!popup.IsVisible && !Application.Current.Windows.Cast<Window>().Any(w=>w.IsVisible && w.ShowInTaskbar),"Hiding an unpinned launcher leaves no persistent taskbar host");
        string shortcut=Path.Combine(output,"test-launcher.lnk");ShellShortcut.Save(shortcut,exe);var saved=ShellShortcut.Read(shortcut);
        check(saved.Target==exe && saved.Icon==exe && saved.Index==-32512,"Pinning prerequisites create a shortcut with the current executable and correct icon resource");
        ShellShortcut.Save(shortcut,exe);check(ShellShortcut.Read(shortcut).Target==exe,"The launcher shortcut can be refreshed after moving its folder");
        var state=TaskbarPinning.Read();var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};var deadline=DateTime.UtcNow.AddSeconds(5);
        timer.Tick+=(_,_)=>{if(state.IsCompleted || DateTime.UtcNow>deadline)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
        check(state.IsCompletedSuccessfully,"Taskbar capability and pin-state detection completes without prompting or changing pins");
        File.WriteAllText(Path.Combine(output,"pin-api-status.txt"),state.IsCompletedSuccessfully?state.Result.ToString():"incomplete");
        var decoder=new IconBitmapDecoder(new Uri(Path.Combine(AppContext.BaseDirectory,"launcher.ico")),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
        check(new[]{16,24,32,48,64,128,256}.All(size=>decoder.Frames.Any(f=>f.PixelWidth==size)),"Launcher icons include seven sizes for taskbars and high-DPI displays");
        popup.Show();var management=new TaskbarPinWindow(lib.InterfaceTheme){Owner=popup,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-32000,Top=-32000};management.Show();management.UpdateLayout();
        check(!management.ShowInTaskbar && management.Owner==popup,"Pin management is owned by the launcher and adds no separate taskbar button");
        Preview.Snapshot(management,Path.Combine(output,"pin-management.png"));management.Close();
        popup.Close();
    }
    private static IEnumerable<System.Windows.Controls.Button> FindButtons(DependencyObject root)
    {
        if(root is System.Windows.Controls.Button button)yield return button;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in FindButtons(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
}
