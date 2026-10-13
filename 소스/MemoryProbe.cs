using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeckLauncher;

/// <summary>
/// DeckLauncher.exe --memtest &lt;data folder copy&gt; &lt;result folder&gt; [software]
/// Opens the same windows a user would and logs process / managed memory after each step.
/// </summary>
internal static class MemoryProbe
{
    public static int Run(string data, string output, bool software)
    {
        Directory.CreateDirectory(output);
        var log = new List<string>();
        if (software) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        void Pump() { for (int i = 0; i < 3; i++) app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
        void Record(string step)
        {
            Pump(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var p = Process.GetCurrentProcess(); p.Refresh();
            long driver = p.Modules.Cast<ProcessModule>().Where(m => m.ModuleName.StartsWith("nv", StringComparison.OrdinalIgnoreCase) || m.ModuleName.StartsWith("d3d", StringComparison.OrdinalIgnoreCase) || m.ModuleName.StartsWith("igd", StringComparison.OrdinalIgnoreCase) || m.ModuleName.StartsWith("amd", StringComparison.OrdinalIgnoreCase)).Sum(m => (long)m.ModuleMemorySize);
            log.Add($"{step,-34} 작업세트 {p.WorkingSet64 / 1048576.0,6:0.0} MB · 전용 {p.PrivateMemorySize64 / 1048576.0,6:0.0} MB · 관리힙 {GC.GetTotalMemory(false) / 1048576.0,5:0.0} MB · 창 {app.Windows.Count,2}개 · 그래픽 드라이버 모듈 {driver / 1048576.0,5:0.0} MB");
        }
        try
        {
            log.Add(software ? "모드: 소프트웨어 렌더링" : "모드: 기본(GPU) 렌더링");
            var store = new Store(data); var library = store.Load(); UI.ApplyFonts(library.Fonts);
            Record("시작 (데이터 로드)");
            var launcher = new LauncherWindow(store, library) { ShowActivated = false, Left = -20000, Top = -20000 }; launcher.Show();
            Record("런처 표시");
            for (int i = 0; i < 3; i++) { var w = new LibraryWindow(store, library) { Owner = launcher, ShowActivated = false, Left = -20000, Top = -20000 }; w.Show(); Pump(); w.Close(); }
            Record("설정 창 열고 닫기 ×3");
            for (int i = 0; i < 3; i++) { var w = new LibraryWindow(store, library) { Owner = launcher, ShowActivated = false, Left = -20000, Top = -20000 }; w.Show(); Pump(); w.Hide(); }
            Record("설정 창 열고 Esc로 숨기기 ×3");
            for (int i = 0; i < 2; i++) { var w = new ThemeWindow(store, library) { Owner = launcher, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 }; w.Show(); Pump(); w.Close(); }
            Record("색상·UI 디자인 창 ×2");
            for (int i = 0; i < 2; i++) { var w = new FontWindow(store, library) { Owner = launcher, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 }; w.Show(); Pump(); w.Close(); }
            Record("글꼴 창 ×2");
            for (int i = 0; i < 2; i++) { var w = new BackgroundWindow(store, library) { Owner = launcher, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 }; w.Show(); Pump(); w.Close(); }
            Record("배경 꾸미기 창 ×2");
            launcher.Hide();
            Record("런처 숨김 (정리 전)");
            Native.TrimMemory();
            Record("런처 숨김 + 메모리 반환 (평소 대기)");
            launcher.Close();
        }
        catch (Exception ex) { log.Add(ex.ToString()); }
        File.WriteAllLines(Path.Combine(output, software ? "메모리-소프트웨어.txt" : "메모리-기본.txt"), log);
        return 0;
    }
}
