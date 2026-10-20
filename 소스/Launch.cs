using System.Diagnostics;
using System.IO;

namespace DeckLauncher;

internal static class Launch
{
    public static Process? Start(Entry e, bool hidden = false)
    {
        return Process.Start(Prepare(e,hidden));
    }
    internal static ProcessStartInfo Prepare(Entry e, bool hidden=false, Func<string,string>? resolve=null)
    {
        var original = Environment.ExpandEnvironmentVariables(e.Target);
        var target = (resolve??(path=>PackagedApps.Resolve(path)))(original);
        bool uri = Uri.TryCreate(target, UriKind.Absolute, out var parsed) && !parsed.IsFile;
        if (!uri && !File.Exists(target) && !Directory.Exists(target)) throw new FileNotFoundException("원본 파일을 찾을 수 없습니다. 우클릭 → 편집에서 경로를 변경해 주세요.", target);
        string working = Environment.ExpandEnvironmentVariables(e.WorkingDirectory);
        working=PackagedApps.RebaseWorkingDirectory(working,original,target);
        if (working.Length == 0 && File.Exists(target)) working = Path.GetDirectoryName(target) ?? "";
        return new ProcessStartInfo { FileName = target, Arguments = e.Arguments, WorkingDirectory = working, UseShellExecute = true, WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal };
    }
}
