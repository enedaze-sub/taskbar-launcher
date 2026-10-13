using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DeckLauncher;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--self-test")) return SelfTests.Run(args.Last());
        if (args.Contains("--preview")) return Preview.Run(args.Last());
        using var mutex = new Mutex(true, @"Local\DeckLauncher.6_1sol." + Environment.UserName, out bool first);
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DeckLauncher.6_1sol.Show." + Environment.UserName);
        if (!first) { signal.Set(); return 0; }
        Native.SetCurrentProcessExplicitAppUserModelID("Enedaze.DeckLauncher");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var store = new Store(Path.Combine(AppContext.BaseDirectory, "Data"));
        Library library;
        try { library = store.Load(); if (library.ApplyUiUpdate()) store.Save(library); }
        catch (Exception ex)
        {
            MessageBox.Show("등록 정보를 읽지 못했습니다. 원본 파일은 유지됩니다.\n\n" + ex.Message + "\n\nData 폴더의 library.json과 library.json.bak를 확인해 주세요.", "Deck Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        var window = new LauncherWindow(store, library);
        app.MainWindow = window;
        var listener = new Thread(() =>
        {
            while (true) { signal.WaitOne(); try { app.Dispatcher.BeginInvoke(new Action(window.Reveal)); } catch { break; } }
        }) { IsBackground = true };
        listener.Start();
        app.DispatcherUnhandledException += (_, e) => { MessageBox.Show(e.Exception.Message, "문제가 발생했습니다", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true; };
        app.Run(window);
        GC.KeepAlive(mutex);
        return 0;
    }
}
