using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DeckLauncher;

internal static class PerformanceProbe
{
    internal static int Run(string data,string output)
    {
        Directory.CreateDirectory(output);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var samples=new List<object>();
        T Measure<T>(string name,Func<T> action)
        {
            long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();T result=action();timer.Stop();
            samples.Add(new{Name=name,Milliseconds=timer.Elapsed.TotalMilliseconds,UIAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes});return result;
        }
        void Pump()=>app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
        var store=new Store(data);var lib=Measure("load-and-fonts",()=>{var library=store.Load();UI.ApplyFonts(library.Fonts);return library;});
        var launcher=Measure("construct",()=>new LauncherWindow(store,lib){ShowActivated=false,Left=-20000,Top=-20000});
        Measure("show-and-layout",()=>{launcher.Show();launcher.UpdateLayout();Pump();return 0;});
        var folders=lib.Children(null).Where(e=>e.IsFolder && launcher.Slot(e.Id)!=null).ToList();
        foreach(int pass in new[]{0,1})
            for(int i=0;i<folders.Count;i++)
            {
                var folder=folders[i];Measure($"folder-{i}-pass-{pass}",()=>{launcher.ShowFolderPreview(folder,launcher.Slot(folder.Id)!);launcher.FolderPreview?.UpdateLayout();Pump();launcher.CloseFolderPreview();return 0;});
            }
        // Wait for queued icon I/O before comparing steady-state pointer work.
        var deadline=DateTime.UtcNow.AddSeconds(15);
        while(launcher.PendingIcons>0 && DateTime.UtcNow<deadline){Pump();Thread.Sleep(5);}
        var buttons=launcher.DisplayedIds.Select(id=>launcher.Slot(id)!).ToArray();
        for(int pass=0;pass<3;pass++)Measure("rapid-hover-1000-"+pass,()=>
        {
            long dispatchTicks=0;
            for(int i=0;i<1000;i++)
            {
                var button=buttons[i%buttons.Length];
                long start=Stopwatch.GetTimestamp();
                button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});
                button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseLeaveEvent});
                dispatchTicks+=Stopwatch.GetTimestamp()-start;
                if(i%16==0){launcher.UpdateLayout();Pump();}
            }
            Pump();samples.Add(new{Name="hover-input-dispatch-"+pass,Milliseconds=dispatchTicks*1000.0/Stopwatch.Frequency});return 0;
        });
        var report=new{Note="Separate process, offscreen windows, UI dispatch and layout timings; not an OS reboot or an on-screen FPS measurement.",Entries=lib.Entries.Count,RenderTier=System.Windows.Media.RenderCapability.Tier>>16,Samples=samples};
        File.WriteAllText(Path.Combine(output,"performance.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        launcher.Hide();app.Shutdown();return 0;
    }
}
