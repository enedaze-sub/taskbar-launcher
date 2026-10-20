using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DeckLauncher;

internal static class TaskbarRegression
{
    internal static void Run(Action<bool,string> check)
    {
        int reveals=0;
        var host=new TaskbarWindow(()=>reveals++);
        host.Show();host.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        check(host.ShowInTaskbar && host.IsVisible && host.WindowState==WindowState.Minimized,"The minimized taskbar host stays available for right-click pinning");
        check(reveals==1,"Startup opens the launcher once without showing a host window");
        var popup=new Window {ShowInTaskbar=false,ShowActivated=false,Width=1,Height=1,Left=-32000,Top=-32000};
        popup.Show();popup.Hide();
        check(host.IsVisible && host.ShowInTaskbar,"Hiding the popup leaves its taskbar button alive");
        host.WindowState=WindowState.Normal;
        check(reveals==2 && host.WindowState==WindowState.Minimized,"A taskbar restore opens the launcher while keeping the host minimized");
        host.WindowState=WindowState.Normal;
        check(reveals==3,"Repeated taskbar clicks continue to reach the launcher");
        string exe=Path.Combine(AppContext.BaseDirectory,"DeckLauncher.exe");
        check(Native.TaskbarProperty(host,2)=="\""+exe+"\"" && Native.TaskbarProperty(host,5)=="Enedaze.DeckLauncher","The persistent button preserves the relaunch command and application identity");
        check(Native.TaskbarProperty(host,3)==exe+",0","Pinned icons use the executable's embedded icon resource");
        check(host.Icon!=null && Native.FileIcon(exe)!=null,"Both the taskbar window and executable provide a launcher icon");
        var decoder=new IconBitmapDecoder(new Uri(Path.Combine(AppContext.BaseDirectory,"launcher.ico")),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
        check(new[]{16,24,32,48,64,128,256}.All(size=>decoder.Frames.Any(f=>f.PixelWidth==size)),"Launcher icons include seven sizes for taskbars and high-DPI displays");
        popup.Close();host.Close();
    }
}
