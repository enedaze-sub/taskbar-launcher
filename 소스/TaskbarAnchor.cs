using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

namespace DeckLauncher;

internal static class TaskbarAnchor
{
    private delegate bool EnumCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, StringBuilder name, int count);
    private static Rect? cached;
    private static readonly object gate = new();
    private static Task<Rect?>? pending;
    public static async Task<Rect?> FindAsync()
    {
        Task<Rect?> query;
        lock (gate) { if (pending == null || pending.IsCompleted) pending = Task.Run(Find); query = pending; }
        var winner = await Task.WhenAny(query, Task.Delay(600));
        if (winner == query) return await query;
        lock (gate) return cached;
    }
    private static Rect? Find()
    {
        try
        {
            var handles = new List<IntPtr>();
            EnumWindows((handle, _) =>
            {
                var name = new StringBuilder(128); GetClassName(handle, name, name.Capacity);
                if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") handles.Add(handle);
                return true;
            }, IntPtr.Zero);
            var matches = new List<Rect>();
            foreach (var handle in handles)
            {
                var root = AutomationElement.FromHandle(handle);
                var buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                foreach (AutomationElement element in buttons)
                {
                    try
                    {
                        var state = element.Current;
                        if (!state.AutomationId.Contains("Enedaze.DeckLauncher", StringComparison.OrdinalIgnoreCase) && !state.AutomationId.Contains("DeckLauncher", StringComparison.OrdinalIgnoreCase) && !state.Name.Contains("작업표시줄 런처") && !state.Name.Contains("DeckLauncher", StringComparison.OrdinalIgnoreCase)) continue;
                        var rect = state.BoundingRectangle;
                        if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0 && !state.IsOffscreen) matches.Add(rect);
                    }
                    catch (ElementNotAvailableException) { }
                }
            }
            if (matches.Count == 0) { lock (gate) return cached; }
            var cursor = Native.CursorWorkArea().Cursor;
            Rect result = matches.OrderBy(r => Math.Pow(r.X + r.Width / 2 - cursor.X, 2) + Math.Pow(r.Y + r.Height / 2 - cursor.Y, 2)).First();
            lock (gate) cached = result; return result;
        }
        catch { lock (gate) return cached; }
    }
    internal static Rect Placement(Rect work, double centerX)
    {
        double room = 2 * Math.Min(centerX - work.Left, work.Right - centerX);
        double width = Math.Min(920, Math.Max(440, room));
        width = Math.Min(width, (work.Height - 10) * 920 / 454);
        double scale = width / 920;
        return new Rect(centerX - width / 2, work.Bottom - 453.5 * scale, width, 454 * scale);
    }
}
