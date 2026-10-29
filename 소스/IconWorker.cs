using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeckLauncher;

/// <summary>Serial STA loader: shell extensions and disk decoding never block hover/input.</summary>
internal static class IconWorker
{
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved,uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    private static readonly Lazy<Task<Dispatcher>> worker=new(()=>
    {
        var ready=new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            bool initialized=false;
            try{Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero,2));initialized=true;ready.SetResult(Dispatcher.CurrentDispatcher);Dispatcher.Run();}
            catch(Exception ex){ready.TrySetException(ex);}
            finally{if(initialized)CoUninitialize();}
        }){IsBackground=true,Name="Launcher icon loader"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return ready.Task;
    });
    internal static async Task<ImageSource?> Load(Func<ImageSource?> read)
    {
        try
        {
            var dispatcher=await worker.Value.ConfigureAwait(false);
            return await dispatcher.InvokeAsync(()=>
            {
                try{var source=read();if(source is {IsFrozen:false}){if(!source.CanFreeze)return null;source.Freeze();}return source;}
                catch{return null;}
            },DispatcherPriority.Background).Task.ConfigureAwait(false);
        }
        catch{return null;}
    }
}
