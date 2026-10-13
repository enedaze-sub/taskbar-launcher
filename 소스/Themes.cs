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
    public string Design { get; set; } = "classic";
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
        if (new[] { Background, Surface, Accent, Text }.Any(x => !ValidColor(x)) || Slots < 5 || Slots > 9 || !double.IsFinite(IconSize) || IconSize < 28 || IconSize > 48 || !double.IsFinite(Radius) || Radius < 280 || Radius > 315 || !double.IsFinite(Gap) || Gap < .3 || Gap > 2.5 || Frame is not ("rounded" or "circle" or "minimal") || Design is not ("classic" or "modern" or "cat" or "neon" or "glass" or "paper" or "space")) throw new InvalidDataException("테마의 색상 또는 배치 값이 올바르지 않습니다.");
    }
    internal Brush B(string color) => UI.Brush(color);
    internal Brush Blend(string first, string second, double amount)
    {
        var a = (Color)ColorConverter.ConvertFromString(first); var b = (Color)ColorConverter.ConvertFromString(second);
        return new SolidColorBrush(Color.FromRgb((byte)(a.R + (b.R - a.R) * amount), (byte)(a.G + (b.G - a.G) * amount), (byte)(a.B + (b.B - a.B) * amount)));
    }
    /// <summary>The six launcher designs, each with its own recommended palette and layout values.</summary>
    public static IReadOnlyList<ThemeSettings> DesignPresets => new[] {
        new ThemeSettings {Name="기본 부채꼴",Design="classic"},
        new ThemeSettings {Name="냥냥 포켓",Design="cat",Background="#FFF5EC",Surface="#FFE3D3",Accent="#F2879B",Text="#6A4A44",Frame="circle",Slots=6},
        new ThemeSettings {Name="사이버 HUD",Design="neon",Background="#0A0F1F",Surface="#16223D",Accent="#35F0FF",Text="#E8FBFF",Frame="circle"},
        new ThemeSettings {Name="다이어리 스크랩",Design="paper",Background="#F3ECDA",Surface="#FFFDF5",Accent="#E0786A",Text="#4A3B31",Slots=8,IconSize=36},
        new ThemeSettings {Name="글래스 도크",Design="modern",Background="#E7EEF8",Surface="#FFFFFF",Accent="#4F74F7",Text="#1E2940",Slots=7,IconSize=40},
        new ThemeSettings {Name="별빛 궤도",Design="space",Background="#100C2A",Surface="#2B2360",Accent="#FFD36E",Text="#F4EFFF",Frame="circle"}
    };
    public static IReadOnlyList<ThemeSettings> Presets => new[] {
        new ThemeSettings(),
        new ThemeSettings {Name="라벤더 문",Background="#242038",Surface="#403755",Accent="#C7ACF8",Text="#F6F0FF",Frame="circle"},
        new ThemeSettings {Name="딸기 밀크",Background="#F4E6EC",Surface="#EAD2DF",Accent="#AC3C6C",Text="#542A40",Gap=1.3},
        new ThemeSettings {Name="바닐라 라테",Background="#EEE6D5",Surface="#DDD1B9",Accent="#80552B",Text="#443526",Frame="minimal",Slots=6,IconSize=42},
        new ThemeSettings {Name="네온 오빗",Background="#151523",Surface="#282737",Accent="#D9EE77",Text="#F4F5E9",Frame="minimal",Slots=8,IconSize=36},
        new ThemeSettings {Name="클라우드 화이트",Background="#E7EEF8",Surface="#FFFFFF",Accent="#4F74F7",Text="#1E2940"},
        new ThemeSettings {Name="피치 크림",Background="#FFF5EC",Surface="#FFE3D3",Accent="#F2879B",Text="#6A4A44"},
        new ThemeSettings {Name="네온 민트",Background="#0A0F1F",Surface="#16223D",Accent="#35F0FF",Text="#E8FBFF"},
        new ThemeSettings {Name="크림 페이퍼",Background="#F3ECDA",Surface="#FFFDF5",Accent="#E0786A",Text="#4A3B31"},
        new ThemeSettings {Name="별빛 보라",Background="#100C2A",Surface="#2B2360",Accent="#FFD36E",Text="#F4EFFF"}
    };
}

public sealed class SavedTheme
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "나의 테마";
    public ThemeSettings Settings { get; set; } = new();
    public SavedTheme Copy() => new() { Id=Id,Name=Name,Settings=Settings.Copy() };
    public override string ToString() => Name;
}

internal sealed class ColorWindow : Window
{
    internal string Result {get;private set;}
    public ColorWindow(string value, ThemeSettings? theme=null)
    {
        Result=value; UI.Theme(this,theme);Title="색상 선택";Width=390;Height=390;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel {Margin=new Thickness(24)};Content=panel;
        var swatch=new Border {Height=75,CornerRadius=new CornerRadius(14),Background=UI.Brush(value)};panel.Children.Add(swatch);var code=UI.Text(value,18);code.Margin=new Thickness(0,12,0,8);panel.Children.Add(code);
        var color=(Color)ColorConverter.ConvertFromString(value);var channels=new List<Slider>();
        foreach(var pair in new[]{("빨강",color.R),("초록",color.G),("파랑",color.B)}) {var row=new DockPanel();row.Children.Add(UI.Text(pair.Item1,12,UI.Muted));var slider=new Slider {Minimum=0,Maximum=255,Value=pair.Item2,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(20,8,0,8)};row.Children.Add(slider);panel.Children.Add(row);channels.Add(slider);}
        foreach(var slider in channels) slider.ValueChanged+=(_,_)=>{Result=$"#{(int)channels[0].Value:X2}{(int)channels[1].Value:X2}{(int)channels[2].Value:X2}";swatch.Background=UI.Brush(Result);code.Text=Result;};
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};buttons.Children.Add(UI.Button("취소",()=>DialogResult=false));buttons.Children.Add(UI.Button("선택",()=>DialogResult=true));panel.Children.Add(buttons);
    }
}
