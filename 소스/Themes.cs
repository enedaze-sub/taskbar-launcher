using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DeckLauncher;

public sealed class ThemeSettings
{
    public string Name { get; set; } = "민트 나이트";
    public string Background { get; set; } = "#111C2B";
    public string Surface { get; set; } = "#2A3B50";
    public string Accent { get; set; } = "#6DE0D4";
    public string Text { get; set; } = "#F0F5FC";
    public string Frame { get; set; } = "rounded";
    public int Slots { get; set; } = 7;
    public double IconSize { get; set; } = 40;
    public double Radius { get; set; } = 301;
    public double Gap { get; set; } = .7;
    public bool ShowLabels { get; set; } = true;
    public bool ShowNotes { get; set; } = true;
    public ThemeSettings Copy() => (ThemeSettings)MemberwiseClone();
    public static bool ValidColor(string value) => value != null && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$");
    public void Validate()
    {
        if (new[] { Background, Surface, Accent, Text }.Any(x => !ValidColor(x)) || Slots < 5 || Slots > 9 || !double.IsFinite(IconSize) || IconSize < 28 || IconSize > 48 || !double.IsFinite(Radius) || Radius < 280 || Radius > 315 || !double.IsFinite(Gap) || Gap < .3 || Gap > 2.5 || Frame is not ("rounded" or "circle" or "minimal")) throw new InvalidDataException("테마의 색상 또는 배치 값이 올바르지 않습니다.");
    }
    internal Brush B(string color) => UI.Brush(color);
    internal Brush Blend(string first, string second, double amount)
    {
        var a = (Color)ColorConverter.ConvertFromString(first); var b = (Color)ColorConverter.ConvertFromString(second);
        return new SolidColorBrush(Color.FromRgb((byte)(a.R + (b.R - a.R) * amount), (byte)(a.G + (b.G - a.G) * amount), (byte)(a.B + (b.B - a.B) * amount)));
    }
    public static IReadOnlyList<ThemeSettings> Presets => new[] {
        new ThemeSettings(),
        new ThemeSettings { Name="라벤더 문", Background="#242038", Surface="#403755", Accent="#C7ACF8", Text="#F6F0FF", Frame="circle" },
        new ThemeSettings { Name="딸기 밀크", Background="#F4E6EC", Surface="#EAD2DF", Accent="#AC3C6C", Text="#542A40", Frame="rounded", Gap=1.3 },
        new ThemeSettings { Name="바닐라 라테", Background="#EEE6D5", Surface="#DDD1B9", Accent="#80552B", Text="#443526", Frame="minimal", Slots=6, IconSize=42 },
        new ThemeSettings { Name="오션 블루", Background="#102C40", Surface="#23516B", Accent="#76D8FF", Text="#ECF8FF", Frame="circle", Gap=1.4 },
        new ThemeSettings { Name="네온 오빗", Background="#151523", Surface="#282737", Accent="#D9EE77", Text="#F4F5E9", Frame="minimal", Slots=8, IconSize=36, Gap=1.8 }
    };
}

public sealed class ThemeWindow : Window
{
    private readonly LauncherWindow preview;
    private readonly StackPanel controls = new();
    private readonly List<TextBox> colorBoxes = new();
    private readonly TextBlock status = UI.Text("", 12, UI.Muted);
    internal ThemeSettings Draft { get; private set; }
    public ThemeSettings? Result { get; private set; }
    public ThemeWindow(Store store, Library library)
    {
        Draft = library.Theme.Copy(); UI.Theme(this); Title="테마와 배치 꾸미기"; Width=1140; Height=780; MinWidth=930; MinHeight=620; ShowInTaskbar=false; Topmost=false; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Width=Math.Min(Width,SystemParameters.WorkArea.Width-24); Height=Math.Min(Height,SystemParameters.WorkArea.Height-30);
        var main=new Grid { Margin=new Thickness(24) }; main.RowDefinitions.Add(new(){Height=GridLength.Auto}); main.RowDefinitions.Add(new(){Height=GridLength.Auto}); main.RowDefinitions.Add(new()); main.RowDefinitions.Add(new(){Height=GridLength.Auto}); Content=main;
        var heading=UI.Text("취향을 담은 작은 다이얼",28); heading.FontFamily=UI.DisplayFont; heading.Margin=new Thickness(0,0,0,6); main.Children.Add(heading);
        var presets=new UniformGrid { Columns=6, Margin=new Thickness(0,12,0,20) }; Grid.SetRow(presets,1); main.Children.Add(presets);
        foreach(var preset in ThemeSettings.Presets)
        {
            var content=new StackPanel { Margin=new Thickness(2,8,2,8) };
            var dots=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center };
            foreach(var color in new[]{preset.Background,preset.Surface,preset.Accent}) dots.Children.Add(new System.Windows.Shapes.Ellipse { Width=23,Height=23,Fill=UI.Brush(color),Stroke=UI.Brush(preset.Text),StrokeThickness=.5,Margin=new Thickness(3) });
            content.Children.Add(dots); var text=UI.Text(preset.Name,13,UI.Brush(preset.Text)); text.TextAlignment=TextAlignment.Center; text.Margin=new Thickness(0,9,0,0); content.Children.Add(text);
            var tile=UI.Button("",()=>UsePreset(preset)); tile.Background=UI.Brush(preset.Background); tile.Content=content; tile.ToolTip=preset.Name; presets.Children.Add(tile);
        }
        var body=new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new(){Width=new GridLength(300)}); Grid.SetRow(body,2); main.Children.Add(body);
        var sampleEntries=library.Entries.Count==0 ? new List<Entry>{new(){Name="내 폴더",IsFolder=true},new(){Name="바로가기"},new(){Name="메모"}} : library.Entries;
        preview=new LauncherWindow(store,new Library { Entries=sampleEntries,Theme=Draft,Background=library.Background.Copy() },true);
        var sample=new Grid { Margin=new Thickness(0,0,20,0) }; sample.RowDefinitions.Add(new()); sample.RowDefinitions.Add(new(){Height=GridLength.Auto});
        sample.Children.Add(new Border { Background=UI.Brush("#243041"),CornerRadius=new CornerRadius(18),Padding=new Thickness(8),Child=preview.DetachPreview() });
        var hint=UI.Text("실제 런처 미리보기 · 색상과 배치를 바꾸면 바로 반영돼요",12,UI.Muted); hint.Margin=new Thickness(10,16,0,8); Grid.SetRow(hint,1); sample.Children.Add(hint); body.Children.Add(sample);
        var scroll=new ScrollViewer { Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; Grid.SetColumn(scroll,1); body.Children.Add(scroll); BuildControls();
        var footer=new DockPanel { Margin=new Thickness(0,18,0,0) }; Grid.SetRow(footer,3); main.Children.Add(footer);
        var actions=new StackPanel { Orientation=Orientation.Horizontal }; DockPanel.SetDock(actions,Dock.Right); actions.Children.Add(UI.Button("취소",()=>DialogResult=false)); actions.Children.Add(UI.Button("적용",()=>{ try { if(colorBoxes.Any(b=>!ThemeSettings.ValidColor(b.Text))){status.Text="색상을 #RRGGBB 형식으로 입력해 주세요.";return;} Draft.Validate(); Result=Draft.Copy(); DialogResult=true; } catch(Exception ex){UI.Error(ex);} })); footer.Children.Add(actions); footer.Children.Add(status); Update();
    }
    internal void UsePreset(ThemeSettings settings) { Draft=settings.Copy(); BuildControls(); Update(); }
    private void Update() { preview.UpdateTheme(Draft); status.Text=Draft.Name+" · "+Draft.Slots+"칸"; }
    private void Changed() { Draft.Name="내 커스텀 테마"; Update(); }
    private void BuildControls()
    {
        controls.Children.Clear(); colorBoxes.Clear(); var label=UI.Text("색상과 디스플레이",19); label.FontFamily=UI.DisplayFont; controls.Children.Add(label);
        void ColorRow(string name,string value,Action<string> assign)
        {
            controls.Children.Add(new TextBlock { Text=name,Foreground=UI.Muted,FontSize=12,Margin=new Thickness(0,15,0,5) });
            var row=new DockPanel(); var box=new TextBox { Text=value,MaxLength=7,ToolTip="#RRGGBB 형식의 색상" }; var pick=UI.Button("●",()=>{var dialog=new ColorWindow(ThemeSettings.ValidColor(box.Text)?box.Text:value){Owner=this};if(dialog.ShowDialog()==true)box.Text=dialog.Result;}); pick.Foreground=UI.Brush(value); pick.Padding=new Thickness(13,7,13,7); DockPanel.SetDock(pick,Dock.Right); row.Children.Add(pick); row.Children.Add(box); controls.Children.Add(row); colorBoxes.Add(box);
            box.TextChanged+=(_,_)=>{ if(!ThemeSettings.ValidColor(box.Text)){box.BorderBrush=UI.Brush("#EF8EAB");return;} box.BorderBrush=UI.Brush("#3B526D");pick.Foreground=UI.Brush(box.Text);assign(box.Text);Changed();};
        }
        ColorRow("런처 바탕",Draft.Background,x=>Draft.Background=x); ColorRow("칸 / 아이콘 바탕",Draft.Surface,x=>Draft.Surface=x); ColorRow("포인트 / 강조",Draft.Accent,x=>Draft.Accent=x); ColorRow("글자",Draft.Text,x=>Draft.Text=x);
        controls.Children.Add(new TextBlock { Text="아이콘 모양",Foreground=UI.Muted,Margin=new Thickness(0,18,0,6) });
        var shape=new ComboBox { ItemsSource=new[]{"둥근 사각형","동그라미","미니멀"},SelectedIndex=Draft.Frame=="circle"?1:Draft.Frame=="minimal"?2:0 }; controls.Children.Add(shape); shape.SelectionChanged+=(_,_)=>{Draft.Frame=new[]{"rounded","circle","minimal"}[shape.SelectedIndex];Changed();};
        void Slider(string name,double min,double max,double value,Action<double> assign,bool integral=false)
        {
            var text=UI.Text(name+"  "+value.ToString("0.#"),12,UI.Muted);text.Margin=new Thickness(0,18,0,6);controls.Children.Add(text);
            var slider=new System.Windows.Controls.Slider { Minimum=min,Maximum=max,Value=value,TickFrequency=integral?1:.1,IsSnapToTickEnabled=integral,Margin=new Thickness(0,4,10,0) }; controls.Children.Add(slider); slider.ValueChanged+=(_,_)=>{assign(slider.Value);text.Text=name+"  "+slider.Value.ToString("0.#");Changed();};
        }
        Slider("한 번에 보이는 칸",5,9,Draft.Slots,x=>Draft.Slots=(int)x,true); Slider("아이콘 크기",28,48,Draft.IconSize,x=>Draft.IconSize=x,true); Slider("아이콘의 바깥쪽 위치",280,315,Draft.Radius,x=>Draft.Radius=x,true); Slider("칸 사이 여백",.3,2.5,Draft.Gap,x=>Draft.Gap=x);
        var labels=new CheckBox { Content="아이콘 이름 표시",IsChecked=Draft.ShowLabels };controls.Children.Add(labels);labels.Click+=(_,_)=>{Draft.ShowLabels=labels.IsChecked==true;Changed();};
        var notes=new CheckBox { Content="중앙 메모 표시",IsChecked=Draft.ShowNotes };controls.Children.Add(notes);notes.Click+=(_,_)=>{Draft.ShowNotes=notes.IsChecked==true;Changed();};
    }
}

internal sealed class ColorWindow : Window
{
    internal string Result {get;private set;}
    public ColorWindow(string value)
    {
        Result=value; UI.Theme(this);Title="색상 선택";Width=390;Height=390;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel {Margin=new Thickness(24)};Content=panel;
        var swatch=new Border {Height=75,CornerRadius=new CornerRadius(14),Background=UI.Brush(value)};panel.Children.Add(swatch);var code=UI.Text(value,18);code.Margin=new Thickness(0,12,0,8);panel.Children.Add(code);
        var color=(Color)ColorConverter.ConvertFromString(value);var channels=new List<Slider>();
        foreach(var pair in new[]{("빨강",color.R),("초록",color.G),("파랑",color.B)}) {var row=new DockPanel();row.Children.Add(UI.Text(pair.Item1,12,UI.Muted));var slider=new Slider {Minimum=0,Maximum=255,Value=pair.Item2,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(20,8,0,8)};row.Children.Add(slider);panel.Children.Add(row);channels.Add(slider);}
        foreach(var slider in channels) slider.ValueChanged+=(_,_)=>{Result=$"#{(int)channels[0].Value:X2}{(int)channels[1].Value:X2}{(int)channels[2].Value:X2}";swatch.Background=UI.Brush(Result);code.Text=Result;};
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};buttons.Children.Add(UI.Button("취소",()=>DialogResult=false));buttons.Children.Add(UI.Button("선택",()=>DialogResult=true));panel.Children.Add(buttons);
    }
}
