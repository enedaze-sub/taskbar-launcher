using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeckLauncher;

internal static class TargetCommand
{
    internal sealed record Parts(string Target,string Arguments);
    internal static Parts Parse(string text)
    {
        string raw=text.Trim();if(raw.Length==0)return new("","");
        if(raw.StartsWith('"'))
        {
            int end=raw.IndexOf('"',1);if(end>0)return new(raw[1..end],raw[(end+1)..].Trim());
        }
        var match=Regex.Match(raw,@"^(.*?\.(?:exe|com|cmd|bat|lnk))(?=\s|$)(.*)$",RegexOptions.IgnoreCase);
        return match.Success?new(match.Groups[1].Value,match.Groups[2].Value.Trim()):new(raw.Trim('"'),"");
    }
}

internal sealed class ActionSheet : Window
{
    private readonly StackPanel choices=new();
    public ActionSheet(string title,string subtitle,ThemeSettings theme)
    {
        UI.Theme(this,theme);Title=title;Width=490;SizeToContent=SizeToContent.Height;MaxHeight=SystemParameters.WorkArea.Height-30;ShowInTaskbar=false;Topmost=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
        var panel=new StackPanel {Margin=new Thickness(22)};
        var shell=new Border {CornerRadius=new CornerRadius(22),BorderThickness=new Thickness(1),Child=panel};shell.SetResourceReference(Border.BackgroundProperty,"SkinBackground");shell.SetResourceReference(Border.BorderBrushProperty,"SkinBorder");Content=shell;
        var heading=new DockPanel();var close=UI.Button("×",()=>DialogResult=false);close.Padding=new Thickness(11,6,11,6);DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);var label=UI.Text(title,24);label.FontFamily=UI.DisplayFont;heading.Children.Add(label);panel.Children.Add(heading);
        var desc=UI.Text(subtitle,14,UI.Muted);desc.Margin=new Thickness(0,8,0,18);panel.Children.Add(desc);
        panel.Children.Add(new ScrollViewer {Content=choices,MaxHeight=Math.Max(300,SystemParameters.WorkArea.Height-220),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        Loaded+=(_,_)=>Skin.Connect(this);
    }
    internal void Add(string glyph,string title,string description,Action action)
    {
        var card=UI.Button("",()=>{DialogResult=true;action();});card.Padding=new Thickness(14);card.Margin=new Thickness(0,0,0,9);card.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(45)});row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new GridLength(16)});
        var icon=new Border {CornerRadius=new CornerRadius(11),Width=34,Height=34,Child=UI.Text(glyph,22,UI.Accent)};icon.SetResourceReference(Border.BackgroundProperty,"SkinHover");row.Children.Add(icon);
        var text=new StackPanel();var name=UI.Text(title,16);name.FontFamily=UI.DisplayFont;text.Children.Add(name);var hint=UI.Text(description,12,UI.Muted);hint.Margin=new Thickness(0,5,4,0);text.Children.Add(hint);Grid.SetColumn(text,1);row.Children.Add(text);var arrow=UI.Text("›",22,UI.Muted);Grid.SetColumn(arrow,2);row.Children.Add(arrow);card.Content=row;choices.Children.Add(card);
    }
    internal void AddToggle(string title,string description,bool selected,Action<bool> action)
    {
        var box=new CheckBox {Content=title,IsChecked=selected,FontSize=16,Margin=new Thickness(13,10,13,6)};box.Click+=(_,_)=>action(box.IsChecked==true);
        var body=new StackPanel();body.Children.Add(box);var hint=UI.Text(description,12,UI.Muted);hint.Margin=new Thickness(13,0,13,13);body.Children.Add(hint);
        var card=new Border {CornerRadius=new CornerRadius(13),Margin=new Thickness(0,0,0,12),Child=body};card.SetResourceReference(Border.BackgroundProperty,"SkinPanel");choices.Children.Add(card);
    }
    internal void Note(string text) {var label=UI.Text(text,13,UI.Muted);label.Margin=new Thickness(10,5,10,10);choices.Children.Add(label);}
}

internal sealed class RegistrationHelpWindow : Window
{
    public RegistrationHelpWindow(ThemeSettings theme,Action? register)
    {
        UI.Theme(this,theme);Title="앱 주소를 찾아 등록하기";Width=1050;Height=835;Width=Math.Min(Width,SystemParameters.WorkArea.Width-24);Height=Math.Min(Height,SystemParameters.WorkArea.Height-30);MinWidth=800;MinHeight=600;ShowInTaskbar=false;Topmost=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var main=new Grid {Margin=new Thickness(24)};main.RowDefinitions.Add(new(){Height=GridLength.Auto});main.RowDefinitions.Add(new(){Height=GridLength.Auto});main.RowDefinitions.Add(new());main.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=main;
        var title=UI.Text("앱 주소를 찾아, 한 번에 등록",26);title.FontFamily=UI.DisplayFont;main.Children.Add(title);
        var summary=UI.Text("우클릭 → 속성 → 대상 주소 복사 → 런처에 붙여넣기",15,UI.Muted);summary.Margin=new Thickness(0,10,0,17);Grid.SetRow(summary,1);main.Children.Add(summary);
        var scroll=new ScrollViewer {VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,2);main.Children.Add(scroll);
        var content=new StackPanel();scroll.Content=content;
        var steps=new Grid();steps.ColumnDefinitions.Add(new());steps.ColumnDefinitions.Add(new());content.Children.Add(steps);
        Border Step(string number,string heading,string detail,string file)
        {
            var panel=new StackPanel {Margin=new Thickness(16)};var headingText=UI.Text(number+"  "+heading,18);headingText.FontFamily=UI.DisplayFont;panel.Children.Add(headingText);var description=UI.Text(detail,14);description.Margin=new Thickness(0,10,0,14);panel.Children.Add(description);
            string path=Path.Combine(AppContext.BaseDirectory,"Assets",file);
            if(File.Exists(path)){var image=new Image {Source=Native.LoadImage(path,720),Stretch=Stretch.Uniform,MaxHeight=250};panel.Children.Add(new Border {CornerRadius=new CornerRadius(12),Padding=new Thickness(8),Background=UI.Brush("#FFFFFF"),Child=image,Tag="keep-colors"});panel.Children.Add(UI.Button("예시 화면 크게 보기",()=>
                {
                    var zoom=new Window {Title="등록 예시 화면",Owner=this,Width=720,Height=Math.Min(860,SystemParameters.WorkArea.Height-30),ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};UI.Theme(zoom,theme);zoom.Content=new Image {Source=Native.LoadImage(path,1200),Stretch=Stretch.Uniform,Margin=new Thickness(18)};zoom.ShowDialog();
                }));}
            var card=new Border {CornerRadius=new CornerRadius(17),Margin=new Thickness(0,0,12,12),Child=panel};card.SetResourceReference(Border.BackgroundProperty,"SkinPanel");return card;
        }
        var first=Step("01","실행 중인 앱의 속성 열기","작업표시줄 아이콘을 우클릭하세요.\n나타난 앱 이름·아이콘을 한 번 더 우클릭한 뒤 ‘속성’을 선택합니다.","등록-우클릭.png");steps.Children.Add(first);
        var second=Step("02","대상 내용을 끝까지 복사","‘바로 가기’ 탭의 ‘대상’을 클릭합니다.\nCtrl+A → Ctrl+C로 전체 내용을 복사하세요.","등록-속성.png");Grid.SetColumn(second,1);steps.Children.Add(second);
        var final=new StackPanel {Margin=new Thickness(17)};var third=UI.Text("03  런처에 붙여넣고 저장",19);third.FontFamily=UI.DisplayFont;final.Children.Add(third);var explain=UI.Text("런처 설정에서 ‘··· → 주소로 직접 등록’을 엽니다.\n‘파일 경로 또는 URL’에 Ctrl+V로 붙여넣고 이름을 적은 뒤 저장하세요.\n.exe 뒤의 --processStart 같은 옵션은 ‘실행 인수’로 자동 분리합니다.",15);explain.Margin=new Thickness(0,9,0,10);final.Children.Add(explain);
        final.Children.Add(UI.Text("탐색기에서 찾는 경우",16,UI.Accent));final.Children.Add(UI.Text("바로가기(.lnk)는 우클릭 → 속성 → 바로 가기 탭의 대상을 복사하세요.\n실제 .exe 파일은 ‘대상’ 칸이 없습니다. Shift+우클릭 → ‘경로로 복사’를 사용하거나 주소와 파일명을 함께 복사하세요. 파일 자체를 런처 설정창에 끌어 넣어도 됩니다.",14,UI.Muted));
        var note=new Border {CornerRadius=new CornerRadius(17),Child=final};note.SetResourceReference(Border.BackgroundProperty,"SkinPanel");content.Children.Add(note);
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,15,0,0)};Grid.SetRow(buttons,3);main.Children.Add(buttons);buttons.Children.Add(UI.Button("닫기",()=>DialogResult=false));
        if(register!=null)buttons.Children.Add(UI.Button("주소 붙여넣어 등록하기",()=>{DialogResult=true;register();}));
        Loaded+=(_,_)=>Skin.Connect(this);
    }
}
