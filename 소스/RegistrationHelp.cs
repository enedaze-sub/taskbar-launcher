using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using WpfPath = System.Windows.Shapes.Path;
using Microsoft.Win32;

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
    /// <summary>Pasting a full shortcut target fills the path and argument boxes separately.</summary>
    internal static void AttachPaste(TextBox target,TextBox arguments)
    {
        DataObject.AddPastingHandler(target,(_,args)=>
        {
            if(args.DataObject.GetData(DataFormats.UnicodeText) is not string raw)return;
            var command=Parse(raw);
            if(command.Arguments.Length==0)return;
            args.CancelCommand();target.Text=command.Target;arguments.Text=command.Arguments;target.CaretIndex=target.Text.Length;
        });
    }
    /// <summary>A friendly default name: Update.exe --processStart Discord.exe becomes "Discord".</summary>
    internal static string SuggestName(string target,string arguments)
    {
        var start=Regex.Match(arguments,@"--processStart\s+""?([^""\s]+)",RegexOptions.IgnoreCase);
        if(start.Success)return System.IO.Path.GetFileNameWithoutExtension(start.Groups[1].Value);
        target=target.Trim().Trim('"');if(target.Length==0)return "";
        if(Uri.TryCreate(target,UriKind.Absolute,out var uri) && !uri.IsFile)return uri.Host.Length>0?uri.Host.Replace("www.",""):uri.Scheme;
        try{string name=System.IO.Path.GetFileNameWithoutExtension(target.TrimEnd('\\','/'));return name.Length>0?name:target;}catch{return target;}
    }
}

/// <summary>A themed choice sheet: grouped rows with centered icon tiles, a switch for toggles and a danger style.</summary>
internal sealed class ActionSheet : Window
{
    private readonly StackPanel choices=new();
    private StackPanel? group;
    public ActionSheet(string title,string subtitle,ThemeSettings theme)
    {
        UI.Theme(this,theme);Title=title;Width=452;SizeToContent=SizeToContent.Height;MaxHeight=SystemParameters.WorkArea.Height-30;ShowInTaskbar=false;Topmost=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
        var panel=new StackPanel {Margin=new Thickness(22,20,22,22)};
        var shell=new Border {CornerRadius=new CornerRadius(20),BorderThickness=new Thickness(1),Child=panel,Margin=new Thickness(10),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=18,ShadowDepth=3,Opacity=.3}};shell.SetResourceReference(Border.BackgroundProperty,"SkinBackground");shell.SetResourceReference(Border.BorderBrushProperty,"SkinBorder");Content=shell;
        shell.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is not Button){try{DragMove();}catch{}}};
        var heading=new Grid();heading.ColumnDefinitions.Add(new());heading.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var titles=new StackPanel();var label=UI.Heading(title,21);titles.Children.Add(label);
        var desc=UI.Text(subtitle,13.5,UI.Muted);desc.Margin=new Thickness(0,4,0,0);titles.Children.Add(desc);heading.Children.Add(titles);
        var close=UI.RoundIcon("\uE8BB","닫기 (Esc)",()=>DialogResult=false,34);close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new Thickness(8,0,0,0);Grid.SetColumn(close,1);heading.Children.Add(close);
        panel.Children.Add(heading);
        choices.Margin=new Thickness(0,8,0,0);
        panel.Children.Add(new ScrollViewer {Content=choices,MaxHeight=Math.Max(300,SystemParameters.WorkArea.Height-220),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){DialogResult=false;e.Handled=true;}};
        Loaded+=(_,_)=>Skin.Connect(this);
    }
    internal void Section(string title)
    {
        var label=UI.Text(title,12.5,UI.Muted);label.FontWeight=FontWeights.SemiBold;label.Margin=new Thickness(4,14,0,7);choices.Children.Add(label);group=null;
    }
    private StackPanel Group()
    {
        if(group!=null)
        {
            var line=new Border {Height=1,Margin=new Thickness(62,0,14,0)};line.SetResourceReference(Border.BackgroundProperty,"SkinBorder");group.Children.Add(line);return group;
        }
        group=new StackPanel();var box=new Border {CornerRadius=new CornerRadius(14),Child=group,Padding=new Thickness(4),Margin=new Thickness(0,6,0,0)};box.SetResourceReference(Border.BackgroundProperty,"SkinPanel");choices.Children.Add(box);return group;
    }
    internal void Add(string glyph,string title,string description,Action action,bool danger=false)
    {
        var host=Group();
        var card=UI.Button("",()=>{DialogResult=true;action();});card.Padding=new Thickness(10,11,12,11);card.Margin=new Thickness(0);card.Background=Brushes.Transparent;card.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(50)});row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new GridLength(20)});
        var icon=UI.IconTile(glyph,38,17,danger?"SkinDangerSoft":"SkinAccentSoft",danger?"SkinDanger":"SkinAccentInk",11);icon.HorizontalAlignment=HorizontalAlignment.Left;row.Children.Add(icon);
        var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var name=UI.Text(title,15);name.FontWeight=FontWeights.SemiBold;if(danger)name.SetResourceReference(TextBlock.ForegroundProperty,"SkinDanger");text.Children.Add(name);
        var hint=UI.Text(description,12.5,UI.Muted);hint.Margin=new Thickness(0,3,4,0);text.Children.Add(hint);Grid.SetColumn(text,1);row.Children.Add(text);
        var arrow=UI.Icon("\uE76C",12,"SkinMuted");Grid.SetColumn(arrow,2);row.Children.Add(arrow);card.Content=row;
        System.Windows.Automation.AutomationProperties.SetName(card,title);host.Children.Add(card);
    }
    internal void AddToggle(string title,string description,bool selected,Action<bool> action)
    {
        var host=Group();
        var box=new CheckBox {Content=title,IsChecked=selected,FontSize=15,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0)};box.SetResourceReference(StyleProperty,"SkinSwitch");box.Click+=(_,_)=>action(box.IsChecked==true);
        var body=new StackPanel {Margin=new Thickness(14,12,14,13)};body.Children.Add(box);var hint=UI.Text(description,12.5,UI.Muted);hint.Margin=new Thickness(0,4,60,0);hint.LineHeight=19;body.Children.Add(hint);
        host.Children.Add(body);
    }
    internal void Note(string text)
    {
        var row=new Grid {Margin=new Thickness(6,14,6,0)};row.ColumnDefinitions.Add(new(){Width=new GridLength(24)});row.ColumnDefinitions.Add(new());
        var icon=UI.Icon("\uE946",14,"SkinMuted");icon.VerticalAlignment=VerticalAlignment.Top;icon.Margin=new Thickness(0,2,0,0);icon.HorizontalAlignment=HorizontalAlignment.Left;row.Children.Add(icon);
        var label=UI.Text(text,12.5,UI.Muted);label.LineHeight=19;Grid.SetColumn(label,1);row.Children.Add(label);choices.Children.Add(row);group=null;
    }
}

/// <summary>
/// 직접 등록: the recommended drag-and-drop path first, then copying a shortcut target, with the registration form beside it.
/// Without a register callback (opened from the editor) it shows only the guide.
/// </summary>
internal sealed class RegistrationHelpWindow : Window
{
    private readonly Action<Entry>? register;
    private readonly Func<string[],int>? registerFiles;
    private readonly TextBox target=new() {Tag="Target",ToolTip="바로가기 ‘대상’을 통째로 붙여 넣어도 돼요"},arguments=new() {Tag="Arguments"},name=new() {Tag="Name",MaxLength=80};
    private readonly Image icon=new() {Width=32,Height=32,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private readonly TextBlock status=UI.Text("",13,UI.Muted);
    private bool nameEdited, settingName;
    internal int RegisteredCount { get; private set; }

    public RegistrationHelpWindow(ThemeSettings theme,Action<Entry>? register,Func<string[],int>? registerFiles=null,string? destination=null)
    {
        this.register=register;this.registerFiles=registerFiles;bool form=register!=null;
        UI.Theme(this,theme);Title=form?"직접 등록":"등록 방법 안내";Width=form?1140:820;Height=860;Width=Math.Min(Width,SystemParameters.WorkArea.Width-24);Height=Math.Min(Height,SystemParameters.WorkArea.Height-30);MinWidth=form?900:640;MinHeight=560;ShowInTaskbar=false;Topmost=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var main=new Grid {Margin=new Thickness(28,22,28,20)};main.RowDefinitions.Add(new(){Height=GridLength.Auto});main.RowDefinitions.Add(new());main.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=main;
        var header=new StackPanel {Margin=new Thickness(0,0,0,18)};header.Children.Add(UI.Heading(form?"직접 등록":"앱을 등록하는 방법",25));
        var summary=UI.Text("대부분은 실행 파일을 끌어다 놓기만 하면 돼요. 작업표시줄에만 있는 앱은 ‘대상’ 주소를 복사해 붙여 넣으세요.",14,UI.Muted);summary.Margin=new Thickness(0,6,0,0);header.Children.Add(summary);main.Children.Add(header);
        var body=new Grid();Grid.SetRow(body,1);main.Children.Add(body);body.ColumnDefinitions.Add(new());if(form)body.ColumnDefinitions.Add(new(){Width=new GridLength(390)});
        var guide=new StackPanel {Margin=new Thickness(0,0,form?22:6,0)};body.Children.Add(new ScrollViewer {Content=guide,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});

        // Method 1 comes first because it covers almost every app.
        var first=Card(guide);var firstHead=MethodTitle("1","실행 파일을 끌어다 놓기","추천");first.Children.Add(firstHead);
        first.Children.Add(Body(form
            ?"바탕화면 아이콘, 시작 메뉴 바로가기, 탐색기의 .exe 파일을 런처 설정 창에 끌어다 놓으면 바로 등록돼요. 오른쪽 점선 칸에 놓아도 됩니다.\n여러 개를 한 번에 놓아도 되고, 폴더 카드 위에 놓으면 그 폴더 안으로 들어가요."
            :"바탕화면 아이콘, 시작 메뉴 바로가기, 탐색기의 .exe 파일을 런처 설정 창에 끌어다 놓으면 바로 등록돼요.\n여러 개를 한 번에 놓아도 되고, 폴더 카드 위에 놓으면 그 폴더 안으로 들어가요."));
        first.Children.Add(DragIllustration());

        var second=Card(guide);second.Children.Add(MethodTitle("2","작업표시줄 아이콘에서 ‘대상’ 복사하기",null));
        second.Children.Add(Body("실행 파일 위치를 모르거나, Discord처럼 실행 옵션(--processStart 등)이 붙어 실행되는 앱에 쓰세요."));
        var shots=new Grid {Margin=new Thickness(0,14,0,0)};shots.ColumnDefinitions.Add(new());shots.ColumnDefinitions.Add(new(){Width=new GridLength(14)});shots.ColumnDefinitions.Add(new());second.Children.Add(shots);
        var shotA=Step("1","작업표시줄 아이콘을 우클릭한 뒤, 메뉴의 앱 이름을 한 번 더 우클릭 → ‘속성’","등록-우클릭.png",theme);shots.Children.Add(shotA);
        var shotB=Step("2","‘바로 가기’ 탭의 ‘대상’ 칸 클릭 → Ctrl+A → Ctrl+C로 전체 복사","등록-속성.png",theme);Grid.SetColumn(shotB,2);shots.Children.Add(shotB);
        var last=Step("3",form?"오른쪽 ‘파일 경로 또는 URL’ 칸에 Ctrl+V → 이름 확인 → 등록. .exe 뒤의 옵션은 ‘실행 인수’로 자동 분리돼요.":"편집 창의 ‘파일 경로 또는 URL’ 칸에 Ctrl+V → 저장. .exe 뒤의 옵션은 ‘실행 인수’로 자동 분리돼요.",null,theme);last.Margin=new Thickness(0,12,0,0);second.Children.Add(last);

        var tip=new Border {CornerRadius=new CornerRadius(14),Padding=new Thickness(16,13,16,14),Margin=new Thickness(0,0,0,4)};tip.SetResourceReference(Border.BackgroundProperty,"SkinAccentSoft");
        var tipRow=new Grid();tipRow.ColumnDefinitions.Add(new(){Width=new GridLength(30)});tipRow.ColumnDefinitions.Add(new());var bulb=UI.Icon("\uEA80",16,"SkinAccentInk");bulb.VerticalAlignment=VerticalAlignment.Top;bulb.HorizontalAlignment=HorizontalAlignment.Left;bulb.Margin=new Thickness(0,2,0,0);tipRow.Children.Add(bulb);
        var tipText=UI.Text("탐색기의 .exe 파일에는 ‘대상’ 칸이 없어요. 그냥 끌어다 놓거나, Shift+우클릭 → ‘경로로 복사’로 주소를 복사하세요. 바로가기(.lnk)는 우클릭 → 속성 → ‘대상’에서 복사할 수 있어요.",13.5);tipText.LineHeight=21;Grid.SetColumn(tipText,1);tipRow.Children.Add(tipText);tip.Child=tipRow;guide.Children.Add(tip);

        if(form)
        {
            var side=BuildForm(destination??"라이브러리");Grid.SetColumn(side,1);body.Children.Add(side);
            AllowDrop=true;DragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
            Drop+=(_,e)=>{if(e.Data.GetData(DataFormats.FileDrop) is string[] paths)RegisterFiles(paths);e.Handled=true;};
        }
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};Grid.SetRow(buttons,2);main.Children.Add(buttons);
        var close=UI.Button("닫기",()=>DialogResult=RegisteredCount>0);close.IsCancel=true;close.MinWidth=84;buttons.Children.Add(close);
        Loaded+=(_,_)=>{Skin.Connect(this);if(form)target.Focus();};
    }
    private static StackPanel Card(Panel host)
    {
        var panel=new StackPanel {Margin=new Thickness(22,20,22,22)};var card=new Border {CornerRadius=new CornerRadius(18),Margin=new Thickness(0,0,0,16),Child=panel};card.SetResourceReference(Border.BackgroundProperty,"SkinPanel");host.Children.Add(card);return panel;
    }
    private static FrameworkElement Number(string number,double size=28)
    {
        var badge=new Grid {Width=size,Height=size,VerticalAlignment=VerticalAlignment.Top};var disk=new Ellipse();disk.SetResourceReference(Shape.FillProperty,"SkinAccent");badge.Children.Add(disk);
        var digit=new TextBlock {Text=number,FontSize=size*.5,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,FontFamily=new FontFamily("Segoe UI")};digit.SetResourceReference(TextBlock.ForegroundProperty,"SkinAccentText");badge.Children.Add(digit);return badge;
    }
    private static FrameworkElement MethodTitle(string number,string title,string? badge)
    {
        var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,10)};
        var method=UI.Text("방법 "+number,13);method.FontWeight=FontWeights.Bold;method.SetResourceReference(TextBlock.ForegroundProperty,"SkinAccentInk");method.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(method);
        var text=UI.Heading(title,19);text.Margin=new Thickness(10,0,0,0);text.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(text);
        if(badge!=null){var pill=new Border {CornerRadius=new CornerRadius(9),Padding=new Thickness(9,2,9,3),Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Center};pill.SetResourceReference(Border.BackgroundProperty,"SkinAccent");var t=new TextBlock {Text=badge,FontSize=12,FontWeight=FontWeights.Bold};t.SetResourceReference(TextBlock.ForegroundProperty,"SkinAccentText");pill.Child=t;row.Children.Add(pill);}
        return row;
    }
    private static TextBlock Body(string text){var block=UI.Text(text,14);block.LineHeight=22;return block;}
    private FrameworkElement Step(string number,string text,string? file,ThemeSettings theme)
    {
        var panel=new StackPanel();var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(36)});row.ColumnDefinitions.Add(new());row.Children.Add(Number(number,26));
        var label=UI.Text(text,14);label.LineHeight=21;label.Margin=new Thickness(0,2,0,0);Grid.SetColumn(label,1);row.Children.Add(label);panel.Children.Add(row);
        if(file==null)return panel;
        string path=System.IO.Path.Combine(AppContext.BaseDirectory,"Assets",file);
        if(!File.Exists(path))return panel;
        var image=new Image {Source=Native.LoadImage(path,720),Stretch=Stretch.Uniform,MaxHeight=250,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        var frame=new Border {CornerRadius=new CornerRadius(12),Padding=new Thickness(8),Height=268,Margin=new Thickness(0,12,0,0),Background=UI.Brush("#FFFFFF"),Child=image,Tag="keep-colors",Cursor=Cursors.Hand,ToolTip="클릭하면 크게 볼 수 있어요",BorderThickness=new Thickness(1),BorderBrush=UI.Brush("#D9DEE5")};
        frame.MouseLeftButtonUp+=(_,_)=>Zoom(path,theme);panel.Children.Add(frame);
        var more=UI.IconButton("\uE8A3","크게 보기",()=>Zoom(path,theme));more.Background=Brushes.Transparent;more.FontSize=12.5;more.Padding=new Thickness(8,5,10,5);more.HorizontalAlignment=HorizontalAlignment.Center;more.SetResourceReference(Control.ForegroundProperty,"SkinMuted");panel.Children.Add(more);
        return panel;
    }
    private void Zoom(string path,ThemeSettings theme)
    {
        var zoom=new Window {Title="등록 예시 화면",Owner=this,Width=760,Height=Math.Min(860,SystemParameters.WorkArea.Height-30),ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};UI.Theme(zoom,theme);
        zoom.Content=new Border {Background=UI.Brush("#FFFFFF"),Tag="keep-colors",Margin=new Thickness(16),CornerRadius=new CornerRadius(12),Padding=new Thickness(10),Child=new Image {Source=Native.LoadImage(path,1200),Stretch=Stretch.Uniform}};
        zoom.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)zoom.Close();};zoom.ShowDialog();
    }
    /// <summary>Drawn illustration of dragging an .exe into the settings window.</summary>
    private static FrameworkElement DragIllustration()
    {
        var canvas=new Canvas {Width=540,Height=150,Margin=new Thickness(0,16,0,0),HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false};
        void Put(UIElement e,double x,double y){Canvas.SetLeft(e,x);Canvas.SetTop(e,y);canvas.Children.Add(e);}
        // Source file on the desktop
        var file=new Border {Width=84,Height=84,CornerRadius=new CornerRadius(20),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=12,ShadowDepth=2,Opacity=.25}};file.SetResourceReference(Border.BackgroundProperty,"SkinCard");
        var fileFace=new Grid();fileFace.Children.Add(UI.Icon("\uE8A5",38,"SkinText"));
        var ext=new Border {CornerRadius=new CornerRadius(5),Padding=new Thickness(5,1,5,2),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,12)};ext.SetResourceReference(Border.BackgroundProperty,"SkinAccent");
        var extText=new TextBlock {Text="EXE",FontSize=10,FontWeight=FontWeights.Bold,FontFamily=new FontFamily("Segoe UI")};extText.SetResourceReference(TextBlock.ForegroundProperty,"SkinAccentText");ext.Child=extText;fileFace.Children.Add(ext);
        file.Child=fileFace;Put(file,20,18);
        var fileName=UI.Text("앱이름.exe",12.5,UI.Muted);fileName.Width=124;fileName.TextAlignment=TextAlignment.Center;Put(fileName,0,110);
        // Dashed motion path that ends at the pointer inside the drop area.
        var path=new WpfPath {Data=Geometry.Parse("M 122,58 C 180,4 262,4 318,46"),StrokeThickness=2.5,StrokeDashArray=new DoubleCollection{2.5,2.2},StrokeDashCap=PenLineCap.Round};path.SetResourceReference(Shape.StrokeProperty,"SkinAccent");canvas.Children.Add(path);
        var cursor=UI.Icon("\uE8B0",26,"SkinText");
        // Drop target: a miniature settings window
        var zone=new Grid {Width=220,Height=132};
        var dashes=new Rectangle {RadiusX=18,RadiusY=18,StrokeThickness=2,StrokeDashArray=new DoubleCollection{4,3}};dashes.SetResourceReference(Shape.StrokeProperty,"SkinAccent");dashes.SetResourceReference(Shape.FillProperty,"SkinAccentSoft");zone.Children.Add(dashes);
        var cards=new UniformGrid {Columns=3,Rows=2,Margin=new Thickness(18,18,18,18)};
        for(int i=0;i<6;i++){var mini=new Border {CornerRadius=new CornerRadius(9),Margin=new Thickness(5)};if(i==5){mini.SetResourceReference(Border.BackgroundProperty,"SkinAccent");mini.Child=UI.Icon("\uE710",14,"SkinAccentText");}else{mini.SetResourceReference(Border.BackgroundProperty,"SkinCard");mini.Opacity=.85;}cards.Children.Add(mini);}
        zone.Children.Add(cards);Put(zone,300,6);Put(cursor,316,42);
        return canvas;
    }
    private FrameworkElement BuildForm(string destination)
    {
        var panel=new StackPanel {Margin=new Thickness(22,20,22,22)};
        var card=new Border {CornerRadius=new CornerRadius(18),Child=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};card.SetResourceReference(Border.BackgroundProperty,"SkinPanel");
        panel.Children.Add(UI.Heading("등록하기",19));
        var where=UI.Text("등록 위치 · "+destination,12.5,UI.Muted);where.Margin=new Thickness(0,4,0,14);where.TextTrimming=TextTrimming.CharacterEllipsis;panel.Children.Add(where);
        // Drop zone
        var zone=new Grid {Height=148,Cursor=Cursors.Hand,Background=Brushes.Transparent,ToolTip="클릭하면 파일 선택 창이 열려요"};
        var dashes=new Rectangle {RadiusX=16,RadiusY=16,StrokeThickness=2,StrokeDashArray=new DoubleCollection{4,3}};dashes.SetResourceReference(Shape.StrokeProperty,"SkinAccent");dashes.SetResourceReference(Shape.FillProperty,"SkinAccentSoft");zone.Children.Add(dashes);
        var zoneText=new StackPanel {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        var drop=UI.IconTile("\uE896",46,20,"SkinAccent","SkinAccentText",14);drop.Margin=new Thickness(0,0,0,10);zoneText.Children.Add(drop);
        var zoneTitle=UI.Text("파일을 여기로 끌어다 놓기",15);zoneTitle.FontWeight=FontWeights.SemiBold;zoneTitle.TextAlignment=TextAlignment.Center;zoneText.Children.Add(zoneTitle);
        var zoneHint=UI.Text("또는 클릭해서 파일 찾아보기",12.5,UI.Muted);zoneHint.TextAlignment=TextAlignment.Center;zoneHint.Margin=new Thickness(0,3,0,0);zoneText.Children.Add(zoneHint);zone.Children.Add(zoneText);
        zone.MouseLeftButtonUp+=(_,_)=>Browse();zone.MouseEnter+=(_,_)=>dashes.StrokeThickness=3;zone.MouseLeave+=(_,_)=>dashes.StrokeThickness=2;
        panel.Children.Add(zone);
        // Divider
        var divider=new Grid {Margin=new Thickness(0,20,0,6)};divider.ColumnDefinitions.Add(new());divider.ColumnDefinitions.Add(new(){Width=GridLength.Auto});divider.ColumnDefinitions.Add(new());
        foreach(int column in new[]{0,2}){var line=new Border {Height=1,VerticalAlignment=VerticalAlignment.Center};line.SetResourceReference(Border.BackgroundProperty,"SkinBorder");Grid.SetColumn(line,column);divider.Children.Add(line);}
        var or=UI.Text("또는 대상 주소 붙여넣기",12.5,UI.Muted);or.Margin=new Thickness(10,0,10,0);Grid.SetColumn(or,1);divider.Children.Add(or);panel.Children.Add(divider);
        Label(panel,"파일 경로 또는 URL");panel.Children.Add(target);
        Label(panel,"실행 인수 (선택)");panel.Children.Add(arguments);
        Label(panel,"이름");
        var nameRow=new Grid();nameRow.ColumnDefinitions.Add(new(){Width=new GridLength(50)});nameRow.ColumnDefinitions.Add(new());
        var iconBox=new Border {Width=42,Height=42,CornerRadius=new CornerRadius(11),HorizontalAlignment=HorizontalAlignment.Left,Child=icon};iconBox.SetResourceReference(Border.BackgroundProperty,"SkinControl");nameRow.Children.Add(iconBox);Grid.SetColumn(name,1);nameRow.Children.Add(name);panel.Children.Add(nameRow);
        var add=UI.IconButton("\uE710","등록",Register,true);add.Margin=new Thickness(0,20,0,0);add.Padding=new Thickness(14,11,14,11);add.HorizontalAlignment=HorizontalAlignment.Stretch;add.IsDefault=true;panel.Children.Add(add);
        status.Margin=new Thickness(2,12,0,0);status.LineHeight=19;panel.Children.Add(status);
        TargetCommand.AttachPaste(target,arguments);
        void Suggest(){UpdateIcon();if(nameEdited)return;settingName=true;name.Text=TargetCommand.SuggestName(target.Text,arguments.Text);settingName=false;}
        target.TextChanged+=(_,_)=>Suggest();arguments.TextChanged+=(_,_)=>Suggest();
        name.TextChanged+=(_,_)=>{if(!settingName)nameEdited=name.Text.Length>0;};
        return card;
    }
    private static void Label(Panel panel,string text){var label=UI.Text(text,12.5,UI.Muted);label.FontWeight=FontWeights.SemiBold;label.Margin=new Thickness(2,14,0,6);panel.Children.Add(label);}
    private void UpdateIcon()
    {
        try{var parsed=TargetCommand.Parse(target.Text);icon.Source=parsed.Target.Length==0?null:Native.FileIcon(Environment.ExpandEnvironmentVariables(parsed.Target));}catch{icon.Source=null;}
    }
    private void Browse()
    {
        var dialog=new OpenFileDialog {Title="등록할 파일 또는 바로가기 선택",Filter="실행 파일 및 바로가기|*.exe;*.lnk;*.url;*.bat;*.cmd|모든 파일|*.*",Multiselect=true};
        if(dialog.ShowDialog(this)==true)RegisterFiles(dialog.FileNames);
    }
    internal void RegisterFiles(string[] paths)
    {
        if(registerFiles==null)return;int added=registerFiles(paths);RegisteredCount+=added;
        Status(added>0?$"{added}개를 등록했어요. 계속 등록하거나 창을 닫으세요.":"이미 등록되어 있거나 찾을 수 없는 파일이에요.",added>0);
    }
    internal void Register()
    {
        var parsed=TargetCommand.Parse(target.Text);
        if(parsed.Target.Length==0){Status("파일 경로나 URL을 먼저 붙여 넣어 주세요.",false);target.Focus();return;}
        string args=parsed.Arguments.Length>0?parsed.Arguments:arguments.Text.Trim();
        string label=name.Text.Trim().Length>0?name.Text.Trim():TargetCommand.SuggestName(parsed.Target,args);
        register!(new Entry {Name=label.Length>0?label:"새 바로가기",Target=parsed.Target,Arguments=args});RegisteredCount++;
        Status($"‘{label}’을(를) 등록했어요. 계속 등록하거나 창을 닫으세요.",true);
        settingName=true;target.Clear();arguments.Clear();name.Clear();settingName=false;nameEdited=false;target.Focus();
    }
    private void Status(string text,bool success){status.Text=(success?"✓  ":"")+text;status.SetResourceReference(TextBlock.ForegroundProperty,success?"SkinAccentInk":"SkinDanger");}
}
