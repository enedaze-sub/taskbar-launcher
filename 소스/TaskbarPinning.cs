using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DeckLauncher;

/// <summary>Documented TaskbarManager ABI; no Taskband edits or shell pinning bypasses.</summary>
internal static class TaskbarPinning
{
    internal const string AppId="Enedaze.DeckLauncher";
    internal sealed record Status(bool Supported,bool Pinned);
    [DllImport("combase.dll")] private static extern int RoInitialize(uint type);
    [DllImport("combase.dll")] private static extern void RoUninitialize();
    [DllImport("combase.dll",CharSet=CharSet.Unicode)] private static extern int WindowsCreateString(string value,uint length,out IntPtr text);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr text);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr name,ref Guid iid,out IntPtr factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PointerCall(IntPtr self,out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int BooleanCall(IntPtr self,out byte value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int IntegerCall(IntPtr self,out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EmptyCall(IntPtr self);
    private static T Method<T>(IntPtr obj,int slot) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size));
    private static void Check(int hr)=>Marshal.ThrowExceptionForHR(hr);
    private sealed class Session:IDisposable
    {
        internal IntPtr Manager;
        private bool initialized;
        internal Session()
        {
            int hr=RoInitialize(0);initialized=hr>=0;
            if(hr<0 && hr!=unchecked((int)0x80010106))Check(hr);
            IntPtr text=IntPtr.Zero,factory=IntPtr.Zero,marker=IntPtr.Zero;
            try
            {
                const string name="Windows.UI.Shell.TaskbarManager";
                Check(WindowsCreateString(name,(uint)name.Length,out text));
                var iid=new Guid("db32ab74-de52-4fe6-b7b6-95ff9f8395df");Check(RoGetActivationFactory(text,ref iid,out factory));
                var desktop=new Guid("cdfefd63-e879-4134-b9a7-8283f05f9480");Check(Marshal.QueryInterface(factory,ref desktop,out marker));
                Check(Method<PointerCall>(factory,6)(factory,out Manager));
            }
            catch{Dispose();throw;}
            finally{if(marker!=IntPtr.Zero)Marshal.Release(marker);if(factory!=IntPtr.Zero)Marshal.Release(factory);if(text!=IntPtr.Zero)WindowsDeleteString(text);}
        }
        internal bool Supported{get{Check(Method<BooleanCall>(Manager,6)(Manager,out byte value));return value!=0;}}
        internal bool Allowed{get{Check(Method<BooleanCall>(Manager,7)(Manager,out byte value));return value!=0;}}
        internal Task<bool> Pinned()=>Operation(8);
        internal Task<bool> Pin()=>Operation(10);
        private async Task<bool> Operation(int slot)
        {
            Check(Method<PointerCall>(Manager,slot)(Manager,out var operation));IntPtr info=IntPtr.Zero;
            try
            {
                var iid=new Guid("00000036-0000-0000-c000-000000000046");Check(Marshal.QueryInterface(operation,ref iid,out info));
                var deadline=DateTime.UtcNow.AddMinutes(2);
                while(true)
                {
                    Check(Method<IntegerCall>(info,7)(info,out int state));
                    if(state==1){Check(Method<BooleanCall>(operation,8)(operation,out byte result));return result!=0;}
                    if(state==2)return false;
                    if(state==3){Check(Method<IntegerCall>(info,8)(info,out int error));Check(error);throw new InvalidOperationException("Windows 고정 요청을 완료하지 못했습니다.");}
                    if(DateTime.UtcNow>=deadline){Method<EmptyCall>(info,9)(info);return false;}
                    await Task.Delay(50);
                }
            }
            finally{if(info!=IntPtr.Zero){Method<EmptyCall>(info,10)(info);Marshal.Release(info);}Marshal.Release(operation);}
        }
        public void Dispose(){if(Manager!=IntPtr.Zero){Marshal.Release(Manager);Manager=IntPtr.Zero;}if(initialized){RoUninitialize();initialized=false;}}
    }
    internal static bool RequiresRestrictedAccess()
    {
        using var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\LimitedAccessFeatures\com.microsoft.windows.taskbar.pin");
        return key?.GetValue("4096B239A7295B635C090E647E867B5707DA6AB6CB78340B01FE4E0C8F4953D4") is int value && value!=0;
    }
    internal static async Task<Status> Read()
    {
        try
        {
            if(RequiresRestrictedAccess())return new(false,false);
            using var session=new Session();return new(session.Supported,await session.Pinned());
        }
        catch(Exception ex) when(ex is COMException or DllNotFoundException or EntryPointNotFoundException or UnauthorizedAccessException){return new(false,false);}
    }
    internal static async Task<bool> Pin()
    {
        if(RequiresRestrictedAccess())return false;
        ShellShortcut.EnsureStartEntry();
        using var session=new Session();
        if(await session.Pinned())return true;
        return session.Supported && session.Allowed && await session.Pin();
    }
}
