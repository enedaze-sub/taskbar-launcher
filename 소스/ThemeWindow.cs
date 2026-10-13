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

        var selectorArea = new StackPanel { Margin = new Thickness(0, 13, 0, 18) }; Grid.SetRow(selectorArea, 1); main.Children.Add(selectorArea);
        var categories = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) }; selectorArea.Children.Add(categories);
        foreach (var item in new[] { ("기본 색상 · 9", 0), ("새 디자인 · 4", 1), ("내 커스텀 테마", 2) })
        {
            int category = item.Item2; var tab = UI.Button(item.Item1, () => SelectCategory(category)); tabs.Add(tab); categories.Children.Add(tab);
        }
        selectors.Height = 104; selectorArea.Children.Add(selectors);
        AddPresetTiles(ThemeSettings.Presets, 9, 0); AddPresetTiles(ThemeSettings.DesignPresets, 4, 1);
        selectors.Children.Add(new Grid()); BuildSavedSelector();

        var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new GridLength(300) }); Grid.SetRow(body, 2); main.Children.Add(body);
        var entries = library.Entries.Count == 0 ? new List<Entry> { new() { Name = "내 폴더", IsFolder = true }, new() { Name = "바로가기" }, new() { Name = "메모" } } : library.Entries;
        preview = new LauncherWindow(store, new Library { Entries = entries, Theme = Draft, Background = library.Background.Copy() }, true);
        var sample = new Grid { Margin = new Thickness(0, 0, 20, 0) }; sample.RowDefinitions.Add(new()); sample.RowDefinitions.Add(new() { Height = GridLength.Auto });
        sample.Children.Add(new Border { Background = UI.Brush("#243041"), CornerRadius = new CornerRadius(18), Padding = new Thickness(8), Child = preview.DetachPreview() });
        var hint = UI.Text("실제 런처 미리보기 · 색상 / 디자인 / 배치를 바로 확인하세요", 12, UI.Muted); hint.Margin = new Thickness(10, 14, 0, 5); Grid.SetRow(hint, 1); sample.Children.Add(hint); body.Children.Add(sample);
        var scroll = new ScrollViewer { Content = controls, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetColumn(scroll, 1); body.Children.Add(scroll);
        BuildControls();
        var footer = new DockPanel { Margin = new Thickness(0, 15, 0, 0) }; Grid.SetRow(footer, 3); main.Children.Add(footer);
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right);
        var cancel = UI.Button("취소", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        var apply = UI.Button("적용", Apply); apply.IsDefault = true; apply.MinWidth = 84; apply.FontWeight = FontWeights.SemiBold; UI.Res(apply, Control.BackgroundProperty, "SkinAccent"); UI.Res(apply, Control.ForegroundProperty, "SkinAccentText"); actions.Children.Add(apply); footer.Children.Add(actions); footer.Children.Add(status);
        SelectCategory(savedId != null ? 2 : Draft.Design == "classic" ? 0 : 1); Update();
    }
    private void AddPresetTiles(IReadOnlyList<ThemeSettings> presets, int columns, int category)
    {
        var grid = new UniformGrid { Columns = columns };
        foreach (var preset in presets)
        {
            var content = new StackPanel { Margin = new Thickness(2, 5, 2, 5) };
            if (category == 0)
            {
                var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                foreach (var color in new[] { preset.Background, preset.Surface, preset.Accent }) dots.Children.Add(new System.Windows.Shapes.Ellipse { Width = 22, Height = 22, Fill = UI.Brush(color), Stroke = UI.Brush(preset.Text), StrokeThickness = .5, Margin = new Thickness(3) });
                content.Children.Add(dots);
            }
            else
            {
                var mark = UI.Text(preset.Design switch { "cat" => "=^·^=", "neon" => "✦", "paper" => "▤", _ => "▦" }, 24, UI.Brush(preset.Accent)); mark.TextAlignment = TextAlignment.Center; content.Children.Add(mark);
            }
            var name = UI.Text(preset.Name, 13, UI.Brush(preset.Text)); name.TextAlignment = TextAlignment.Center; name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis; name.Margin = new Thickness(0, 7, 0, 0); content.Children.Add(name);
            var tile = UI.Button("", () => UsePreset(preset)); tile.Tag="keep-colors"; tile.Background = UI.Brush(preset.Background); tile.Content = content; tile.ToolTip = preset.Name; grid.Children.Add(tile);
        }
        selectors.Children.Add(grid);
    }
    internal void SelectCategory(int category)
    {
        for (int i = 0; i < selectors.Children.Count; i++) selectors.Children[i].Visibility = i == category ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < tabs.Count; i++) tabs[i].Background = UI.Brush(i == category ? "#315B68" : "#223247");
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
    internal void UsePreset(ThemeSettings settings)
    {
        var previous=Draft.Copy();
        if(settings.Design=="classic")
        {
            Draft=previous;Draft.Name=settings.Name;Draft.Background=settings.Background;Draft.Surface=settings.Surface;Draft.Accent=settings.Accent;Draft.Text=settings.Text;
        }
        else
        {
            Draft=settings.Copy();Draft.Background=previous.Background;Draft.Surface=previous.Surface;Draft.Accent=previous.Accent;Draft.Text=previous.Text;
        }
        savedId = null; modified = false; BuildSavedSelector(); BuildControls(); SelectCategory(settings.Design == "classic" ? 0 : 1); Update();
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
        var styles = new[] { "classic", "modern", "cat", "neon", "paper" };
        var design = new ComboBox { ItemsSource = new[] { "기본 부채꼴", "모던", "고양이", "네온", "카드 보드" }, SelectedIndex = Array.IndexOf(styles, Draft.Design) }; controls.Children.Add(design);
        design.SelectionChanged += (_, _) => { if (design.SelectedIndex >= 0) { Draft.Design = styles[design.SelectedIndex]; Changed(); } };
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
