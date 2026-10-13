using System.Diagnostics;
using System.IO;

namespace DeckLauncher;

internal static class Launch
{
    public static Process? Start(Entry e, bool hidden = false)
    {
        var target = Environment.ExpandEnvironmentVariables(e.Target);
        bool uri = Uri.TryCreate(target, UriKind.Absolute, out var parsed) && !parsed.IsFile;
        if (!uri && !File.Exists(target) && !Directory.Exists(target)) throw new FileNotFoundException("원본 파일을 찾을 수 없습니다. 우클릭 → 편집에서 경로를 변경해 주세요.", target);
        string working = Environment.ExpandEnvironmentVariables(e.WorkingDirectory);
        if (working.Length == 0 && File.Exists(target)) working = Path.GetDirectoryName(target) ?? "";
        return Process.Start(new ProcessStartInfo { FileName = target, Arguments = e.Arguments, WorkingDirectory = working, UseShellExecute = true, WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal });
    }
}
