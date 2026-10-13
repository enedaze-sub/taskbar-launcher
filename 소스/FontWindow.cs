using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DeckLauncher;

/// <summary>Pick the launcher font and the settings font separately, with a live launcher preview.</summary>
public sealed class FontWindow : Window
{
    private readonly FontSettings draft;
    private readonly LauncherWindow preview;
    private readonly ListBox list = new() { Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBox search = new() { Padding = new Thickness(34, 8, 10, 8), ToolTip = "글꼴 이름 검색" };
    private readonly TextBlock targetHint = UI.Text("", 13, UI.Muted);
    private readonly TextBlock launcherName = UI.Text("", 13), interfaceName = UI.Text("", 13);
    private readonly Border sample = new() { CornerRadius = new CornerRadius(16), Padding = new Thickness(20, 18, 20, 18) };
    private readonly List<Button> tabs = new();
    private bool editingLauncher = true, filling;
    public FontSettings? Result { get; private set; }
    internal FontSettings Draft => draft;

    public FontWindow(Store store, Library library)
    {
        draft = (library.Fonts ?? new()).Copy();
        UI.Theme(this, library.Theme); Title = "글꼴"; Width = 1140; Height = 800; MinWidth = 900; MinHeight = 600;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 24); Height = Math.Min(Height, SystemParameters.WorkArea.Height - 30);
        ShowInTaskbar = false; Topmost = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var main = new Grid { Margin = new Thickness(28, 22, 28, 20) }; main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new()); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = main;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) }; header.Children.Add(UI.Heading("글꼴", 25));
        var summary = UI.Text("런처와 설정 창의 글꼴을 따로 고를 수 있어요. 한글을 표시할 수 있는 글꼴만 보여요.", 14, UI.Muted); summary.Margin = new Thickness(0, 6, 0, 0); header.Children.Add(summary); main.Children.Add(header);
        var body = new Grid(); Grid.SetRow(body, 1); body.ColumnDefinitions.Add(new() { Width = new GridLength(370) }); body.ColumnDefinitions.Add(new()); main.Children.Add(body);

        var left = UI.Res(new Border { CornerRadius = new CornerRadius(18), Padding = new Thickness(16), Margin = new Thickness(0, 0, 20, 0) }, Border.BackgroundProperty, "SkinPanel"); body.Children.Add(left);
        var picker = new Grid(); picker.RowDefinitions.Add(new() { Height = GridLength.Auto }); picker.RowDefinitions.Add(new() { Height = GridLength.Auto }); picker.RowDefinitions.Add(new() { Height = GridLength.Auto }); picker.RowDefinitions.Add(new()); left.Child = picker;
        var segments = UI.Res(new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(4) }, Border.BackgroundProperty, "SkinControl");
        var segmentGrid = new Grid(); segmentGrid.ColumnDefinitions.Add(new()); segmentGrid.ColumnDefinitions.Add(new()); segments.Child = segmentGrid; picker.Children.Add(segments);
        foreach (var (label, launcher) in new[] { ("런처 글꼴", true), ("설정 창 글꼴", false) })
        {
            var tab = UI.Button(label, () => SelectTarget(launcher)); tab.Margin = new Thickness(0); tab.Padding = new Thickness(10, 8, 10, 8); Grid.SetColumn(tab, launcher ? 0 : 1); segmentGrid.Children.Add(tab); tabs.Add(tab);
        }
        targetHint.Margin = new Thickness(4, 12, 4, 0); targetHint.LineHeight = 19; Grid.SetRow(targetHint, 1); picker.Children.Add(targetHint);
        var searchWrap = new Grid { Margin = new Thickness(0, 12, 0, 0) }; Grid.SetRow(searchWrap, 2); searchWrap.Children.Add(search);
        var searchIcon = UI.Icon("\uE721", 14, "SkinMuted"); searchIcon.HorizontalAlignment = HorizontalAlignment.Left; searchIcon.Margin = new Thickness(13, 0, 0, 0); searchWrap.Children.Add(searchIcon);
        var placeholder = UI.Text("글꼴 검색", 13.5, UI.Muted); placeholder.IsHitTestVisible = false; placeholder.VerticalAlignment = VerticalAlignment.Center; placeholder.Margin = new Thickness(36, 0, 0, 1); searchWrap.Children.Add(placeholder);
        search.TextChanged += (_, _) => { placeholder.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Fill(); };
        picker.Children.Add(searchWrap);
        VirtualizingPanel.SetIsVirtualizing(list, true); ScrollViewer.SetCanContentScroll(list, true); Grid.SetRow(list, 3); picker.Children.Add(list);
        list.SelectionChanged += (_, _) => { if (!filling && list.SelectedItem is ListBoxItem { Tag: string key }) Choose(key); };

        var right = new Grid(); Grid.SetColumn(right, 1); right.RowDefinitions.Add(new() { Height = GridLength.Auto }); right.RowDefinitions.Add(new()); right.RowDefinitions.Add(new() { Height = GridLength.Auto }); right.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.Children.Add(right);
        var launcherCaption = Caption("런처 미리보기", launcherName); right.Children.Add(launcherCaption);
        var entries = library.Entries.Count == 0 ? new List<Entry> { new() { Name = "내 폴더", IsFolder = true }, new() { Name = "바로가기" }, new() { Name = "메모" } } : library.Entries;
        preview = new LauncherWindow(store, new Library { Entries = entries, Theme = library.Theme.Copy(), Background = library.Background.Copy() }, true);
        var stage = UI.Res(new Border { CornerRadius = new CornerRadius(18), Padding = new Thickness(10, 8, 10, 0), Margin = new Thickness(0, 8, 0, 16), Child = preview.DetachPreview() }, Border.BackgroundProperty, "SkinPanel"); Grid.SetRow(stage, 1); right.Children.Add(stage);
        var interfaceCaption = Caption("설정 창 미리보기", interfaceName); Grid.SetRow(interfaceCaption, 2); right.Children.Add(interfaceCaption);
        UI.Res(sample, Border.BackgroundProperty, "SkinPanel"); sample.Margin = new Thickness(0, 8, 0, 0); Grid.SetRow(sample, 3); right.Children.Add(sample); BuildSample();

        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) }; Grid.SetRow(footer, 2); main.Children.Add(footer);
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right);
        var cancel = UI.Button("취소", () => DialogResult = false); cancel.IsCancel = true; cancel.MinWidth = 84; actions.Children.Add(cancel);
        var apply = UI.Button("적용", Apply); apply.IsDefault = true; apply.MinWidth = 84; apply.FontWeight = FontWeights.SemiBold; UI.Res(apply, Control.BackgroundProperty, "SkinAccent"); UI.Res(apply, Control.ForegroundProperty, "SkinAccentText"); actions.Children.Add(apply); footer.Children.Add(actions);
        var reset = UI.IconButton("\uE72C", "모두 기본값으로", () => { draft.Launcher = ""; draft.Interface = ""; Update(); Fill(); }); reset.Background = Brushes.Transparent; reset.HorizontalAlignment = HorizontalAlignment.Left; footer.Children.Add(reset);
        SelectTarget(true); Update(); Loaded += (_, _) => Skin.Connect(this);
    }
    private static FrameworkElement Caption(string title, TextBlock value)
    {
        var row = new DockPanel(); var label = UI.Text(title, 13, UI.Muted); label.FontWeight = FontWeights.SemiBold; row.Children.Add(label);
        value.Margin = new Thickness(10, 0, 0, 0); value.SetResourceReference(TextBlock.ForegroundProperty, "SkinAccentInk"); value.FontWeight = FontWeights.SemiBold; value.TextTrimming = TextTrimming.CharacterEllipsis; row.Children.Add(value); return row;
    }
    private void BuildSample()
    {
        var stack = new StackPanel();
        var head = new TextBlock { Text = "런처 옵션", FontSize = 19, FontWeight = FontWeights.Bold }; head.SetResourceReference(TextBlock.ForegroundProperty, "SkinText"); stack.Children.Add(head);
        var sub = new TextBlock { Text = "동작 설정과 라이브러리 관리 · 가나다라마바사 ABC 123", FontSize = 13.5, Margin = new Thickness(0, 4, 0, 14), TextWrapping = TextWrapping.Wrap }; sub.SetResourceReference(TextBlock.ForegroundProperty, "SkinMuted"); stack.Children.Add(sub);
        var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new GridLength(50) }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var tile = UI.IconTile("\uE898", 38, 17, radius: 11); tile.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(tile);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(text, 1);
        var title = new TextBlock { Text = "라이브러리 백업", FontSize = 15, FontWeight = FontWeights.SemiBold }; title.SetResourceReference(TextBlock.ForegroundProperty, "SkinText"); text.Children.Add(title);
        var desc = new TextBlock { Text = "등록 정보, 아이콘, 배경을 ZIP으로 저장", FontSize = 12.5, Margin = new Thickness(0, 3, 0, 0) }; desc.SetResourceReference(TextBlock.ForegroundProperty, "SkinMuted"); text.Children.Add(desc); row.Children.Add(text);
        var button = UI.Button("적용", () => { }); button.IsHitTestVisible = false; button.Focusable = false; UI.Res(button, Control.BackgroundProperty, "SkinAccent"); UI.Res(button, Control.ForegroundProperty, "SkinAccentText"); button.FontWeight = FontWeights.SemiBold; Grid.SetColumn(button, 2); row.Children.Add(button);
        stack.Children.Add(row); sample.Child = stack;
    }
    internal void SelectTarget(bool launcher)
    {
        editingLauncher = launcher;
        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = (i == 0) == launcher; tabs[i].FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            if (active) { UI.Res(tabs[i], Control.BackgroundProperty, "SkinAccent"); UI.Res(tabs[i], Control.ForegroundProperty, "SkinAccentText"); }
            else { tabs[i].Background = Brushes.Transparent; UI.Res(tabs[i], Control.ForegroundProperty, "SkinText"); }
        }
        targetHint.Text = launcher ? "부채꼴 런처의 제목, 아이콘 이름, 가운데 메모에 쓰여요. 둥글고 개성 있는 글꼴이 잘 어울려요." : "런처 설정, 메뉴, 도움말, 편집 창에 쓰여요. 작은 글자도 또렷한 고딕 계열을 추천해요.";
        Fill();
    }
    private void Fill()
    {
        filling = true; list.Items.Clear(); string query = search.Text.Trim(); string selected = editingLauncher ? draft.Launcher : draft.Interface;
        var defaultFamily = editingLauncher ? FontCatalog.DefaultLauncher : FontCatalog.DefaultInterface;
        var recommended = editingLauncher ? new[] { "Cafe24 Ssurround", "Jua", "Gowun Dodum", "BM JUA_TTF", "Noto Sans KR" } : new[] { "Noto Sans KR", "Malgun Gothic", "NanumGothic", "Gowun Dodum" };
        var picks = FontCatalog.All.Where(c => recommended.Contains(c.Key)).OrderBy(c => Array.IndexOf(recommended, c.Key)).ToList();
        if (query.Length == 0)
        {
            list.Items.Add(Item("", editingLauncher ? "기본 글꼴 (카페24 써라운드 / 주아)" : "기본 글꼴 (Noto Sans KR)", "기본값", defaultFamily));
            foreach (var choice in picks) list.Items.Add(Item(choice.Key, choice.Display, "추천 · " + choice.Origin, choice.Family));
            list.Items.Add(SectionLabel("설치된 모든 글꼴"));
        }
        foreach (var choice in FontCatalog.All.Where(c => (query.Length == 0 && !picks.Contains(c)) || query.Length > 0 && (c.Display.Contains(query, StringComparison.OrdinalIgnoreCase) || c.Key.Contains(query, StringComparison.OrdinalIgnoreCase))))
            list.Items.Add(Item(choice.Key, choice.Display, choice.Origin, choice.Family));
        list.SelectedItem = list.Items.Cast<ListBoxItem>().FirstOrDefault(i => (string)i.Tag == selected);
        if (list.SelectedItem != null) list.ScrollIntoView(list.SelectedItem);
        filling = false;
    }
    private static ListBoxItem SectionLabel(string text)
    {
        var label = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) }; label.SetResourceReference(TextBlock.ForegroundProperty, "SkinMuted");
        return new ListBoxItem { Content = label, Tag = "@section", IsEnabled = false, Focusable = false, Cursor = null };
    }
    private static ListBoxItem Item(string key, string display, string origin, FontFamily family)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = display, FontFamily = family, FontSize = 17, TextTrimming = TextTrimming.CharacterEllipsis });
        var detail = new TextBlock { Text = origin + " · 가나다 ABC 123", FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0) }; detail.SetResourceReference(TextBlock.ForegroundProperty, "SkinMuted"); stack.Children.Add(detail);
        return new ListBoxItem { Content = stack, Tag = key, ToolTip = key.Length == 0 ? null : key };
    }
    internal void Choose(string key)
    {
        if (editingLauncher) draft.Launcher = key; else draft.Interface = key;
        Update();
    }
    private void Update()
    {
        var launcher = FontCatalog.Resolve(draft.Launcher, FontCatalog.DefaultLauncher); var settings = FontCatalog.Resolve(draft.Interface, FontCatalog.DefaultInterface);
        preview.FontOverride = launcher; preview.UpdateTheme(preview.library.Theme);
        sample.SetValue(TextElement.FontFamilyProperty, settings);
        launcherName.Text = Name(draft.Launcher, "기본"); interfaceName.Text = Name(draft.Interface, "기본");
    }
    private static string Name(string key, string fallback) => key.Length == 0 ? fallback : FontCatalog.All.FirstOrDefault(c => c.Key == key)?.Display ?? key;
    private void Apply()
    {
        try { draft.Validate(); Result = draft.Copy(); DialogResult = true; }
        catch (Exception ex) { UI.Error(ex); }
    }
}
