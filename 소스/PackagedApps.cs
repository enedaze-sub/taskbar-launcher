using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DeckLauncher;

internal static class PackagedApps
{
    internal sealed record PackagePath(string Family, string Architecture, string Root, string RelativePath);
    private static readonly Regex Identity = new(@"^(?<name>.+)_(?<version>\d+\.\d+\.\d+\.\d+)_(?<arch>[^_]+)_[^_]*_(?<publisher>[^_]+)$", RegexOptions.CultureInvariant);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern int GetPackagesByPackageFamily(string family, ref uint count, IntPtr names, ref uint length, IntPtr buffer);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern int GetPackagePathByFullName(string fullName, ref uint length, StringBuilder? path);

    internal static PackagePath? Parse(string target)
    {
        const string marker="\\WindowsApps\\";
        int at=target.IndexOf(marker,StringComparison.OrdinalIgnoreCase);
        if(at<0 || !Path.IsPathFullyQualified(target))return null;
        int start=at+marker.Length,end=target.IndexOf('\\',start);
        if(end<0)return null;
        var match=Identity.Match(target[start..end]);
        if(!match.Success)return null;
        string relative=target[(end+1)..];
        if(relative.Length==0 || relative.Split('\\','/').Any(p=>p is ".." or "."))return null;
        return new(match.Groups["name"].Value+"_"+match.Groups["publisher"].Value,match.Groups["arch"].Value,target[..end],relative);
    }
    internal static IEnumerable<string> InstalledRoots(PackagePath package)
    {
        var result=new List<(string Name,string Root)>();
        try
        {
            uint count=0,length=0;
            if(GetPackagesByPackageFamily(package.Family,ref count,IntPtr.Zero,ref length,IntPtr.Zero)!=122 || count==0 || count>4096 || length>1048576)return Array.Empty<string>();
            IntPtr names=Marshal.AllocHGlobal(checked((int)count*IntPtr.Size)),buffer=Marshal.AllocHGlobal(checked((int)length*2));
            try
            {
                if(GetPackagesByPackageFamily(package.Family,ref count,names,ref length,buffer)!=0)return Array.Empty<string>();
                for(int i=0;i<count;i++)
                {
                    string? full=Marshal.PtrToStringUni(Marshal.ReadIntPtr(names,i*IntPtr.Size));
                    if(full==null)continue;
                    uint size=0;if(GetPackagePathByFullName(full,ref size,null)!=122 || size>32768)continue;
                    var path=new StringBuilder((int)size);if(GetPackagePathByFullName(full,ref size,path)==0)result.Add((full,path.ToString()));
                }
            }
            finally{Marshal.FreeHGlobal(names);Marshal.FreeHGlobal(buffer);}
        }
        catch(Exception ex) when(ex is DllNotFoundException or EntryPointNotFoundException or UnauthorizedAccessException){ }
        return result.OrderByDescending(p=>Identity.Match(p.Name).Groups["arch"].Value.Equals(package.Architecture,StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p=>Version.TryParse(Identity.Match(p.Name).Groups["version"].Value,out var v)?v:new Version()).Select(p=>p.Root).ToArray();
    }
    internal static string Resolve(string target, Func<PackagePath,IEnumerable<string>>? roots=null)
    {
        var package=Parse(target);if(package==null)return target;
        foreach(string root in (roots??InstalledRoots)(package))
        {
            string full=Path.GetFullPath(Path.Combine(root,package.RelativePath));
            if(full.StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase) && File.Exists(full))return full;
        }
        return target;
    }
    internal static string RebaseWorkingDirectory(string working,string original,string resolved)
    {
        var old=Parse(original);var current=Parse(resolved);
        if(old==null || current==null || old.Family!=current.Family || string.IsNullOrEmpty(working))return working;
        if(working.Equals(old.Root,StringComparison.OrdinalIgnoreCase))return current.Root;
        return working.StartsWith(old.Root+"\\",StringComparison.OrdinalIgnoreCase)?current.Root+working[old.Root.Length..]:working;
    }
}
