using System.Windows.Threading;

namespace DeckLauncher;

public sealed partial class LauncherWindow
{
    private DispatcherOperation? noteUpdate;
    private Entry? hoverNote;
    private void ScheduleNote(Entry? entry)
    {
        hoverNote=entry;
        if(noteUpdate is {Status:DispatcherOperationStatus.Pending})return;
        noteUpdate=Dispatcher.BeginInvoke(new Action(()=>
        {
            noteUpdate=null;
            if(hoverNote is Entry latest)ShowNote(latest);else DefaultNote();
        }),DispatcherPriority.Render);
    }
    private void CancelNoteUpdate(){noteUpdate?.Abort();noteUpdate=null;hoverNote=null;}
}
