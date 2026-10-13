using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeckLauncher;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attributes, out SHFILEINFO info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
    public static void ConfigureTaskbar(Window window)
    {
        var guid = typeof(IPropertyStore).GUID;
        Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(new WindowInteropHelper(window).Handle, ref guid, out var props));
        try
        {
            string executable = Path.Combine(AppContext.BaseDirectory, "DeckLauncher.exe");
            SetProperty(props, 2, "\"" + executable + "\"");
            SetProperty(props, 3, Path.Combine(AppContext.BaseDirectory, "launcher.ico") + ",0");
            SetProperty(props, 4, "작업표시줄 런처");
            SetProperty(props, 5, "Enedaze.DeckLauncher");
            Marshal.ThrowExceptionForHR(props.Commit());
        }
        finally { Marshal.ReleaseComObject(props); }
    }
    private static void SetProperty(IPropertyStore store, uint id, string text)
    {
        var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = id };
        var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(text) };
        try { Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref value)); }
        finally { Marshal.FreeCoTaskMem(value.Pointer); }
    }
    internal static string? TaskbarProperty(Window window, uint id)
    {
        var guid = typeof(IPropertyStore).GUID;
        Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(new WindowInteropHelper(window).Handle, ref guid, out var props));
        try
        {
            var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = id };
            Marshal.ThrowExceptionForHR(props.GetValue(ref key, out var value));
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : null; }
            finally { if (value.Pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(value.Pointer); }
        }
        finally { Marshal.ReleaseComObject(props); }
    }

    public static ImageSource? FileIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        uint flags = 0x100; // SHGFI_ICON, large icon; Shell resolves shortcut icons too.
        if (!File.Exists(path) && !Directory.Exists(path)) flags |= 0x10;
        SHGetFileInfo(path, 0x80, out var info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        if (info.hIcon == IntPtr.Zero) return null;
        try
        {
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze(); return bitmap;
        }
        finally { DestroyIcon(info.hIcon); }
    }
    public static ImageSource LoadImage(string path)
    {
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 256; image.UriSource = new Uri(Path.GetFullPath(path)); image.EndInit(); image.Freeze(); return image;
    }
    public static ImageSource FolderIcon()
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(42, 164, 178)), null, new Rect(4, 12, 56, 43), 7, 7);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(42, 164, 178)), null, new Rect(4, 6, 26, 19), 5, 5);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(95, 221, 211)), null, new Rect(4, 22, 56, 34), 6, 6);
        }
        group.Freeze(); return new DrawingImage(group);
    }
}
