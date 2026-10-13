using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfPath = System.Windows.Shapes.Path;

namespace DeckLauncher;

/// <summary>A transparent, paginated semicircle. Management is a separate window.</summary>
public sealed partial class LauncherWindow : Window
{
    internal const int PageSize = 7;
    private readonly Store store;
    internal Library library;
    private string? current;
    private int page;
    private int dialStart;
    private bool dialMode;
    private readonly Dictionary<string, (int Start, bool Mode)> dials = new();
    private double dialSpan = 178.0/7;
    private readonly Canvas dialHost = new() { Width=920, Height=454 };
    private readonly Canvas dial = new() { Width=920, Height=454 };
    private readonly Dictionary<string, Geometry> slotShapes = new();
    private readonly Dictionary<string, string?> slotOverrides = new();
    private readonly Dictionary<string, BitmapSource> slotImages = new();
    private ThemeSettings Theme => library.Theme;
    private Brush T(string color) => UI.Brush(color);
    private Brush Tone(double amount) => Theme.Blend(Theme.Background, Theme.Surface, amount);
    private Brush AccentTone(double amount) => Theme.Blend(Theme.Surface, Theme.Accent, amount);
    private Brush MutedTone => Theme.Blend(Theme.Text, Theme.Background, .18);
    private bool modal;
    private OutsideClickWatcher? clickWatcher;
    private ContextMenu? settingsContext;
    private readonly bool previewMode;
    private string? backgroundOverride, backgroundKey;
    private BitmapSource? backgroundImage;
    private readonly Canvas fan = new() { Width = 920, Height = 454 };
    private readonly Grid root = new();
    private readonly TextBlock title = UI.Text("", 23);
    private readonly TextBlock note = UI.Text("", 16, UI.Muted);
    private readonly TextBlock tag = UI.Text("", 10, UI.Accent);
    private readonly TextBlock pageLabel = UI.Text("", 11, UI.Muted);
    private readonly Dictionary<string, int> pages = new();
    private readonly Dictionary<string, ImageSource?> icons = new();
    private readonly Dictionary<string, Button> slots = new();
    private readonly ScaleTransform unfolding = new(1, 1);
    private static readonly Style SliceStyle = (Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><ContentPresenter /></ControlTemplate></Setter.Value></Setter><Setter Property='Cursor' Value='Hand'/></Style>");
    internal int RenderedSlots => slots.Count;
    internal int PageCount => Math.Max(1, (library.Children(current).Count() + Theme.Slots - 1) / Theme.Slots);
    internal int CurrentPage => page;
    internal string DisplayedNote => note.Text;
    internal IReadOnlyList<string> DisplayedIds => slots.Keys.ToList();
    internal int DialStart => dialStart;
    internal string? CurrentFolder => current;
    /// <summary>Lets the font picker preview a family without changing the app-wide choice.</summary>
    internal FontFamily? FontOverride { get; set; }
    private FontFamily LFont => FontOverride ?? UI.LauncherFont;

    public LauncherWindow(Store store, Library library, bool previewMode = false)
    {
        this.store = store; this.library = library; this.previewMode = previewMode;
        dialHost.Clip=Sector(216,380,-180,0);dialHost.Children.Add(dial);
        root.Tag="keep-colors"; UI.Theme(this,library.Theme); Title = "작업표시줄 런처";
        Width = 920; Height = 454; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = false;
        if (File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "launcher.ico"))) Icon = Native.LoadImage(System.IO.Path.Combine(AppContext.BaseDirectory, "launcher.ico"));
        var view = new Viewbox { Child = fan, Stretch = Stretch.Uniform }; root.Children.Add(view); Content = root;
        fan.RenderTransform = unfolding; fan.RenderTransformOrigin = new Point(.5, 1); fan.IsHitTestVisible = !previewMode;
        if (previewMode) KeyboardNavigation.SetTabNavigation(fan, KeyboardNavigationMode.None);
        SourceInitialized += (_, _) => { Native.ConfigureTaskbar(this);if(!previewMode && ShowActivated)clickWatcher=new OutsideClickWatcher(point=>Dispatcher.BeginInvoke(new Action(()=>HandleScreenClick(point)))); };
        Closed+=(_,_)=>clickWatcher?.Dispose();
        Loaded += async (_, _) => { if (!previewMode && ShowActivated) { await PositionAtTaskbarAsync(); AnimateOpen(); } };
        Activated += async (_, _) => { if (!previewMode && ShowActivated && !modal) await PositionAtTaskbarAsync(); };
        DpiChanged += async (_, _) => { if (!previewMode && ShowActivated && IsVisible && !modal) await PositionAtTaskbarAsync(); };
        if (!previewMode) Closed += (_, _) => Application.Current.Shutdown();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (current != null) Navigate(library.Find(current)?.ParentId); else Hide(); e.Handled = true; }
            if (e.Key == Key.Home) { Navigate(null); e.Handled = true; }
            if (e.Key == Key.PageDown || e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.Control) { ChangePage(1); e.Handled = true; }
            if (e.Key == Key.PageUp || e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Control) { ChangePage(-1); e.Handled = true; }
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { Manage(); e.Handled = true; }
        };
        MouseWheel += (_, e) => { if(library.WheelDialEnabled) RotateDial(e.Delta < 0 ? 1 : -1); e.Handled = true; };
        IsVisibleChanged += (_, _) => { if (!IsVisible) CloseFolderPreview(); };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(new Action(HandleFocusLoss), System.Windows.Threading.DispatcherPriority.Background);
        Refresh(); if (!previewMode) PositionAtTaskbar(null);
    }
    internal void HandleScreenClick(Point screen)
    {
        if(!IsVisible || previewMode || modal)return;
        try
        {
            if(ShellGeometry().FillContains(fan.PointFromScreen(screen)))return;
            foreach(Window window in OwnedWindows)
            {
                if(!window.IsVisible)continue;var local=window.PointFromScreen(screen);
                if(new Rect(0,0,window.ActualWidth,window.ActualHeight).Contains(local))return;
            }
            foreach(var menu in slots.Values.Select(b=>b.ContextMenu).Append(settingsContext).Where(m=>m?.IsOpen==true))
            {
                var local=menu!.PointFromScreen(screen);if(new Rect(0,0,menu.ActualWidth,menu.ActualHeight).Contains(local))return;
            }
        }
        catch { }
        if(settingsContext!=null)settingsContext.IsOpen=false;
        foreach(var button in slots.Values)if(button.ContextMenu!=null)button.ContextMenu.IsOpen=false;
        Hide();
    }
    internal void HandleFocusLoss()
    {
        if(previewMode || modal || IsActive || OwnedWindows.Cast<Window>().Any(w=>w.IsActive) || slots.Values.Any(b=>b.ContextMenu?.IsOpen==true) || settingsContext?.IsOpen==true)return;
        Hide();CloseFolderPreview();
    }
    private async System.Threading.Tasks.Task PositionAtTaskbarAsync()
        => PositionAtTaskbar(await TaskbarAnchor.FindAsync());
    private void PositionAtTaskbar(Rect? anchor)
    {
        var location = Native.CursorWorkArea(); var dpi = VisualTreeHelper.GetDpi(this);
        var physicalWork = anchor.HasValue ? Native.WorkAreaAt(new Point(anchor.Value.X + anchor.Value.Width / 2, anchor.Value.Y + anchor.Value.Height / 2)) : location.Work;
        var work = new Rect(physicalWork.Left / dpi.DpiScaleX, physicalWork.Top / dpi.DpiScaleY, physicalWork.Width / dpi.DpiScaleX, physicalWork.Height / dpi.DpiScaleY);
        double center = anchor.HasValue ? (anchor.Value.X + anchor.Value.Width / 2) / dpi.DpiScaleX : !physicalWork.Contains(location.Cursor) && location.Cursor.X > physicalWork.Left ? location.Cursor.X / dpi.DpiScaleX : work.Left + work.Width / 2;
        var position = TaskbarAnchor.Placement(work, center); Width = position.Width; Height = position.Height; Left = position.X; Top = position.Y;
    }
    public void Reveal()
    {
        if (modal) { foreach (Window window in OwnedWindows) window.Activate(); return; }
        if (IsVisible && IsActive) { Hide(); return; }
        Show(); WindowState = WindowState.Normal; Activate(); Focus(); _ = PositionAtTaskbarAsync(); AnimateOpen();
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
        CloseFolderPreview(); dial.Children.Clear(); slotShapes.Clear();
        if(!library.WheelDialEnabled) { dialMode=false; dialStart=page*Theme.Slots; }
        fan.Children.Clear(); slots.Clear(); FontFamily=LFont; title.FontFamily=LFont; title.Foreground=T(Theme.Text);note.Foreground=MutedTone;tag.Foreground=T(Theme.Accent);pageLabel.Foreground=MutedTone;note.Visibility=Theme.ShowNotes?Visibility.Visible:Visibility.Collapsed;page = Math.Clamp(page, 0, PageCount - 1);
        // The window outside this shape remains transparent and does not intercept clicks.
        var shell = new WpfPath { Data = ShellGeometry(), Fill = Tone(.2), Stroke = AccentTone(.25), StrokeThickness = 1, Effect = Shadow(26, .48) };
        StyleShell(shell); fan.Children.Add(shell);
        string? imagePath = backgroundOverride ?? (library.Background.ImageFile != null ? store.IconPath(library.Background.ImageFile) : null);
        if (backgroundKey != imagePath)
        {
            backgroundKey = imagePath; backgroundImage = null;
            try { if (imagePath != null) backgroundImage = (BitmapSource)Native.LoadImage(imagePath, 1920); } catch { }
        }
        bool hasBackground = backgroundImage != null;
        if (hasBackground) fan.Children.Add(BackgroundTools.Layer(backgroundImage!, library.Background,ShellGeometry()));
        var outerLine = new WpfPath { Data = Sector(386, 388, -177, -3), Fill = T(Theme.Accent), IsHitTestVisible = false, Opacity = .6 }; if(!AlternateLayout)fan.Children.Add(outerLine);
        DrawShellDecorations();
        var all = library.Children(current).ToList();
        dialStart=all.Count==0?0:((dialStart%all.Count)+all.Count)%all.Count;
        var visible = dialMode && all.Count>0 ? Enumerable.Range(0, Math.Min(Theme.Slots,all.Count)).Select(i=>all[(dialStart+i)%all.Count]).ToList() : all.Skip(page * Theme.Slots).Take(Theme.Slots).ToList();
        fan.Children.Add(dialHost);
        if(AlternateLayout)AddAlternateSlots(visible); else
        {
        dialHost.Clip=Sector(216,380,-180,0);
        int count = visible.Count switch { 0 or 1 or 3 => 5, 2 or 4 => 6, _ => visible.Count };
        int offset = (count - visible.Count) / 2;
        double span = dialSpan = 178.0 / count;
        for (int index = 0; index < count; index++)
        {
            double start = -179 + index * span + Theme.Gap, end = -179 + (index + 1) * span - Theme.Gap;
            var geometry = Sector(216, 380, start, end);
            int entryIndex = index - offset;
            if (entryIndex < 0 || entryIndex >= visible.Count)
            {
                var placeholder = new WpfPath { Data = geometry, Fill = Tone(.55), Stroke = AccentTone(.15), StrokeThickness = .7, Opacity = hasBackground ? .25 : .45, IsHitTestVisible = false }; dial.Children.Add(placeholder); continue;
            }
            AddSlot(visible[entryIndex], geometry, (start + end) / 2);
        }
        }
        var inner = new WpfPath { Data = HubGeometry(), Fill = Tone(.14), Stroke = AccentTone(.18), StrokeThickness = 1, IsHitTestVisible = false }; fan.Children.Add(inner);
        if (hasBackground) inner.Fill.Opacity = .65; StyleHub(inner); DrawHubDecorations();
        var info = new StackPanel { Width = InfoBox.Width, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        tag.Visibility=AlternateLayout && Theme.Design!="neon"?Visibility.Collapsed:Visibility.Visible;tag.TextAlignment = TextAlignment.Center; tag.Margin = new Thickness(0, 0, 0, 7);
        title.TextAlignment = TextAlignment.Center; title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap; title.FontWeight = FontWeights.Medium;
        note.TextAlignment = TextAlignment.Center; note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(8, AlternateLayout && Theme.Design!="neon" ? 6 : 9, 8, 0); note.FontFamily=LFont;note.FontSize=15;note.MaxHeight=48;note.LineHeight=24;note.LineStackingStrategy=LineStackingStrategy.BlockLineHeight; note.TextTrimming = TextTrimming.CharacterEllipsis;
        info.Children.Add(tag); info.Children.Add(title); info.Children.Add(note); Place(fan, info, InfoBox.X, InfoBox.Y);
        var back = RoundButton(current == null ? "\uE80F" : "\uE72B", current == null ? "처음으로" : "상위 폴더", () => Navigate(library.Find(current)?.ParentId)); back.Opacity = current == null ? .5 : 1;
        back.ToolTip = current == null ? "라이브러리 · 여기로 끌어 꺼내기" : "상위 폴더 · 여기로 끌어 꺼내기";
        SetupFanDrop(back, library.Find(current)?.ParentId);
        // Bottom controls are mirrored around the hub center (x = 460): 77px to the small buttons, 126px to the page arrows.
        const double hubX = 460, sideOffset = 77, pageOffset = 126;
        Place(fan, back, hubX - sideOffset - 22, 403);
        var settings = RoundButton("\uE713", "설정 · 파일 / 폴더 / 이미지 / 메모", Manage, true); Place(fan, settings, hubX - 25, 396);
        var hide = RoundButton("\uE711", "숨기기 (Esc)", Hide); Place(fan, hide, hubX + sideOffset - 22, 403);
        var settingsMenu = new ContextMenu(); settingsMenu.Items.Add(UI.Menu("설정 열기", Manage)); settingsMenu.Items.Add(UI.Menu("런처 종료", () => Application.Current.Shutdown())); settings.ContextMenu = settingsMenu; settingsContext=settingsMenu;
        if (PageCount > 1)
        {
            var previous = RoundButton("\uE76B", "이전 페이지", () => ChangePage(-1)); previous.Width = previous.Height = 32; previous.IsEnabled = page > 0; Place(fan, previous, hubX - pageOffset - 16, 409);
            var next = RoundButton("\uE76C", "다음 페이지", () => ChangePage(1)); next.Width = next.Height = 32; next.IsEnabled = page < PageCount - 1; Place(fan, next, hubX + pageOffset - 16, 409);
            pageLabel.Text = dialMode ? $"{dialStart + 1} ↻ {all.Count}" : $"{page + 1} / {PageCount}"; pageLabel.TextAlignment = TextAlignment.Center; pageLabel.Width = 54; Place(fan, pageLabel, hubX + pageOffset + 20, 417);
        }
        DefaultNote();
    }
    private void AddSlot(Entry entry, Geometry geometry, double angle)
    {
        var button = new Button { Width = 920, Height = 454, Style = SliceStyle, Clip = geometry, Background = Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(0), Tag = entry.Id };
        AutomationProperties.SetName(button, entry.Name); AutomationProperties.SetHelpText(button, entry.Note);
        if (entry.Note.Length > 80 || entry.Note.Count(x => x == '\n') > 2)
        {
            button.ToolTip = new ToolTip { Background = T(Theme.Background), Foreground = T(Theme.Text), BorderBrush = AccentTone(.4), Padding = new Thickness(14), FontFamily = LFont, Content = new TextBlock { Text = entry.Note, Width = 280, TextWrapping = TextWrapping.Wrap, FontSize = 13 } };
            ToolTipService.SetInitialShowDelay(button, 650);
        }
        var contents = new Canvas { Width = 920, Height = 454 };
        var shape = new WpfPath { Data = geometry, Fill = Tone(1), Stroke = AccentTone(.2), StrokeThickness = .8 };
        if (backgroundImage != null) shape.Fill.Opacity = .35;
        StyleSlot(shape); contents.Children.Add(shape);
        string? slotPath = slotOverrides.GetValueOrDefault(entry.Id) ?? (entry.Background.ImageFile != null ? store.IconPath(entry.Background.ImageFile) : null);
        if (slotPath != null)
        {
            try { if(!slotImages.TryGetValue(slotPath,out var bitmap))slotImages[slotPath]=bitmap=(BitmapSource)Native.LoadImage(slotPath,1024);contents.Children.Add(BackgroundTools.Layer(bitmap,entry.Background,geometry)); } catch { }
        }
        var point = IconPoint(geometry,angle); var bounds = geometry.Bounds;
        bool tiles = AlternateLayout && Theme.Design!="neon";
        // Tile layouts size the frame from the slot so the icon always has even padding inside it.
        double frameSize = !tiles ? 60 : Theme.Design=="cat" ? Math.Min(46, bounds.Height-12) : Theme.Design=="paper" && bounds.Height<80 ? 34 : 50;
        var iconFrame = new Border { Width = frameSize, Height = frameSize, CornerRadius = new CornerRadius(Theme.Frame=="circle"?frameSize/2:frameSize*.28), Background = Theme.Frame=="minimal"?Brushes.Transparent:AccentTone(.12), BorderBrush = Theme.Frame=="minimal"?Brushes.Transparent:AccentTone(.3), BorderThickness = new Thickness(.7), Effect = Shadow(12, .28), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1), IsHitTestVisible = false };
        ImageSource? source = IconFor(entry);
        double iconSize = tiles ? Math.Min(Theme.IconSize, Math.Round(frameSize * (Theme.Design=="cat" ? .7 : .8))) : Theme.IconSize;
        iconFrame.Child = source != null ? new Image { Source = source, Width = iconSize, Height = iconSize, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : UI.Icon("\uE8E5", iconSize * .6, null);
        if (source == null) ((TextBlock)iconFrame.Child).Foreground = T(Theme.Accent);
        StyleIconFrame(iconFrame);
        double labelHeight = 20, nameLeft, nameTop, nameWidth; var nameAlign = HorizontalAlignment.Center;
        if (Theme.Design=="cat")
        {
            // Row tile: icon on the left with equal top/left/bottom padding, name vertically centered beside it.
            double pad = (bounds.Height - frameSize) / 2; Place(contents, iconFrame, bounds.X + pad, bounds.Y + pad);
            nameLeft = bounds.X + pad + frameSize + 10; nameWidth = bounds.Right - 34 - nameLeft; nameTop = bounds.Y + (bounds.Height - labelHeight) / 2; nameAlign = HorizontalAlignment.Left;
        }
        else if (tiles)
        {
            // Column tile: the icon + name group is centered vertically in the card.
            double gap = bounds.Height < 80 ? 3 : 7, top = bounds.Y + (bounds.Height - frameSize - gap - labelHeight) / 2;
            Place(contents, iconFrame, bounds.X + (bounds.Width - frameSize) / 2, top);
            nameLeft = bounds.X + 6; nameWidth = bounds.Width - 12; nameTop = top + frameSize + gap;
        }
        else if (Theme.Design=="neon")
        {
            // Centered on the orbit circle (drawn 3px above the polar point); the name sits in its pill below.
            Place(contents, iconFrame, point.X - 30, point.Y - 33);
            nameLeft = point.X - 47.5; nameWidth = 95; nameTop = point.Y + 41;
        }
        else
        {
            Place(contents, iconFrame, point.X - 30, point.Y - 38);
            nameLeft = point.X - 47.5; nameWidth = 95; nameTop = point.Y + 30;
        }
        DrawSlotDecorations(contents,point);
        var name = new TextBlock { Text = entry.Name, FontSize = 14, FontFamily = LFont, TextAlignment = nameAlign == HorizontalAlignment.Left ? TextAlignment.Left : TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = T(Theme.Text), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = nameAlign, IsHitTestVisible = false };
        var nameBox = new Grid { Width = nameWidth, Height = labelHeight, IsHitTestVisible = false, Visibility = Theme.ShowLabels ? Visibility.Visible : Visibility.Collapsed };
        if (slotPath != null)
        {
            // Names over a slot picture get a soft plate so they stay readable on any image.
            var plateColor = (Color)ColorConverter.ConvertFromString(Theme.Surface); plateColor.A = 215; name.MaxWidth = nameWidth - 12;
            nameBox.Children.Add(new Border { Background = new SolidColorBrush(plateColor), CornerRadius = new CornerRadius(7), Padding = new Thickness(6, 0, 6, 0), HorizontalAlignment = nameAlign, Child = name });
        }
        else { name.MaxWidth = nameWidth; nameBox.Children.Add(name); }
        Place(contents, nameBox, nameLeft, nameTop);
        if (entry.IsFolder)
        {
            var count = new TextBlock { Text = library.Children(entry.Id).Count().ToString(), FontSize = 10.5, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Segoe UI"), Foreground = T(Skin.IsLight(Theme.Accent) ? Theme.Background : "#FFFFFF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border { Background = T(Theme.Accent), CornerRadius = new CornerRadius(9), MinWidth = 18, Height = 18, Padding = new Thickness(5, 0, 5, 0), IsHitTestVisible = false, Child = count, HorizontalAlignment = tiles ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            var badgeBox = new Grid { Width = 40, Height = 18, IsHitTestVisible = false }; badgeBox.Children.Add(badge);
            if (Theme.Design=="cat") Place(contents, badgeBox, bounds.Right - 12 - 40, bounds.Y + (bounds.Height - 18) / 2);
            else if (tiles) Place(contents, badgeBox, bounds.Right - 8 - 40, bounds.Y + 7);
            else Place(contents, badgeBox, point.X + 12, point.Y + (Theme.Design=="neon" ? 8 : 3));
        }
        button.Content = contents;
        void Highlight(bool active)
        {
            shape.Fill = active ? AccentTone(.32) : SlotFill();
            if (backgroundImage != null) shape.Fill.Opacity = active ? .68 : .35;
            shape.Stroke = active ? T(Theme.Accent) : AccentTone(.2); shape.StrokeThickness = active ? 1.8 : .8;
            var transform = (ScaleTransform)iconFrame.RenderTransform; var animation = new DoubleAnimation(active ? 1.09 : 1, TimeSpan.FromMilliseconds(130)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation); transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            if (active) ShowNote(entry); else DefaultNote();
        }
        button.MouseEnter += (_, _) => { Highlight(true); if(entry.IsFolder && !previewMode) ScheduleFolderPreview(entry,button); else SchedulePreviewClose(); };
        button.MouseLeave += (_, _) => { Highlight(button.IsKeyboardFocused); SchedulePreviewClose(); };
        button.GotKeyboardFocus += (_, _) => Highlight(true); button.LostKeyboardFocus += (_, _) => Highlight(button.IsMouseOver);
        bool dragged = false; Point? pressed = null;
        button.PreviewMouseLeftButtonDown += (_, e) => { pressed = e.GetPosition(this); dragged = false; };
        button.PreviewMouseMove += (_, e) =>
        {
            if (previewMode || pressed == null || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(this);
            if (Math.Abs(point.X - pressed.Value.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - pressed.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            pressed = null; dragged = true;
            DragDrop.DoDragDrop(button, new DataObject(LibraryWindow.EntryDragFormat, new[] { entry.Id }), DragDropEffects.Move); e.Handled = true;
        };
        button.Click += (_, _) => { if (!dragged) Open(entry); };
        SetupSliceDrop(button, entry, angle);
        var menu = new ContextMenu(); menu.Items.Add(UI.Menu("이름 / 이미지 / 메모 편집", () => Edit(entry))); menu.Items.Add(UI.Menu("이 칸의 배경 이미지…", () => CustomizeSlot(entry))); menu.Items.Add(UI.Menu("폴더 관리 열기", Manage)); button.ContextMenu = menu;
        dial.Children.Add(button); slots[entry.Id] = button; slotShapes[entry.Id]=geometry;
    }
    private Button RoundButton(string glyph, string tooltip, Action action, bool main = false)
    {
        var label = new TextBlock { Text = glyph, FontFamily = UI.IconFont, FontSize = main ? 23 : 16, Foreground = main ? T(Theme.Background) : T(Theme.Text), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var button = UI.Button("", action); button.Width = button.Height = main ? 50 : 44; button.Margin = new Thickness(0); button.Padding = new Thickness(0); button.ToolTip = tooltip;
        var disk = new Ellipse { Fill = main ? T(Theme.Accent) : Tone(.85), Stroke = main ? T(Theme.Text) : AccentTone(.3), StrokeThickness = 1 };
        button.Style = SliceStyle;
        var grid = new Grid(); grid.Children.Add(disk); grid.Children.Add(label); button.Content = grid;
        AutomationProperties.SetName(button, tooltip);
        button.MouseEnter += (_, _) => { disk.Stroke = T(Theme.Accent); disk.StrokeThickness = 2; };
        button.MouseLeave += (_, _) => { disk.Stroke = main ? T(Theme.Text) : AccentTone(.3); disk.StrokeThickness = 1; };
        button.GotKeyboardFocus += (_, _) => disk.StrokeThickness = 3; button.LostKeyboardFocus += (_, _) => disk.StrokeThickness = 1;
        return button;
    }
    private void DefaultNote()
    {
        tag.Text = current == null ? Theme.Design switch { "cat" => "H E L L O ,   H U M A N", "neon" => "R E A D Y   T O   L A U N C H", "modern" => "M Y   W O R K S P A C E", "glass" => "Y O U R   S P A C E", _ => "Q U I C K   L A U N C H" } : "F O L D E R";
        title.Text = library.Find(current)?.Name ?? "나의 바로가기";
        note.Text = library.Children(current).Any() ? AlternateLayout && Theme.Design!="neon" ? "자주 쓰는 것들을 한 번에. 실행할 아이콘을 선택하세요." : "자주 쓰는 것들을 한 번에.\n실행할 아이콘을 선택하세요." : "아직 등록된 항목이 없어요.\n아래 설정 버튼에서 추가해 주세요.";
    }
    internal void ShowNote(Entry entry)
    {
        tag.Text = entry.IsFolder ? $"폴더  ·  {library.Children(entry.Id).Count()}개 항목" : "바로가기";
        title.Text = entry.Name;
        note.Text = string.IsNullOrWhiteSpace(entry.Note) ? entry.IsFolder ? "마우스를 올리면 옆으로 펼쳐집니다." : "클릭하여 실행 · 우클릭하여 메모 편집" : entry.Note;
    }
    internal void Navigate(string? id)
    {
        pages[current ?? ""] = page; dials[current ?? ""]=(dialStart,dialMode); current = id; page = pages.GetValueOrDefault(id ?? ""); (dialStart,dialMode)=dials.GetValueOrDefault(id ?? ""); Refresh();
    }
    internal void ChangePage(int direction) { dialMode=false; page = Math.Clamp(page + direction, 0, PageCount - 1); dialStart=page*Theme.Slots; Refresh(); }
    internal void RotateDial(int direction)
    {
        int total=library.Children(current).Count();if(total<2)return;
        if(!dialMode)dialStart=page*Theme.Slots;
        dialStart=((dialStart+direction)%total+total)%total;dialMode=true;page=dialStart/Theme.Slots;Refresh();
        if(!previewMode && ShowActivated)
        {
            if(AlternateLayout && Theme.Design!="neon"){var slide=new TranslateTransform(direction*35,0);dial.RenderTransform=slide;slide.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(180)));return;}
            var rotation=new RotateTransform(-direction*dialSpan,460,453);dial.RenderTransform=rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(210)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
            dial.BeginAnimation(OpacityProperty,new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(170)));
        }
    }
    internal Button? Slot(string id) => slots.GetValueOrDefault(id);
    private void SetupFanDrop(Button target, string? parent)
    {
        if (previewMode) return;
        target.AllowDrop = true;
        target.DragOver += (_, e) =>
        {
            bool allowed = e.Data.GetData(LibraryWindow.EntryDragFormat) is string[] ids && ids.All(id => library.Find(id) is Entry entry && library.CanMove(entry, parent));
            e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true;
            target.Effect = allowed ? new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#6DE0D4"), BlurRadius = 12, ShadowDepth = 0, Opacity = .8 } : null;
        };
        target.DragLeave += (_, _) => target.Effect = null;
        target.Drop += (_, e) =>
        {
            e.Handled = true; target.Effect = null;
            if (e.Data.GetData(LibraryWindow.EntryDragFormat) is string[] ids && library.MoveEntries(ids, parent)) { store.Save(library); Refresh(); }
        };
    }
    internal UIElement DetachPreview() { Content = null; return root; }
    internal void UpdateBackground(BackgroundSettings settings, string? path, string? entryId = null) { if(entryId==null) { library.Background = settings; backgroundOverride = path; } else { library.Find(entryId)!.Background=settings; slotOverrides[entryId]=path; } Refresh(); }
    internal void UpdateTheme(ThemeSettings theme) { library.Theme=theme;Refresh(); }
    internal Geometry? SlotGeometry(string id) => slotShapes.GetValueOrDefault(id);
    internal void PreviewEntry(Entry entry) { Navigate(entry.ParentId); int index=library.Children(entry.ParentId).ToList().FindIndex(e=>e.Id==entry.Id);page=Math.Max(0,index/Theme.Slots);Refresh();ShowNote(entry); }
    private void CustomizeSlot(Entry entry)
    {
        CloseFolderPreview(); modal=true;try { var dialog=new BackgroundWindow(store,library,entry){Owner=this};if(dialog.ShowDialog()==true){entry.Background=dialog.Result!;store.Save(library);Refresh();} }finally{modal=false;}
    }
    internal void Open(Entry entry)
    {
        if (entry.IsFolder) { Navigate(entry.Id); return; }
        try { using var process = Launch.Start(entry); Hide(); }
        catch (Exception ex) { modal = true; try { UI.Error(ex); } finally { modal = false; } }
    }
    private void Manage()
    {
        CloseFolderPreview(); modal = true;
        try
        {
            var manager = new LibraryWindow(store, library) { Owner = this, Topmost = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            manager.Navigate(current); manager.ShowDialog(); library = manager.library;
            if (current != null && library.Find(current) == null) current = null;
            icons.Clear(); Refresh();
        }
        finally { modal = false; if (IsVisible) Activate(); }
    }
    private void Edit(Entry entry)
    {
        CloseFolderPreview(); modal = true;
        try
        {
            var editor = new EditorWindow(store, entry,library.Theme) { Owner = this, Topmost = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            if (editor.ShowDialog() == true) { library.Entries[library.Entries.IndexOf(entry)] = editor.Result!; store.Save(library); icons.Clear(); Refresh(); }
        }
        finally { modal = false; if (IsVisible) Activate(); }
    }
}
