using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DeckLauncher;

public sealed class ThemeWindow : Window
{
    private readonly LauncherWindow preview;
    private readonly StackPanel controls = new();
    private readonly Grid selectors = new();
    private readonly List<Button> tabs = new();
    private readonly List<TextBox> colorBoxes = new();
    private readonly List<SavedTheme> saved;
    private readonly TextBlock status = UI.Text("", 12, UI.Muted);
    private ComboBox? savedChoice;
    private TextBox? customName;
    private string? savedId;
    private bool modified;
    internal ThemeSettings Draft { get; private set; }
    internal int CustomCount => saved.Count;
    internal IReadOnlyList<SavedTheme> SavedDraft => saved;
    public ThemeSettings? Result { get; private set; }
    public List<SavedTheme>? SavedThemesResult { get; private set; }

    public ThemeWindow(Store store, Library library)
    {
        Draft = library.Theme.Copy(); saved = library.CustomThemes.Select(t => t.Copy()).ToList();
        savedId = saved.FirstOrDefault(t => t.Settings.Name == Draft.Name)?.Id;
        UI.Theme(this, library.Theme); Title = "테마와 디자인 꾸미기"; Width = 1140; Height = 840;
        MinWidth = 930; MinHeight = 660; ShowInTaskbar = false; Topmost = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 24); Height = Math.Min(Height, SystemParameters.WorkArea.Height - 30);
        var main = new Grid { Margin = new Thickness(24) };
        main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new() { Height = GridLength.Auto });
        main.RowDefinitions.Add(new()); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = main;
        var heading = UI.Heading("색상 · UI 디자인", 25); main.Children.Add(heading);

        var selectorArea = new StackPanel { Margin = new Thickness(0, 13, 0, 16) }; Grid.SetRow(selectorArea, 1); main.Children.Add(selectorArea);
        var categories = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) }; selectorArea.Children.Add(categories);
        foreach (var item in new[] { ($"UI 디자인 · {ThemeSettings.DesignPresets.Count}", 1), ($"색상 팔레트 · {ThemeSettings.Presets.Count}", 0), ("내 커스텀 테마", 2) })
        {
            int category = item.Item2; var tab = UI.Button(item.Item1, () => SelectCategory(category)); tab.Tag = category; tabs.Add(tab); categories.Children.Add(tab);
        }
        var entries = library.Entries.Count == 0 ? new List<Entry> { new() { Name = "내 폴더", IsFolder = true }, new() { Name = "바로가기" }, new() { Name = "메모" } } : library.Entries;
        selectors.Height = 138; selectorArea.Children.Add(selectors);
        AddPresetTiles(store, ThemeSettings.Presets, 5, 0, entries); AddPresetTiles(store, ThemeSettings.DesignPresets, 6, 1, entries);
        selectors.Children.Add(new Grid()); BuildSavedSelector();

        var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new GridLength(300) }); Grid.SetRow(body, 2); main.Children.Add(body);
        preview = new LauncherWindow(store, new Library { Entries = entries, Theme = Draft, Background = library.Background.Copy() }, true);
        var sample = new Grid { Margin = new Thickness(0, 0, 20, 0) }; sample.RowDefinitions.Add(new()); sample.RowDefinitions.Add(new() { Height = GridLength.Auto });
        sample.Children.Add(UI.Res(new Border { CornerRadius = new CornerRadius(18), Padding = new Thickness(8), Child = preview.DetachPreview() }, Border.BackgroundProperty, "SkinPanel"));
        var hint = UI.Text("실제 런처 미리보기 · 디자인과 색상은 서로 독립이라 자유롭게 조합할 수 있어요", 12.5, UI.Muted); hint.Margin = new Thickness(10, 12, 0, 4); Grid.SetRow(hint, 1); sample.Children.Add(hint); body.Children.Add(sample);
        var scroll = new ScrollViewer { Content = controls, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetColumn(scroll, 1); body.Children.Add(scroll);
        BuildControls();
        var footer = new DockPanel { Margin = new Thickness(0, 15, 0, 0) }; Grid.SetRow(footer, 3); main.Children.Add(footer);
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right);
        var cancel = UI.Button("취소", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        var apply = UI.Button("적용", Apply); apply.IsDefault = true; apply.MinWidth = 84; apply.FontWeight = FontWeights.SemiBold; UI.Res(apply, Control.BackgroundProperty, "SkinAccent"); UI.Res(apply, Control.ForegroundProperty, "SkinAccentText"); actions.Children.Add(apply); footer.Children.Add(actions); footer.Children.Add(status);
        SelectCategory(savedId != null ? 2 : 1); Update();
    }
    private void AddPresetTiles(Store store, IReadOnlyList<ThemeSettings> presets, int columns, int category, List<Entry> entries)
    {
        var grid = new UniformGrid { Columns = columns };
        foreach (var preset in presets)
        {
            var content = new StackPanel { Margin = new Thickness(2, 2, 2, 3) };
            if (category == 0)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                foreach (var color in new[] { preset.Background, preset.Surface, preset.Accent }) row.Children.Add(new System.Windows.Shapes.Ellipse { Width = 15, Height = 15, Fill = UI.Brush(color), Stroke = UI.Brush(preset.Text), StrokeThickness = .6, Margin = new Thickness(2, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center });
                var label = UI.Text(preset.Name, 12.5, UI.Brush(preset.Text)); label.Margin = new Thickness(8, 0, 0, 1); label.TextWrapping = TextWrapping.NoWrap; label.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(label);
                content.Children.Add(row);
            }
            else
            {
                // A real miniature of the design in its own recommended palette.
                var mini = new LauncherWindow(store, new Library { Entries = entries, Theme = preset.Copy() }, true);
                content.Children.Add(new Viewbox { Height = 86, Stretch = Stretch.Uniform, Child = mini.DetachPreview(), IsHitTestVisible = false });
                var name = UI.Text(preset.Name, 13, UI.Brush(preset.Text)); name.FontWeight = FontWeights.SemiBold; name.TextAlignment = TextAlignment.Center; name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis; name.Margin = new Thickness(0, 4, 0, 0); content.Children.Add(name);
            }
            var tile = UI.Button("", () => UsePreset(preset)); tile.Tag="keep-colors"; tile.Background = UI.Brush(preset.Background); tile.Content = content; tile.ToolTip = category == 1 ? preset.Name + " · 디자인만 바꾸고 지금 색상은 유지해요" : preset.Name; tile.Padding = new Thickness(6, category == 0 ? 10 : 6, 6, 6); grid.Children.Add(tile);
        }
        selectors.Children.Add(grid);
    }
    internal void SelectCategory(int category)
    {
        for (int i = 0; i < selectors.Children.Count; i++) selectors.Children[i].Visibility = i == category ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tab in tabs)
        {
            bool active = (int)tab.Tag == category; tab.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            if (active) { UI.Res(tab, Control.BackgroundProperty, "SkinAccent"); UI.Res(tab, Control.ForegroundProperty, "SkinAccentText"); }
            else { UI.Res(tab, Control.BackgroundProperty, "SkinButton"); UI.Res(tab, Control.ForegroundProperty, "SkinText"); }
        }
    }
    private void BuildSavedSelector()
    {
        if (selectors.Children.Count < 3) return;
        var host = (Grid)selectors.Children[2]; host.Children.Clear();
        var panel = new StackPanel { Margin = new Thickness(4, 0, 4, 0) }; host.Children.Add(panel);
        panel.Children.Add(UI.Text("기본 테마와 별도로 이름을 붙여 보관하는 나만의 테마", 12, UI.Muted));
        var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) }; panel.Children.Add(row);
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right);
        actions.Children.Add(UI.Button("＋ 새 커스텀", NewCustom)); var remove = UI.Button("선택 테마 삭제", DeleteSaved); remove.IsEnabled = savedId != null; actions.Children.Add(remove); row.Children.Add(actions);
        savedChoice = new ComboBox { ItemsSource = saved.ToList(), SelectedItem = saved.FirstOrDefault(t => t.Id == savedId), MinWidth = 230, Margin = new Thickness(0, 3, 14, 3), ToolTip = "저장한 커스텀 테마 선택" }; row.Children.Add(savedChoice);
        savedChoice.SelectionChanged += (_, _) => { if (savedChoice.SelectedItem is SavedTheme selected) UseSaved(selected.Id); };
        if (saved.Count == 0) panel.Children.Add(UI.Text("아직 저장된 테마가 없어요. ‘새 커스텀’으로 만들어 보세요.", 11, UI.Muted));
    }
    /// <summary>A design tile changes only the layout; a palette tile changes only the colors.</summary>
    internal void UsePreset(ThemeSettings settings)
    {
        var previous=Draft.Copy(); bool design = ThemeSettings.DesignPresets.Any(d => d.Name == settings.Name);
        if(!design)
        {
            Draft=previous;Draft.Name=settings.Name;Draft.Background=settings.Background;Draft.Surface=settings.Surface;Draft.Accent=settings.Accent;Draft.Text=settings.Text;
        }
        else
        {
            Draft=settings.Copy();Draft.Background=previous.Background;Draft.Surface=previous.Surface;Draft.Accent=previous.Accent;Draft.Text=previous.Text;
        }
        savedId = null; modified = false; BuildSavedSelector(); BuildControls(); SelectCategory(design ? 1 : 0); Update();
    }
    /// <summary>Applies the palette the current design was drawn with.</summary>
    internal void UseRecommendedColors()
    {
        var match = ThemeSettings.DesignPresets.FirstOrDefault(d => d.Design == Draft.Design); if (match == null) return;
        Draft.Background = match.Background; Draft.Surface = match.Surface; Draft.Accent = match.Accent; Draft.Text = match.Text;
        BuildControls(); Changed();
    }
    internal void UseSaved(string id)
    {
        var selected = saved.FirstOrDefault(t => t.Id == id); if (selected == null) return;
        savedId = id; Draft = selected.Settings.Copy(); Draft.Name = selected.Name; modified = false;
        BuildSavedSelector(); BuildControls(); SelectCategory(2); Update();
    }
    internal void NewCustom()
    {
        savedId = null; Draft = Draft.Copy(); Draft.Name = NewName(); modified = true;
        BuildSavedSelector(); BuildControls(); SelectCategory(2); Update();
    }
    private string NewName()
    {
        int index = 1; while (saved.Any(t => t.Name == "나의 테마 " + index)) index++;
        return "나의 테마 " + index;
    }
    private void DeleteSaved()
    {
        if (savedId == null) return; saved.RemoveAll(t => t.Id == savedId); savedId = null;
        Draft = ThemeSettings.Presets[0].Copy(); modified = false; BuildSavedSelector(); BuildControls(); SelectCategory(2); Update();
    }
    internal bool SaveCustomNamed(string name)
    {
        customName!.Text = name; return SaveCustom();
    }
    private bool ColorsValid()
    {
        if (colorBoxes.All(t => ThemeSettings.ValidColor(t.Text))) return true;
        status.Text = "색상을 #RRGGBB 형식으로 입력해 주세요."; return false;
    }
    private bool SaveCustom()
    {
        if (!ColorsValid()) return false;
        string name = customName?.Text.Trim() ?? "";
        if (name.Length == 0) { status.Text = "커스텀 테마 이름을 입력해 주세요."; return false; }
        if (saved.Any(t => t.Name == name && t.Id != savedId)) { status.Text = "같은 이름의 테마가 있어요. 다른 이름을 입력해 주세요."; return false; }
        Draft.Validate(); Draft.Name = name;
        var existing = saved.FirstOrDefault(t => t.Id == savedId);
        if (existing == null) { existing = new SavedTheme(); saved.Add(existing); savedId = existing.Id; }
        existing.Name = name; existing.Settings = Draft.Copy(); modified = false;
        BuildSavedSelector(); SelectCategory(2); Update(); status.Text = "‘" + name + "’ 보관됨 · 적용하면 저장됩니다."; return true;
    }
    private void Apply()
    {
        try
        {
            if (!ColorsValid()) return;
            if ((modified || savedId != null) && !SaveCustom()) return;
            Draft.Validate(); Result = Draft.Copy(); SavedThemesResult = saved.Select(t => t.Copy()).ToList(); DialogResult = true;
        }
        catch (Exception ex) { UI.Error(ex); }
    }
    private void Changed()
    {
        modified = true; Draft.Name = customName?.Text.Trim() ?? "나의 테마"; Update();
    }
    private void Update() { Skin.Palette(this,Draft); Skin.Connect(this); preview.UpdateTheme(Draft); status.Text = Draft.Name + " · " + Draft.Slots + "칸"; }
    private void BuildControls()
    {
        controls.Children.Clear(); colorBoxes.Clear(); var label = UI.Heading("나만의 색상과 배치", 17); controls.Children.Add(label);
        var designLabel = UI.Text("디자인 스타일", 12, UI.Muted); designLabel.Margin = new Thickness(0, 13, 0, 5); controls.Children.Add(designLabel);
        var styles = ThemeSettings.DesignPresets.Select(d => d.Design).ToArray();
        var design = new ComboBox { ItemsSource = ThemeSettings.DesignPresets.Select(d => d.Name).ToArray(), SelectedIndex = Array.IndexOf(styles, Draft.Design) }; controls.Children.Add(design);
        design.SelectionChanged += (_, _) => { if (design.SelectedIndex >= 0 && Draft.Design != styles[design.SelectedIndex]) { Draft.Design = styles[design.SelectedIndex]; Changed(); Dispatcher.BeginInvoke(new Action(BuildControls)); } };
        var recommended = UI.IconButton("\uE790", "이 디자인의 추천 색상 쓰기", UseRecommendedColors); recommended.HorizontalAlignment = HorizontalAlignment.Left; recommended.Margin = new Thickness(0, 8, 0, 0); recommended.FontSize = 13; controls.Children.Add(recommended);
        void ColorRow(string name, string value, Action<string> assign)
        {
            controls.Children.Add(new TextBlock { Text = name, Foreground = UI.Muted, FontSize = 12, Margin = new Thickness(0, 14, 0, 5) });
            var row = new DockPanel(); var box = new TextBox { Text = value, MaxLength = 7, ToolTip = "#RRGGBB 형식의 색상" };
            var pick = UI.Button("●", () => { var dialog = new ColorWindow(ThemeSettings.ValidColor(box.Text) ? box.Text : value,Draft) { Owner = this }; if (dialog.ShowDialog() == true) box.Text = dialog.Result; });
            pick.Foreground = UI.Brush(value); pick.Padding = new Thickness(13, 7, 13, 7); DockPanel.SetDock(pick, Dock.Right); row.Children.Add(pick); row.Children.Add(box); controls.Children.Add(row); colorBoxes.Add(box);
            box.TextChanged += (_, _) => { if (!ThemeSettings.ValidColor(box.Text)) { box.BorderBrush = UI.Brush("#EF8EAB"); return; } box.BorderBrush = UI.Brush("#3B526D"); pick.Foreground = UI.Brush(box.Text); assign(box.Text); Changed(); };
        }
        ColorRow("런처 바탕", Draft.Background, x => Draft.Background = x); ColorRow("칸 / 아이콘 바탕", Draft.Surface, x => Draft.Surface = x);
        ColorRow("포인트 / 강조", Draft.Accent, x => Draft.Accent = x); ColorRow("글자", Draft.Text, x => Draft.Text = x);
        controls.Children.Add(new TextBlock { Text = "아이콘 모양", Foreground = UI.Muted, Margin = new Thickness(0, 17, 0, 5) });
        var shape = new ComboBox { ItemsSource = new[] { "둥근 사각형", "동그라미", "미니멀" }, SelectedIndex = Draft.Frame == "circle" ? 1 : Draft.Frame == "minimal" ? 2 : 0 }; controls.Children.Add(shape);
        shape.SelectionChanged += (_, _) => { Draft.Frame = new[] { "rounded", "circle", "minimal" }[shape.SelectedIndex]; Changed(); };
        void Slider(string name, double min, double max, double value, Action<double> assign, bool integral = false)
        {
            var text = UI.Text(name + "  " + value.ToString("0.#"), 12, UI.Muted); text.Margin = new Thickness(0, 17, 0, 6); controls.Children.Add(text);
            var slider = new System.Windows.Controls.Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = integral ? 1 : .1, IsSnapToTickEnabled = integral, Margin = new Thickness(0, 4, 10, 0) }; controls.Children.Add(slider);
            slider.ValueChanged += (_, _) => { assign(slider.Value); text.Text = name + "  " + slider.Value.ToString("0.#"); Changed(); };
        }
        Slider("한 번에 보이는 칸", 5, 9, Draft.Slots, x => Draft.Slots = (int)x, true); Slider("아이콘 크기", 28, 48, Draft.IconSize, x => Draft.IconSize = x, true);
        Slider("아이콘의 바깥쪽 위치", 280, 315, Draft.Radius, x => Draft.Radius = x, true); Slider("칸 사이 여백", .3, 2.5, Draft.Gap, x => Draft.Gap = x);
        var labels = new CheckBox { Content = "아이콘 이름 표시", IsChecked = Draft.ShowLabels }; controls.Children.Add(labels); labels.Click += (_, _) => { Draft.ShowLabels = labels.IsChecked == true; Changed(); };
        var notes = new CheckBox { Content = "중앙 메모 표시", IsChecked = Draft.ShowNotes }; controls.Children.Add(notes); notes.Click += (_, _) => { Draft.ShowNotes = notes.IsChecked == true; Changed(); };
        controls.Children.Add(new TextBlock { Text = "커스텀 테마 이름", Foreground = UI.Muted, Margin = new Thickness(0, 17, 0, 5) });
        customName = new TextBox { Text = savedId != null || modified ? Draft.Name : NewName(), MaxLength = 40, ToolTip = "커스텀 테마 이름" }; controls.Children.Add(customName);
        customName.TextChanged += (_, _) => { modified = true; Draft.Name = customName.Text.Trim(); status.Text = "커스텀 이름: " + Draft.Name; };
        controls.Children.Add(UI.Button("내 커스텀 테마에 보관", () => { try { SaveCustom(); } catch (Exception ex) { UI.Error(ex); } }));
        var explanation = UI.Text("적용할 때 이름과 함께 별도 목록에 저장됩니다. 취소하면 변경을 되돌립니다.", 11, UI.Muted); explanation.Margin = new Thickness(0, 7, 7, 14); controls.Children.Add(explanation);
    }
}
