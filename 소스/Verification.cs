using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DeckLauncher;

internal static class SelfTests
{
    public static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var log = new List<string>();
        void Check(bool passed, string text) { if (!passed) throw new Exception("FAIL: " + text); log.Add("PASS: " + text); }
        try
        {
            var store = new Store(Path.Combine(output, "test-data-" + Guid.NewGuid().ToString("N")));
            var lib = store.Load(); Check(lib.Entries.Count == 0, "First run creates an empty library");
            string legacyFile = Path.Combine(store.DirectoryPath, "legacy.json");
            File.WriteAllText(legacyFile, "{\"Version\":1,\"Entries\":[{\"Id\":\"legacy\",\"Name\":\"기존 항목\"}]}");
            Check(Store.Read(legacyFile).Find("legacy")?.Note == "", "Previous library files remain compatible without note fields");
            var a = new Entry { Name = "게임", IsFolder = true };
            var b = new Entry { Name = "도구", IsFolder = true, ParentId = a.Id };
            var c = new Entry { Name = "하위 폴더", IsFolder = true, ParentId = b.Id };
            var item = new Entry { Name = "메모장", Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe"), ParentId = c.Id, Arguments = "example.txt", WorkingDirectory = output, Note = "아이디어를 빠르게 기록\n두 번째 줄" };
            lib.Entries.AddRange(new[] { a, b, c, item });
            Check(!lib.CanMove(a, c.Id) && !lib.CanMove(b, b.Id), "Moving folders into themselves or descendants is blocked");
            Check(lib.CanMove(item, a.Id) && lib.CanMove(c, null), "Moving entries into folders and root works");
            Check(lib.Trail(c.Id) == "라이브러리 / 게임 / 도구 / 하위 폴더", "Nested folder breadcrumb is correct");
            store.Save(lib); var loaded = store.Load();
            Check(loaded.Find(item.Id)?.Arguments == "example.txt" && loaded.Find(item.Id)?.WorkingDirectory == output, "Arguments and working directory survive save/load");
            Check(loaded.Find(item.Id)?.Note == item.Note, "Multiline hover notes survive save/load");
            lib.HideAfterLaunch = false; store.Save(lib);
            Check(File.Exists(store.FilePath + ".bak") && !store.Load().HideAfterLaunch, "Atomic save keeps a previous copy and persists settings");
            var copy = Store.Read(store.FilePath); copy.Remove(copy.Find(b.Id)!);
            Check(copy.Entries.Count == 1 && copy.Find(a.Id) != null, "Removing a virtual folder removes descendants only");
            Check(File.Exists(item.Target), "Removing registrations does not delete original files");
            var bad = new Library { Entries = new() { new Entry { Id = "x", IsFolder = true, ParentId = "x" } } };
            bool rejected = false; try { bad.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Cyclic folder data is rejected");
            rejected = false; try { new Library { Entries = new() { new Entry { IconFile = "../outside.png" } } }.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Imported icon path traversal is rejected");
            var source = Path.Combine(AppContext.BaseDirectory, "launcher.ico");
            string copied = store.CopyIcon(source); Check(File.Exists(store.IconPath(copied)) && Native.LoadImage(store.IconPath(copied)) != null, "Custom images are copied and decoded");
            item.IconFile = copied;
            string backgroundSource = Path.Combine(store.DirectoryPath, "sample-background.png"); Preview.CreateBackground(backgroundSource);
            lib.Background = new BackgroundSettings { ImageFile = store.CopyIcon(backgroundSource), Zoom = 1.45, OffsetX = .3, OffsetY = -.45, Darkness = .32 };
            item.Background=new BackgroundSettings { ImageFile=store.CopyIcon(backgroundSource),Zoom=1.7,OffsetX=-.4,Darkness=.25 };
            lib.Theme=ThemeSettings.Presets[0].Copy();lib.Theme.Name="테스트 테마";lib.Theme.Accent="#D7AFEF";
            var backupFile = Path.Combine(store.DirectoryPath, "backup.zip"); store.Backup(backupFile, lib);
            var backup = Store.ReadBackup(backupFile);
            var restored = store.Restore(backup);
            Check(restored.Find(item.Id)?.IconFile != copied && Native.LoadImage(store.IconPath(restored.Find(item.Id)!.IconFile!)) != null && restored.Trail(c.Id) == lib.Trail(c.Id), "ZIP backup restores hierarchy and custom images without overwriting old icons");
            Check(restored.Find(item.Id)!.Background.ImageFile!=item.Background.ImageFile && File.Exists(store.IconPath(restored.Find(item.Id)!.Background.ImageFile!)) && restored.Find(item.Id)!.Background.OffsetX==-.4,"ZIP backup restores per-slot images and independent crop settings");
            Check(restored.Theme.Accent=="#D7AFEF" && restored.Theme.Name=="테스트 테마","ZIP backup preserves custom theme settings");
            Check(restored.Find(item.Id)?.Note == item.Note, "ZIP backup preserves user notes");
            Check(restored.Background.ImageFile != lib.Background.ImageFile && File.Exists(store.IconPath(restored.Background.ImageFile!)) && restored.Background.Zoom == lib.Background.Zoom && restored.Background.OffsetY == lib.Background.OffsetY, "ZIP backup restores background image and crop settings");
            Check(lib.ApplyUiUpdate() && !lib.HideAfterLaunch && !lib.ApplyUiUpdate() && lib.Entries.Count == 4, "UI migration changes preferences once while preserving registered entries");
            var imageRect = BackgroundTools.ImageRect(800, 600, new BackgroundSettings { Zoom = 2, OffsetX = 1, OffsetY = -1 });
            Check(imageRect.Contains(BackgroundTools.Bounds), "Background stays covered at extreme crop positions");
            var centered = BackgroundTools.ImageRect(800, 600, new BackgroundSettings { Zoom = 2 });
            Check(imageRect.X != centered.X && imageRect.Y != centered.Y, "Position adjustments change the actual image crop");
            bool invalidBackground = false; try { new BackgroundSettings { ImageFile = "../image.png" }.Validate(); } catch (InvalidDataException) { invalidBackground = true; }
            Check(invalidBackground, "Background backup paths are validated");
            var placement = TaskbarAnchor.Placement(new Rect(0, 0, 1920, 1040), 623);
            Check(Math.Abs(placement.X + placement.Width / 2 - 623) < .001 && Math.Abs(placement.Y + 453.5 * placement.Width / 920 - 1040) < .001, "Visible fan base touches the taskbar and centers on its button");
            var secondMonitor = TaskbarAnchor.Placement(new Rect(-1920, 0, 1920, 1040), -623);
            Check(Math.Abs(secondMonitor.X + secondMonitor.Width / 2 + 623) < .001, "Taskbar placement supports negative monitor coordinates");
            Check(Native.FileIcon(item.Target) != null, "Native executable icon is extracted");
            var shortcut = Path.Combine(output, "test-shortcut.lnk");
            if (File.Exists(shortcut)) Check(Native.FileIcon(shortcut) != null, "Windows shortcut default icon is extracted");
            var launchDir = Path.Combine(store.DirectoryPath, "launch"); Directory.CreateDirectory(launchDir);
            using (var process = Launch.Start(new Entry { Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd.exe"), Arguments = "/d /c echo launcher-ok>launch-test.txt", WorkingDirectory = launchDir }, true))
            {
                Check(process != null && process.WaitForExit(5000) && process.ExitCode == 0 && File.ReadAllText(Path.Combine(launchDir, "launch-test.txt")).Trim() == "launcher-ok", "Real process launches with arguments and the configured working directory");
            }
            bool missingRejected = false; try { Launch.Start(new Entry { Target = Path.Combine(launchDir, "missing.exe") }, true); } catch (FileNotFoundException) { missingRejected = true; }
            Check(missingRejected, "Missing launch targets produce a helpful error");

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var win = new LibraryWindow(store, lib) { ShowActivated = false, Left = -20000, Top = -20000 };
            win.Show(); win.UpdateLayout();
            Check(!win.Topmost, "Management window uses normal stacking order");
            Check(win.RenderedCards == 1, "Root renders only immediate children");
            var folderDrop = Find<Button>(win).First(x => x.Tag as string == a.Id);
            Check(folderDrop.AllowDrop, "Folder cards accept dragged library items");
            RaiseDrop(folderDrop, item.Id);
            Check(item.ParentId == a.Id && store.Load().Find(item.Id)?.ParentId == a.Id, "Actual folder drop moves and saves an existing shortcut");
            var rootDrop = Find<Button>(win).First(x => x.Content as string == "라이브러리"); RaiseDrop(rootDrop, item.Id);
            Check(item.ParentId == null, "Dropping on the root breadcrumb takes a shortcut out of its folder");
            win.Navigate(a.Id); var childDrop = Find<Button>(win).First(x => x.Tag as string == b.Id); RaiseDrop(childDrop, a.Id);
            Check(a.ParentId == null && b.ParentId == a.Id, "Folder drag cannot create a cycle");
            win.DropEntries(new[] { item.Id }, c.Id); win.Navigate(null);
            win.Navigate(c.Id); win.UpdateLayout(); Check(win.RenderedCards == 1, "Nested folder renders contained shortcut");
            win.RegisterPaths(new[] { item.Target, item.Target }); Check(lib.Children(c.Id).Count() == 1, "Duplicate files in the same folder are skipped");
            win.Navigate(null); win.RegisterPaths(new[] { item.Target }); Check(lib.Children(null).Count() == 2, "Drag/drop registration uses the selected folder");
            var search = Find<TextBox>(win).First(x => x.ToolTip != null);
            search.Text = "메모장"; Check(win.RenderedCards == 1, "Search reaches deeply nested entries");
            search.Text = "unlikely-no-result-61892"; Check(win.RenderedCards == 0, "Search empty state works");
            search.Text = "빠르게 기록"; Check(win.RenderedCards == 1, "Management search includes user notes");
            win.Navigate(null);
            var editor = new EditorWindow(store, a) { Owner = win, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -19000, Top = -19000 };
            editor.Show(); editor.UpdateLayout(); Preview.Snapshot(editor, Path.Combine(output, "폴더-편집.png")); editor.Close();
            win.Hide();
            var backgroundEditor = new BackgroundWindow(store, lib) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
            backgroundEditor.Show(); backgroundEditor.UpdateLayout();
            backgroundEditor.SelectImage(backgroundSource);
            var oldZoom = lib.Background.Zoom; backgroundEditor.Draft.Zoom = 2.1;
            Check(lib.Background.Zoom == oldZoom && !backgroundEditor.Topmost, "Background preview edits are isolated until applied and use normal stacking");
            Preview.Snapshot(backgroundEditor, Path.Combine(output, "미리보기-배경편집.png")); backgroundEditor.Close();
            var applyBackground = new BackgroundWindow(store, lib) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
            applyBackground.SelectImage(backgroundSource);
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                Find<Slider>(applyBackground).First(x => x.Minimum == 1).Value = 2.25;
                Find<Button>(applyBackground).First(x => x.Content as string == "적용").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            bool? applied = applyBackground.ShowDialog();
            Check(applied == true && applyBackground.Result!.Zoom == 2.25 && File.Exists(store.IconPath(applyBackground.Result.ImageFile!)) && applyBackground.Result.ImageFile != lib.Background.ImageFile, "Apply button saves a copied background image with live preview adjustments");
            var radial = new LauncherWindow(store, lib) { ShowActivated = false, Left = -20000, Top = -20000 };
            radial.Show(); radial.UpdateLayout();
            Check(Native.TaskbarProperty(radial, 5) == "Enedaze.DeckLauncher" && Native.TaskbarProperty(radial, 2) == "\"" + Path.Combine(AppContext.BaseDirectory, "DeckLauncher.exe") + "\"", "Taskbar identity and pinned relaunch command are preserved");
            Check(radial.AllowsTransparency && radial.Background == Brushes.Transparent, "Fan opens as a transparent shaped window");
            Check(!radial.Topmost && radial.Height <= 454, "Launcher uses normal stacking and removes the bottom floating gap");
            Check(!Find<TextBlock>(radial).Any(x => x.Text == "아이콘에 마우스를 올려 메모 보기"), "Unrequested hover instruction is removed");
            Check(radial.RenderedSlots == 2, "Fan renders only immediate children");
            RaiseDrop(radial.Slot(a.Id)!, item.Id); Check(item.ParentId == a.Id, "Fan folder slices accept existing shortcuts by drag/drop");
            radial.Navigate(a.Id); var fanBack = Find<Button>(radial).First(x => (x.ToolTip as string)?.StartsWith("상위 폴더 ·") == true); RaiseDrop(fanBack, item.Id);
            Check(item.ParentId == null, "Dropping on the fan back button takes a shortcut out of its folder");
            lib.MoveEntries(new[] { item.Id }, c.Id); store.Save(lib); radial.Navigate(null); radial.UpdateLayout();
            var hit = radial.InputHitTest(new Point(383, 162)) as DependencyObject;
            while (hit != null && hit is not Button) hit = VisualTreeHelper.GetParent(hit);
            Check(hit == radial.Slot(a.Id), "Actual WPF pointer hit testing selects the correct fan slice");
            radial.Slot(a.Id)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(radial.Slot(b.Id) != null && radial.RenderedSlots == 1, "Clicking a folder slice opens its contents");
            radial.Navigate(c.Id); Check(radial.RenderedSlots == 1, "Nested folders replace fan contents");
            radial.Slot(item.Id)!.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
            Check(radial.DisplayedNote == item.Note, "Real mouse-enter event displays the configured note");
            radial.Slot(item.Id)!.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
            Check(radial.DisplayedNote != item.Note, "Leaving a slot restores folder summary");
            var sector = LauncherWindow.Sector(216, 380, -179, -154);
            Check(!sector.FillContains(new Point(460, 100)) && sector.FillContains(new Point(160, 415)), "Sector hit area excludes transparent parts of the window");
            for (int i = 0; i < 17; i++) lib.Entries.Add(new Entry { Name = "페이지 항목 " + i, ParentId = c.Id });
            radial.Navigate(c.Id); Check(radial.PageCount == 3 && radial.RenderedSlots == 7, "Large folders paginate at seven slots without overlap");
            radial.ChangePage(2); Check(radial.CurrentPage == 2 && radial.RenderedSlots == 4, "Last page displays remaining entries");
            radial.Navigate(null); radial.Navigate(c.Id); Check(radial.CurrentPage == 2, "Returning to a folder restores its page");
            radial.ChangePage(99); Check(radial.CurrentPage == 2, "Page navigation remains within bounds");
            // Wheel shifts one item at a time, including reverse wrapping and folder state.
            radial.ChangePage(-99);var ordered=lib.Children(c.Id).Select(e=>e.Id).ToList();
            radial.RotateDial(-1);Check(radial.DialStart==ordered.Count-1 && radial.DisplayedIds[0]==ordered[^1] && radial.RenderedSlots==7,"Reverse dial wraps to the last entry while keeping seven visible slots");
            radial.RotateDial(1);Check(radial.DialStart==0 && radial.DisplayedIds.SequenceEqual(ordered.Take(7)),"Forward dial wraps back to the first entry");
            radial.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.MouseWheelEvent});
            Check(radial.DialStart==1 && radial.DisplayedIds[0]==ordered[1],"Actual mouse wheel event rotates by one item rather than a page");
            radial.Navigate(null);radial.Navigate(c.Id);Check(radial.DialStart==1,"Folder navigation restores its dial position");
            radial.ChangePage(1);Check(radial.CurrentPage==1 && radial.DisplayedIds[0]==ordered[7],"Page buttons remain independent after dial scrolling");
            // Actual card edges reorder the saved list, and folder centers still move entries.
            win.Show();win.Navigate(c.Id);win.UpdateLayout();var firstCard=Find<Button>(win).First(x=>x.Tag as string==item.Id);
            string lastId=lib.Children(c.Id).Last().Id;
            RaiseDrop(firstCard,lastId,new Point(5,80));Check(lib.Children(c.Id).First().Id==lastId && store.Load().Children(c.Id).First().Id==lastId,"Dragging onto a card's leading edge reorders both saved and rendered entries");
            radial.Navigate(c.Id);Check(radial.DisplayedIds.Contains(lastId) || radial.CurrentPage>0,"Fan reloads the same ordered library");
            win.Navigate(null);win.UpdateLayout();var rootFolder=Find<Button>(win).First(x=>x.Tag as string==a.Id);RaiseDrop(rootFolder,lastId);
            Check(lib.Find(lastId)!.ParentId==a.Id,"Folder center remains a move-into target after adding edge reordering");
            var beforeOrder=lib.Entries.Select(e=>e.Id).ToArray();Check(!lib.ReorderEntries(new[]{a.Id},c.Id,false) && beforeOrder.SequenceEqual(lib.Entries.Select(e=>e.Id)),"Reordering rejects moving a folder into descendants atomically");
            win.Hide();radial.Navigate(null);radial.UpdateLayout();
            radial.Slot(a.Id)!.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseEnterEvent});
            PumpUntil(()=>radial.PreviewDepth==1);Check(radial.PreviewDepth==1,"Actual mouse-enter event opens the adjacent folder panel without clicking");
            radial.Slot(a.Id)!.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseLeaveEvent});PumpUntil(()=>radial.PreviewDepth==0);
            Check(radial.PreviewDepth==0,"Leaving the folder and its panel dismisses it after a transit delay");
            radial.ShowFolderPreview(a,radial.Slot(a.Id)!);var quick=radial.FolderPreview!;quick.UpdateLayout();
            Check(radial.PreviewDepth==1 && !quick.Topmost && quick.Owner==radial && Find<Button>(quick).Any(x=>x.Tag as string==b.Id),"Hover folder panel shows its children beside the fan with normal window stacking");
            var nestedQuick=Find<Button>(quick).First(x=>x.Tag as string==b.Id);radial.ShowFolderPreview(b,nestedQuick,1);
            Check(radial.PreviewDepth==2 && Find<Button>(radial.FolderPreview!).Any(x=>x.Tag as string==c.Id),"Nested hover folders open a second adjacent panel");
            var nestedFolder=Find<Button>(radial.FolderPreview!).First(x=>x.Tag as string==c.Id);nestedFolder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(radial.PreviewDepth==0 && radial.CurrentFolder==c.Id,"Choosing a hover panel item opens it directly and closes the panels");
            radial.Navigate(null);radial.ShowFolderPreview(a,radial.Slot(a.Id)!);radial.Hide();Check(radial.PreviewDepth==0,"Hiding the launcher also closes its hover panels");radial.Show();
            foreach(var preset in ThemeSettings.Presets){preset.Validate();radial.UpdateTheme(preset.Copy());Check(radial.library.Theme.Name==preset.Name && radial.PageCount==Math.Max(1,(lib.Children(null).Count()+preset.Slots-1)/preset.Slots),"Preset palette and layout apply: "+preset.Name);}
            var slotRect=BackgroundTools.ImageRect(900,600,new BackgroundSettings {Zoom=2.8,OffsetX=1,OffsetY=-1},sector.Bounds);
            Check(slotRect.Contains(sector.Bounds),"Per-slot images cover their curved slot at extreme crop offsets");
            var themeEditor=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};themeEditor.Show();themeEditor.UsePreset(ThemeSettings.Presets[2]);
            Check(lib.Theme.Name=="네온 오빗" && themeEditor.Draft.Name=="딸기 밀크","Theme preview edits are isolated until applying");
            var colorBox=Find<TextBox>(themeEditor).First();colorBox.Text="#234567";Check(themeEditor.Draft.Background=="#234567" && themeEditor.Draft.Name=="내 커스텀 테마","Custom hex colors update the actual theme draft");
            Find<Slider>(themeEditor).First(x=>x.Minimum==5).Value=9;Check(themeEditor.Draft.Slots==9,"Custom display layout supports nine slots");themeEditor.Close();
            var invalidEditor=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
            bool invalidStayedOpen=false;
            app.Dispatcher.BeginInvoke(new Action(()=>{Find<TextBox>(invalidEditor).First().Text="bad";Find<Button>(invalidEditor).First(x=>x.Content as string=="적용").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));invalidStayedOpen=invalidEditor.IsVisible && invalidEditor.Result==null;Find<Button>(invalidEditor).First(x=>x.Content as string=="취소").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
            invalidEditor.ShowDialog();Check(invalidStayedOpen,"Invalid custom color input keeps the dialog open and does not apply a theme");
            var applyTheme=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
            app.Dispatcher.BeginInvoke(new Action(()=>{applyTheme.UsePreset(ThemeSettings.Presets[4]);Find<Button>(applyTheme).First(x=>x.Content as string=="적용").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
            Check(applyTheme.ShowDialog()==true && applyTheme.Result!.Name=="오션 블루","Theme Apply button commits a selected preset");
            var slotEditor=new BackgroundWindow(store,lib,item){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};slotEditor.SelectImage(backgroundSource);var slotOldZoom=item.Background.Zoom;slotEditor.Draft.Zoom=2.4;Check(item.Background.Zoom==slotOldZoom,"Per-slot background preview leaves the entry unchanged until applying");
            app.Dispatcher.BeginInvoke(new Action(()=>{Find<Slider>(slotEditor).First(x=>x.Minimum==1).Value=2.6;Find<Button>(slotEditor).First(x=>x.Content as string=="적용").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
            Check(slotEditor.ShowDialog()==true && slotEditor.Result!.Zoom==2.6 && slotEditor.Result.ImageFile!=item.Background.ImageFile && lib.Background.Zoom==oldZoom,"Per-slot Apply saves an independent image and crop without changing the overall background");
            var entryEditor=new EditorWindow(store,item){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
            app.Dispatcher.BeginInvoke(new Action(()=>Find<Button>(entryEditor).First(x=>x.Content as string=="저장").RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
            Check(entryEditor.ShowDialog()==true && entryEditor.Result!.Background.ImageFile==item.Background.ImageFile && entryEditor.Result.Background.Zoom==item.Background.Zoom,"Editing an entry retains its per-slot background");
            var stable=new Library{UiRevision=3,HideAfterLaunch=true};Check(stable.ApplyUiUpdate() && stable.HideAfterLaunch && stable.UiRevision==4,"Revision four preserves the user's existing launch preference");
            bool invalidTheme=false;try{new ThemeSettings{Accent="not-a-color"}.Validate();}catch(InvalidDataException){invalidTheme=true;}Check(invalidTheme,"Imported theme values are validated");
            bool displayFont = UI.DisplayFont.GetTypefaces().Any(x => x.TryGetGlyphTypeface(out var g) && g.FamilyNames.Values.Any(n => n == "Jua"));
            bool bodyFont = UI.RoundedFont.GetTypefaces().Any(x => x.TryGetGlyphTypeface(out var g) && g.FamilyNames.Values.Any(n => n == "Gowun Dodum"));
            Check(displayFont && bodyFont, "Bundled rounded Jua and Gowun Dodum fonts resolve to actual typefaces");
            radial.Hide(); app.Shutdown();
            log.Add("ALL CHECKS PASSED"); File.WriteAllLines(Path.Combine(output, "검증결과.txt"), log); return 0;
        }
        catch (Exception ex) { log.Add(ex.ToString()); File.WriteAllLines(Path.Combine(output, "검증결과.txt"), log); return 1; }
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var frame=new DispatcherFrame();var watch=System.Diagnostics.Stopwatch.StartNew();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(15)};
        timer.Tick+=(_,_)=>{if(condition() || watch.ElapsedMilliseconds>1000)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
    }
    private static IEnumerable<T> Find<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i); if (child is T value) yield return value;
            foreach (var x in Find<T>(child)) yield return x;
        }
    }
    private static void RaiseDrop(Button target, string id, Point? position = null)
    {
        target.UpdateLayout();
        Point point=position ?? new Point(target.ActualWidth/2,target.ActualHeight/2);
        if(position==null && target.Width==920 && target.Content is Canvas canvas) {var shape=canvas.Children.OfType<System.Windows.Shapes.Path>().First();var rect=shape.Data.Bounds;point=new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2);}
        var constructor = typeof(DragEventArgs).GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).First();
        var args = (DragEventArgs)constructor.Invoke(new object[] { new DataObject(LibraryWindow.EntryDragFormat, new[] { id }), DragDropKeyStates.None, DragDropEffects.Move, target, point });
        args.RoutedEvent = DragDrop.DropEvent; target.RaiseEvent(args);
    }
}

internal static class Preview
{
    internal static void CreateBackground(string path)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(73, 64, 142), Color.FromRgb(22, 129, 145), 25), null, new Rect(0, 0, 1200, 650));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(95, 244, 184, 225)), null, new Point(280, 220), 260, 260);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 175, 238, 226)), null, new Point(975, 390), 280, 230);
        }
        var bitmap = new RenderTargetBitmap(1200, 650, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
    internal static void Snapshot(Window window, string path)
    {
        window.UpdateLayout(); var content = (FrameworkElement)window.Content;
        double width=content.ActualWidth+content.Margin.Left+content.Margin.Right,height=content.ActualHeight+content.Margin.Top+content.Margin.Bottom;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width),(int)Math.Ceiling(height),96,96,PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual(); using (var dc = backdrop.RenderOpen()) dc.DrawRectangle(window.Background,null,new Rect(0,0,width,height));bitmap.Render(backdrop);
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(content) { Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(content.Margin.Left,content.Margin.Top,content.ActualWidth,content.ActualHeight),AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top },null,new Rect(content.Margin.Left,content.Margin.Top,content.ActualWidth,content.ActualHeight));
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    public static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var store = new Store(Path.Combine(output, "preview-data"));
        var lib = new Library(); string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var games = new Entry { Name = "게임", IsFolder = true, Note = "잠깐 쉬어 가고 싶을 때.\n즐겨 하는 게임을 모아 두었어요." };
        var work = new Entry { Name = "작업", IsFolder = true, Note = "작업에 필요한 앱과 자료를 한곳에." };
        var tools = new Entry { Name = "유틸리티", IsFolder = true, Note = "매일 쓰는 작은 도구들" };
        var sub = new Entry { Name = "문서 작업", IsFolder = true, ParentId = work.Id };
        lib.Entries.AddRange(new[] { games, work, tools,
            new Entry { Name = "메모장", Target = Path.Combine(windows, "System32", "notepad.exe"), Note = "떠오른 아이디어를 빠르게 기록하세요.\n오늘 해야 할 일도 가볍게 메모해 두기." },
            new Entry { Name = "파일 탐색기", Target = Path.Combine(windows, "explorer.exe") },
            new Entry { Name = "계산기", Target = Path.Combine(windows, "System32", "calc.exe") },
            new Entry { Name = "설정", Target = "ms-settings:" }, sub,
            new Entry { Name = "메모장", Target = Path.Combine(windows, "System32", "notepad.exe"), ParentId = work.Id },
            new Entry { Name = "작업 파일", Target = windows, ParentId = work.Id },
            new Entry { Name = "문서 초안", Target = Path.Combine(output, "초안.txt"), ParentId = sub.Id }
        });
        var win = new LauncherWindow(store, lib) { ShowActivated = false, Left = -20000, Top = -20000 };
        win.Show(); win.UpdateLayout();
        Snapshot(win, Path.Combine(output, "미리보기-부채꼴.png"));
        var hoverEntry = lib.Entries.First(x => x.Name == "메모장");
        win.Slot(hoverEntry.Id)!.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
        Snapshot(win, Path.Combine(output, "미리보기-메모.png"));
        win.Navigate(work.Id); Snapshot(win, Path.Combine(output, "미리보기-폴더.png"));
        var manager = new LibraryWindow(store, lib) { ShowActivated = false, Left = -20000, Top = -20000 };
        manager.Show(); manager.UpdateLayout(); Snapshot(manager, Path.Combine(output, "미리보기-설정.png")); manager.Close();
        Directory.CreateDirectory(store.IconDirectory); string backgroundPath = Path.Combine(store.DirectoryPath, "sample-background.png"); CreateBackground(backgroundPath);
        var backgroundEditor = new BackgroundWindow(store, lib) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
        backgroundEditor.Show(); backgroundEditor.SelectImage(backgroundPath); Snapshot(backgroundEditor, Path.Combine(output, "미리보기-배경편집.png")); backgroundEditor.Close();
        win.UpdateBackground(new BackgroundSettings { Darkness = .2, Zoom = 1.35, OffsetX = .2 }, backgroundPath); Snapshot(win, Path.Combine(output, "미리보기-배경.png"));
        var editor = new EditorWindow(store, lib.Entries.First(x => x.Name == "메모장")) { Owner = win, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -19000, Top = -19000 };
        editor.Show(); Snapshot(editor, Path.Combine(output, "미리보기-아이콘편집.png")); editor.Close();
        win.UpdateBackground(new BackgroundSettings(),null);win.Navigate(null);
        foreach(var preset in ThemeSettings.Presets){win.UpdateTheme(preset.Copy());Snapshot(win,Path.Combine(output,"테마-"+preset.Name+".png"));}
        lib.Theme=ThemeSettings.Presets[0].Copy();win.UpdateTheme(lib.Theme);win.Navigate(null);win.ShowFolderPreview(work,win.Slot(work.Id)!);Snapshot(win.FolderPreview!,Path.Combine(output,"미리보기-옆폴더.png"));win.CloseFolderPreview();
        var themeEditor=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};themeEditor.Show();Snapshot(themeEditor,Path.Combine(output,"미리보기-테마설정.png"));themeEditor.Close();
        hoverEntry.Background=new BackgroundSettings { ImageFile=store.CopyIcon(backgroundPath),Zoom=1.3,OffsetX=.35,Darkness=.25 };
        win.UpdateTheme(lib.Theme);Snapshot(win,Path.Combine(output,"미리보기-칸배경.png"));
        var slotEditor=new BackgroundWindow(store,lib,hoverEntry){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};slotEditor.Show();Snapshot(slotEditor,Path.Combine(output,"미리보기-칸배경설정.png"));slotEditor.Close();
        win.RotateDial(1);Snapshot(win,Path.Combine(output,"미리보기-휠순환.png"));
        win.Hide(); app.Shutdown(); return 0;
    }
}
