using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeckLauncher;

internal static class HoverRegression
{
    private static void Until(Func<bool> condition)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(5)};var end=DateTime.UtcNow.AddSeconds(5);
        timer.Tick+=(_,_)=>{if(condition() || DateTime.UtcNow>=end)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
        if(!condition())throw new TimeoutException("Icon/hover test did not complete.");
    }
    private static void Enter(Button b)=>b.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent});
    private static void Leave(Button b)=>b.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseLeaveEvent});
    internal static void Run(string output,Action<bool,string> check)
    {
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        int uiThread=Environment.CurrentManagedThreadId,loaderThread=0;ApartmentState apartment=ApartmentState.Unknown;
        var read=IconWorker.Load(()=>{loaderThread=Environment.CurrentManagedThreadId;apartment=Thread.CurrentThread.GetApartmentState();entered.Set();release.Wait(TimeSpan.FromSeconds(4));return Native.FolderIcon();});
        try
        {
            Until(()=>entered.IsSet);bool responded=false;Dispatcher.CurrentDispatcher.Invoke(()=>responded=true);
            check(responded && !read.IsCompleted && loaderThread!=uiThread && apartment==ApartmentState.STA,"Input dispatcher remains responsive while icon I/O is blocked on its separate STA worker");
        }
        finally{release.Set();}
        Until(()=>read.IsCompleted);check(read.Result?.IsFrozen==true,"Loaded icons are frozen before crossing back to the UI thread");
        var store=new Store(Path.Combine(output,"hover-regression"));store.Load();
        var a=new Entry{Name="첫 항목",Target=Path.Combine(AppContext.BaseDirectory,"DeckLauncher.exe"),Note="첫 메모"};
        var b=new Entry{Name="마지막 항목",Target=a.Target,Note="마지막 메모"};var lib=new Library{Entries=new(){a,b}};
        var window=new LauncherWindow(store,lib){ShowActivated=false,Left=-20000,Top=-20000};window.Show();
        Until(()=>window.PendingIcons==0);
        check(Images(window).Count(i=>i.Source!=null)>=2,"Asynchronously loaded app icons reach the live slot images through the UI dispatcher");
        var first=window.Slot(a.Id)!;var last=window.Slot(b.Id)!;
        for(int i=0;i<100;i++){Enter(first);Leave(first);Enter(last);Leave(last);}Enter(last);
        window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        check(window.DisplayedNote==b.Note && window.DisplayedTitle==b.Name,"Rapid pointer changes display the final hovered entry's note on the next frame");
        Leave(last);Enter(first);window.Navigate(null);window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        check(window.DisplayedTitle==lib.Appearance.Title,"Navigation cancels a pending hover note so it cannot overwrite the new screen");
        first=window.Slot(a.Id)!;var backdrop=(Canvas)((Canvas)first.Content).Children[0];var shape=backdrop.Children.OfType<System.Windows.Shapes.Path>().First();var idle=shape.Fill;
        Enter(first);window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);var hover=shape.Fill;
        Leave(first);window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);bool idleReused=ReferenceEquals(idle,shape.Fill);
        Enter(first);window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        check(idleReused && ReferenceEquals(hover,shape.Fill),"Hover transitions reuse their theme appearances instead of reconstructing gradients");
        // Normal launcher Close shuts down the application; leave this hidden
        // test window for the suite's final Application.Shutdown instead.
        window.Hide();
    }
    private static IEnumerable<Image> Images(DependencyObject root)
    {
        if(root is Image image)yield return image;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Images(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
}
