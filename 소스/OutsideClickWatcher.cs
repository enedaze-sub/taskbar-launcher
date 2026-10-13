using System.Runtime.InteropServices;
using System.Windows;

namespace DeckLauncher;

internal sealed class OutsideClickWatcher : IDisposable
{
    private delegate IntPtr Hook(int code,IntPtr message,IntPtr data);
    private readonly Hook callback;
    private IntPtr handle;
    [StructLayout(LayoutKind.Sequential)] private struct MouseInfo {public int X,Y;public uint MouseData,Flags,Time;public UIntPtr Extra;}
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int kind,Hook handler,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? name);
    internal OutsideClickWatcher(Action<Point> click)
    {
        callback=(code,message,data)=>
        {
            if(code>=0 && message.ToInt64() is 0x201 or 0x204 or 0x207)
            {
                try {var mouse=Marshal.PtrToStructure<MouseInfo>(data);click(new Point(mouse.X,mouse.Y));}catch { }
            }
            return CallNextHookEx(handle,code,message,data);
        };
        handle=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);
    }
    public void Dispose(){if(handle!=IntPtr.Zero){UnhookWindowsHookEx(handle);handle=IntPtr.Zero;}GC.KeepAlive(callback);}
}
