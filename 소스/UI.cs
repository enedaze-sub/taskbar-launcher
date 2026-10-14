using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeckLauncher;

internal static partial class UI
{
    /// <summary>Settings, menus and help text. Chosen in 꾸미기 → 글꼴.</summary>
    public static FontFamily InterfaceFont { get; private set; } = FontCatalog.DefaultInterface;
    /// <summary>Launcher title, icon names and notes. Chosen in 꾸미기 → 글꼴.</summary>
    public static FontFamily LauncherFont { get; private set; } = FontCatalog.DefaultLauncher;
    public static FontFamily DisplayFont => LauncherFont;
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static void ApplyFonts(FontSettings? fonts)
    {
        InterfaceFont = FontCatalog.Resolve(fonts?.Interface, FontCatalog.DefaultInterface);
        LauncherFont = FontCatalog.Resolve(fonts?.Launcher, FontCatalog.DefaultLauncher);
    }
    public static readonly Brush Background = Brush("#101722"), Panel = Brush("#182332"), Muted = Brush("#93A5BA"), Accent = Brush("#6DE0D4");
    public static Brush Brush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    public static void Theme(Window window, ThemeSettings? theme = null) => Skin.Apply(window, theme ?? new());
    public static T Res<T>(T element, DependencyProperty property, string key) where T : FrameworkElement { element.SetResourceReference(property, key); return element; }
    public static Button Button(string label, Action action)
    {
        var b = new Button { Content = label }; b.Click += (_, _) => action(); return b;
    }
    /// <summary>A Segoe icon glyph centered in its own box, inheriting the parent's foreground unless a key is given.</summary>
    public static TextBlock Icon(string glyph, double size, string? brushKey = null)
    {
        var icon = new TextBlock { Text = glyph, FontFamily = IconFont, FontSize = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
        if (brushKey != null) icon.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return icon;
    }
    /// <summary>Icon inside a rounded tile. The glyph is centered by layout, never by padding.</summary>
    public static Border IconTile(string glyph, double box, double size, string background = "SkinAccentSoft", string foreground = "SkinAccentInk", double radius = 10)
    {
        var tile = new Border { Width = box, Height = box, CornerRadius = new CornerRadius(radius), Child = Icon(glyph, size, foreground), IsHitTestVisible = false };
        tile.SetResourceReference(Border.BackgroundProperty, background); return tile;
    }
    public static Button IconButton(string glyph, string label, Action action, bool primary = false)
    {
        var button = Button("", action); var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Icon(glyph, 14); icon.Margin = new Thickness(0, 0, 8, 0); row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        button.Content = row; button.Padding = new Thickness(14, 9, 15, 9); System.Windows.Automation.AutomationProperties.SetName(button, label);
        if (primary) { Res(button, Control.BackgroundProperty, "SkinAccent"); Res(button, Control.ForegroundProperty, "SkinAccentText"); button.FontWeight = FontWeights.SemiBold; }
        return button;
    }
    public static Button RoundIcon(string glyph, string tooltip, Action action, double box = 36)
    {
        var button = Button("", action); button.Width = button.Height = box; button.Padding = new Thickness(0); button.Content = Icon(glyph, box * .36); button.ToolTip = tooltip;
        System.Windows.Automation.AutomationProperties.SetName(button, tooltip); return button;
    }
    public static TextBlock Heading(string value, double size)
    {
        var text = Text(value, size); text.FontWeight = FontWeights.Bold; return text;
    }
    public static TextBlock Text(string value, double size = 13, Brush? color = null)
    {
        var text = new TextBlock { Text=value,FontSize=size,Foreground=color ?? Brushes.White,TextWrapping=TextWrapping.Wrap };
        string? key=color==null || ReferenceEquals(color,Brushes.White)?"SkinText":ReferenceEquals(color,Muted)?"SkinMuted":ReferenceEquals(color,Accent)?"SkinAccent":null;
        if(key!=null)text.SetResourceReference(TextBlock.ForegroundProperty,key);return text;
    }
    public static MenuItem Menu(string label, Action action)
    {
        var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); return item;
    }
    public static void Error(Exception ex) => MessageBox.Show(ex.Message, "작업을 완료하지 못했습니다", MessageBoxButton.OK, MessageBoxImage.Error);
}

public sealed class LibraryWindow : Window
{
    internal const string EntryDragFormat = "DeckLauncher.EntryIds";
    private readonly Store store;
    internal Library library;
    private string? current;
    private readonly StackPanel sidebar = new();
    private readonly WrapPanel cards = new();
    private readonly StackPanel crumbs = new() { Orientation = Orientation.Horizontal };
    private readonly TextBox search = new() { Width = 250, Padding = new Thickness(34, 8, 10, 8), ToolTip = "이름, 경로, 폴더를 검색합니다 (Ctrl+F)" };
    private readonly TextBlock heading = UI.Heading("라이브러리", 26);
    private readonly TextBlock subtitle = UI.Text("", 13, UI.Muted);
    private readonly TextBlock footer = UI.Text("", 12, UI.Muted);
    private readonly StackPanel empty = new() { Margin = new Thickness(30, 60, 30, 35), HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 460 };
    private readonly TextBlock emptyTitle = UI.Heading("", 21);
    private readonly TextBlock emptyDescription = UI.Text("", 14, UI.Muted);
    private readonly Border dropOverlay = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(2), Margin = new Thickness(-8) };
    private readonly System.Windows.Threading.DispatcherTimer overlayTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private DateTime lastFileDrag;
    private readonly Dictionary<string, ImageSource?> iconCache = new();
    internal int RenderedCards => cards.Children.Count;

    public LibraryWindow(Store store, Library library)
    {
        this.store = store; this.library = library;
        UI.Theme(this, library.InterfaceTheme); Title = "런처 설정 · 파일과 폴더 관리"; ShowInTaskbar = false; Topmost = false;
        Width = 1080; Height = 720; MinWidth = 820; MinHeight = 540;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip;
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width - 32); Height = Math.Min(Height, area.Height - 40);
        Left = area.Left + (area.Width - Width) / 2; Top = area.Bottom - Height - 16;
        SourceInitialized += (_, _) => Native.RoundCorners(this);
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "launcher.ico"))) Icon = Native.LoadImage(Path.Combine(AppContext.BaseDirectory, "launcher.ico"));
        var outer = new Grid(); outer.RowDefinitions.Add(new() { Height = GridLength.Auto }); outer.RowDefinitions.Add(new()); outer.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Content = UI.Res(new Border { BorderThickness = new Thickness(1), Child = outer }, Border.BorderBrushProperty, "SkinBorder");
        var header = UI.Res(new Grid(), Panel.BackgroundProperty, "SkinPanel");
        header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is not Button && e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch { } } };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 16, 12, 16) };
        var logo = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Center, Child = Logo(20) }; UI.Res(logo, Border.BackgroundProperty, "SkinAccentSoft"); brand.Children.Add(logo);
        var brandText = new StackPanel { Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; brandText.Children.Add(UI.Heading("런처 설정", 18));
        var brandHint = UI.Text("파일 · 폴더 · 이미지 · 메모", 12, UI.Muted); brandHint.Margin = new Thickness(0, 2, 0, 0); brandText.Children.Add(brandHint); brand.Children.Add(brandText); header.Children.Add(brand);
        var commands = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) }; Grid.SetColumn(commands, 1);
        var add = UI.IconButton("\uE710", "파일 등록", AddFiles, true); add.ToolTip = "파일 선택 창으로 등록 (Ctrl+N) · 창에 파일을 끌어다 놓아도 됩니다"; commands.Children.Add(add);
        var direct = UI.IconButton("\uE71B", "직접 등록", ShowDirectRegister); direct.ToolTip = "끌어다 놓기 · 대상 주소 붙여넣기 · 등록 방법 안내"; commands.Children.Add(direct);
        commands.Children.Add(UI.IconButton("\uE8F4", "새 폴더", AddFolder)); commands.Children.Add(UI.IconButton("\uE790", "꾸미기", CustomizeAppearance));
        var divider = UI.Res(new Border { Width = 1, Height = 22, Margin = new Thickness(8, 0, 8, 0) }, Border.BackgroundProperty, "SkinBorder"); commands.Children.Add(divider);
        commands.Children.Add(UI.RoundIcon("\uE712", "런처 옵션 · 백업, 복원, 종료", ShowSettings, 38)); commands.Children.Add(UI.RoundIcon("\uE8BB", "닫기 (Esc)", Close, 38));
        header.Children.Add(commands); outer.Children.Add(header);

        var body = new Grid(); Grid.SetRow(body, 1); body.ColumnDefinitions.Add(new() { Width = new GridLength(212) }); body.ColumnDefinitions.Add(new()); outer.Children.Add(body);
        var nav = UI.Res(new DockPanel(), Panel.BackgroundProperty, "SkinSidebar");
        var navLabel = UI.Text("폴더", 12, UI.Muted); navLabel.FontWeight = FontWeights.SemiBold; navLabel.Margin = new Thickness(22, 22, 0, 10); DockPanel.SetDock(navLabel, Dock.Top); nav.Children.Add(navLabel);
        var navHelp = new Border { Margin = new Thickness(14, 10, 14, 16), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 11) }; DockPanel.SetDock(navHelp, Dock.Bottom); UI.Res(navHelp, Border.BackgroundProperty, "SkinPanel");
        var navHelpText = UI.Text("폴더로 끌어 정리하고,\n위쪽 경로로 끌어 꺼내요.", 12, UI.Muted); navHelpText.LineHeight = 19; navHelp.Child = navHelpText; nav.Children.Add(navHelp);
        nav.Children.Add(new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }); body.Children.Add(nav);

        var main = new Grid { Margin = new Thickness(28, 18, 20, 8) }; Grid.SetColumn(main, 1); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new()); body.Children.Add(main);
        crumbs.Margin = new Thickness(-6, 0, 0, 0); main.Children.Add(crumbs);
        var top = new Grid { Margin = new Thickness(0, 10, 0, 20) }; top.ColumnDefinitions.Add(new()); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); Grid.SetRow(top, 1);
        var titles = new StackPanel(); titles.Children.Add(heading); subtitle.Margin = new Thickness(0, 6, 0, 0); titles.Children.Add(subtitle); top.Children.Add(titles);
        var searchWrap = new Grid { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(searchWrap, 1); searchWrap.Children.Add(search);
        var searchIcon = UI.Icon("\uE721", 14, "SkinMuted"); searchIcon.HorizontalAlignment = HorizontalAlignment.Left; searchIcon.Margin = new Thickness(13, 0, 0, 0); searchWrap.Children.Add(searchIcon);
        var placeholder = UI.Text("검색 (Ctrl+F)", 13.5, UI.Muted); placeholder.IsHitTestVisible = false; placeholder.VerticalAlignment = VerticalAlignment.Center; placeholder.Margin = new Thickness(36, 0, 0, 1); searchWrap.Children.Add(placeholder);
        top.Children.Add(searchWrap); main.Children.Add(top);
        search.TextChanged += (_, _) => { placeholder.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; RenderCards(); };
        var content = new Grid(); Grid.SetRow(content, 2);
        content.Children.Add(new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        emptyTitle.TextAlignment = TextAlignment.Center; emptyDescription.TextAlignment = TextAlignment.Center; emptyDescription.LineHeight = 23; emptyDescription.Margin = new Thickness(0, 10, 0, 20);
        var emptyIcon = UI.IconTile("\uE8E5", 76, 34, radius: 24); emptyIcon.Margin = new Thickness(0, 0, 0, 18); empty.Children.Add(emptyIcon); empty.Children.Add(emptyTitle); empty.Children.Add(emptyDescription);
        var emptyActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        emptyActions.Children.Add(UI.IconButton("\uE710", "파일 등록하기", AddFiles, true)); emptyActions.Children.Add(UI.IconButton("\uE71B", "직접 등록", ShowDirectRegister)); empty.Children.Add(emptyActions);
        content.Children.Add(empty);
        UI.Res(dropOverlay, Border.BorderBrushProperty, "SkinAccent"); UI.Res(dropOverlay, Border.BackgroundProperty, "SkinAccentSoft");
        var dropText = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var dropIcon = UI.IconTile("\uE896", 64, 28, "SkinAccent", "SkinAccentText", 20); dropIcon.Margin = new Thickness(0, 0, 0, 14); dropText.Children.Add(dropIcon);
        var dropTitle = UI.Heading("여기에 놓으면 바로 등록돼요", 19); dropTitle.TextAlignment = TextAlignment.Center; dropText.Children.Add(dropTitle);
        var dropHint = UI.Text("실행 파일 · 바로가기 · 폴더 모두 가능", 13, UI.Muted); dropHint.TextAlignment = TextAlignment.Center; dropHint.Margin = new Thickness(0, 6, 0, 0); dropText.Children.Add(dropHint);
        dropOverlay.Child = dropText; content.Children.Add(dropOverlay); main.Children.Add(content);
        var footerBar = UI.Res(new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(22, 10, 22, 11) }, Border.BorderBrushProperty, "SkinBorder"); footerBar.Child = footer; Grid.SetRow(footerBar, 2); outer.Children.Add(footerBar);
        AllowDrop = true;
        DragOver += (_, e) =>
        {
            bool files = e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(EntryDragFormat);
            e.Effects = e.Data.GetDataPresent(EntryDragFormat) ? DragDropEffects.Move : files ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
            if (files) { dropOverlay.Visibility = Visibility.Visible; lastFileDrag = DateTime.UtcNow; overlayTimer.Start(); }
        };
        // DragOver repeats while a drag is alive; if it stops (cancelled with Esc inside the window), hide the hint.
        overlayTimer.Tick += (_, _) => { if ((DateTime.UtcNow - lastFileDrag).TotalMilliseconds > 400) { dropOverlay.Visibility = Visibility.Collapsed; overlayTimer.Stop(); } };
        DragLeave += (_, e) => { var p = e.GetPosition(this); if (p.X <= 1 || p.Y <= 1 || p.X >= ActualWidth - 1 || p.Y >= ActualHeight - 1) dropOverlay.Visibility = Visibility.Collapsed; };
        PreviewDrop += (_, _) => dropOverlay.Visibility = Visibility.Collapsed;
        Drop += (_, e) => { if (e.Data.GetDataPresent(EntryDragFormat) && e.Data.GetData(EntryDragFormat) is string[] ids) DropEntries(ids, current); else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) RegisterPaths(paths); e.Handled = true; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (search.Text.Length > 0) search.Clear(); else if (current != null) Navigate(library.Find(current)?.ParentId); else Close(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F) { search.Focus(); search.SelectAll(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N) { AddFiles(); e.Handled = true; }
        };
        Refresh();
    }
    /// <summary>The launcher mark: four rounded tiles laid out by alignment, so it is exactly centered in any box.</summary>
    internal static FrameworkElement Logo(double size, string brush = "SkinAccentInk")
    {
        var grid = new Grid { Width = size, Height = size, IsHitTestVisible = false }; double tile = size * .42;
        for (int i = 0; i < 4; i++)
        {
            var square = new Border { Width = tile, Height = tile, CornerRadius = new CornerRadius(size * .1), HorizontalAlignment = i % 2 == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment = i < 2 ? VerticalAlignment.Top : VerticalAlignment.Bottom, Opacity = i == 1 ? .55 : 1 };
            square.SetResourceReference(Border.BackgroundProperty, brush); grid.Children.Add(square);
        }
        return grid;
    }

    public void Reveal()
    {
        if (IsVisible && IsActive && WindowState != WindowState.Minimized) { Hide(); return; }
        Show(); WindowState = WindowState.Normal; Activate(); Focus();
    }
    internal void Navigate(string? id) { current = id; search.Clear(); Refresh(); }
    private void Refresh()
    {
        Skin.Apply(this,library.InterfaceTheme); sidebar.Children.Clear();
        Button NavItem(string glyph, string name, string? id, int depth, int count)
        {
            var button = UI.Button("", () => Navigate(id)); button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Margin = new Thickness(12 + Math.Min(depth * 14, 56), 0, 12, 3); button.Padding = new Thickness(11, 9, 11, 9);
            var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new GridLength(26) }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var icon = UI.Icon(glyph, 15, current == id ? "SkinAccentInk" : "SkinMuted"); icon.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(icon);
            var label = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, FontWeight = current == id ? FontWeights.SemiBold : FontWeights.Normal }; Grid.SetColumn(label, 1); row.Children.Add(label);
            if (count > 0) { var number = UI.Text(count.ToString(), 11.5, UI.Muted); number.VerticalAlignment = VerticalAlignment.Center; number.Margin = new Thickness(6, 0, 0, 0); Grid.SetColumn(number, 2); row.Children.Add(number); }
            button.Content = row; if (current == id) UI.Res(button, Control.BackgroundProperty, "SkinAccentSoft"); else button.Background = Brushes.Transparent;
            SetupDropTarget(button, id); return button;
        }
        var root = NavItem("\uE80F", "라이브러리", null, 0, library.Children(null).Count()); root.Margin = new Thickness(12, 0, 12, 8); sidebar.Children.Add(root);
        void AddBranch(string? parent, int depth)
        {
            foreach (var folder in library.Children(parent).Where(x => x.IsFolder))
            {
                var button = NavItem(current == folder.Id ? "\uE838" : "\uE8B7", folder.Name, folder.Id, depth, library.Children(folder.Id).Count());
                button.ToolTip = library.Trail(folder.Id); button.ContextMenu = EntryMenu(folder);
                sidebar.Children.Add(button); AddBranch(folder.Id, depth + 1);
            }
        }
        AddBranch(null, 0);
        crumbs.Children.Clear();
        var back = UI.RoundIcon("\uE72B", "상위 폴더 (Esc) · 여기로 끌어 꺼내기", () => Navigate(library.Find(current)?.ParentId), 32); back.IsEnabled = current != null; back.Background = Brushes.Transparent; crumbs.Children.Add(back);
        SetupDropTarget(back, library.Find(current)?.ParentId);
        var home = UI.Button("라이브러리", () => Navigate(null)); home.Background = Brushes.Transparent; home.Padding = new Thickness(8, 5, 8, 5); home.FontSize = 13.5; UI.Res(home, Control.ForegroundProperty, current == null ? "SkinText" : "SkinMuted"); crumbs.Children.Add(home);
        SetupDropTarget(home, null);
        var chain = new List<Entry>(); var cursor = library.Find(current);
        while (cursor != null) { chain.Insert(0, cursor); cursor = library.Find(cursor.ParentId); }
        // The full breadcrumb is available as a tooltip when the hierarchy grows.
        foreach (var folder in chain.TakeLast(3))
        {
            var separator = UI.Icon("\uE76C", 10, "SkinMuted"); separator.Margin = new Thickness(2, 1, 2, 0); crumbs.Children.Add(separator);
            var b = UI.Button(folder.Name.Length > 15 ? folder.Name[..15] + "…" : folder.Name, () => Navigate(folder.Id)); b.Padding = new Thickness(8, 5, 8, 5); b.FontSize = 13.5; b.Background = Brushes.Transparent; b.ToolTip = library.Trail(folder.Id); crumbs.Children.Add(b);
            UI.Res(b, Control.ForegroundProperty, folder.Id == current ? "SkinText" : "SkinMuted");
            SetupDropTarget(b, folder.Id);
        }
        RenderCards(); Skin.Connect(this);
    }
    private ImageSource? EntryIcon(Entry e)
    {
        string key = e.IconFile ?? (e.IsFolder ? "@folder" : e.Target);
        if (iconCache.TryGetValue(key, out var cached)) return cached;
        ImageSource? icon = null;
        try { if (e.IconFile != null) icon = Native.LoadImage(store.IconPath(e.IconFile)); } catch { }
        icon ??= e.IsFolder ? Native.FolderIcon() : Native.FileIcon(Environment.ExpandEnvironmentVariables(e.Target));
        iconCache[key] = icon; return icon;
    }
    private const double CardWidth = 152, CardHeight = 168, CardPadding = 12;
    internal void RenderCards()
    {
        cards.Children.Clear(); string query = search.Text.Trim();
        var entries = query.Length == 0 ? library.Children(current) : library.Entries.Where(e => (e.Name + " " + e.Target + " " + e.Note + " " + library.Trail(e.ParentId)).Contains(query, StringComparison.OrdinalIgnoreCase));
        var list = entries.ToList(); heading.Text = query.Length > 0 ? "검색 결과" : library.Find(current)?.Name ?? "라이브러리";
        subtitle.Text = query.Length > 0 ? $"전체 라이브러리에서 {list.Count}개 항목" : $"폴더 {list.Count(x => x.IsFolder)}개 · 바로가기 {list.Count(x => !x.IsFolder)}개";
        double inner = CardWidth - CardPadding * 2, innerHeight = CardHeight - CardPadding * 2;
        foreach (var e in list)
        {
            var card = new Button { Width = CardWidth, Height = CardHeight, Margin = new Thickness(0, 0, 14, 14), Padding = new Thickness(CardPadding), Background = UI.Brush(library.InterfaceTheme.Surface), Foreground = UI.Brush(library.InterfaceTheme.Text), ToolTip = e.IsFolder ? library.Trail(e.Id) : e.Target, Tag = e.Id, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var frame = new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(18), Background = library.InterfaceTheme.Blend(library.InterfaceTheme.Surface, library.InterfaceTheme.Background, .32), Margin = new Thickness(0, 0, 0, 12), HorizontalAlignment = HorizontalAlignment.Center };
            var icon = EntryIcon(e);
            frame.Child = icon != null ? new Image { Source = icon, Width = 46, Height = 46, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : UI.Icon("\uE8E5", 28, "SkinAccentInk");
            stack.Children.Add(frame);
            stack.Children.Add(new TextBlock { Text = e.Name, FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = inner - 4, HorizontalAlignment = HorizontalAlignment.Center });
            string label = query.Length > 0 ? library.Trail(e.ParentId) : e.IsFolder ? $"폴더 · {library.Children(e.Id).Count()}개" : Directory.Exists(e.Target) ? "실제 폴더" : e.Target.Contains("://") ? "링크" : Path.GetExtension(e.Target).TrimStart('.').ToUpperInvariant();
            stack.Children.Add(new TextBlock { Text = label, FontSize = 11.5, Foreground = library.InterfaceTheme.Blend(library.InterfaceTheme.Text, library.InterfaceTheme.Surface, .35), TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0), MaxWidth = inner - 4, HorizontalAlignment = HorizontalAlignment.Center });
            if(e.Background.ImageFile!=null)
            {
                var caption=new StackPanel();while(stack.Children.Count>1){var child=stack.Children[1];stack.Children.RemoveAt(1);caption.Children.Add(child);}
                var color=(Color)ColorConverter.ConvertFromString(library.InterfaceTheme.Surface);color.A=236;stack.Children.Add(new Border {Background=new SolidColorBrush(color),CornerRadius=new CornerRadius(8),Padding=new Thickness(4,4,4,5),Child=caption});
            }
            var cardContent = new Grid();
            if (e.Background.ImageFile != null)
            {
                try { var bitmap = Native.LoadImage(store.IconPath(e.Background.ImageFile), 320); var image = new Image { Source=bitmap, Stretch=Stretch.UniformToFill, Opacity=1-e.Background.Darkness }; var crop = BackgroundTools.ImageRect(((System.Windows.Media.Imaging.BitmapSource)bitmap).PixelWidth,((System.Windows.Media.Imaging.BitmapSource)bitmap).PixelHeight,e.Background,new Rect(0,0,inner,innerHeight)); image.Width=crop.Width;image.Height=crop.Height;var canvas=new Canvas { Width=inner,Height=innerHeight,Clip=new RectangleGeometry(new Rect(0,0,inner,innerHeight),10,10) };Canvas.SetLeft(image,crop.X);Canvas.SetTop(image,crop.Y);canvas.Children.Add(image);cardContent.Children.Add(canvas); } catch { }
            }
            cardContent.Children.Add(stack); card.Content = cardContent; card.ContextMenu = EntryMenu(e);
            bool dragged = false; Point? pressed = null;
            card.PreviewMouseLeftButtonDown += (_, args) => { pressed = args.GetPosition(this); dragged = false; };
            card.PreviewMouseMove += (_, args) =>
            {
                if (args.LeftButton != MouseButtonState.Pressed || pressed == null) return;
                var position = args.GetPosition(this);
                if (Math.Abs(position.X - pressed.Value.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - pressed.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                pressed = null; dragged = true;
                var data = new DataObject(EntryDragFormat, new[] { e.Id });
                DragDrop.DoDragDrop(card, data, DragDropEffects.Move); args.Handled = true;
            };
            card.Click += (_, _) => { if (!dragged) Open(e); };
            SetupCardDropTarget(card, e);
            card.PreviewKeyDown += (_, args) => { if (args.Key == Key.F2) { Edit(e); args.Handled = true; } if (args.Key == Key.Delete) { Delete(e); args.Handled = true; } };
            cards.Children.Add(card);
        }
        empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        emptyTitle.Text = query.Length > 0 ? "검색 결과가 없어요" : current == null ? "자주 쓰는 파일을 한곳에" : "이 폴더를 채워 보세요";
        emptyDescription.Text = query.Length > 0 ? "다른 이름이나 경로로 검색해 보세요." : "실행 파일(.exe)이나 바로가기를 이 창에 끌어다 놓으면 바로 등록돼요.\n용도별 폴더로 묶고, 원하는 이미지로 꾸밀 수 있어요.";
        footer.Text = "파일을 창에 끌어다 놓으면 등록 · 카드 가장자리: 순서 변경 · 폴더 가운데: 안으로 넣기 · 우클릭: 편집 / 칸 배경";
    }
    private void SetupCardDropTarget(Button target, Entry anchor)
    {
        target.AllowDrop = true;
        (bool Into, bool After) Location(DragEventArgs e) { double x=e.GetPosition(target).X; return (anchor.IsFolder && x >= 30 && x <= target.ActualWidth-30, x > target.ActualWidth/2); }
        void Clear() { target.BorderThickness=new Thickness(0); target.Background=UI.Brush(library.InterfaceTheme.Surface); target.ToolTip=anchor.IsFolder?library.Trail(anchor.Id):anchor.Target; }
        void Over(DragEventArgs e)
        {
            var location=Location(e); var ids=e.Data.GetData(EntryDragFormat) as string[];
            bool allowed=ids != null && ids.All(id=>library.Find(id) is Entry moving && moving!=anchor && library.CanMove(moving,location.Into?anchor.Id:anchor.ParentId));
            bool files=anchor.IsFolder && e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effects=allowed?DragDropEffects.Move:files?DragDropEffects.Copy:DragDropEffects.None; e.Handled=true;
            target.BorderBrush=UI.Brush(library.InterfaceTheme.Accent); target.BorderThickness=allowed ? location.Into?new Thickness(2):location.After?new Thickness(0,0,5,0):new Thickness(5,0,0,0) : new Thickness(0);
            target.Background=allowed?library.InterfaceTheme.Blend(library.InterfaceTheme.Surface,library.InterfaceTheme.Accent,.24):UI.Brush(library.InterfaceTheme.Surface);
            target.ToolTip=location.Into?"폴더 안으로 넣기":location.After?"이 카드 뒤로 이동":"이 카드 앞으로 이동";
        }
        target.DragOver+=(_,e)=>Over(e); target.DragEnter+=(_,e)=>Over(e);target.DragLeave+=(_,_)=>Clear();
        target.Drop+=(_,e)=>
        {
            e.Handled=true;var location=Location(e);Clear();
            if(e.Data.GetData(EntryDragFormat) is string[] ids)
            {
                bool changed=location.Into?library.MoveEntries(ids,anchor.Id):library.ReorderEntries(ids,anchor.Id,location.After);
                if(changed)Commit();
            }
            else if(anchor.IsFolder && e.Data.GetData(DataFormats.FileDrop) is string[] paths)RegisterInto(paths,anchor.Id);
        };
    }
    private void SetupDropTarget(Button target, string? parent)
    {
        target.AllowDrop = true; Brush? original = null;
        void Over(DragEventArgs e)
        {
            bool allowed = e.Data.GetDataPresent(EntryDragFormat) && e.Data.GetData(EntryDragFormat) is string[] ids && ids.All(id => library.Find(id) is Entry entry && library.CanMove(entry, parent));
            bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effects = allowed ? DragDropEffects.Move : files ? DragDropEffects.Copy : DragDropEffects.None;
            if (allowed || files) { original ??= target.Background; target.SetResourceReference(Control.BackgroundProperty, "SkinHover"); }
            e.Handled = true;
        }
        target.DragEnter += (_, e) => Over(e); target.DragOver += (_, e) => Over(e);
        target.DragLeave += (_, _) => { if (original != null) target.Background = original; original = null; };
        target.Drop += (_, e) =>
        {
            e.Handled = true;
            if (e.Data.GetDataPresent(EntryDragFormat) && e.Data.GetData(EntryDragFormat) is string[] ids) DropEntries(ids, parent);
            else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) RegisterInto(paths, parent);
            if (original != null) target.Background = original; original = null;
        };
    }
    internal bool DropEntries(IEnumerable<string> ids, string? parent)
    {
        if (!library.MoveEntries(ids, parent)) return false;
        Commit(); return true;
    }
    private void CustomizeAppearance()
    {
        var menu = new ActionSheet("화면 꾸미기", "색상과 모양, 글꼴을 내 취향으로", library.InterfaceTheme);
        menu.Add("\uE790", "런처 색상 · UI 디자인", "기본 색상, 새 레이아웃, 내 커스텀 테마", CustomizeTheme);
        menu.Add("\uE790","설정창 색상","런처와 연동하거나 별도의 색상 사용",CustomizeSettingsColors);
        menu.Add("\uEB9F", "전체 배경 이미지", "실제 화면을 보며 이미지 위치와 확대 조절", CustomizeBackground);
        menu.Add("\uEB9F", "칸 배경 일괄 설정", "전체 또는 폴더 안의 칸에 이미지·투명도 적용", CustomizeBulk);
        menu.Add("\uE8D2", "글꼴", "런처와 설정 창의 글꼴을 각각 고르기", CustomizeFonts);
        menu.Note("칸별 배경은 카드 우클릭 → ‘이 칸의 배경 이미지’에서 바꿀 수 있어요.");
        menu.Owner=this;menu.ShowDialog();
    }
    private void CustomizeText()
    {
        var dialog = new AppearanceWindow(store, library) { Owner = this };
        if (dialog.ShowDialog() == true) { dialog.ApplyTo(library); Commit(); }
    }
    private void CustomizeFonts()
    {
        var dialog = new FontWindow(store, library) { Owner = this };
        if (dialog.ShowDialog() == true) { library.Fonts = dialog.Result!; UI.ApplyFonts(library.Fonts); Commit(); }
    }
    /// <summary>One place for both registration paths: drop files, or paste a shortcut's target.</summary>
    private void ShowDirectRegister()
    {
        var window = new RegistrationHelpWindow(library.InterfaceTheme, entry => { entry.ParentId = current; library.Entries.Add(entry); search.Clear(); Commit(); }, paths => RegisterInto(paths, current), library.Trail(current)) { Owner = this };
        window.ShowDialog();
    }
    private void CustomizeSettingsColors()
    { var dialog=new SettingsColorsWindow(library){Owner=this};if(dialog.ShowDialog()==true){library.SettingsColors=dialog.Result!;Commit();} }
    private void CustomizeTheme()
    {
        var dialog = new ThemeWindow(store, library) { Owner = this };
        if (dialog.ShowDialog() == true) { library.Theme = dialog.Result!; library.CustomThemes = dialog.SavedThemesResult!; Commit(); }
    }
    private void CustomizeSlot(Entry entry)
    {
        var dialog = new BackgroundWindow(store, library, entry) { Owner = this };
        if (dialog.ShowDialog() == true) { dialog.ApplyTo(entry); Commit(); }
    }
    private void CustomizeBackground()
    {
        var dialog = new BackgroundWindow(store, library) { Owner = this };
        if (dialog.ShowDialog() == true) { library.Background = dialog.Result!; library.Appearance.Opacity=dialog.ResultOpacity; Commit(); }
    }
    private void CustomizeBulk()
    {
        var dialog=new BackgroundWindow(store,library,null,true,current){Owner=this};if(dialog.ShowDialog()==true){dialog.ApplyBulkTo(library);Commit();}
    }
    private ContextMenu EntryMenu(Entry e)
    {
        var menu = new ContextMenu(); menu.Items.Add(UI.Menu(e.IsFolder ? "폴더 열기" : "실행", () => Open(e)));
        menu.Items.Add(UI.Menu("편집 / 아이콘 변경", () => Edit(e))); menu.Items.Add(UI.Menu("이 칸의 배경 이미지…", () => CustomizeSlot(e))); menu.Items.Add(UI.Menu("폴더로 이동…", () => Move(e)));
        menu.Items.Add(new Separator()); menu.Items.Add(UI.Menu("앞으로 이동", () => Reorder(e, -1))); menu.Items.Add(UI.Menu("뒤로 이동", () => Reorder(e, 1)));
        menu.Items.Add(new Separator()); menu.Items.Add(UI.Menu("등록 해제", () => Delete(e))); return menu;
    }
    private void Commit() { store.Save(library); iconCache.Clear(); Refresh(); }
    internal void RegisterPaths(IEnumerable<string> paths)
        => RegisterInto(paths, current);
    private int RegisterInto(IEnumerable<string> paths, string? parent)
    {
        int added = 0;
        foreach (string path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) continue;
            var full = Path.GetFullPath(path);
            if (library.Children(parent).Any(x => !x.IsFolder && string.Equals(x.Target, full, StringComparison.OrdinalIgnoreCase))) continue;
            library.Entries.Add(new Entry { Name = Directory.Exists(full) ? new DirectoryInfo(full).Name : Path.GetFileNameWithoutExtension(full), Target = full, ParentId = parent }); added++;
        }
        search.Clear(); if (added > 0) Commit();
        return added;
    }
    private void AddFiles()
    {
        var dialog = new OpenFileDialog { Title = "등록할 파일 또는 바로가기 선택", Filter = "실행 파일 및 바로가기|*.exe;*.lnk;*.url;*.bat;*.cmd|모든 파일|*.*", FilterIndex = 2, Multiselect = true };
        if (dialog.ShowDialog(this) == true) RegisterPaths(dialog.FileNames);
    }
    private void AddFolder()
    {
        var entry = new Entry { Name = "새 폴더", IsFolder = true, ParentId = current };
        var dialog = new EditorWindow(store, entry,library.InterfaceTheme) { Owner = this };
        if (dialog.ShowDialog() == true) { library.Entries.Add(dialog.Result!); search.Clear(); Commit(); }
    }
    private void Edit(Entry e)
    {
        var dialog = new EditorWindow(store, e,library.InterfaceTheme) { Owner = this };
        if (dialog.ShowDialog() == true) { library.Entries[library.Entries.IndexOf(e)] = dialog.Result!; Commit(); }
    }
    private void Move(Entry e)
    {
        var dialog = new MoveWindow(library, e) { Owner = this };
        if (dialog.ShowDialog() == true) { e.ParentId = dialog.SelectedParent; Commit(); }
    }
    private void Reorder(Entry e, int direction)
    {
        var siblings = library.Children(e.ParentId).ToList(); int pos = siblings.IndexOf(e), next = pos + direction;
        if (next < 0 || next >= siblings.Count) return;
        int a = library.Entries.IndexOf(e), b = library.Entries.IndexOf(siblings[next]);
        (library.Entries[a], library.Entries[b]) = (library.Entries[b], library.Entries[a]); Commit();
    }
    private void Delete(Entry e)
    {
        string detail = e.IsFolder ? "내부의 등록 항목도 함께 해제됩니다." : "";
        if (MessageBox.Show(this, $"‘{e.Name}’ 등록을 해제할까요?\n{detail}\n실제 파일은 삭제되지 않습니다.", "등록 해제", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        library.Remove(e); if (current != null && library.Find(current) == null) current = null; Commit();
    }
    private void Open(Entry e)
    {
        if (e.IsFolder) { Navigate(e.Id); return; }
        try
        {
            using var process = Launch.Start(e);
        }
        catch (Exception ex) { UI.Error(ex); }
    }
    private void ShowSettings() { var menu=OptionsSheet();menu.Owner=this;menu.ShowDialog(); }
    internal ActionSheet OptionsSheet()
    {
        var menu = new ActionSheet("런처 옵션", "동작 설정과 라이브러리 관리",library.InterfaceTheme);
        menu.Add("\uE8D2","중앙 문구 수정","제목과 안내 문구를 작성하기",CustomizeText);
        menu.Section("동작");
        menu.AddToggle("휠로 다이얼 돌리기", "끄면 이전 / 다음 페이지 버튼으로 넘겨요. 켜면 휠 한 칸에 아이콘이 하나씩 돌아요.",library.WheelDialEnabled,value=>{library.WheelDialEnabled=value;Commit();});
        menu.Section("라이브러리");
        menu.Add("\uE898","라이브러리 백업","등록 정보, 아이콘, 배경, 커스텀 테마를 ZIP으로 저장",Export);
        menu.Add("\uE896","백업 복원","이전에 저장한 ZIP에서 불러오기",Import);
        menu.Add("\uE838","저장 폴더 열기","현재 등록 정보와 이미지가 있는 폴더",()=>Process.Start(new ProcessStartInfo(store.DirectoryPath){UseShellExecute=true}));
        menu.Section("앱");
        menu.Add("\uE7E8","런처 종료","작업표시줄 아이콘을 누르면 다시 실행돼요",()=>Application.Current.Shutdown(),danger:true);
        return menu;
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { Title = "아이콘을 포함하여 백업", Filter = "런처 백업|*.zip", FileName = Store.BackupFileName(), OverwritePrompt = false };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var savedPath = store.Backup(dialog.FileName, library);
            MessageBox.Show(this, "등록 정보와 사용자 지정 아이콘을 백업했습니다.\n" + savedPath, "백업 완료");
        }
        catch (Exception ex) { UI.Error(ex); }
    }
    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "런처 백업|*.zip", Title = "라이브러리 백업 선택" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var backup = Store.ReadBackup(dialog.FileName);
            if (MessageBox.Show(this, "현재 등록 항목을 선택한 백업 내용으로 교체할까요?\n현재 등록 정보는 library.json.bak에 보관됩니다.", "백업 복원", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            library = store.Restore(backup); UI.ApplyFonts(library.Fonts); current = null; search.Clear(); iconCache.Clear(); Refresh();
        }
        catch (Exception ex) { UI.Error(ex); }
    }
}

public sealed class EditorWindow : Window
{
    private readonly Store store;
    private readonly Entry original;
    private readonly TextBox name, target, arguments, working, note;
    private readonly Image preview = new() { Width = 46, Height = 46, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private string? pickedIcon;
    private string? existingIcon;
    public Entry? Result { get; private set; }
    public EditorWindow(Store store, Entry original, ThemeSettings? theme = null)
    {
        this.store = store; this.original = original; existingIcon = original.IconFile;
        UI.Theme(this, theme); Title = original.IsFolder ? "폴더 편집" : "바로가기 편집"; Width = 600; Height = original.IsFolder ? 520 : 760;
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 30);
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var layout = new Grid(); layout.RowDefinitions.Add(new()); layout.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = layout;
        var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 10) }; layout.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        if (!original.IsFolder)
        {
            var help = UI.IconButton("\uE897", "대상 주소 찾는 법", () => new RegistrationHelpWindow(theme ?? new(), null) { Owner = this }.ShowDialog()); help.Background = Brushes.Transparent; UI.Res(help, Control.ForegroundProperty, "SkinAccentInk"); help.Padding = new Thickness(10, 7, 12, 7); DockPanel.SetDock(help, Dock.Right); header.Children.Add(help);
        }
        var titleText = UI.Heading(Title, 22); titleText.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(titleText); panel.Children.Add(header);

        // Icon and name sit together so it is obvious which image belongs to the entry.
        var identity = new Grid(); identity.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); identity.ColumnDefinitions.Add(new());
        var iconColumn = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        var tile = UI.Res(new Border { Width = 76, Height = 76, CornerRadius = new CornerRadius(20), Child = preview, HorizontalAlignment = HorizontalAlignment.Center }, Border.BackgroundProperty, "SkinPanel"); iconColumn.Children.Add(tile);
        var pick = UI.Button("이미지 변경", PickIcon); pick.FontSize = 12.5; pick.Padding = new Thickness(10, 6, 10, 6); pick.Margin = new Thickness(0, 8, 0, 0); iconColumn.Children.Add(pick);
        var reset = UI.Button("기본 아이콘", () => { pickedIcon = null; existingIcon = null; UpdatePreview(); }); reset.FontSize = 12.5; reset.Padding = new Thickness(10, 6, 10, 6); reset.Margin = new Thickness(0, 4, 0, 0); reset.Background = Brushes.Transparent; UI.Res(reset, Control.ForegroundProperty, "SkinMuted"); iconColumn.Children.Add(reset);
        identity.Children.Add(iconColumn);
        var nameColumn = new StackPanel(); Grid.SetColumn(nameColumn, 1); identity.Children.Add(nameColumn);
        name = Field(nameColumn, "이름", original.Name, out _, first: true);
        note = Field(nameColumn, "마우스를 올렸을 때 표시할 메모", original.Note, out _);
        note.Height = 82; note.AcceptsReturn = true; note.TextWrapping = TextWrapping.Wrap; note.VerticalContentAlignment = VerticalAlignment.Top; note.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; note.MaxLength = 300; note.ToolTip = "최대 300자. 런처 가운데에 표시됩니다.";
        panel.Children.Add(identity);
        var targetRow = new Grid(); targetRow.ColumnDefinitions.Add(new()); targetRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        target = Field(panel, "파일 경로 또는 URL", original.Target, out var targetLabel, host: targetRow);
        var browse = UI.Button("찾아보기…", () => { var d = new OpenFileDialog { Filter = "모든 파일|*.*" }; if (d.ShowDialog(this) == true) { target.Text = d.FileName; UpdatePreview(); } }); browse.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(browse, 1); targetRow.Children.Add(browse);
        arguments = Field(panel, "실행 인수 (선택)", original.Arguments, out var argumentsLabel); working = Field(panel, "작업 폴더 (선택)", original.WorkingDirectory, out var workingLabel);
        target.Tag="Target";arguments.Tag="Arguments";
        var hint = UI.Text("실제 폴더 경로, https:// 주소, steam:// 링크도 등록할 수 있어요. 바로가기 ‘대상’을 통째로 붙여 넣으면 실행 인수가 자동으로 나뉩니다.", 12, UI.Muted); hint.LineHeight = 19; hint.Margin = new Thickness(0, 12, 0, 0); panel.Children.Add(hint);
        if (original.IsFolder) foreach (UIElement element in new UIElement[] { targetLabel, targetRow, argumentsLabel, arguments, workingLabel, working, hint }) element.Visibility = Visibility.Collapsed;
        else
        {
            target.TextChanged += (_, _) => UpdatePreview();
            TargetCommand.AttachPaste(target, arguments);
        }
        var footer = UI.Res(new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 12, 24, 14) }, Border.BorderBrushProperty, "SkinBorder"); Grid.SetRow(footer, 1); layout.Children.Add(footer);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; footer.Child = row;
        var cancel = UI.Button("취소", () => DialogResult = false); cancel.IsCancel = true; row.Children.Add(cancel);
        var save = UI.Button("저장", Save); save.IsDefault = true; save.FontWeight = FontWeights.SemiBold; save.MinWidth = 84; UI.Res(save, Control.BackgroundProperty, "SkinAccent"); UI.Res(save, Control.ForegroundProperty, "SkinAccentText"); row.Children.Add(save);
        Loaded += (_, _) => { Skin.Connect(this); UpdatePreview(); name.Focus(); name.SelectAll(); };
    }
    private static TextBox Field(Panel panel, string label, string value, out TextBlock caption, bool first = false, Grid? host = null)
    {
        caption = UI.Text(label, 12.5, UI.Muted); caption.FontWeight = FontWeights.SemiBold; caption.Margin = new Thickness(2, first ? 0 : 16, 0, 6); panel.Children.Add(caption);
        var box = new TextBox { Text = value };
        if (host != null) { host.Children.Add(box); panel.Children.Add(host); } else panel.Children.Add(box);
        return box;
    }
    private void PickIcon()
    {
        var dialog = new OpenFileDialog { Title = "아이콘으로 사용할 이미지", Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif|모든 파일|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try { Native.LoadImage(dialog.FileName); pickedIcon = dialog.FileName; UpdatePreview(); } catch (Exception ex) { UI.Error(ex); }
    }
    private void UpdatePreview()
    {
        try { preview.Source = pickedIcon != null ? Native.LoadImage(pickedIcon) : existingIcon != null ? Native.LoadImage(store.IconPath(existingIcon)) : original.IsFolder ? Native.FolderIcon() : Native.FileIcon(Environment.ExpandEnvironmentVariables(target.Text.Trim().Trim('"'))); }
        catch { preview.Source = original.IsFolder ? Native.FolderIcon() : null; }
    }
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show(this, "이름을 입력해 주세요."); name.Focus(); return; }
        var parsedTarget=TargetCommand.Parse(target.Text); string path=parsedTarget.Target;
        if(parsedTarget.Arguments.Length>0)arguments.Text=parsedTarget.Arguments;
        if (!original.IsFolder && path.Length == 0) { MessageBox.Show(this, "실행할 파일 경로나 링크를 입력해 주세요."); target.Focus(); return; }
        try
        {
            Result = new Entry { Id = original.Id, ParentId = original.ParentId, IsFolder = original.IsFolder, Name = name.Text.Trim(), Target = path, Arguments = arguments.Text, WorkingDirectory = working.Text.Trim().Trim('"'), Note = note.Text.Trim(), Opacity = original.Opacity, SeparateOpacity=original.SeparateOpacity, IconOpacity=original.IconOpacity, TextOpacity=original.TextOpacity, IconFile = pickedIcon != null ? store.CopyIcon(pickedIcon) : existingIcon, Background = original.Background.Copy() };
            DialogResult = true;
        }
        catch (Exception ex) { UI.Error(ex); }
    }
}

internal sealed class MoveWindow : Window
{
    private sealed record Destination(string? Id, string Label) { public override string ToString() => Label; }
    private readonly ComboBox combo = new() { Margin = new Thickness(0, 16, 0, 24) };
    public string? SelectedParent => (combo.SelectedItem as Destination)?.Id;
    public MoveWindow(Library library, Entry entry)
    {
        UI.Theme(this,library.InterfaceTheme); Title = "폴더로 이동"; Width = 520; Height = 270; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(26, 22, 26, 20) }; panel.Children.Add(UI.Heading("‘" + entry.Name + "’ 이동", 20));
        var hint = UI.Text("옮길 폴더를 고르세요. 카드를 폴더로 끌어다 놓아도 됩니다.", 13, UI.Muted); hint.Margin = new Thickness(0, 6, 0, 0); panel.Children.Add(hint);
        combo.Items.Add(new Destination(null, "라이브러리 (맨 위)"));
        foreach (var folder in library.Entries.Where(e => e.IsFolder && library.CanMove(entry, e.Id))) combo.Items.Add(new Destination(folder.Id, library.Trail(folder.Id)));
        combo.SelectedItem = combo.Items.Cast<Destination>().FirstOrDefault(x => x.Id == entry.ParentId); panel.Children.Add(combo);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; var cancel = UI.Button("취소", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        var move = UI.Button("이동", () => DialogResult = true); move.IsDefault = true; move.FontWeight = FontWeights.SemiBold; move.MinWidth = 84; UI.Res(move, Control.BackgroundProperty, "SkinAccent"); UI.Res(move, Control.ForegroundProperty, "SkinAccentText"); actions.Children.Add(move); panel.Children.Add(actions); Content = panel;
    }
}
