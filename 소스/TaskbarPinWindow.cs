using System.Windows;
using System.Windows.Controls;

namespace DeckLauncher;

internal sealed class TaskbarPinWindow:Window
{
    private readonly TextBlock status=UI.Text("Windows 상태를 확인하고 있어요.",14);
    private readonly Button pin;
    internal TaskbarPinWindow(ThemeSettings theme)
    {
        UI.Theme(this,theme);Title="작업표시줄 고정 / 해제";Width=450;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;Icon=Native.LauncherIcon();
        var panel=new StackPanel{Margin=new Thickness(24)};Content=panel;
        panel.Children.Add(UI.Heading("작업표시줄에 두기",23));status.Margin=new Thickness(0,12,0,14);panel.Children.Add(status);
        pin=UI.Button("작업표시줄에 고정",()=>{});pin.IsEnabled=false;pin.Click+=async(_,_)=>
        {
            pin.IsEnabled=false;
            try{status.Text=await TaskbarPinning.Pin()?"고정됐어요. 런처를 숨겨도 고정한 아이콘은 남습니다.":"고정하지 않았습니다. 다시 시도하거나 아래 방법을 사용해 주세요.";}
            catch(System.Exception ex){status.Text="Windows에서 고정을 완료하지 못했습니다.\n"+ex.Message;}
            await RefreshStatus(false);
        };
        panel.Children.Add(pin);
        var guide=UI.Text("고정 해제 · 이전 Windows\n\n① 이 창을 열어 둡니다.\n② 작업표시줄의 런처 아이콘을 우클릭합니다.\n③ ‘작업 표시줄에 고정’ 또는 ‘고정 해제’를 선택합니다.\n\n이 창이 열린 동안에는 런처가 자동으로 숨겨지지 않아요.",13.5,UI.Muted);guide.Margin=new Thickness(0,16,0,14);panel.Children.Add(guide);
        panel.Children.Add(UI.Button("완료",Close));
        Loaded+=async(_,_)=>{Skin.Connect(this);await RefreshStatus(true);};
    }
    private async System.Threading.Tasks.Task RefreshStatus(bool updateMessage)
    {
        var state=await TaskbarPinning.Read();pin.IsEnabled=state.Supported && !state.Pinned;
        pin.Content=state.Pinned?"이미 고정되어 있어요":"작업표시줄에 고정";
        if(updateMessage)status.Text=state.Pinned?"이미 고정되어 있어요. 해제는 아래 방법으로 할 수 있습니다.":state.Supported?"버튼을 누른 뒤 Windows 확인 창에서 승인해 주세요.":"이 Windows에서는 아래 방법으로 고정하거나 해제해 주세요.";
    }
}
