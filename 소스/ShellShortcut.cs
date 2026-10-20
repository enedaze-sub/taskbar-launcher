using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace DeckLauncher;

internal static class ShellShortcut
{
    [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int length,IntPtr findData,uint flags);
        void GetIDList(out IntPtr pidl);void SetIDList(IntPtr pidl);
        void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);
        void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string path);
        void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string text);
        void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int command);void SetShowCmd(int command);
        void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int length,out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string path,int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(IntPtr hwnd,uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
    }
    internal static string EnsureStartEntry()
    {
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Enedaze.DeckLauncher.lnk");
        Save(path,Path.Combine(AppContext.BaseDirectory,"DeckLauncher.exe"));Native.RefreshShortcutIcon(path);return path;
    }
    internal static void Save(string path,string executable)
    {
        object obj=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
        try
        {
            var link=(IShellLink)obj;
            if(File.Exists(path))
            {
                ((IPersistFile)obj).Load(path,2);var prior=new StringBuilder(32768);link.GetPath(prior,prior.Capacity,IntPtr.Zero,0);
                if(!Path.GetFileName(prior.ToString()).Equals("DeckLauncher.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("같은 이름의 다른 바로가기가 있어 덮어쓰지 않았습니다.");
            }
            link.SetPath(executable);link.SetArguments("");link.SetDescription("작업표시줄 런처");link.SetWorkingDirectory(Path.GetDirectoryName(executable)!);link.SetIconLocation(executable,-32512);link.SetShowCmd(1);
            var props=(Native.IPropertyStore)obj;Native.SetProperty(props,5,TaskbarPinning.AppId);Marshal.ThrowExceptionForHR(props.Commit());
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);((IPersistFile)obj).Save(path,true);
        }
        finally{Marshal.ReleaseComObject(obj);}
    }
    internal static (string Target,string Icon,int Index) Read(string path)
    {
        object obj=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
        try{((IPersistFile)obj).Load(path,0);var link=(IShellLink)obj;var target=new StringBuilder(32768);var icon=new StringBuilder(32768);link.GetPath(target,target.Capacity,IntPtr.Zero,0);link.GetIconLocation(icon,icon.Capacity,out int index);return(target.ToString(),icon.ToString(),index);}
        finally{Marshal.ReleaseComObject(obj);}
    }
}
