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
            Check(Store.BackupFileName(new DateTime(2026,10,14,14,15,30))=="DeckLauncher-backup-20261014-141530.zip","Backup default filename includes date and 24-hour time to seconds");
            var nameStore=new Store(Path.Combine(output,"backup-names-"+Guid.NewGuid().ToString("N")));
            var nameLibrary=new Library {Entries=new(){new Entry{Name="원래 내용"}}};string nameRequested=Path.Combine(nameStore.DirectoryPath,"same-second.zip");
            string nameFirst=nameStore.Backup(nameRequested,nameLibrary);byte[] originalBackupBytes=File.ReadAllBytes(nameFirst);nameLibrary.Entries[0].Name="새 내용";string nameSecond=nameStore.Backup(nameRequested,nameLibrary);
            Check(Path.GetFileName(nameSecond)=="same-second_2.zip" && originalBackupBytes.SequenceEqual(File.ReadAllBytes(nameFirst)),"Duplicate names use a suffix without overwriting the previous backup");
            Check(Store.ReadBackup(nameSecond).Library.Entries[0].Name=="새 내용","A suffixed backup contains the newly saved data");
            Directory.CreateDirectory(Path.Combine(nameStore.DirectoryPath,"same-second_3.zip"));string nameFourth=nameStore.Backup(nameRequested,nameLibrary);Check(Path.GetFileName(nameFourth)=="same-second_4.zip","Backup filenames skip both existing files and directory names");
            var lib = store.Load(); Check(lib.Entries.Count == 0, "First run creates an empty library");
            Check(!lib.WheelDialEnabled,"Wheel dial is disabled by default for new libraries");
            string legacyFile = Path.Combine(store.DirectoryPath, "legacy.json");
            File.WriteAllText(legacyFile, "{\"Version\":1,\"Entries\":[{\"Id\":\"legacy\",\"Name\":\"기존 항목\"}]}");
            Check(Store.Read(legacyFile).Find("legacy")?.Note == "", "Previous library files remain compatible without note fields");
            var oldAppearance = Store.Read(legacyFile);
            Check(oldAppearance.Appearance.Title=="나의 바로가기" && oldAppearance.Appearance.Opacity==1 && oldAppearance.Find("legacy")!.Opacity==1,"Legacy libraries get opaque appearance defaults without changing entries");
            var appearanceLib = new Library { Appearance=new AppearanceSettings {Title="내 공간",Note="직접 작성한 문구\n한 줄 더",Opacity=.55},Entries=new(){new Entry{Name="테스트",Opacity=.35}} };
            var appearanceStore = new Store(Path.Combine(output,"appearance-test"));appearanceStore.Save(appearanceLib);
            var appearanceRead=appearanceStore.Load();Check(appearanceRead.Appearance.Title=="내 공간" && appearanceRead.Appearance.Note.Contains("한 줄 더") && appearanceRead.Entries[0].Opacity==.35,"Custom wording and per-entry opacity survive save/load");
            string appearanceBackup=Path.Combine(output,"appearance-backup.zip");appearanceStore.Backup(appearanceBackup,appearanceLib);var appearanceRestored=appearanceStore.Restore(Store.ReadBackup(appearanceBackup));
            Check(appearanceRestored.Appearance.Opacity==.55 && appearanceRestored.Entries[0].Opacity==.35,"Backups retain appearance and slot opacity");
            foreach(double value in new[]{-.01,1.2,double.NaN}) {bool opacityRejected=false;try{new AppearanceSettings {Opacity=value}.Validate();}catch(InvalidDataException){opacityRejected=true;}Check(opacityRejected,"Invalid whole-launcher opacity rejected: "+value);}
            bool badSlotOpacity=false;try{new Library {Entries=new(){new Entry{Name="오류",Opacity=-.01}}}.Validate();}catch(InvalidDataException){badSlotOpacity=true;}Check(badSlotOpacity,"Invalid slot opacity is rejected");
            new AppearanceSettings {Title="",Note=""}.Validate();bool longNote=false;try{new AppearanceSettings {Note=new string('가',301)}.Validate();}catch(InvalidDataException){longNote=true;}Check(longNote,"Empty wording is supported and excessive note length is rejected");
            var batchLibrary=new Library();var batchFolder=new Entry{Name="폴더",IsFolder=true};var batchNested=new Entry{Name="하위",IsFolder=true,ParentId=batchFolder.Id};var batchChild=new Entry{Name="앱",ParentId=batchNested.Id,Opacity=.4,Background=new BackgroundSettings{ImageFile="old.png"}};batchLibrary.Entries.AddRange(new[]{batchFolder,batchNested,batchChild});
            Check(batchLibrary.BackgroundTargets(batchFolder.Id,"level").Count==1 && batchLibrary.BackgroundTargets(batchFolder.Id,"tree").Count==2,"Batch scopes distinguish direct children and descendants");
            batchLibrary.ApplyBackgrounds(new[]{batchNested.Id,batchChild.Id},null,1.7);Check(batchChild.Background.ImageFile=="old.png" && batchChild.Opacity==1.7,"Opacity-only batch preserves existing images");
            batchLibrary.ApplyBackgrounds(new[]{batchNested.Id,batchChild.Id},new BackgroundSettings{ImageFile="shared.png"},null);batchNested.Background.Zoom=2;Check(batchChild.Background.Zoom==1 && batchChild.Opacity==1.7,"Batch images use independent settings without changing opacity");
            bool badBatch=false;try{batchLibrary.ApplyBackgrounds(new[]{batchChild.Id,"missing"},null,.2);}catch(InvalidDataException){badBatch=true;}Check(badBatch && batchChild.Opacity==1.7,"Invalid batch targets cannot cause partial changes");
            var densityDialog=new BackgroundWindow(store,batchLibrary,batchChild){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};densityDialog.Show();densityDialog.UpdateLayout();var densitySlider=Find<Slider>(densityDialog).First(t=>t.Minimum==-100);densitySlider.Value=-80;Check(densityDialog.ResultOpacity==1.8 && batchChild.Opacity==1.7,"Negative slot density previews inside background settings without mutating originals");SaveBulkSnapshot(densityDialog,Path.Combine(output,"칸-배경-투명도.png"));densityDialog.Close();
            var batchDialog=new BackgroundWindow(store,batchLibrary,null,true,batchFolder.Id){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};batchDialog.Show();batchDialog.UpdateLayout();Find<Slider>(batchDialog).First(t=>t.Minimum==-100).Value=-60;batchDialog.UpdateLayout();SaveBulkSnapshot(batchDialog,Path.Combine(output,"칸-배경-일괄.png"));batchDialog.Close();
            var separateEntry=new Entry{Name="독립 투명도",Target="https://example.com",Note="메모 보존",SeparateOpacity=true,Opacity=.2,IconOpacity=.7,TextOpacity=.4};
            var separateLib=new Library{Entries=new(){separateEntry}};var separateStore=new Store(Path.Combine(output,"separate-data"));separateStore.Save(separateLib);
            Check(separateStore.Load().Entries[0].SeparateOpacity && separateStore.Load().Entries[0].IconOpacity==.7 && separateStore.Load().Entries[0].TextOpacity==.4,"Separate mode and foreground alpha persist through save/load");
            string separateBackup=separateStore.Backup(Path.Combine(output,"separate.zip"),separateLib);var restoredSeparate=separateStore.Restore(Store.ReadBackup(separateBackup));Check(restoredSeparate.Entries[0].TextOpacity==.4 && restoredSeparate.Entries[0].SeparateOpacity,"Backup retains independent opacity");
            Check(!oldAppearance.Find("legacy")!.SeparateOpacity && oldAppearance.Find("legacy")!.IconOpacity==1,"Old entries retain linked opacity defaults");
            bool rejectedSeparate=false;try{separateLib.ApplyBackgrounds(new[]{separateEntry.Id},null,.6,true,double.NaN,.8);}catch(InvalidDataException){rejectedSeparate=true;}Check(rejectedSeparate && separateEntry.Opacity==.2 && separateEntry.TextOpacity==.4,"Invalid independent batch alpha cannot partially mutate entries");
            foreach(var design in new[]{"classic","cat","neon","paper","modern","space"})
            {
                separateLib.Theme.Design=design;var separateWin=new LauncherWindow(separateStore,separateLib,true);Check(separateWin.SlotOpacity(separateEntry.Id)==1 && separateWin.SlotLayerOpacity(separateEntry.Id,0)==.2 && separateWin.SlotLayerOpacity(separateEntry.Id,1)==.7 && separateWin.SlotLayerOpacity(separateEntry.Id,2)==.4,"Independent background/icon/text layer alpha: "+design);
                var renderedContents=(Canvas)separateWin.Slot(separateEntry.Id)!.Content;renderedContents.Measure(new Size(920,454));renderedContents.Arrange(new Rect(0,0,920,454));var fullAlpha=MaximumAlpha(renderedContents);
                renderedContents.Opacity=.2;Check(fullAlpha>100 && MaximumAlpha(renderedContents)<52,"Actual WPF composition preserves foreground alpha, then applies group alpha: "+design);separateWin.Close();
            }
            var separateDialog=new BackgroundWindow(separateStore,separateLib,separateEntry){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};separateDialog.Show();var modeBox=Find<CheckBox>(separateDialog).First(c=>c.Content?.ToString()?.StartsWith("배경 · 아이콘")==true);Find<Slider>(separateDialog).First(c=>c.Tag?.ToString()=="icon-opacity").Value=20;Find<Slider>(separateDialog).First(c=>c.Tag?.ToString()=="text-opacity").Value=55;
            Check(separateDialog.ResultSeparateOpacity && separateDialog.ResultIconOpacity==.8 && separateDialog.ResultTextOpacity==.45 && separateEntry.IconOpacity==.7,"Independent preview does not mutate original entries");SaveBulkSnapshot(separateDialog,Path.Combine(output,"칸-독립-투명도.png"));modeBox.IsChecked=false;Check(!separateDialog.ResultSeparateOpacity && !Find<Slider>(separateDialog).First(c=>c.Tag?.ToString()=="icon-opacity").IsEnabled,"Relink restores group controls while preserving independent slider values");separateDialog.Close();
            separateLib.ApplyBackgrounds(new[]{separateEntry.Id},null,.5,false,.8,.45);var linkedWin=new LauncherWindow(separateStore,separateLib,true);Check(linkedWin.SlotOpacity(separateEntry.Id)==.5 && linkedWin.SlotLayerOpacity(separateEntry.Id,1)==1,"Linked mode keeps original group opacity behavior");linkedWin.Close();
            separateLib.ApplyBackgrounds(new[]{separateEntry.Id},new BackgroundSettings(),null);Check(!separateEntry.SeparateOpacity && separateEntry.IconOpacity==.8 && separateEntry.TextOpacity==.45,"Image-only application preserves independent mode and values");
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
            lib.CustomThemes.Add(new SavedTheme {Name="냥냥",Settings=ThemeSettings.DesignPresets[1].Copy()});
            var backupFile = Path.Combine(store.DirectoryPath, "backup.zip"); store.Backup(backupFile, lib);
            var backup = Store.ReadBackup(backupFile);
            var restored = store.Restore(backup);
            Check(restored.Find(item.Id)?.IconFile != copied && Native.LoadImage(store.IconPath(restored.Find(item.Id)!.IconFile!)) != null && restored.Trail(c.Id) == lib.Trail(c.Id), "ZIP backup restores hierarchy and custom images without overwriting old icons");
            Check(restored.Find(item.Id)!.Background.ImageFile!=item.Background.ImageFile && File.Exists(store.IconPath(restored.Find(item.Id)!.Background.ImageFile!)) && restored.Find(item.Id)!.Background.OffsetX==-.4,"ZIP backup restores per-slot images and independent crop settings");
            Check(restored.CustomThemes.Single().Name=="냥냥" && restored.CustomThemes.Single().Settings.Design=="cat","Backup restores separately saved custom themes and their design style");
            Check(restored.Theme.Accent=="#D7AFEF" && restored.Theme.Name=="테스트 테마","ZIP backup preserves custom theme settings");
            Check(restored.Find(item.Id)?.Note == item.Note, "ZIP backup preserves user notes");
            Check(restored.Background.ImageFile != lib.Background.ImageFile && File.Exists(store.IconPath(restored.Background.ImageFile!)) && restored.Background.Zoom == lib.Background.Zoom && restored.Background.OffsetY == lib.Background.OffsetY, "ZIP backup restores background image and crop settings");
            Check(lib.ApplyUiUpdate() && lib.HideAfterLaunch && !lib.ApplyUiUpdate() && lib.Entries.Count == 4, "UI migration changes preferences once while preserving registered entries");
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


            var fullyTransparentLibrary=new Library {Appearance=new AppearanceSettings {Opacity=0},Entries=new(){new Entry{Name="투명 칸",Opacity=0}}};var fullStore=new Store(Path.Combine(output,"fully-transparent"));fullStore.Save(fullyTransparentLibrary);var fullBackup=fullStore.Backup(Path.Combine(output,"fully-transparent.zip"),fullyTransparentLibrary);Check(fullStore.Load().Appearance.Opacity==0 && fullStore.Restore(Store.ReadBackup(fullBackup)).Entries[0].Opacity==0,"Fully transparent whole launcher and slots persist through save and backup");
            var zeroDialog=new BackgroundWindow(fullStore,fullyTransparentLibrary,fullyTransparentLibrary.Entries[0]){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};zeroDialog.Show();var zeroSlider=Find<Slider>(zeroDialog).First(x=>x.Minimum==-100);Check(zeroSlider.Maximum==100 && zeroDialog.ResultOpacity==0,"Slot controls support 100 percent transparency");zeroDialog.Close();
            var globalZeroDialog=new BackgroundWindow(fullStore,fullyTransparentLibrary){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};globalZeroDialog.Show();Check(Find<Slider>(globalZeroDialog).Any(x=>x.Minimum==0 && x.Maximum==100 && x.Value==100),"Whole launcher controls support 100 percent transparency");globalZeroDialog.Close();
            foreach(var d in new[]{"classic","cat","neon","paper","modern","space"})
            {
                fullyTransparentLibrary.Theme.Design=d;var e=fullyTransparentLibrary.Entries[0];e.SeparateOpacity=false;var zeroWin=new LauncherWindow(fullStore,fullyTransparentLibrary,true);var content=(Canvas)zeroWin.Slot(e.Id)!.Content;content.Measure(new Size(920,454));content.Arrange(new Rect(0,0,920,454));Check(MaximumAlpha(content)==0,"Linked 100 percent transparency draws no slot pixels: "+d);zeroWin.Close();
                e.SeparateOpacity=true;var foregroundWin=new LauncherWindow(fullStore,fullyTransparentLibrary,true);content=(Canvas)foregroundWin.Slot(e.Id)!.Content;content.Measure(new Size(920,454));content.Arrange(new Rect(0,0,920,454));Check(MaximumAlpha(content)>240,"100 percent background transparency leaves independent foreground visible: "+d);foregroundWin.Close();
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ReopenRegression.Run(output,Check);
            TaskbarRegression.Run(output,Check);
            fullyTransparentLibrary.Theme.Design="classic";fullStore.Save(fullyTransparentLibrary);var recoveryWindow=new LauncherWindow(fullStore,fullyTransparentLibrary){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};recoveryWindow.Show();
            app.Dispatcher.BeginInvoke(new Action(()=>{var manager=recoveryWindow.OwnedWindows.OfType<LibraryWindow>().First();Check(manager.IsVisible && !manager.Topmost,"Activating a fully transparent launcher opens settings for recovery");manager.Close();}),DispatcherPriority.ApplicationIdle);recoveryWindow.Reveal();recoveryWindow.Hide();
            var uiColorLibrary=new Library {Theme=ThemeSettings.DesignPresets[1].Copy()};Check(uiColorLibrary.InterfaceTheme.Background==uiColorLibrary.Theme.Background,"Settings palette follows launcher colors by default");uiColorLibrary.SettingsColors.Take(ThemeSettings.Presets[0]);uiColorLibrary.SettingsColors.Separate=true;uiColorLibrary.SettingsColors.Initialized=true;Check(uiColorLibrary.InterfaceTheme.Background=="#111C2B" && uiColorLibrary.Theme.Design=="cat" && uiColorLibrary.Theme.Background=="#FFF5EC","Separate settings palette does not alter launcher design or colors");
            var uiColorStore=new Store(Path.Combine(output,"ui-colors"));uiColorStore.Save(uiColorLibrary);var uiColorBackup=uiColorStore.Backup(Path.Combine(output,"ui-colors.zip"),uiColorLibrary);Check(uiColorStore.Restore(Store.ReadBackup(uiColorBackup)).SettingsColors.Separate && uiColorStore.Load().InterfaceTheme.Background=="#111C2B","Settings palette survives save and backup restore");
            var paletteWindow=new SettingsColorsWindow(uiColorLibrary){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};paletteWindow.Show();paletteWindow.Draft.Take(ThemeSettings.Presets[2]);Check(uiColorLibrary.InterfaceTheme.Background=="#111C2B","Settings color editor works on a private draft until Apply");SaveBulkSnapshot(paletteWindow,Path.Combine(output,"설정창-색상-분리.png"));paletteWindow.Close();
            var uiColorManager=new LibraryWindow(uiColorStore,uiColorLibrary){ShowActivated=false,Left=-20000,Top=-20000};uiColorManager.Show();Check(((SolidColorBrush)uiColorManager.Background).Color==((SolidColorBrush)UI.Brush("#111C2B")).Color,"Manager uses the separate settings background");uiColorManager.Close();
            var isolatedThemeWindow=new ThemeWindow(uiColorStore,uiColorLibrary);isolatedThemeWindow.UsePreset(ThemeSettings.Presets[2]);Check(((SolidColorBrush)isolatedThemeWindow.Background).Color==((SolidColorBrush)UI.Brush("#111C2B")).Color,"Changing launcher presets keeps settings editor colors separate");isolatedThemeWindow.Close();
            foreach(var accent in new[]{"#FFFFFF","#FFFF00","#FFF5EC","#777777","#35F0FF","#F2879B","#111C2B"}){double accentLuminance=Skin.Luminance(accent),inkLuminance=Skin.Luminance(Skin.ContrastText(accent));Check((Math.Max(accentLuminance,inkLuminance)+.05)/(Math.Min(accentLuminance,inkLuminance)+.05)>=4.5,"Folder badge text has at least 4.5 contrast for accent "+accent);}
            var recoveryHintWindow=new BackgroundWindow(fullStore,fullyTransparentLibrary){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};recoveryHintWindow.Show();Check(Find<TextBlock>(recoveryHintWindow).Any(t=>t.Text.Contains("한 번 더") && t.Text.Contains("설정창")),"Overall transparency settings explain how to recover at 100 percent");recoveryHintWindow.Close();
            var appearancePreview=new LauncherWindow(appearanceStore,appearanceLib,true);
            Check(appearancePreview.DisplayedTitle=="내 공간" && appearancePreview.DisplayedNote==appearanceLib.Appearance.Note,"Custom central wording is actually rendered");
            Check(appearancePreview.RenderedOpacity==.55 && appearancePreview.SlotOpacity(appearanceLib.Entries[0].Id)==.35,"Whole launcher and entry visual groups apply separate opacity");
            appearancePreview.ShowNote(appearanceLib.Entries[0]);appearancePreview.Navigate(null);Check(appearancePreview.DisplayedNote==appearanceLib.Appearance.Note,"Hover note returns to the custom central wording");
            var wordingDialog=new AppearanceWindow(appearanceStore,appearanceLib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};wordingDialog.Show();wordingDialog.UpdateLayout();var wordingBoxes=Find<TextBox>(wordingDialog).ToList();wordingBoxes[0].Text="미리보기만";wordingDialog.RefreshPreview();
            Check(wordingDialog.Draft.Title=="미리보기만" && wordingDialog.Preview.RenderedOpacity==.55,"Wording dialog previews live opacity changes");
            Check(appearanceLib.Appearance.Title=="내 공간" && appearanceLib.Appearance.Opacity==.55,"Preview edits remain isolated until Apply");wordingBoxes[0].Text="오늘의 작은 작업실";wordingBoxes[1].Text="필요한 앱만, 가볍게 한 번에.\n오늘도 천천히 시작해요.";wordingDialog.UpdateLayout();
            var appearanceBitmap=new RenderTargetBitmap((int)Math.Ceiling(wordingDialog.ActualWidth),(int)Math.Ceiling(wordingDialog.ActualHeight),96,96,PixelFormats.Pbgra32);appearanceBitmap.Render(wordingDialog);var appearanceEncoder=new PngBitmapEncoder();appearanceEncoder.Frames.Add(BitmapFrame.Create(appearanceBitmap));using(var appearanceStream=File.Create(Path.Combine(output,"문구-투명도-설정.png")))appearanceEncoder.Save(appearanceStream);
            wordingDialog.Close();appearancePreview.Close();

            var win = new LibraryWindow(store, lib) { ShowActivated = false, Left = -20000, Top = -20000 };
            win.Show(); win.UpdateLayout();
            void SetWheel(bool value)
            {
                var sheet=win.OptionsSheet();sheet.ShowActivated=false;sheet.WindowStartupLocation=WindowStartupLocation.Manual;sheet.Left=sheet.Top=-20000;sheet.Show();sheet.UpdateLayout();var choice=Find<CheckBox>(sheet).First(t=>t.Content as string=="휠로 다이얼 돌리기");choice.IsChecked=value;choice.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));sheet.Close();
            }
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
            // Wheel is opt-in; use the real settings checkable menu to turn it on.
            radial.ChangePage(-99);var defaultIds=radial.DisplayedIds.ToArray();
            radial.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.MouseWheelEvent});
            Check(defaultIds.SequenceEqual(radial.DisplayedIds) && radial.DialStart==0,"Actual mouse wheel has no navigation effect with the default disabled preference");
            SetWheel(true);
            Check(lib.WheelDialEnabled && store.Load().WheelDialEnabled,"Settings menu explicitly enables wheel scrolling and persists it");
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
            Check(lib.Theme.Name==ThemeSettings.Presets[^1].Name && themeEditor.Draft.Name=="딸기 밀크","Theme preview edits are isolated until applying");
            var colorBox=Find<TextBox>(themeEditor).First();colorBox.Text="#234567";Check(themeEditor.Draft.Background=="#234567" && themeEditor.Draft.Name.StartsWith("나의 테마"),"Custom hex colors update the actual theme draft");
            Find<Slider>(themeEditor).First(x=>x.Minimum==5).Value=9;Check(themeEditor.Draft.Slots==9,"Custom display layout supports nine slots");themeEditor.Close();
            var invalidEditor=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
            bool invalidStayedOpen=false;
            app.Dispatcher.BeginInvoke(new Action(()=>{Find<TextBox>(invalidEditor).First().Text="bad";Find<Button>(invalidEditor).First(x=>x.Content as string=="적용").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));invalidStayedOpen=invalidEditor.IsVisible && invalidEditor.Result==null;Find<Button>(invalidEditor).First(x=>x.Content as string=="취소").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
            invalidEditor.ShowDialog();Check(invalidStayedOpen,"Invalid custom color input keeps the dialog open and does not apply a theme");
            var applyTheme=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
            app.Dispatcher.BeginInvoke(new Action(()=>{applyTheme.UsePreset(ThemeSettings.Presets[4]);Find<Button>(applyTheme).First(x=>x.Content as string=="적용").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
            Check(applyTheme.ShowDialog()==true && applyTheme.Result!.Name==ThemeSettings.Presets[4].Name,"Theme Apply button commits a selected preset");
            var slotEditor=new BackgroundWindow(store,lib,item){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};slotEditor.SelectImage(backgroundSource);var slotOldZoom=item.Background.Zoom;slotEditor.Draft.Zoom=2.4;Check(item.Background.Zoom==slotOldZoom,"Per-slot background preview leaves the entry unchanged until applying");
            app.Dispatcher.BeginInvoke(new Action(()=>{Find<Slider>(slotEditor).First(x=>x.Minimum==1).Value=2.6;Find<Button>(slotEditor).First(x=>x.Content as string=="적용").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
            Check(slotEditor.ShowDialog()==true && slotEditor.Result!.Zoom==2.6 && slotEditor.Result.ImageFile!=item.Background.ImageFile && lib.Background.Zoom==oldZoom,"Per-slot Apply saves an independent image and crop without changing the overall background");
            item.SeparateOpacity=true;item.IconOpacity=.72;item.TextOpacity=.48;
            var entryEditor=new EditorWindow(store,item){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
            app.Dispatcher.BeginInvoke(new Action(()=>Find<Button>(entryEditor).First(x=>x.Content as string=="저장").RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
            Check(entryEditor.ShowDialog()==true && entryEditor.Result!.Background.ImageFile==item.Background.ImageFile && entryEditor.Result.Background.Zoom==item.Background.Zoom,"Editing an entry retains its per-slot background");
            Check(entryEditor.Result!.SeparateOpacity && entryEditor.Result.IconOpacity==.72 && entryEditor.Result.TextOpacity==.48,"Editing name, notes or icon preserves independent transparency settings");
            var migratedCustom=new Library {UiRevision=4,Theme=new ThemeSettings {Name="내 커스텀 테마",Accent="#AABBCC"}};
            Check(migratedCustom.ApplyUiUpdate() && migratedCustom.CustomThemes.Count==1 && migratedCustom.CustomThemes[0].Settings.Accent=="#AABBCC" && !migratedCustom.ApplyUiUpdate(),"Migration preserves the previous custom appearance in a separate saved theme exactly once");
            var stable=new Library{UiRevision=3,HideAfterLaunch=true};Check(stable.ApplyUiUpdate() && stable.HideAfterLaunch && stable.UiRevision==6 && !stable.WheelDialEnabled,"Revision five preserves the launch preference and defaults wheel scrolling to disabled");
            foreach(var design in ThemeSettings.DesignPresets){design.Validate();radial.UpdateTheme(design.Copy());Check(radial.library.Theme.Design==design.Design && radial.RenderedSlots>0,"New design renders with its dedicated style: "+design.Name);}
            Check(ThemeSettings.Presets.Count==10 && ThemeSettings.DesignPresets.Count==6 && ThemeSettings.DesignPresets.Select(d=>d.Design).Distinct().Count()==6,"Ten color palettes and six distinct launcher designs remain separate collections");
            var customEditor=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};customEditor.Show();
            int oldCustomCount=lib.CustomThemes.Count;customEditor.UsePreset(ThemeSettings.DesignPresets[1]);customEditor.NewCustom();
            Check(customEditor.SaveCustomNamed("냥냥 커스텀") && customEditor.CustomCount==oldCustomCount+1 && lib.CustomThemes.Count==oldCustomCount,"Creating a named custom theme uses a separate isolated collection");
            customEditor.UsePreset(ThemeSettings.Presets.First(t=>t.Name=="딸기 밀크"));Check(customEditor.Draft.Design=="cat" && customEditor.Draft.Accent=="#AC3C6C","Changing color palette preserves the selected launcher layout");
            customEditor.UsePreset(ThemeSettings.DesignPresets[2]);Check(customEditor.Draft.Design=="neon" && customEditor.Draft.Accent=="#AC3C6C","Changing UI layout preserves the selected colors");
            customEditor.UseSaved(customEditor.SavedDraft.Last().Id);
            var firstCustom=customEditor.SavedDraft.Last().Id;customEditor.UseSaved(firstCustom);customEditor.Draft.Accent="#AA6688";
            Check(customEditor.SaveCustomNamed("냥냥 리본") && customEditor.CustomCount==oldCustomCount+1 && customEditor.SavedDraft.Last().Id==firstCustom,"Editing a saved custom theme retains its identity without duplicating it");
            customEditor.NewCustom();customEditor.Draft.Design="neon";Check(customEditor.SaveCustomNamed("밤의 네온") && customEditor.CustomCount==oldCustomCount+2,"Multiple custom themes can be saved independently");
            customEditor.UseSaved(firstCustom);Check(customEditor.Draft.Design=="cat" && customEditor.Draft.Accent=="#AA6688","Selecting a saved custom theme restores its individual colors and design");
            var savedToDelete=customEditor.SavedDraft.Last().Id;customEditor.UseSaved(savedToDelete);Find<Button>(customEditor).First(b=>b.Content as string=="선택 테마 삭제").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(customEditor.CustomCount==oldCustomCount+1 && customEditor.SavedDraft.Any(t=>t.Id==firstCustom),"Deleting one saved custom theme preserves the others");
            Check(ThemeSettings.Presets.Count==10 && !ThemeSettings.Presets.Any(t=>t.Name=="오션 블루" || t.Name=="글래스 스카이"),"Custom themes never reintroduce removed color choices");
            customEditor.Close();Check(lib.CustomThemes.Count==oldCustomCount,"Cancelling the custom editor leaves saved themes unchanged");
            var customApply=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
            app.Dispatcher.BeginInvoke(new Action(()=>{customApply.UsePreset(ThemeSettings.DesignPresets[2]);customApply.NewCustom();customApply.SaveCustomNamed("저장할 네온");Find<Button>(customApply).First(b=>b.Content as string=="적용").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));
            Check(customApply.ShowDialog()==true && customApply.Result!.Design=="neon" && customApply.SavedThemesResult!.Count==oldCustomCount+1,"Applying a custom theme returns both the selected appearance and named saved collection");
            lib.Theme=customApply.Result!;lib.CustomThemes=customApply.SavedThemesResult!;store.Save(lib);
            Check(store.Load().CustomThemes.Any(t=>t.Name=="저장할 네온" && t.Settings.Design=="neon"),"Named custom themes persist across reloads");
            var customBackup=Path.Combine(store.DirectoryPath,"custom-backup.zip");store.Backup(customBackup,lib);Check(Store.ReadBackup(customBackup).Library.CustomThemes.Any(t=>t.Name=="저장할 네온"),"New custom themes are included in future library backups");
            Check(Store.ReadBackup(customBackup).Library.WheelDialEnabled,"Library backups retain the opt-in wheel preference");
            SetWheel(false);radial.Navigate(c.Id);radial.ChangePage(1);var noWheelIds=radial.DisplayedIds.ToArray();
            radial.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.MouseWheelEvent});
            Check(!store.Load().WheelDialEnabled && noWheelIds.SequenceEqual(radial.DisplayedIds),"Disabling the settings toggle stops wheel navigation and keeps page buttons active");
            bool invalidTheme=false;try{new ThemeSettings{Accent="not-a-color"}.Validate();}catch(InvalidDataException){invalidTheme=true;}Check(invalidTheme,"Imported theme values are validated");
            var defaultPalette=ThemeSettings.Presets;
            Check(!defaultPalette.Any(t=>t.Name=="오션 블루" || t.Name=="글래스 스카이") && defaultPalette.Any(t=>t.Name=="클라우드 화이트") && defaultPalette.Any(t=>t.Name=="피치 크림") && defaultPalette.Any(t=>t.Name=="네온 민트"),"Removed colors are absent and design-derived colors are in the palette collection");
            var parsedCommand=TargetCommand.Parse("\"C:\\Program Files\\Discord\\Update.exe\" --processStart Discord.exe");
            Check(parsedCommand.Target=="C:\\Program Files\\Discord\\Update.exe" && parsedCommand.Arguments=="--processStart Discord.exe","Pasted shortcut targets split executable paths from launch arguments");
            var pasteEditor=new EditorWindow(store,new Entry {Name="복사 등록 테스트"},lib.Theme){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};pasteEditor.Show();
            var targetBox=Find<TextBox>(pasteEditor).First(t=>t.Tag as string=="Target");string cmdPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","cmd.exe");
            var pasteData=new DataObject(DataFormats.UnicodeText,"\""+cmdPath+"\" /d /c exit 0");var pasteArgs=new DataObjectPastingEventArgs(pasteData,false,DataFormats.UnicodeText){RoutedEvent=DataObject.PastingEvent};targetBox.RaiseEvent(pasteArgs);
            Check(pasteArgs.CommandCancelled && targetBox.Text==cmdPath && Find<TextBox>(pasteEditor).First(t=>t.Tag as string=="Arguments").Text=="/d /c exit 0","Actual paste handler fills executable and arguments fields separately");pasteEditor.Close();
            var unquoted=TargetCommand.Parse("C:\\Program Files\\Some App\\App.exe --profile \"My Profile\"");
            Check(unquoted.Target=="C:\\Program Files\\Some App\\App.exe" && unquoted.Arguments=="--profile \"My Profile\"","Unquoted executable paths with spaces retain quoted argument values");
            Check(TargetCommand.Parse("steam://rungameid/1").Target=="steam://rungameid/1","URI registration remains intact");
            win.Show();lib.Theme=ThemeSettings.Presets.First(t=>t.Name=="피치 크림").Copy();win.Navigate(null);win.UpdateLayout();
            Check(((SolidColorBrush)win.Background).Color==(Color)ColorConverter.ConvertFromString(lib.Theme.Background) && ((SolidColorBrush)win.Foreground).Color==(Color)ColorConverter.ConvertFromString(lib.Theme.Text),"Selected palette applies to management background and text");
            var textColor=Find<TextBlock>(win).First(t=>t.Text=="라이브러리").Foreground;
            Check(((SolidColorBrush)textColor).Color==(Color)ColorConverter.ConvertFromString(lib.Theme.Text),"Management headings inherit palette text colors");win.Hide();
            foreach(var layout in ThemeSettings.DesignPresets.Where(d=>d.Design!="classic"))
            {
                radial.UpdateTheme(layout.Copy());radial.Navigate(c.Id);radial.ChangePage(-99);radial.UpdateLayout();var firstId=radial.DisplayedIds[0];var rect=radial.SlotGeometry(firstId)!.Bounds;
                Point testPoint=layout.Design is "neon" or "space"?new Point(rect.X+rect.Width/2,rect.Y+38):new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2);
                var actualHit=radial.InputHitTest(testPoint) as DependencyObject;while(actualHit!=null && actualHit is not Button)actualHit=VisualTreeHelper.GetParent(actualHit);
                Check(actualHit==radial.Slot(firstId) && Math.Abs(radial.ShellGeometry().Bounds.Height-390)>.1,"Alternate layout has its own shell and correct icon hit testing: "+layout.Name);
                var last=lib.Children(c.Id).Last();RaiseDrop(radial.Slot(firstId)!,last.Id,new Point(rect.Right-4,rect.Y+rect.Height/2));Check(lib.Children(c.Id).ToList().IndexOf(last)==1,"Reordering works in alternate layout: "+layout.Name);
            }
            var helpWindow=new RegistrationHelpWindow(lib.Theme,null){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};helpWindow.Show();helpWindow.UpdateLayout();
            Check(Find<Image>(helpWindow).Count(i=>i.Source!=null)>=2 && Find<TextBlock>(helpWindow).Any(t=>t.Text.Contains("한 번 더 우클릭")) && Find<TextBlock>(helpWindow).Any(t=>t.Text.Contains("자동 분리")),"Registration help shows both reference images and the extra right-click and argument steps");
            var guideTexts=Find<TextBlock>(helpWindow).Select(t=>t.Text).ToList();int dragIndex=guideTexts.FindIndex(t=>t.Contains("끌어다 놓기")),copyIndex=guideTexts.FindIndex(t=>t.Contains("‘대상’ 복사"));
            Check(dragIndex>=0 && copyIndex>dragIndex && !Find<TextBox>(helpWindow).Any(),"Help explains dragging the executable first and hides the form when opened as a guide");helpWindow.Close();
            Check(TargetCommand.SuggestName("C:\\Users\\me\\AppData\\Local\\Discord\\Update.exe","--processStart Discord.exe")=="Discord" && TargetCommand.SuggestName("C:\\Games\\Game.exe","")=="Game","Direct registration suggests a friendly name from the pasted target");
            win.Show();win.Navigate(null);int beforeDirect=lib.Entries.Count;
            var direct=new RegistrationHelpWindow(lib.Theme,entry=>{lib.Entries.Add(entry);store.Save(lib);},paths=>{win.RegisterPaths(paths);return 1;},"라이브러리"){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};direct.Show();direct.UpdateLayout();
            var directTarget=Find<TextBox>(direct).First(t=>t.Tag as string=="Target");var directPaste=new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText,"\""+cmdPath+"\" /d /c exit 0"),false,DataFormats.UnicodeText){RoutedEvent=DataObject.PastingEvent};directTarget.RaiseEvent(directPaste);
            Check(Find<TextBox>(direct).First(t=>t.Tag as string=="Arguments").Text=="/d /c exit 0" && Find<TextBox>(direct).First(t=>t.Tag as string=="Name").Text=="cmd","Direct registration splits pasted targets and fills the name");
            direct.Register();Check(lib.Entries.Count==beforeDirect+1 && lib.Entries.Last().Target==cmdPath && lib.Entries.Last().Arguments=="/d /c exit 0" && store.Load().Entries.Any(e=>e.Target==cmdPath && e.Arguments=="/d /c exit 0"),"Direct registration saves the pasted shortcut in one step");
            direct.RegisterFiles(new[]{item.Target});Check(direct.RegisteredCount==2,"Dropping files on the direct registration window registers them");direct.Close();win.Hide();
            var optionsTexts=new List<string>();{var sheet=win.OptionsSheet();sheet.ShowActivated=false;sheet.WindowStartupLocation=WindowStartupLocation.Manual;sheet.Left=sheet.Top=-20000;sheet.Show();sheet.UpdateLayout();optionsTexts=Find<TextBlock>(sheet).Select(t=>t.Text).ToList();sheet.Close();}
            Check(!optionsTexts.Any(t=>t.Contains("등록 도움말") || t.Contains("주소로 직접 등록")) && optionsTexts.Contains("라이브러리 백업") && optionsTexts.Contains("런처 종료"),"Options sheet no longer duplicates registration help");
            var actionSheet=new ActionSheet("꾸미기","미리보기",lib.Theme){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};actionSheet.Add("◈","색상과 디자인","직접 골라 꾸미기",()=>{});actionSheet.Show();actionSheet.UpdateLayout();
            Check(actionSheet.WindowStyle==WindowStyle.None && actionSheet.AllowsTransparency && Find<Button>(actionSheet).Any(b=>b.Content is Grid),"Choice sheets use rounded themed action cards instead of basic context lists");actionSheet.Close();
            radial.Show();radial.HandleScreenClick(radial.PointToScreen(new Point(1200,700)));Check(!radial.IsVisible,"A screen click outside the launcher dismisses it even when initially inactive");radial.Show();
            var owned=new Window {Owner=radial,Title="설정 예외",Width=100,Height=100,Left=-20000,Top=-20000};owned.Show();radial.HandleScreenClick(owned.PointToScreen(new Point(20,20)));Check(radial.IsVisible,"Clicks inside an owned settings window do not dismiss the launcher");owned.Close();
            radial.Show();radial.library.HideAfterLaunch=false;radial.Open(new Entry {Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","cmd.exe"),Arguments="/d /c exit 0"});Check(!radial.IsVisible,"Successful launch always dismisses the launcher even with an old disabled preference");
            UI.ApplyFonts(new FontSettings());
            bool displayFont = UI.LauncherFont.GetTypefaces().Any(x => x.TryGetGlyphTypeface(out var g) && g.FamilyNames.Values.Any(n => n == "Cafe24 Ssurround" || n == "Jua"));
            bool bodyFont = UI.InterfaceFont.GetTypefaces().Any(x => x.TryGetGlyphTypeface(out var g) && g.CharacterToGlyphMap.ContainsKey(0xAC00));
            Check(displayFont && bodyFont, "Default launcher font (Cafe24 / bundled Jua) and readable Korean settings font resolve to real typefaces");
            // Font choices: catalog, persistence, resolution and the live picker.
            var catalog=FontCatalog.All;Check(catalog.Count>0 && catalog.Any(c=>c.Key=="Jua") && catalog.All(c=>c.Family.GetTypefaces().Any()),"Font catalog lists bundled and installed Korean fonts");
            var gowun=FontCatalog.Resolve("Gowun Dodum",FontCatalog.DefaultInterface);Check(gowun.GetTypefaces().Any(t=>t.TryGetGlyphTypeface(out var g) && g.FamilyNames.Values.Contains("Gowun Dodum")),"A chosen bundled font resolves by name");
            Check(FontCatalog.Resolve("존재하지 않는 글꼴 61892",FontCatalog.DefaultInterface).Source==FontCatalog.DefaultInterface.Source,"Unknown fonts fall back to the default");
            lib.Fonts=new FontSettings{Launcher="Gowun Dodum",Interface="Malgun Gothic"};store.Save(lib);Check(store.Load().Fonts.Launcher=="Gowun Dodum" && store.Load().Fonts.Interface=="Malgun Gothic","Font choices persist with the library");
            var fontBackup=Path.Combine(store.DirectoryPath,"font-backup.zip");store.Backup(fontBackup,lib);Check(Store.ReadBackup(fontBackup).Library.Fonts.Launcher=="Gowun Dodum","Font choices are included in backups");
            Check(Store.Read(legacyFile).Fonts.Launcher=="" ,"Older libraries without font settings still load");
            var fontEditor=new FontWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};fontEditor.Show();fontEditor.UpdateLayout();
            fontEditor.SelectTarget(true);fontEditor.Choose("Jua");fontEditor.SelectTarget(false);fontEditor.Choose("");
            Check(fontEditor.Draft.Launcher=="Jua" && fontEditor.Draft.Interface=="" && lib.Fonts.Launcher=="Gowun Dodum","Font picker edits launcher and settings fonts separately and in isolation");fontEditor.Close();
            UI.ApplyFonts(lib.Fonts);radial.Navigate(null);radial.UpdateLayout();
            Check(Find<TextBlock>(radial).Any(t=>t.FontFamily.Source.EndsWith("#Gowun Dodum")) ,"Launcher text uses the chosen launcher font");
            lib.Fonts=new FontSettings();UI.ApplyFonts(lib.Fonts);store.Save(lib);
            int windowsBefore=app.Windows.Count;var leakCheck=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};leakCheck.Show();leakCheck.UpdateLayout();bool previewsOpen=app.Windows.Count>windowsBefore+1;leakCheck.Close();var fontLeak=new FontWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};fontLeak.Show();fontLeak.Close();
            Check(previewsOpen && app.Windows.Count==windowsBefore,"Closing design and font windows also closes their hidden preview windows (no window leak)");
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
    private static void SaveBulkSnapshot(Window window,string path)
    {
        window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
    }
    private static int MaximumAlpha(Visual visual)
    {
        var bitmap=new RenderTargetBitmap(920,454,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var pixels=new byte[920*454*4];bitmap.CopyPixels(pixels,920*4,0);int maximum=0;for(int i=3;i<pixels.Length;i+=4)maximum=Math.Max(maximum,pixels[i]);return maximum;
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
        if(position==null && target.Width==920 && target.Content is Canvas canvas) {var shape=Find<System.Windows.Shapes.Path>(canvas).First();var rect=shape.Data.Bounds;point=new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2);}
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
    internal static void Snapshot(Window window, string path, double scale = 1)
    {
        window.UpdateLayout(); var content = (FrameworkElement)window.Content;
        double width=content.ActualWidth+content.Margin.Left+content.Margin.Right,height=content.ActualHeight+content.Margin.Top+content.Margin.Bottom;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);
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
        foreach(var preset in ThemeSettings.DesignPresets){win.UpdateTheme(preset.Copy());Snapshot(win,Path.Combine(output,"디자인-"+preset.Name+".png"));}
        foreach(var preset in ThemeSettings.Presets){win.UpdateTheme(preset.Copy());Snapshot(win,Path.Combine(output,"테마-"+preset.Name+".png"));}
        lib.Theme=ThemeSettings.Presets[0].Copy();win.UpdateTheme(lib.Theme);win.Navigate(null);win.ShowFolderPreview(work,win.Slot(work.Id)!);Snapshot(win.FolderPreview!,Path.Combine(output,"미리보기-옆폴더.png"));win.CloseFolderPreview();
        var themeEditor=new ThemeWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};themeEditor.Show();Snapshot(themeEditor,Path.Combine(output,"미리보기-테마설정.png"));themeEditor.UsePreset(ThemeSettings.DesignPresets[1]);Snapshot(themeEditor,Path.Combine(output,"미리보기-새디자인설정.png"));themeEditor.NewCustom();themeEditor.SaveCustomNamed("나의 고양이 다이얼");Snapshot(themeEditor,Path.Combine(output,"미리보기-커스텀설정.png"));themeEditor.Close();
        hoverEntry.Background=new BackgroundSettings { ImageFile=store.CopyIcon(backgroundPath),Zoom=1.3,OffsetX=.35,Darkness=.25 };
        win.UpdateTheme(lib.Theme);Snapshot(win,Path.Combine(output,"미리보기-칸배경.png"));
        var slotEditor=new BackgroundWindow(store,lib,hoverEntry){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};slotEditor.Show();Snapshot(slotEditor,Path.Combine(output,"미리보기-칸배경설정.png"));slotEditor.Close();
        lib.WheelDialEnabled=true;win.RotateDial(1);Snapshot(win,Path.Combine(output,"미리보기-휠순환.png"));
        var paletteManager=new LibraryWindow(store,new Library {Entries=lib.Entries,Theme=ThemeSettings.Presets.First(t=>t.Name=="피치 크림").Copy()}){ShowActivated=false,Left=-20000,Top=-20000};paletteManager.Show();Snapshot(paletteManager,Path.Combine(output,"미리보기-설정색상.png"));paletteManager.Close();
        var help=new RegistrationHelpWindow(lib.Theme,null){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,Height=1450};help.Show();Snapshot(help,Path.Combine(output,"미리보기-등록도움말.png"));help.Close();
        var appearance=new ActionSheet("화면 꾸미기","색상과 모양을 내 취향으로",lib.Theme){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};appearance.Add("◈","색상 · UI 디자인","새 레이아웃과 내 커스텀 테마",()=>{});appearance.Add("▧","전체 배경 이미지","미리보기에서 위치와 확대를 맞추세요",()=>{});appearance.Note("칸별 배경은 카드 우클릭으로 바꿀 수 있어요.");appearance.Show();Snapshot(appearance,Path.Combine(output,"미리보기-꾸미기메뉴.png"));appearance.Close();
        var optionSource=new LibraryWindow(store,lib);var options=optionSource.OptionsSheet();options.ShowActivated=false;options.WindowStartupLocation=WindowStartupLocation.Manual;options.Left=options.Top=-20000;options.Show();Snapshot(options,Path.Combine(output,"미리보기-옵션메뉴.png"));Snapshot(options,Path.Combine(output,"확대-옵션메뉴.png"),2);options.Close();
        var direct=new RegistrationHelpWindow(lib.Theme,_=>{},_=>0,"라이브러리"){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,Width=1140,Height=1500};direct.Show();Snapshot(direct,Path.Combine(output,"미리보기-직접등록.png"));direct.Close();
        var fonts=new FontWindow(store,lib){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,Width=1140,Height=800};fonts.Show();Snapshot(fonts,Path.Combine(output,"미리보기-글꼴.png"));fonts.Close();
        var decorate=new ActionSheet("화면 꾸미기","색상과 모양, 글꼴을 내 취향으로",lib.Theme){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};decorate.Add("\uE790","색상 · UI 디자인","기본 색상, 새 레이아웃, 내 커스텀 테마",()=>{});decorate.Add("\uEB9F","전체 배경 이미지","실제 화면을 보며 이미지 위치와 확대 조절",()=>{});decorate.Add("\uE8D2","글꼴","런처와 설정 창의 글꼴을 각각 고르기",()=>{});decorate.Note("칸별 배경은 카드 우클릭 → ‘이 칸의 배경 이미지’에서 바꿀 수 있어요.");decorate.Show();Snapshot(decorate,Path.Combine(output,"미리보기-꾸미기메뉴2.png"));decorate.Close();
        var shortcutEditor=new EditorWindow(store,lib.Entries.First(x=>x.Name=="메모장"),lib.Theme){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-19000,Top=-19000};shortcutEditor.Show();Snapshot(shortcutEditor,Path.Combine(output,"미리보기-편집창.png"));shortcutEditor.Close();
        win.UpdateTheme(ThemeSettings.Presets[0].Copy());win.Navigate(null);win.UpdateBackground(new BackgroundSettings(),null);foreach(var d in new[]{"classic","modern","cat","neon","paper","space"}){var t=ThemeSettings.Presets[0].Copy();if(d!="classic"){t=ThemeSettings.DesignPresets.First(x=>x.Design==d).Copy();}win.UpdateTheme(t);Snapshot(win,Path.Combine(output,"확대-"+d+".png"),2);}
        lib.Theme=ThemeSettings.DesignPresets[0].Copy();lib.Appearance.Opacity=1;hoverEntry.Opacity=0;hoverEntry.SeparateOpacity=true;hoverEntry.IconOpacity=hoverEntry.TextOpacity=1;
        var completeTransparency=new BackgroundWindow(store,lib,hoverEntry){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};completeTransparency.Show();Snapshot(completeTransparency,Path.Combine(output,"미리보기-100퍼센트-투명도.png"));completeTransparency.Close();
        win.Hide(); app.Shutdown(); return 0;
    }
}
