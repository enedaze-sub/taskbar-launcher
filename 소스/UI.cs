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

internal static class UI
{
    public static FontFamily RoundedFont => BundledFont("Gowun Dodum");
    public static FontFamily DisplayFont => BundledFont("Jua");
    private static FontFamily BundledFont(string family)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Fonts");
        return Directory.Exists(directory) ? new FontFamily(new Uri(directory + Path.DirectorySeparatorChar), "./#" + family) : new FontFamily("Noto Sans KR, Segoe UI, Malgun Gothic");
    }
    public static readonly Brush Background = Brush("#101722"), Panel = Brush("#182332"), Muted = Brush("#93A5BA"), Accent = Brush("#6DE0D4");
    public static Brush Brush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    public static void Theme(Window window)
    {
        window.FontFamily = RoundedFont; window.FontSize = 14;
        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
        window.Foreground = Brushes.White; window.Background = Background;
        window.Resources = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='Button'>
  <Setter Property='Foreground' Value='#EAF2FC'/><Setter Property='Background' Value='#223247'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='14,9'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Margin' Value='3'/><Setter Property='HorizontalContentAlignment' Value='Center'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='box' Background='{TemplateBinding Background}' CornerRadius='9' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Opacity' Value='0.85'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#6DE0D4'/><Setter TargetName='box' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='TextBox'><Setter Property='Foreground' Value='#EAF2FC'/><Setter Property='Background' Value='#202D3F'/><Setter Property='BorderBrush' Value='#3B526D'/><Setter Property='Padding' Value='10,8'/><Setter Property='CaretBrush' Value='#6DE0D4'/><Setter Property='SelectionBrush' Value='#396B81'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
 <Style TargetType='CheckBox'><Setter Property='Foreground' Value='#EAF2FC'/><Setter Property='Margin' Value='5,10'/></Style>
 <Style TargetType='ComboBox'><Setter Property='Padding' Value='7'/><Setter Property='Foreground' Value='#101722'/></Style>
 <Style TargetType='ContextMenu'><Setter Property='Background' Value='#223247'/><Setter Property='Foreground' Value='#EAF2FC'/><Setter Property='BorderBrush' Value='#3B526D'/></Style>
 <Style TargetType='MenuItem'><Setter Property='Padding' Value='12,7'/></Style>
</ResourceDictionary>");
    }
    public static Button Button(string label, Action action)
    {
        var b = new Button { Content = label }; b.Click += (_, _) => action(); return b;
    }
    public static TextBlock Text(string value, double size = 13, Brush? color = null) => new() { Text = value, FontSize = size, Foreground = color ?? Brushes.White, TextWrapping = TextWrapping.Wrap };
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
    private readonly TextBox search = new() { Width = 225, Margin = new Thickness(5), ToolTip = "이름, 경로, 폴더를 검색합니다 (Ctrl+F)" };
    private readonly TextBlock heading = UI.Text("라이브러리", 27);
    private readonly TextBlock subtitle = UI.Text("", 12, UI.Muted);
    private readonly TextBlock footer = UI.Text("", 11, UI.Muted);
    private readonly StackPanel empty = new() { Margin = new Thickness(30, 75, 30, 35), HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock emptyTitle = UI.Text("", 23);
    private readonly TextBlock emptyDescription = UI.Text("", 13, UI.Muted);
    private readonly Dictionary<string, ImageSource?> iconCache = new();
    internal int RenderedCards => cards.Children.Count;

    public LibraryWindow(Store store, Library library)
    {
        this.store = store; this.library = library;
        UI.Theme(this); Title = "런처 설정 · 파일과 폴더 관리"; ShowInTaskbar = false; Topmost = false;
        heading.FontFamily = UI.DisplayFont;
        Width = 1040; Height = 710; MinWidth = 780; MinHeight = 520;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip;
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width - 32); Height = Math.Min(Height, area.Height - 40);
        Left = area.Left + (area.Width - Width) / 2; Top = area.Bottom - Height - 16;
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "launcher.ico"))) Icon = Native.LoadImage(Path.Combine(AppContext.BaseDirectory, "launcher.ico"));
        var outer = new Grid(); outer.RowDefinitions.Add(new() { Height = GridLength.Auto }); outer.RowDefinitions.Add(new()); outer.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Content = new Border { BorderBrush = UI.Brush("#304159"), BorderThickness = new Thickness(1), Child = outer };
        var header = new Grid { Background = UI.Panel, Margin = new Thickness(0) };
        header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is not Button && e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch { } } };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 19, 12, 19) };
        brand.Children.Add(UI.Text("▦", 28, UI.Accent));
        var brandText = new StackPanel { Margin = new Thickness(12, 0, 0, 0) }; brandText.Children.Add(UI.Text("런처 설정", 19)); brandText.Children.Add(UI.Text("파일 · 폴더 · 이미지 · 메모", 11, UI.Muted)); brand.Children.Add(brandText); header.Children.Add(brand);
        var commands = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) }; Grid.SetColumn(commands, 1);
        var add = UI.Button("＋ 파일 등록", AddFiles); add.Background = UI.Brush("#285D67"); commands.Children.Add(add);
        commands.Children.Add(UI.Button("＋ 폴더", AddFolder)); commands.Children.Add(UI.Button("꾸미기", CustomizeAppearance)); commands.Children.Add(UI.Button("···", ShowSettings));
        commands.Children.Add(UI.Button("×", Close)); header.Children.Add(commands); outer.Children.Add(header);

        var body = new Grid(); Grid.SetRow(body, 1); body.ColumnDefinitions.Add(new() { Width = new GridLength(186) }); body.ColumnDefinitions.Add(new()); outer.Children.Add(body);
        var nav = new DockPanel { Background = UI.Brush("#131D2A") };
        var navLabel = UI.Text("COLLECTIONS", 10, UI.Muted); navLabel.Margin = new Thickness(19, 26, 0, 15); DockPanel.SetDock(navLabel, Dock.Top); nav.Children.Add(navLabel);
        var navHelp = UI.Text("폴더로 드래그해서 분류\n상위 경로로 드래그해 꺼내기", 11, UI.Muted); navHelp.Margin = new Thickness(19, 15, 15, 22); DockPanel.SetDock(navHelp, Dock.Bottom); nav.Children.Add(navHelp);
        nav.Children.Add(new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }); body.Children.Add(nav);

        var main = new Grid { Margin = new Thickness(24, 20, 18, 12) }; Grid.SetColumn(main, 1); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new()); body.Children.Add(main);
        main.Children.Add(crumbs);
        var top = new Grid { Margin = new Thickness(0, 14, 0, 19) }; top.ColumnDefinitions.Add(new()); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); Grid.SetRow(top, 1);
        var titles = new StackPanel(); titles.Children.Add(heading); subtitle.Margin = new Thickness(0, 7, 0, 0); titles.Children.Add(subtitle); top.Children.Add(titles);
        var searchWrap = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(searchWrap, 1); searchWrap.Children.Add(UI.Text("검색", 10, UI.Muted)); searchWrap.Children.Add(search); top.Children.Add(searchWrap); main.Children.Add(top);
        search.TextChanged += (_, _) => RenderCards();
        var content = new Grid(); Grid.SetRow(content, 2);
        content.Children.Add(new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        emptyTitle.TextAlignment = TextAlignment.Center; emptyDescription.TextAlignment = TextAlignment.Center; emptyDescription.Margin = new Thickness(0, 14, 0, 20);
        empty.Children.Add(UI.Text("▦", 62, UI.Accent)); empty.Children.Add(emptyTitle); empty.Children.Add(emptyDescription);
        empty.Children.Add(UI.Button("파일 등록하기", AddFiles)); content.Children.Add(empty); main.Children.Add(content);
        footer.Margin = new Thickness(22, 11, 22, 12); Grid.SetRow(footer, 2); outer.Children.Add(footer);
        AllowDrop = true;
        DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(EntryDragFormat) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) => { if (e.Data.GetDataPresent(EntryDragFormat) && e.Data.GetData(EntryDragFormat) is string[] ids) DropEntries(ids, current); else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) RegisterPaths(paths); e.Handled = true; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (search.Text.Length > 0) search.Clear(); else if (current != null) Navigate(library.Find(current)?.ParentId); else Hide(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F) { search.Focus(); search.SelectAll(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N) { AddFiles(); e.Handled = true; }
        };
        Refresh();
    }

    public void Reveal()
    {
        if (IsVisible && IsActive && WindowState != WindowState.Minimized) { Hide(); return; }
        Show(); WindowState = WindowState.Normal; Activate(); Focus();
    }
    internal void Navigate(string? id) { current = id; search.Clear(); Refresh(); }
    private void Refresh()
    {
        sidebar.Children.Clear();
        var root = UI.Button("▦  라이브러리", () => Navigate(null)); root.HorizontalContentAlignment = HorizontalAlignment.Left;
        SetupDropTarget(root, null);
        root.Margin = new Thickness(10, 0, 10, 7); root.Background = current == null ? UI.Brush("#254B57") : Brushes.Transparent; sidebar.Children.Add(root);
        void AddBranch(string? parent, int depth)
        {
            foreach (var folder in library.Children(parent).Where(x => x.IsFolder))
            {
                var button = UI.Button("▸  " + folder.Name, () => Navigate(folder.Id)); button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Content = new TextBlock { Text = "▸  " + folder.Name, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = library.Trail(folder.Id) };
                button.Margin = new Thickness(10 + Math.Min(depth * 10, 50), 0, 10, 3); button.Padding = new Thickness(10, 10, 8, 10);
                button.Background = current == folder.Id ? UI.Brush("#254B57") : Brushes.Transparent; button.ContextMenu = EntryMenu(folder);
                SetupDropTarget(button, folder.Id);
                sidebar.Children.Add(button); AddBranch(folder.Id, depth + 1);
            }
        }
        AddBranch(null, 0);
        crumbs.Children.Clear();
        var back = UI.Button("‹", () => Navigate(library.Find(current)?.ParentId)); back.IsEnabled = current != null; back.Padding = new Thickness(10, 4, 10, 4); crumbs.Children.Add(back);
        SetupDropTarget(back, library.Find(current)?.ParentId);
        var home = UI.Button("라이브러리", () => Navigate(null)); home.Background = Brushes.Transparent; home.Padding = new Thickness(8, 4, 8, 4); crumbs.Children.Add(home);
        SetupDropTarget(home, null);
        var chain = new List<Entry>(); var cursor = library.Find(current);
        while (cursor != null) { chain.Insert(0, cursor); cursor = library.Find(cursor.ParentId); }
        // The full breadcrumb is available as a tooltip when the hierarchy grows.
        foreach (var folder in chain.TakeLast(3))
        {
            var separator = UI.Text("/", 12, UI.Muted); separator.VerticalAlignment = VerticalAlignment.Center; crumbs.Children.Add(separator);
            var b = UI.Button(folder.Name.Length > 15 ? folder.Name[..15] + "…" : folder.Name, () => Navigate(folder.Id)); b.Padding = new Thickness(8, 4, 8, 4); b.Background = Brushes.Transparent; b.ToolTip = library.Trail(folder.Id); crumbs.Children.Add(b);
            SetupDropTarget(b, folder.Id);
        }
        RenderCards();
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
    internal void RenderCards()
    {
        cards.Children.Clear(); string query = search.Text.Trim();
        var entries = query.Length == 0 ? library.Children(current) : library.Entries.Where(e => (e.Name + " " + e.Target + " " + e.Note + " " + library.Trail(e.ParentId)).Contains(query, StringComparison.OrdinalIgnoreCase));
        var list = entries.ToList(); heading.Text = query.Length > 0 ? "검색 결과" : library.Find(current)?.Name ?? "라이브러리";
        subtitle.Text = query.Length > 0 ? $"전체 라이브러리에서 {list.Count}개 항목" : $"{list.Count(x => x.IsFolder)}개 폴더 · {list.Count(x => !x.IsFolder)}개 바로가기";
        foreach (var e in list)
        {
            var card = new Button { Width = 148, Height = 160, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(12), Background = UI.Brush(library.Theme.Surface), Foreground = UI.Brush(library.Theme.Text), ToolTip = e.IsFolder ? library.Trail(e.Id) : e.Target, Tag = e.Id };
            var stack = new StackPanel();
            var frame = new Border { Width = 70, Height = 70, CornerRadius = new CornerRadius(16), Background = UI.Brush("#213247"), Margin = new Thickness(0, 0, 0, 12) };
            var icon = EntryIcon(e);
            frame.Child = icon != null ? new Image { Source = icon, Width = 48, Height = 48, Stretch = Stretch.Uniform } : UI.Text("↗", 36, UI.Accent);
            stack.Children.Add(frame);
            stack.Children.Add(new TextBlock { Text = e.Name, FontSize = 13, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 124 });
            string label = query.Length > 0 ? library.Trail(e.ParentId) : e.IsFolder ? $"{library.Children(e.Id).Count()}개 항목" : Directory.Exists(e.Target) ? "실제 폴더" : Path.GetExtension(e.Target).TrimStart('.').ToUpperInvariant();
            stack.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = UI.Muted, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 6, 0, 0), MaxWidth = 124 });
            var cardContent = new Grid();
            if (e.Background.ImageFile != null)
            {
                try { var bitmap = Native.LoadImage(store.IconPath(e.Background.ImageFile), 512); var image = new Image { Source=bitmap, Stretch=Stretch.UniformToFill, Opacity=1-e.Background.Darkness }; var crop = BackgroundTools.ImageRect(((System.Windows.Media.Imaging.BitmapSource)bitmap).PixelWidth,((System.Windows.Media.Imaging.BitmapSource)bitmap).PixelHeight,e.Background,new Rect(0,0,124,136)); image.Width=crop.Width;image.Height=crop.Height;var canvas=new Canvas { Width=124,Height=136,Clip=new RectangleGeometry(new Rect(0,0,124,136),9,9) };Canvas.SetLeft(image,crop.X);Canvas.SetTop(image,crop.Y);canvas.Children.Add(image);cardContent.Children.Add(canvas); } catch { }
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
        emptyTitle.Text = query.Length > 0 ? "검색 결과가 없습니다" : current == null ? "자주 쓰는 파일을 한곳에" : "이 폴더를 채워 보세요";
        emptyDescription.Text = query.Length > 0 ? "다른 이름이나 경로로 검색해 보세요." : "실행 파일이나 바로가기를 끌어다 놓으세요.\n용도별 폴더로 묶고, 원하는 이미지로 꾸밀 수 있어요.";
        footer.Text = "카드 가장자리: 순서 변경 · 폴더 가운데: 안으로 넣기 · 위쪽 경로: 꺼내기 · 우클릭: 칸 배경";
    }
    private void SetupCardDropTarget(Button target, Entry anchor)
    {
        target.AllowDrop = true;
        (bool Into, bool After) Location(DragEventArgs e) { double x=e.GetPosition(target).X; return (anchor.IsFolder && x >= 30 && x <= target.ActualWidth-30, x > target.ActualWidth/2); }
        void Clear() { target.BorderThickness=new Thickness(0); target.Background=UI.Brush(library.Theme.Surface); target.ToolTip=anchor.IsFolder?library.Trail(anchor.Id):anchor.Target; }
        void Over(DragEventArgs e)
        {
            var location=Location(e); var ids=e.Data.GetData(EntryDragFormat) as string[];
            bool allowed=ids != null && ids.All(id=>library.Find(id) is Entry moving && moving!=anchor && library.CanMove(moving,location.Into?anchor.Id:anchor.ParentId));
            bool files=anchor.IsFolder && e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effects=allowed?DragDropEffects.Move:files?DragDropEffects.Copy:DragDropEffects.None; e.Handled=true;
            target.BorderBrush=UI.Brush(library.Theme.Accent); target.BorderThickness=allowed ? location.Into?new Thickness(2):location.After?new Thickness(0,0,5,0):new Thickness(5,0,0,0) : new Thickness(0);
            target.Background=allowed?library.Theme.Blend(library.Theme.Surface,library.Theme.Accent,.24):UI.Brush(library.Theme.Surface);
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
            if (allowed || files) { original ??= target.Background; target.Background = UI.Brush("#2F6871"); }
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
        var menu = new ContextMenu(); menu.Items.Add(UI.Menu("테마 / 색상 / 배치…", CustomizeTheme)); menu.Items.Add(UI.Menu("전체 배경 이미지…", CustomizeBackground)); menu.IsOpen = true;
    }
    private void CustomizeTheme()
    {
        var dialog = new ThemeWindow(store, library) { Owner = this };
        if (dialog.ShowDialog() == true) { library.Theme = dialog.Result!; library.CustomThemes = dialog.SavedThemesResult!; Commit(); }
    }
    private void CustomizeSlot(Entry entry)
    {
        var dialog = new BackgroundWindow(store, library, entry) { Owner = this };
        if (dialog.ShowDialog() == true) { entry.Background = dialog.Result!; Commit(); }
    }
    private void CustomizeBackground()
    {
        var dialog = new BackgroundWindow(store, library) { Owner = this };
        if (dialog.ShowDialog() == true) { library.Background = dialog.Result!; Commit(); }
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
    private void RegisterInto(IEnumerable<string> paths, string? parent)
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
    }
    private void AddFiles()
    {
        var dialog = new OpenFileDialog { Title = "등록할 파일 또는 바로가기 선택", Filter = "실행 파일 및 바로가기|*.exe;*.lnk;*.url;*.bat;*.cmd|모든 파일|*.*", FilterIndex = 2, Multiselect = true };
        if (dialog.ShowDialog(this) == true) RegisterPaths(dialog.FileNames);
    }
    private void AddFolder()
    {
        var entry = new Entry { Name = "새 폴더", IsFolder = true, ParentId = current };
        var dialog = new EditorWindow(store, entry) { Owner = this };
        if (dialog.ShowDialog() == true) { library.Entries.Add(dialog.Result!); search.Clear(); Commit(); }
    }
    private void Edit(Entry e)
    {
        var dialog = new EditorWindow(store, e) { Owner = this };
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
    private void ShowSettings() { var menu = SettingsMenu(); menu.IsOpen = true; }
    internal ContextMenu SettingsMenu()
    {
        var menu = new ContextMenu();
        var wheel = new MenuItem { Header = "휠로 다이얼 돌리기", IsCheckable = true, IsChecked = library.WheelDialEnabled };
        wheel.Click += (_, _) => { library.WheelDialEnabled = wheel.IsChecked; Commit(); }; menu.Items.Add(wheel);
        var hide = new MenuItem { Header = "실행 후 런처 숨기기", IsCheckable = true, IsChecked = library.HideAfterLaunch };
        hide.Click += (_, _) => { library.HideAfterLaunch = hide.IsChecked; Commit(); }; menu.Items.Add(hide);
        menu.Items.Add(UI.Menu("테마 / 색상 / 배치…", CustomizeTheme)); menu.Items.Add(UI.Menu("런처 배경 꾸미기…", CustomizeBackground));
        menu.Items.Add(UI.Menu("파일 경로 / 링크 직접 등록…", () =>
        {
            var dialog = new EditorWindow(store, new Entry { Name = "새 바로가기", ParentId = current }) { Owner = this };
            if (dialog.ShowDialog() == true) { library.Entries.Add(dialog.Result!); Commit(); }
        }));
        menu.Items.Add(new Separator()); menu.Items.Add(UI.Menu("라이브러리 백업…", Export)); menu.Items.Add(UI.Menu("백업 복원…", Import));
        menu.Items.Add(UI.Menu("저장 폴더 열기", () => Process.Start(new ProcessStartInfo(store.DirectoryPath) { UseShellExecute = true })));
        menu.Items.Add(UI.Menu("작업표시줄 고정 방법", () => MessageBox.Show(this, "1. 이 창이 열려 있는 상태에서 작업표시줄의 런처 아이콘을 우클릭합니다.\n2. ‘작업 표시줄에 고정’을 선택합니다.\n\n고정 후에는 아이콘 하나로 라이브러리를 열 수 있습니다.\n실행파일 폴더는 고정 후에도 같은 위치에 보관해 주세요.\n\n파일 등록: 끌어다 놓기 또는 ‘파일 등록’\n가상 폴더: ‘＋ 폴더’ → 항목 우클릭 → ‘폴더로 이동’\n아이콘 변경: 항목 우클릭 → ‘편집 / 아이콘 변경’\n\n실제 폴더도 끌어다 놓으면 탐색기로 여는 바로가기가 됩니다.", "사용 안내")));
        menu.Items.Add(new Separator()); menu.Items.Add(UI.Menu("런처 종료", () => Application.Current.Shutdown())); return menu;
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { Title = "아이콘을 포함하여 백업", Filter = "런처 백업|*.zip", FileName = "DeckLauncher-backup-" + DateTime.Now.ToString("yyyyMMdd") + ".zip" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            store.Backup(dialog.FileName, library);
            MessageBox.Show(this, "등록 정보와 사용자 지정 아이콘을 백업했습니다.", "백업 완료");
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
            library = store.Restore(backup); current = null; search.Clear(); iconCache.Clear(); Refresh();
        }
        catch (Exception ex) { UI.Error(ex); }
    }
}

public sealed class EditorWindow : Window
{
    private readonly Store store;
    private readonly Entry original;
    private readonly TextBox name, target, arguments, working, note;
    private readonly Image preview = new() { Width = 64, Height = 64, Margin = new Thickness(0, 0, 20, 0) };
    private string? pickedIcon;
    private string? existingIcon;
    public Entry? Result { get; private set; }
    public EditorWindow(Store store, Entry original)
    {
        this.store = store; this.original = original; existingIcon = original.IconFile;
        UI.Theme(this); Title = original.IsFolder ? "폴더 편집" : "바로가기 편집"; Width = 580; Height = original.IsFolder ? 510 : 790;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(26) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(UI.Text(Title, 23));
        name = Field(panel, "이름", original.Name);
        target = Field(panel, "파일 경로 또는 URL", original.Target); arguments = Field(panel, "실행 인수 (선택)", original.Arguments); working = Field(panel, "작업 폴더 (선택)", original.WorkingDirectory);
        if (original.IsFolder) { target.Visibility = arguments.Visibility = working.Visibility = Visibility.Collapsed; foreach (var t in panel.Children.OfType<TextBlock>().Skip(2).ToArray()) t.Visibility = Visibility.Collapsed; }
        else
        {
            var browse = UI.Button("파일 찾아보기…", () => { var d = new OpenFileDialog { Filter = "모든 파일|*.*" }; if (d.ShowDialog(this) == true) { target.Text = d.FileName; UpdatePreview(); } }); panel.Children.Insert(4, browse);
            target.TextChanged += (_, _) => UpdatePreview();
            var hint = UI.Text("실제 폴더 경로, https:// 주소, steam:// 링크도 등록할 수 있습니다.", 11, UI.Muted); hint.Margin = new Thickness(0, 9, 0, 0); panel.Children.Add(hint);
        }
        note = Field(panel, "마우스를 올렸을 때 표시할 메모", original.Note);
        note.Height = 78; note.AcceptsReturn = true; note.TextWrapping = TextWrapping.Wrap; note.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; note.MaxLength = 300; note.ToolTip = "최대 300자. 실행 화면 가운데에 표시됩니다.";
        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 15) }; iconRow.Children.Add(preview);
        var iconActions = new StackPanel(); iconActions.Children.Add(UI.Button("이미지 선택…", PickIcon)); iconActions.Children.Add(UI.Button("기본 아이콘 사용", () => { pickedIcon = null; existingIcon = null; UpdatePreview(); })); iconRow.Children.Add(iconActions); panel.Children.Add(iconRow);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = UI.Button("취소", () => DialogResult = false); cancel.IsCancel = true; row.Children.Add(cancel);
        var save = UI.Button("저장", Save); save.IsDefault = true; save.Background = UI.Brush("#285D67"); row.Children.Add(save); panel.Children.Add(row);
        Loaded += (_, _) => { UpdatePreview(); name.Focus(); name.SelectAll(); };
    }
    private static TextBox Field(Panel panel, string label, string value)
    {
        var text = UI.Text(label, 11, UI.Muted); text.Margin = new Thickness(0, 15, 0, 5); panel.Children.Add(text);
        var box = new TextBox { Text = value }; panel.Children.Add(box); return box;
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
        string path = target.Text.Trim().Trim('"');
        if (!original.IsFolder && path.Length == 0) { MessageBox.Show(this, "실행할 파일 경로나 링크를 입력해 주세요."); target.Focus(); return; }
        try
        {
            Result = new Entry { Id = original.Id, ParentId = original.ParentId, IsFolder = original.IsFolder, Name = name.Text.Trim(), Target = path, Arguments = arguments.Text, WorkingDirectory = working.Text.Trim().Trim('"'), Note = note.Text.Trim(), IconFile = pickedIcon != null ? store.CopyIcon(pickedIcon) : existingIcon, Background = original.Background.Copy() };
            DialogResult = true;
        }
        catch (Exception ex) { UI.Error(ex); }
    }
}

internal sealed class MoveWindow : Window
{
    private sealed record Destination(string? Id, string Label) { public override string ToString() => Label; }
    private readonly ComboBox combo = new() { Margin = new Thickness(0, 20, 0, 25) };
    public string? SelectedParent => (combo.SelectedItem as Destination)?.Id;
    public MoveWindow(Library library, Entry entry)
    {
        UI.Theme(this); Title = "폴더로 이동"; Width = 520; Height = 255; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(25) }; panel.Children.Add(UI.Text("‘" + entry.Name + "’ 이동", 20));
        combo.Items.Add(new Destination(null, "라이브러리 (맨 위)"));
        foreach (var folder in library.Entries.Where(e => e.IsFolder && library.CanMove(entry, e.Id))) combo.Items.Add(new Destination(folder.Id, library.Trail(folder.Id)));
        combo.SelectedItem = combo.Items.Cast<Destination>().FirstOrDefault(x => x.Id == entry.ParentId); panel.Children.Add(combo);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; var cancel = UI.Button("취소", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        var move = UI.Button("이동", () => DialogResult = true); move.IsDefault = true; actions.Children.Add(move); panel.Children.Add(actions); Content = panel;
    }
}
