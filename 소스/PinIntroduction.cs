using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DeckLauncher;

internal static class PinIntroduction
{
    private const string Marker="taskbar-pin-guide-v1.seen";
    internal static bool ShouldShow(string data)=>!File.Exists(Path.Combine(data,Marker));
    internal static void MarkSeen(string data)
    {
        try{Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,Marker),"1");}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){ }
    }
}

internal sealed class PinIntroductionWindow:Window
{
    internal PinIntroductionWindow(ThemeSettings theme)
    {
        UI.Theme(this,theme);Title="처음 사용 · 작업표시줄 고정 안내";Width=480;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;Icon=Native.LauncherIcon();
        var panel=new StackPanel{Margin=new Thickness(26)};Content=panel;
        panel.Children.Add(UI.Heading("작업표시줄에 고정해 두세요",23));
        var description=UI.Text("런처를 숨기면 실행 중인 작업표시줄 아이콘도 사라져요.\n다음부터 편하게 열려면 런처 안의 작은 핀 버튼으로 고정하세요.",14);description.Margin=new Thickness(0,12,0,18);panel.Children.Add(description);
        var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,6)};
        controls.Children.Add(UI.IconTile("\uE713",46,24,"SkinAccentSoft","SkinAccentInk",23));
        var pin=UI.IconTile("\uE718",28,16,"SkinAccentSoft","SkinAccentInk",14);pin.Margin=new Thickness(7,13,7,0);controls.Children.Add(pin);
        controls.Children.Add(UI.IconTile("\uE711",40,18,"SkinPanel","SkinText",20));panel.Children.Add(controls);
        var location=UI.Text("설정(⚙) 바로 오른쪽의 작은 핀(📌)",13,UI.Muted);location.HorizontalAlignment=HorizontalAlignment.Center;location.Margin=new Thickness(0,0,0,18);panel.Children.Add(location);
        var steps=UI.Text("① 런처 아래쪽의 작은 핀 버튼을 누르세요.\n② Windows 확인 창에서 고정을 승인하세요.\n③ 이후에는 고정된 작업표시줄 아이콘으로 열 수 있어요.\n\n고정 해제·이전 Windows: 핀 버튼의 안내 창을 열어 둔 채 작업표시줄 아이콘을 우클릭하세요.",14);steps.Margin=new Thickness(0,0,0,18);panel.Children.Add(steps);
        panel.Children.Add(UI.Button("지금 고정하기",()=>DialogResult=true));
        panel.Children.Add(UI.Button("확인 · 나중에 고정할게요",()=>DialogResult=false));
        Loaded+=(_,_)=>Skin.Connect(this);
    }
}
