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
    private readonly string? entryId;
    private readonly Rect cropBounds;
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

    public BackgroundWindow(Store store, Library library, Entry? entry = null)
    {
        this.store = store; entryId = entry?.Id; draft = (entry?.Background ?? library.Background).Copy(); UI.Theme(this,library.Theme);
        Title = entry == null ? "런처 배경 꾸미기" : entry.Name + " · 칸 배경 꾸미기"; Width = 1020; Height = 790; MinWidth = 740; MinHeight = 680; Topmost = false; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var area = SystemParameters.WorkArea; Width = Math.Min(Width, area.Width - 24); Height = Math.Min(Height, area.Height - 30);
        var main = new Grid { Margin = new Thickness(24) }; main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new()); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = main;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var commands = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(commands, Dock.Right);
        commands.Children.Add(UI.Button("이미지 선택…", Choose)); commands.Children.Add(UI.Button("기본 배경", ResetImage)); header.Children.Add(commands);
        var labels = new StackPanel(); var title = UI.Heading(entry == null ? "런처 배경 꾸미기" : "이 칸의 배경 꾸미기", 25); labels.Children.Add(title); var dragHint = UI.Text("미리보기의 이미지를 끌어 위치를 맞추고, 휠로 확대해 보세요.", 13.5, UI.Muted); dragHint.Margin = new Thickness(0, 5, 0, 0); labels.Children.Add(dragHint); header.Children.Add(labels); main.Children.Add(header);
        var copy = System.Text.Json.JsonSerializer.Deserialize<Library>(System.Text.Json.JsonSerializer.Serialize(library))!;
        if (entry == null) copy.Background = draft; else copy.Find(entry.Id)!.Background = draft;
        preview = new LauncherWindow(store, copy, true);
        if (entry != null) preview.PreviewEntry(entry);
        cropBounds = entry != null ? preview.SlotGeometry(entry.Id)?.Bounds ?? BackgroundTools.Bounds : preview.ShellGeometry().Bounds;
        previewArea = new Border { Background = UI.Brush("#233245"), BorderBrush = UI.Brush("#42546C"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(12, 0, 12, 0), Child = preview.DetachPreview(), Cursor = Cursors.SizeAll };
        Grid.SetRow(previewArea, 1); main.Children.Add(previewArea);
        var controls = new Grid { Margin = new Thickness(0, 14, 0, 0) }; controls.ColumnDefinitions.Add(new()); controls.ColumnDefinitions.Add(new()); controls.RowDefinitions.Add(new()); controls.RowDefinitions.Add(new()); Grid.SetRow(controls, 2);
        AddSlider(controls, "확대 / 축소", zoom, 0, 0); AddSlider(controls, "배경 어둡게", darkness, 1, 0); AddSlider(controls, "가로 위치", horizontal, 0, 1); AddSlider(controls, "세로 위치", vertical, 1, 1); main.Children.Add(controls);
        zoom.Value = draft.Zoom; horizontal.Value = draft.OffsetX; vertical.Value = draft.OffsetY; darkness.Value = draft.Darkness;
        foreach (var slider in new[] { zoom, horizontal, vertical, darkness }) slider.ValueChanged += (_, _) => { draft.Zoom = zoom.Value; draft.OffsetX = horizontal.Value; draft.OffsetY = vertical.Value; draft.Darkness = darkness.Value; Update(); };
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
        image = (BitmapSource)Native.LoadImage(path, 1920); selectedPath = path; newlySelected = newImage;
        preview.UpdateBackground(draft, path, entryId); Update();
    }
    private void Choose()
    {
        var dialog = new OpenFileDialog { Title = "런처 배경 이미지 선택", Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dialog.ShowDialog(this) != true) return;
        try { SelectImage(dialog.FileName); ResetPosition(); } catch (Exception ex) { UI.Error(ex); }
    }
    private void ResetPosition() { zoom.Value = 1; horizontal.Value = vertical.Value = 0; Update(); }
    private void ResetImage() { selectedPath = null; newlySelected = false; image = null; draft.ImageFile = null; ResetPosition(); Update(); }
    private void Update()
    {
        preview.UpdateBackground(draft, selectedPath, entryId);
        status.Text = image == null ? "기본 배경 · 이미지가 아직 선택되지 않았어요" : $"{Path.GetFileName(selectedPath)}   ·   {draft.Zoom:0.00}배";
        zoom.IsEnabled = horizontal.IsEnabled = vertical.IsEnabled = darkness.IsEnabled = image != null;
    }
    private void Save()
    {
        try { Result = draft.Copy(); if (newlySelected && selectedPath != null) Result.ImageFile = store.CopyIcon(selectedPath); Result.Validate(); DialogResult = true; }
        catch (Exception ex) { UI.Error(ex); }
    }
}
