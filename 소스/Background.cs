using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DeckLauncher;

public sealed class BackgroundSettings
{
    public string? ImageFile { get; set; }
    public double Zoom { get; set; } = 1;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double Darkness { get; set; } = .42;
    public BackgroundSettings Copy() => new() { ImageFile = ImageFile, Zoom = Zoom, OffsetX = OffsetX, OffsetY = OffsetY, Darkness = Darkness };
    public void Validate()
    {
        if (ImageFile != null && (Path.GetFileName(ImageFile) != ImageFile || ImageFile.Contains(':'))) throw new InvalidDataException("잘못된 배경 이미지 경로입니다.");
        if (!double.IsFinite(Zoom) || Zoom < 1 || Zoom > 4 || !double.IsFinite(OffsetX) || Math.Abs(OffsetX) > 1 || !double.IsFinite(OffsetY) || Math.Abs(OffsetY) > 1 || !double.IsFinite(Darkness) || Darkness < 0 || Darkness > .85) throw new InvalidDataException("배경 이미지 조정 값이 올바르지 않습니다.");
    }
}

internal static class BackgroundTools
{
    internal static readonly Rect Bounds = new(70, 63, 780, 390);
    public static Rect ImageRect(double pixelWidth, double pixelHeight, BackgroundSettings settings) => ImageRect(pixelWidth, pixelHeight, settings, Bounds);
    public static Rect ImageRect(double pixelWidth, double pixelHeight, BackgroundSettings settings, Rect bounds)
    {
        double scale = Math.Max(bounds.Width / pixelWidth, bounds.Height / pixelHeight) * settings.Zoom;
        double width = pixelWidth * scale, height = pixelHeight * scale;
        return new Rect(bounds.X + (bounds.Width - width) / 2 + settings.OffsetX * (width - bounds.Width) / 2, bounds.Y + (bounds.Height - height) / 2 + settings.OffsetY * (height - bounds.Height) / 2, width, height);
    }
    public static Canvas Layer(BitmapSource image, BackgroundSettings settings, Geometry? clip = null)
    {
        var geometry = clip ?? LauncherWindow.Sector(.01, 390, -180, 0);
        var rect = ImageRect(image.PixelWidth, image.PixelHeight, settings, clip?.Bounds ?? Bounds);
        var layer = new Canvas { Width = 920, Height = 454, Clip = geometry, IsHitTestVisible = false };
        var visual = new Image { Source = image, Width = rect.Width, Height = rect.Height, Stretch = Stretch.Fill };
        Canvas.SetLeft(visual, rect.X); Canvas.SetTop(visual, rect.Y); layer.Children.Add(visual);
        layer.Children.Add(new System.Windows.Shapes.Rectangle { Width = 920, Height = 454, Fill = UI.Brush("#071421"), Opacity = settings.Darkness });
        return layer;
    }
}

public sealed class BackgroundWindow : Window
{
    private readonly Store store;
    private string? entryId;
    private readonly bool bulk;
    private readonly string? parentId;
    private readonly Library sourceLibrary;
    private readonly ComboBox scope = new() { Width=200 };
    private readonly CheckBox applyImage = new() { Content="배경 이미지·위치" };
    private readonly CheckBox applyOpacity = new() { Content="투명도", IsChecked=true };
    private readonly CheckBox separate = new() {Content="배경 · 아이콘 · 글자 투명도를 따로 조절"};
    private readonly Slider iconOpacity = new() {Tag="icon-opacity",Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true};
    private readonly Slider textOpacity = new() {Tag="text-opacity",Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true};
    private readonly TextBlock detailLabel=UI.Text("",12,UI.Muted);
    private readonly Slider opacity = new() { Minimum=-100,Maximum=85,TickFrequency=1,IsSnapToTickEnabled=true };
    private readonly TextBlock opacityLabel = UI.Text("",12,UI.Muted);
    private Rect cropBounds;
    private readonly BackgroundSettings draft;
    private readonly LauncherWindow preview;
    private readonly Border previewArea;
    private readonly Slider zoom = new() { Minimum = 1, Maximum = 4, TickFrequency = .05 };
    private readonly Slider horizontal = new() { Minimum = -1, Maximum = 1, TickFrequency = .05 };
    private readonly Slider vertical = new() { Minimum = -1, Maximum = 1, TickFrequency = .05 };
    private readonly Slider darkness = new() { Minimum = 0, Maximum = .85, TickFrequency = .05 };
    private readonly TextBlock status = UI.Text("", 12, UI.Muted);
    private string? selectedPath;
    private bool newlySelected;
    private Point? dragStart;
    private double initialX, initialY;
    private BitmapSource? image;
    public BackgroundSettings? Result { get; private set; }
    internal BackgroundSettings Draft => draft;
    public double ResultOpacity => Math.Round(1-opacity.Value/100,2);
    public bool ResultSeparateOpacity => separate.IsChecked==true;
    public double ResultIconOpacity => Math.Round(1-iconOpacity.Value/100,2);
    public double ResultTextOpacity => Math.Round(1-textOpacity.Value/100,2);
    public void ApplyTo(Entry entry) {entry.Background=Result!.Copy();entry.Opacity=ResultOpacity;entry.SeparateOpacity=ResultSeparateOpacity;entry.IconOpacity=ResultIconOpacity;entry.TextOpacity=ResultTextOpacity;}
    internal List<string> TargetIds => sourceLibrary.BackgroundTargets(parentId,(scope.SelectedItem as ComboBoxItem)?.Tag as string ?? "all").Select(e=>e.Id).ToList();
    public int ApplyBulkTo(Library library) => library.ApplyBackgrounds(TargetIds,applyImage.IsChecked==true?Result:null,applyOpacity.IsChecked==true?ResultOpacity:null,applyOpacity.IsChecked==true?ResultSeparateOpacity:null,applyOpacity.IsChecked==true?ResultIconOpacity:null,applyOpacity.IsChecked==true?ResultTextOpacity:null);

    public BackgroundWindow(Store store, Library library, Entry? entry = null, bool bulk = false, string? parentId = null)
    {
        this.store = store;this.bulk=bulk;this.parentId=parentId;sourceLibrary=System.Text.Json.JsonSerializer.Deserialize<Library>(System.Text.Json.JsonSerializer.Serialize(library))!;if(bulk)entry=sourceLibrary.Entries.FirstOrDefault(); entryId = entry?.Id; draft = (entry?.Background ?? library.Background).Copy(); UI.Theme(this,library.Theme);
        Title = bulk ? "칸 배경 일괄 설정" : entry == null ? "런처 배경 꾸미기" : entry.Name + " · 칸 배경 꾸미기"; Width = 1020; Height = bulk ? 980 : entry==null ? 810 : 950; MinWidth = 740; MinHeight = 680; Topmost = false; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var area = SystemParameters.WorkArea; Width = Math.Min(Width, area.Width - 24); Height = Math.Min(Height, area.Height - 30);
        var main = new Grid { Margin = new Thickness(24) }; main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new()); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = main;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var commands = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(commands, Dock.Right);
        commands.Children.Add(UI.Button("이미지 선택…", Choose)); commands.Children.Add(UI.Button("기본 배경", ResetImage)); header.Children.Add(commands);
        var labels = new StackPanel(); var title = UI.Heading(bulk ? "칸 배경 일괄 설정" : entry == null ? "런처 배경 꾸미기" : "이 칸의 배경 꾸미기", 25); labels.Children.Add(title); var dragHint = UI.Text("미리보기의 이미지를 끌어 위치를 맞추고, 휠로 확대해 보세요.", 13.5, UI.Muted); dragHint.Margin = new Thickness(0, 5, 0, 0); labels.Children.Add(dragHint);
        if(bulk)
        {
            var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};
            foreach(var pair in new[]{("전체 칸","all"),("현재 폴더의 칸","level"),("현재 폴더와 하위 폴더","tree")})scope.Items.Add(new ComboBoxItem {Content=pair.Item1,Tag=pair.Item2});scope.SelectedIndex=0;
            applyImage.Content="배경 이미지·위치";applyImage.Margin=new Thickness(12,0,12,0);row.Children.Add(scope);row.Children.Add(applyImage);row.Children.Add(applyOpacity);labels.Children.Add(row);
        }
        header.Children.Add(labels); main.Children.Add(header);
        var copy = System.Text.Json.JsonSerializer.Deserialize<Library>(System.Text.Json.JsonSerializer.Serialize(library))!;
        if (entry == null) copy.Background = draft; else copy.Find(entry.Id)!.Background = draft;
        preview = new LauncherWindow(store, copy, true);
        if (entry != null) preview.PreviewEntry(entry);
        cropBounds = entry != null ? preview.SlotGeometry(entry.Id)?.Bounds ?? BackgroundTools.Bounds : preview.ShellGeometry().Bounds;
        previewArea = new Border { Background = UI.Brush("#233245"), BorderBrush = UI.Brush("#42546C"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(12, 0, 12, 0), Child = preview.DetachPreview(this), Cursor = Cursors.SizeAll };
        Grid.SetRow(previewArea, 1); main.Children.Add(previewArea);
        var controls = new Grid { Margin = new Thickness(0, 14, 0, 0) }; controls.ColumnDefinitions.Add(new()); controls.ColumnDefinitions.Add(new()); controls.RowDefinitions.Add(new()); controls.RowDefinitions.Add(new());controls.RowDefinitions.Add(new());controls.RowDefinitions.Add(new(){Height=GridLength.Auto});controls.RowDefinitions.Add(new(){Height=GridLength.Auto}); Grid.SetRow(controls, 2);
        AddSlider(controls, "확대 / 축소", zoom, 0, 0); AddSlider(controls, "배경 어둡게", darkness, 1, 0); AddSlider(controls, "가로 위치", horizontal, 0, 1); AddSlider(controls, "세로 위치", vertical, 1, 1);
        if(entry==null && !bulk){opacity.Minimum=0;opacity.Maximum=80;}opacity.Value=Math.Round((1-(entry?.Opacity ?? library.Appearance.Opacity))*100);AddSlider(controls,entry!=null || bulk?"칸의 투명도 · 음수는 바탕 강화":"런처 전체 투명도",opacity,0,2);Grid.SetColumn(opacityLabel,1);Grid.SetRow(opacityLabel,2);controls.Children.Add(opacityLabel);
        if(entry!=null || bulk)
        {
            separate.IsChecked=entry?.SeparateOpacity ?? false;iconOpacity.Value=Math.Round((1-(entry?.IconOpacity ?? 1))*100);textOpacity.Value=Math.Round((1-(entry?.TextOpacity ?? 1))*100);
            var mode=new StackPanel();mode.Children.Add(separate);mode.Children.Add(detailLabel);Grid.SetRow(mode,3);Grid.SetColumnSpan(mode,2);controls.Children.Add(mode);
            AddSlider(controls,"아이콘 투명도",iconOpacity,0,4);AddSlider(controls,"글자 투명도",textOpacity,1,4);
            separate.Checked+=(_,_)=>Update();separate.Unchecked+=(_,_)=>Update();iconOpacity.ValueChanged+=(_,_)=>Update();textOpacity.ValueChanged+=(_,_)=>Update();
        }
        main.Children.Add(controls);
        zoom.Value = draft.Zoom; horizontal.Value = draft.OffsetX; vertical.Value = draft.OffsetY; darkness.Value = draft.Darkness;
        foreach (var slider in new[] { zoom, horizontal, vertical, darkness }) slider.ValueChanged += (_, _) => { draft.Zoom = zoom.Value; draft.OffsetX = horizontal.Value; draft.OffsetY = vertical.Value; draft.Darkness = darkness.Value;if(bulk)applyImage.IsChecked=true; Update(); };
        opacity.ValueChanged+=(_,_)=>Update();if(bulk){scope.SelectionChanged+=(_,_)=>Update();applyImage.Checked+=(_,_)=>Update();applyImage.Unchecked+=(_,_)=>Update();applyOpacity.Checked+=(_,_)=>Update();applyOpacity.Unchecked+=(_,_)=>Update();}
        var footer = new DockPanel { Margin = new Thickness(0, 17, 0, 0) }; Grid.SetRow(footer, 3);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(buttons, Dock.Right);
        buttons.Children.Add(UI.Button("위치 초기화", ResetPosition)); var cancel = UI.Button("취소", () => DialogResult = false); cancel.IsCancel = true; buttons.Children.Add(cancel);
        var save = UI.Button("적용", Save); save.MinWidth = 84; save.FontWeight = FontWeights.SemiBold; UI.Res(save, Control.BackgroundProperty, "SkinAccent"); UI.Res(save, Control.ForegroundProperty, "SkinAccentText"); save.IsDefault = true; buttons.Children.Add(save); footer.Children.Add(buttons); status.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(status); main.Children.Add(footer);
        previewArea.MouseLeftButtonDown += (_, e) => { if (image == null) return; dragStart = e.GetPosition(previewArea); initialX = draft.OffsetX; initialY = draft.OffsetY; previewArea.CaptureMouse(); e.Handled = true; };
        previewArea.MouseMove += (_, e) =>
        {
            if (dragStart == null || image == null || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(previewArea); var rect = BackgroundTools.ImageRect(image.PixelWidth, image.PixelHeight, draft, cropBounds);
            double scale = Math.Min((previewArea.ActualWidth - 24) / 920, previewArea.ActualHeight / 454);
            double dx = (rect.Width - cropBounds.Width) / 2, dy = (rect.Height - cropBounds.Height) / 2;
            horizontal.Value = dx > .01 ? Math.Clamp(initialX + (point.X - dragStart.Value.X) / scale / dx, -1, 1) : 0;
            vertical.Value = dy > .01 ? Math.Clamp(initialY + (point.Y - dragStart.Value.Y) / scale / dy, -1, 1) : 0;
            e.Handled = true;
        };
        previewArea.MouseLeftButtonUp += (_, e) => { dragStart = null; previewArea.ReleaseMouseCapture(); e.Handled = true; };
        previewArea.LostMouseCapture += (_, _) => dragStart = null;
        previewArea.MouseWheel += (_, e) => { if (image != null) zoom.Value = Math.Clamp(zoom.Value + (e.Delta > 0 ? .1 : -.1), 1, 4); e.Handled = true; };
        if (draft.ImageFile != null)
        {
            try { SelectImage(store.IconPath(draft.ImageFile), false); } catch { status.Text = "저장된 이미지를 찾을 수 없어요. 다른 이미지를 선택해 주세요."; }
        }
        Update(); Loaded+=(_,_)=>Skin.Connect(this);
    }
    private static void AddSlider(Grid grid, string label, Slider slider, int column, int row)
    {
        var panel = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 18, 0, column == 0 ? 18 : 0, 10) };
        panel.Children.Add(UI.Text(label, 12, UI.Muted)); slider.Margin = new Thickness(0, 9, 0, 4); panel.Children.Add(slider); Grid.SetColumn(panel, column); Grid.SetRow(panel, row); grid.Children.Add(panel);
    }
    internal void SelectImage(string path, bool newImage = true)
    {
        image = (BitmapSource)Native.LoadImage(path, 1920); selectedPath = path; newlySelected = newImage;if(bulk && newImage)applyImage.IsChecked=true;
        preview.UpdateBackground(draft, path, entryId); Update();
    }
    private void Choose()
    {
        var dialog = new OpenFileDialog { Title = "런처 배경 이미지 선택", Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dialog.ShowDialog(this) != true) return;
        try { SelectImage(dialog.FileName); ResetPosition(); } catch (Exception ex) { UI.Error(ex); }
    }
    private void ResetPosition() { zoom.Value = 1; horizontal.Value = vertical.Value = 0; Update(); }
    private void ResetImage() { selectedPath = null; newlySelected = false; image = null; draft.ImageFile = null;if(bulk)applyImage.IsChecked=true; ResetPosition(); Update(); }
    private void Update()
    {
        iconOpacity.IsEnabled=textOpacity.IsEnabled=ResultSeparateOpacity;detailLabel.Text=$"아이콘 {iconOpacity.Value:0}% · 글자 {textOpacity.Value:0}%\n"+(ResultSeparateOpacity?"칸 슬라이더는 배경에만 적용됩니다.":"칸 전체를 함께 조절합니다. 별도 조절을 켜면 위 값을 사용합니다.");
        opacityLabel.Text=$"{opacity.Value:0}%"+(opacity.Value<0?" · 칸 바탕을 더 진하게":" · 높을수록 투명")+"\n전체 런처 투명도도 함께 적용됩니다.";
        if(bulk)
        {
            preview.ClearBackgroundOverrides();
            foreach(var e in preview.library.Entries){var original=sourceLibrary.Find(e.Id)!;e.Background=original.Background.Copy();e.Opacity=original.Opacity;e.SeparateOpacity=original.SeparateOpacity;e.IconOpacity=original.IconOpacity;e.TextOpacity=original.TextOpacity;}
            var ids=TargetIds;
            if(ids.Count>0){preview.library.ApplyBackgrounds(ids,applyImage.IsChecked==true?draft:null,applyOpacity.IsChecked==true?ResultOpacity:null,applyOpacity.IsChecked==true?ResultSeparateOpacity:null,applyOpacity.IsChecked==true?ResultIconOpacity:null,applyOpacity.IsChecked==true?ResultTextOpacity:null);entryId=ids[0];preview.PreviewEntry(preview.library.Find(entryId)!);cropBounds=preview.SlotGeometry(entryId)?.Bounds ?? BackgroundTools.Bounds;}
        }
        else if(entryId!=null){var e=preview.library.Find(entryId)!;e.Opacity=ResultOpacity;e.SeparateOpacity=ResultSeparateOpacity;e.IconOpacity=ResultIconOpacity;e.TextOpacity=ResultTextOpacity;}
        else preview.library.Appearance.Opacity=ResultOpacity;
        if(!bulk)preview.UpdateBackground(draft, selectedPath, entryId);
        else if(applyImage.IsChecked==true && TargetIds.Count>0)preview.UpdateBackgrounds(draft,selectedPath,TargetIds);
        else preview.UpdateAppearance(preview.library.Appearance);
        status.Text = image == null ? "기본 배경 · 이미지가 아직 선택되지 않았어요" : $"{Path.GetFileName(selectedPath)}   ·   {draft.Zoom:0.00}배";
        if(bulk)status.Text=$"{TargetIds.Count}개 칸에 적용 · "+status.Text;
        zoom.IsEnabled = horizontal.IsEnabled = vertical.IsEnabled = darkness.IsEnabled = image != null;
    }
    private void Save()
    {
        try { if(bulk && (TargetIds.Count==0 || applyImage.IsChecked!=true && applyOpacity.IsChecked!=true)){MessageBox.Show(this,"적용할 칸과 적용 항목을 선택해 주세요.");return;}Result = draft.Copy(); if (newlySelected && selectedPath != null && (!bulk || applyImage.IsChecked==true)) Result.ImageFile = store.CopyIcon(selectedPath); Result.Validate(); DialogResult = true; }
        catch (Exception ex) { UI.Error(ex); }
    }
}
