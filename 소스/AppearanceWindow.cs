using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
namespace DeckLauncher;
public sealed class AppearanceWindow : Window
{
    private readonly Library snapshot;
    private readonly LauncherWindow preview;
    private readonly TextBox titleBox,noteBox;
    internal AppearanceSettings Draft=>snapshot.Appearance;
    internal LauncherWindow Preview=>preview;
    public AppearanceSettings? Result {get;private set;}
    public AppearanceWindow(Store store,Library library)
    {
        snapshot=JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(library))!;
        UI.Theme(this,library.InterfaceTheme);Title="중앙 문구 수정";Width=1000;Height=670;MinWidth=740;MinHeight=550;ShowInTaskbar=false;Topmost=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Height=System.Math.Min(Height,SystemParameters.WorkArea.Height-30);Width=System.Math.Min(Width,SystemParameters.WorkArea.Width-24);
        var main=new Grid {Margin=new Thickness(24)};main.RowDefinitions.Add(new(){Height=GridLength.Auto});main.RowDefinitions.Add(new());main.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=main;
        var controls=new StackPanel();controls.Children.Add(UI.Heading("나만의 제목과 안내 문구",25));controls.Children.Add(UI.Text("아이콘을 가리키면 개별 메모, 떼면 작성한 문구를 표시합니다. 폴더 안에서는 폴더 이름을 표시합니다.",13,UI.Muted));
        controls.Children.Add(UI.Text("제목 · 최대 60자",12.5,UI.Muted));titleBox=new TextBox {Text=Draft.Title,MaxLength=60,Margin=new Thickness(0,5,0,12)};controls.Children.Add(titleBox);
        controls.Children.Add(UI.Text("중앙 안내 문구 · 최대 300자",12.5,UI.Muted));noteBox=new TextBox {Text=Draft.Note,MaxLength=300,Height=70,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top};controls.Children.Add(noteBox);main.Children.Add(controls);
        preview=new LauncherWindow(store,snapshot,true);var box=new Border {Child=preview.DetachPreview(this),Margin=new Thickness(0,14,0,14)};Grid.SetRow(box,1);main.Children.Add(box);
        titleBox.TextChanged+=(_,_)=>RefreshPreview();noteBox.TextChanged+=(_,_)=>RefreshPreview();
        var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(footer,2);main.Children.Add(footer);
        footer.Children.Add(UI.Button("기본 문구",()=>{titleBox.Text="나의 바로가기";noteBox.Text=AppearanceSettings.DefaultNote;}));var cancel=UI.Button("취소",()=>DialogResult=false);cancel.IsCancel=true;footer.Children.Add(cancel);var save=UI.Button("적용",()=>{RefreshPreview();Draft.Validate();Result=Draft.Copy();DialogResult=true;});save.IsDefault=true;UI.Res(save,Control.BackgroundProperty,"SkinAccent");UI.Res(save,Control.ForegroundProperty,"SkinAccentText");footer.Children.Add(save);
    }
    internal void RefreshPreview(){Draft.Title=titleBox.Text;Draft.Note=noteBox.Text;preview.UpdateAppearance(Draft);}
    public void ApplyTo(Library library){if(Result!=null)library.Appearance=Result.Copy();}
}
