using System.Windows;
using System.Windows.Media;

namespace DeckLauncher;

/// <summary>Keeps a pinnable taskbar button alive while the launcher popup is hidden.</summary>
internal sealed class TaskbarWindow : Window
{
    private bool restoring;
    internal TaskbarWindow(Action reveal)
    {
        Title="작업표시줄 런처";
        Width=1;Height=1;Left=-32000;Top=-32000;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=true;ShowActivated=false;
        AllowsTransparency=true;Background=Brushes.Transparent;Opacity=0;
        Icon=Native.LauncherIcon();
        WindowState=WindowState.Minimized;
        SourceInitialized+=(_,_)=>Native.ConfigureTaskbar(this);
        StateChanged+=(_,_)=>
        {
            if(restoring || WindowState!=WindowState.Normal)return;
            restoring=true;
            try{WindowState=WindowState.Minimized;}finally{restoring=false;}
            reveal();
        };
        RoutedEventHandler? loaded=null;
        loaded=(_,_)=>{Loaded-=loaded;reveal();};
        Loaded+=loaded;
    }
}
