using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeckLauncher;

public sealed partial class LauncherWindow
{
    private readonly Dictionary<string,Task<ImageSource?>> iconRequests=new();
    private int iconGeneration;
    private bool iconsClosed;
    internal int PendingIcons=>iconRequests.Count;
    private void ClearIcons(){iconGeneration++;icons.Clear();iconRequests.Clear();}
    private async void BindIcon(Image image,Entry entry,FrameworkElement? placeholder=null)
    {
        string key=entry.IconFile??(entry.IsFolder?"@folder":entry.Target);
        if(previewMode || entry.IsFolder && entry.IconFile==null)
        {
            image.Source=IconFor(entry);if(placeholder!=null)placeholder.Visibility=image.Source==null?Visibility.Visible:Visibility.Collapsed;return;
        }
        if(icons.TryGetValue(key,out var existing))
        {
            image.Source=existing;if(placeholder!=null)placeholder.Visibility=existing==null?Visibility.Visible:Visibility.Collapsed;return;
        }
        int generation=iconGeneration;
        if(!iconRequests.TryGetValue(key,out var task))
        {
            string? custom=entry.IconFile==null?null:store.IconPath(entry.IconFile);string target=entry.Target;bool folder=entry.IsFolder;
            iconRequests[key]=task=IconWorker.Load(()=>
            {
                ImageSource? source=null;
                if(custom!=null){try{source=Native.LoadImage(custom,128);}catch{ }}
                return source??(folder?Native.FolderIcon():Native.FileIcon(Environment.ExpandEnvironmentVariables(target)));
            });
        }
        var icon=await task.ConfigureAwait(false);
        // Constructors can run before WPF installs a SynchronizationContext.
        // Always marshal the completion explicitly instead of relying on await.
        if(Dispatcher.HasShutdownStarted)return;
        try
        {
            await Dispatcher.InvokeAsync(()=>
            {
                if(generation!=iconGeneration || iconsClosed)return;
                icons[key]=icon;iconRequests.Remove(key);image.Source=icon;
                if(placeholder!=null)placeholder.Visibility=icon==null?Visibility.Visible:Visibility.Collapsed;
            },System.Windows.Threading.DispatcherPriority.Background).Task.ConfigureAwait(false);
        }
        catch(TaskCanceledException){ }
    }
}
