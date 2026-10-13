using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WpfPath = System.Windows.Shapes.Path;

namespace DeckLauncher;

/// <summary>A transparent, paginated semicircle. Management is a separate window.</summary>
public sealed class LauncherWindow : Window
{
    internal const int PageSize = 7;
    private readonly Store store;
    internal Library library;
    private string? current;
    private int page;
    private bool modal;
    private bool menuOpen;
    private readonly Canvas fan = new() { Width = 920, Height = 510 };
    private readonly Grid root = new();
    private readonly TextBlock title = UI.Text("", 23);
    private readonly TextBlock note = UI.Text("", 13, UI.Muted);
    private readonly TextBlock tag = UI.Text("", 10, UI.Accent);
    private readonly TextBlock pageLabel = UI.Text("", 11, UI.Muted);
    private readonly Dictionary<string, int> pages = new();
    private readonly Dictionary<string, ImageSource?> icons = new();
    private readonly Dictionary<string, Button> slots = new();
    private readonly ScaleTransform unfolding = new(1, 1);
    private static readonly Style SliceStyle = (Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><ContentPresenter /></ControlTemplate></Setter.Value></Setter><Setter Property='Cursor' Value='Hand'/></Style>");
    internal int RenderedSlots => slots.Count;
    internal int PageCount => Math.Max(1, (library.Children(current).Count() + PageSize - 1) / PageSize);
    internal int CurrentPage => page;
    internal string DisplayedNote => note.Text;

    public LauncherWindow(Store store, Library library)
    {
        this.store = store; this.library = library;
        UI.Theme(this); Title = "작업표시줄 런처";
        Width = 920; Height = 510; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        if (File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "launcher.ico"))) Icon = Native.LoadImage(System.IO.Path.Combine(AppContext.BaseDirectory, "launcher.ico"));
        var view = new Viewbox { Child = fan, Stretch = Stretch.Uniform }; root.Children.Add(view); Content = root;
        fan.RenderTransform = unfolding; fan.RenderTransformOrigin = new Point(.5, .89);
        SourceInitialized += (_, _) => Native.ConfigureTaskbar(this);
        Loaded += (_, _) => { if (ShowActivated) { PositionAtTaskbar(); AnimateOpen(); } };
        ContextMenuOpening += (_, _) => menuOpen = true;
        ContextMenuClosing += (_, _) => menuOpen = false;
        Deactivated += (_, _) => Dispatcher.BeginInvoke(new Action(() => { if (!modal && !menuOpen && !IsActive && ShowActivated) Hide(); }));
        Closed += (_, _) => Application.Current.Shutdown();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (current != null) Navigate(library.Find(current)?.ParentId); else Hide(); e.Handled = true; }
            if (e.Key == Key.Home) { Navigate(null); e.Handled = true; }
            if (e.Key == Key.PageDown || e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.Control) { ChangePage(1); e.Handled = true; }
            if (e.Key == Key.PageUp || e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Control) { ChangePage(-1); e.Handled = true; }
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { Manage(); e.Handled = true; }
        };
        MouseWheel += (_, e) => { ChangePage(e.Delta < 0 ? 1 : -1); e.Handled = true; };
        Refresh(); PositionAtTaskbar();
    }
    private void PositionAtTaskbar()
    {
        var location = Native.CursorWorkArea(); var dpi = VisualTreeHelper.GetDpi(this);
        var work = new Rect(location.Work.Left / dpi.DpiScaleX, location.Work.Top / dpi.DpiScaleY, location.Work.Width / dpi.DpiScaleX, location.Work.Height / dpi.DpiScaleY);
        Width = Math.Min(920, work.Width - 20); Height = Width * 510 / 920;
        if (Height > work.Height - 20) { Height = work.Height - 20; Width = Height * 920 / 510; }
        Left = Math.Clamp(location.Cursor.X / dpi.DpiScaleX - Width / 2, work.Left + 10, Math.Max(work.Left + 10, work.Right - Width - 10));
        Top = work.Bottom - Height - 7;
    }
    public void Reveal()
    {
        if (modal) { foreach (Window window in OwnedWindows) window.Activate(); return; }
        if (IsVisible && IsActive) { Hide(); return; }
        PositionAtTaskbar(); Show(); WindowState = WindowState.Normal; Activate(); Focus(); AnimateOpen();
    }
    private void AnimateOpen()
    {
        fan.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190)));
        unfolding.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.88, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    private static Point Polar(double radius, double angle)
    {
        double a = angle * Math.PI / 180; return new Point(460 + radius * Math.Cos(a), 453 + radius * Math.Sin(a));
    }
    internal static Geometry Sector(double inside, double outside, double start, double end)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(Polar(outside, start), true, true);
            context.ArcTo(Polar(outside, end), new Size(outside, outside), 0, end - start > 180, SweepDirection.Clockwise, true, false);
            context.LineTo(Polar(inside, end), true, false);
            context.ArcTo(Polar(inside, start), new Size(inside, inside), 0, end - start > 180, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze(); return geometry;
    }
    private static Brush Gradient(string from, string to) => new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), new Point(0, 0), new Point(0, 1));
    private static DropShadowEffect Shadow(double blur = 22, double opacity = .35) => new() { Color = Colors.Black, BlurRadius = blur, ShadowDepth = 5, Opacity = opacity };
    private static void Place(Canvas canvas, UIElement element, double x, double y) { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); canvas.Children.Add(element); }

    private ImageSource? IconFor(Entry e)
    {
        string key = e.IconFile ?? (e.IsFolder ? "@folder" : e.Target);
        if (icons.TryGetValue(key, out var value)) return value;
        ImageSource? icon = null;
        try { if (e.IconFile != null) icon = Native.LoadImage(store.IconPath(e.IconFile)); } catch { }
        icon ??= e.IsFolder ? Native.FolderIcon() : Native.FileIcon(Environment.ExpandEnvironmentVariables(e.Target));
        return icons[key] = icon;
    }
    private void Refresh()
    {
        if (tag.Parent is Panel oldInfo) oldInfo.Children.Clear();
        fan.Children.Clear(); slots.Clear(); page = Math.Clamp(page, 0, PageCount - 1);
        // The window outside this shape remains transparent and does not intercept clicks.
        var shell = new WpfPath { Data = Sector(0.01, 390, -180, 0), Fill = Gradient("#1C293B", "#111C2B"), Stroke = UI.Brush("#425269"), StrokeThickness = 1, Effect = Shadow(26, .48) };
        fan.Children.Add(shell);
        var outerLine = new WpfPath { Data = Sector(386, 388, -177, -3), Fill = Gradient("#657F92", "#324459"), IsHitTestVisible = false, Opacity = .6 }; fan.Children.Add(outerLine);
        var all = library.Children(current).ToList(); var visible = all.Skip(page * PageSize).Take(PageSize).ToList();
        int count = visible.Count switch { 0 or 1 or 3 => 5, 2 or 4 => 6, _ => visible.Count };
        int offset = (count - visible.Count) / 2;
        double span = 178.0 / count;
        for (int index = 0; index < count; index++)
        {
            double start = -179 + index * span + .7, end = -179 + (index + 1) * span - .7;
            var geometry = Sector(216, 380, start, end);
            int entryIndex = index - offset;
            if (entryIndex < 0 || entryIndex >= visible.Count)
            {
                var placeholder = new WpfPath { Data = geometry, Fill = Gradient("#263348", "#1D2A3B"), Stroke = UI.Brush("#37465D"), StrokeThickness = .7, Opacity = .45, IsHitTestVisible = false }; fan.Children.Add(placeholder); continue;
            }
            AddSlot(visible[entryIndex], geometry, (start + end) / 2);
        }
        var inner = new WpfPath { Data = Sector(.01, 205, -180, 0), Fill = Gradient("#182739", "#111C2B"), Stroke = UI.Brush("#314458"), StrokeThickness = 1, IsHitTestVisible = false }; fan.Children.Add(inner);
        var info = new StackPanel { Width = 310, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        tag.TextAlignment = TextAlignment.Center; tag.Margin = new Thickness(0, 0, 0, 7);
        title.TextAlignment = TextAlignment.Center; title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap; title.FontWeight = FontWeights.Medium;
        note.TextAlignment = TextAlignment.Center; note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(8, 9, 8, 0); note.MaxHeight = 57; note.LineHeight = 19; note.TextTrimming = TextTrimming.CharacterEllipsis;
        info.Children.Add(tag); info.Children.Add(title); info.Children.Add(note); Place(fan, info, 305, 278);
        var back = RoundButton(current == null ? "\uE80F" : "\uE72B", current == null ? "처음으로" : "상위 폴더", () => Navigate(library.Find(current)?.ParentId)); back.Opacity = current == null ? .5 : 1;
        Place(fan, back, 359, 403);
        var settings = RoundButton("\uE713", "설정 · 파일 / 폴더 / 이미지 / 메모", Manage, true); Place(fan, settings, 435, 396);
        var hide = RoundButton("\uE711", "숨기기 (Esc)", Hide); Place(fan, hide, 513, 403);
        var settingsMenu = new ContextMenu(); settingsMenu.Items.Add(UI.Menu("설정 열기", Manage)); settingsMenu.Items.Add(UI.Menu("런처 종료", () => Application.Current.Shutdown())); settings.ContextMenu = settingsMenu;
        if (PageCount > 1)
        {
            var previous = RoundButton("\uE76B", "이전 페이지", () => ChangePage(-1)); previous.Width = previous.Height = 32; previous.IsEnabled = page > 0; Place(fan, previous, 284, 460);
            var next = RoundButton("\uE76C", "다음 페이지", () => ChangePage(1)); next.Width = next.Height = 32; next.IsEnabled = page < PageCount - 1; Place(fan, next, 604, 460);
            pageLabel.Text = $"{page + 1} / {PageCount}    ·    마우스 휠로 넘기기";
        }
        else pageLabel.Text = "아이콘에 마우스를 올려 메모 보기";
        pageLabel.TextAlignment = TextAlignment.Center; pageLabel.Width = 280; Place(fan, pageLabel, 320, 464);
        DefaultNote();
    }
    private void AddSlot(Entry entry, Geometry geometry, double angle)
    {
        var button = new Button { Width = 920, Height = 510, Style = SliceStyle, Clip = geometry, Background = Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(0), Tag = entry.Id };
        AutomationProperties.SetName(button, entry.Name); AutomationProperties.SetHelpText(button, entry.Note);
        if (entry.Note.Length > 80 || entry.Note.Count(x => x == '\n') > 2)
        {
            button.ToolTip = new ToolTip { Background = UI.Panel, Foreground = Brushes.White, BorderBrush = UI.Brush("#4A6077"), Padding = new Thickness(14), FontFamily = FontFamily, Content = new TextBlock { Text = entry.Note, Width = 280, TextWrapping = TextWrapping.Wrap, FontSize = 13 } };
            ToolTipService.SetInitialShowDelay(button, 650);
        }
        var contents = new Canvas { Width = 920, Height = 510 };
        var shape = new WpfPath { Data = geometry, Fill = Gradient("#2A3B50", "#213043"), Stroke = UI.Brush("#4A6077"), StrokeThickness = .8 };
        contents.Children.Add(shape);
        var point = Polar(301, angle);
        var iconFrame = new Border { Width = 60, Height = 60, CornerRadius = new CornerRadius(17), Background = Gradient("#3C5067", "#2A3C51"), BorderBrush = UI.Brush("#50637A"), BorderThickness = new Thickness(.7), Effect = Shadow(12, .28), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1), IsHitTestVisible = false };
        ImageSource? source = IconFor(entry);
        iconFrame.Child = source != null ? new Image { Source = source, Width = 40, Height = 40, Stretch = Stretch.Uniform } : new TextBlock { Text = "↗", FontSize = 28, Foreground = UI.Accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Place(contents, iconFrame, point.X - 30, point.Y - 38);
        var name = new TextBlock { Text = entry.Name, Width = 95, FontSize = 13, FontWeight = FontWeights.Medium, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = UI.Brush("#F0F5FC"), IsHitTestVisible = false }; Place(contents, name, point.X - 47.5, point.Y + 31);
        if (entry.IsFolder)
        {
            var badge = new Border { Background = UI.Brush("#65D7CB"), CornerRadius = new CornerRadius(8), Padding = new Thickness(5, 1, 5, 1), IsHitTestVisible = false, Child = UI.Text(library.Children(entry.Id).Count().ToString(), 9, UI.Brush("#103B43")) }; Place(contents, badge, point.X + 13, point.Y + 3);
        }
        button.Content = contents;
        void Highlight(bool active)
        {
            shape.Fill = active ? Gradient("#385D72", "#2A485F") : Gradient("#2A3B50", "#213043");
            shape.Stroke = active ? UI.Accent : UI.Brush("#4A6077"); shape.StrokeThickness = active ? 1.8 : .8;
            var transform = (ScaleTransform)iconFrame.RenderTransform; var animation = new DoubleAnimation(active ? 1.09 : 1, TimeSpan.FromMilliseconds(130)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation); transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            if (active) ShowNote(entry); else DefaultNote();
        }
        button.MouseEnter += (_, _) => Highlight(true); button.MouseLeave += (_, _) => Highlight(button.IsKeyboardFocused);
        button.GotKeyboardFocus += (_, _) => Highlight(true); button.LostKeyboardFocus += (_, _) => Highlight(button.IsMouseOver);
        button.Click += (_, _) => Open(entry);
        var menu = new ContextMenu(); menu.Items.Add(UI.Menu("이름 / 이미지 / 메모 편집", () => Edit(entry))); menu.Items.Add(UI.Menu("폴더 관리 열기", Manage)); button.ContextMenu = menu;
        fan.Children.Add(button); slots[entry.Id] = button;
    }
    private Button RoundButton(string glyph, string tooltip, Action action, bool main = false)
    {
        var label = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = main ? 23 : 16, Foreground = main ? UI.Brush("#132F3C") : UI.Brush("#BDCDD9"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var button = UI.Button("", action); button.Width = button.Height = main ? 50 : 44; button.Margin = new Thickness(0); button.Padding = new Thickness(0); button.ToolTip = tooltip;
        var disk = new Ellipse { Fill = main ? Gradient("#ABEEE5", "#64D4D0") : Gradient("#293F53", "#203144"), Stroke = main ? UI.Brush("#C1F6ED") : UI.Brush("#425D72"), StrokeThickness = 1 };
        button.Style = SliceStyle;
        var grid = new Grid(); grid.Children.Add(disk); grid.Children.Add(label); button.Content = grid;
        AutomationProperties.SetName(button, tooltip);
        button.MouseEnter += (_, _) => { disk.Stroke = UI.Accent; disk.StrokeThickness = 2; };
        button.MouseLeave += (_, _) => { disk.Stroke = main ? UI.Brush("#C1F6ED") : UI.Brush("#425D72"); disk.StrokeThickness = 1; };
        button.GotKeyboardFocus += (_, _) => disk.StrokeThickness = 3; button.LostKeyboardFocus += (_, _) => disk.StrokeThickness = 1;
        return button;
    }
    private void DefaultNote()
    {
        tag.Text = current == null ? "Q U I C K   L A U N C H" : "F O L D E R";
        title.Text = library.Find(current)?.Name ?? "나의 바로가기";
        note.Text = library.Children(current).Any() ? "자주 쓰는 것들을 한 번에.\n실행할 아이콘을 선택하세요." : "아직 등록된 항목이 없어요.\n아래 설정 버튼에서 추가해 주세요.";
    }
    internal void ShowNote(Entry entry)
    {
        tag.Text = entry.IsFolder ? $"폴더  ·  {library.Children(entry.Id).Count()}개 항목" : "바로가기";
        title.Text = entry.Name;
        note.Text = string.IsNullOrWhiteSpace(entry.Note) ? entry.IsFolder ? "클릭하면 폴더 안의 항목이 펼쳐집니다." : "클릭하여 실행 · 우클릭하여 메모 편집" : entry.Note;
    }
    internal void Navigate(string? id)
    {
        pages[current ?? ""] = page; current = id; page = pages.GetValueOrDefault(id ?? ""); Refresh();
    }
    internal void ChangePage(int direction) { page = Math.Clamp(page + direction, 0, PageCount - 1); Refresh(); }
    internal Button? Slot(string id) => slots.GetValueOrDefault(id);
    private void Open(Entry entry)
    {
        if (entry.IsFolder) { Navigate(entry.Id); return; }
        try { using var process = Launch.Start(entry); if (library.HideAfterLaunch) Hide(); }
        catch (Exception ex) { modal = true; try { UI.Error(ex); } finally { modal = false; } }
    }
    private void Manage()
    {
        modal = true;
        try
        {
            var manager = new LibraryWindow(store, library) { Owner = this, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            manager.Navigate(current); manager.ShowDialog(); library = manager.library;
            if (current != null && library.Find(current) == null) current = null;
            icons.Clear(); Refresh();
        }
        finally { modal = false; if (IsVisible) Activate(); }
    }
    private void Edit(Entry entry)
    {
        modal = true;
        try
        {
            var editor = new EditorWindow(store, entry) { Owner = this, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            if (editor.ShowDialog() == true) { library.Entries[library.Entries.IndexOf(entry)] = editor.Result!; store.Save(library); icons.Clear(); Refresh(); }
        }
        finally { modal = false; if (IsVisible) Activate(); }
    }
}
