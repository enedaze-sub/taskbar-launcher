using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeckLauncher;

public sealed class SettingsColors
{
    public bool Separate {get;set;}
    public bool Initialized {get;set;}
    public string Background {get;set;}="#111C2B";
    public string Surface {get;set;}="#2A3B50";
    public string Accent {get;set;}="#6DE0D4";
    public string Text {get;set;}="#F0F5FC";
    public SettingsColors Copy() => (SettingsColors)MemberwiseClone();
    public void Take(ThemeSettings theme) {Background=theme.Background;Surface=theme.Surface;Accent=theme.Accent;Text=theme.Text;}
    public ThemeSettings Apply(ThemeSettings launcher)
    {
        if(!Separate)return launcher;
        var result=launcher.Copy();result.Background=Background;result.Surface=Surface;result.Accent=Accent;result.Text=Text;return result;
    }
    public void Validate() {if(new[]{Background,Surface,Accent,Text}.Any(v=>!ThemeSettings.ValidColor(v)))throw new InvalidDataException("설정창 색상이 잘못되었습니다.");}
}

public sealed class SettingsColorsWindow : Window
{
    private readonly ThemeSettings launcher;
    private readonly CheckBox separate=new() {Content="런처와 설정창 색상을 따로 사용"};
    private readonly StackPanel controls=new();
    private readonly Dictionary<string,TextBox> fields=new();
    private bool filling;
    internal SettingsColors Draft {get;}
    public SettingsColors? Result {get;private set;}
    public SettingsColorsWindow(Library library)
    {
        launcher=library.Theme.Copy();Draft=library.SettingsColors.Copy();if(!Draft.Initialized)Draft.Take(launcher);
        UI.Theme(this,library.InterfaceTheme);Title="설정창 색상";Width=900;Height=750;MinWidth=650;MinHeight=540;ShowInTaskbar=false;Topmost=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Width=Math.Min(Width,SystemParameters.WorkArea.Width-24);Height=Math.Min(Height,SystemParameters.WorkArea.Height-30);
        var main=new Grid {Margin=new Thickness(26)};main.RowDefinitions.Add(new(){Height=GridLength.Auto});main.RowDefinitions.Add(new(){Height=GridLength.Auto});main.RowDefinitions.Add(new());main.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=main;
        var heading=new StackPanel();heading.Children.Add(UI.Heading("설정창만 다른 색으로",25));heading.Children.Add(UI.Text("기본은 런처 색상과 연동됩니다. 분리하면 런처 디자인·배경·아이콘 색은 유지됩니다.",13,UI.Muted));main.Children.Add(heading);
        separate.IsChecked=Draft.Separate;separate.Margin=new Thickness(0,18,0,14);Grid.SetRow(separate,1);main.Children.Add(separate);
        var scroll=new ScrollViewer {Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,2);main.Children.Add(scroll);
        var presets=new WrapPanel();foreach(var preset in ThemeSettings.Presets){var chosen=preset;var b=UI.Button(preset.Name,()=>{Draft.Take(chosen);Fill();RefreshPalette();});b.Background=UI.Brush(preset.Background);b.Foreground=UI.Brush(preset.Text);b.BorderBrush=UI.Brush(preset.Accent);b.Tag="keep-colors";b.MinWidth=144;presets.Children.Add(b);}controls.Children.Add(presets);
        void ColorRow(string key,string title,string value)
        {
            var row=new DockPanel {Margin=new Thickness(0,12,0,0)};row.Children.Add(UI.Text(title,13,UI.Muted));
            var box=new TextBox {Text=value,Width=130,Margin=new Thickness(20,0,8,0),MaxLength=7};fields[key]=box;row.Children.Add(box);
            row.Children.Add(UI.Button("색상 선택…",()=>{var color=new ColorWindow(ThemeSettings.ValidColor(box.Text)?box.Text:value,Draft.Apply(launcher)){Owner=this};if(color.ShowDialog()==true)box.Text=color.Result;}));controls.Children.Add(row);
            box.TextChanged+=(_,_)=>{if(filling || !ThemeSettings.ValidColor(box.Text))return;Assign(key,box.Text);RefreshPalette();};
        }
        ColorRow("Background","설정창 바탕",Draft.Background);ColorRow("Surface","패널 / 입력칸",Draft.Surface);ColorRow("Accent","포인트 / 강조",Draft.Accent);ColorRow("Text","글자",Draft.Text);
        var sample=UI.Res(new Border {CornerRadius=new CornerRadius(16),Padding=new Thickness(20),Margin=new Thickness(0,22,12,14)},Border.BackgroundProperty,"SkinPanel");var items=new StackPanel();items.Children.Add(UI.Heading("설정창 미리보기",20));items.Children.Add(new TextBox {Text="이곳에 앱 이름이나 메모를 입력해요.",Margin=new Thickness(0,10,0,10)});items.Children.Add(UI.IconButton("\uE73E","적용 버튼 미리보기",()=>{},primary:true));sample.Child=items;controls.Children.Add(sample);
        var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,15,0,0)};Grid.SetRow(footer,3);main.Children.Add(footer);var cancel=UI.Button("취소",()=>DialogResult=false);cancel.IsCancel=true;footer.Children.Add(cancel);var save=UI.IconButton("\uE73E","적용",Save,primary:true);save.IsDefault=true;footer.Children.Add(save);
        separate.Checked+=(_,_)=>{Draft.Separate=true;RefreshPalette();};separate.Unchecked+=(_,_)=>{Draft.Separate=false;RefreshPalette();};Loaded+=(_,_)=>Skin.Connect(this);RefreshPalette();
    }
    private void Assign(string key,string value) {switch(key){case "Background":Draft.Background=value;break;case "Surface":Draft.Surface=value;break;case "Accent":Draft.Accent=value;break;case "Text":Draft.Text=value;break;}}
    private void Fill() {filling=true;fields["Background"].Text=Draft.Background;fields["Surface"].Text=Draft.Surface;fields["Accent"].Text=Draft.Accent;fields["Text"].Text=Draft.Text;filling=false;}
    private void RefreshPalette() {controls.IsEnabled=Draft.Separate;Skin.Palette(this,Draft.Apply(launcher));}
    private void Save()
    {
        try {foreach(var pair in fields){if(!ThemeSettings.ValidColor(pair.Value.Text))throw new InvalidDataException("색상은 #RRGGBB 형식으로 입력해 주세요.");Assign(pair.Key,pair.Value.Text);}Draft.Initialized=true;Draft.Validate();Result=Draft.Copy();DialogResult=true;}catch(Exception ex){UI.Error(ex);}
    }
}
