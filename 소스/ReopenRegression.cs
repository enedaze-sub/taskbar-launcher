using System.IO;
using System.Text.Json;
using System.Windows;

namespace DeckLauncher;

internal static class ReopenRegression
{
    internal static void Run(string output,Action<bool,string> check)
    {
        var lib=new Library{WheelDialEnabled=true,Appearance=new(){Title="처음 화면",Note="내 시작 메모"}};var folder=new Entry{Name="폴더",IsFolder=true};
        lib.Entries.Add(folder);lib.Entries.Add(new Entry{Name="내부 앱",ParentId=folder.Id,Target="https://example.com"});
        for(int i=0;i<18;i++)lib.Entries.Add(new Entry{Name="앱 "+i,Target="https://example.com"});
        var store=new Store(Path.Combine(output,"reopen-tests"));
        var win=new LauncherWindow(store,lib,true);
        win.ChangePage(1);win.Navigate(folder.Id);win.ShowNote(lib.Entries[1]);win.Hide();win.ResetForReveal();
        check(win.CurrentFolder==null && win.CurrentPage==0 && win.DialStart==0,"Reopen returns to the first root page after leaving a folder");
        check(win.DisplayedTitle==lib.Appearance.Title && win.DisplayedNote==lib.Appearance.Note,"Reopen restores central wording rather than the previously hovered app");
        win.Navigate(folder.Id);win.Navigate(null);check(win.CurrentPage==0,"Reopen discards previous root page history");
        win.RotateDial(4);win.ResetForReveal();check(win.DialStart==0 && win.CurrentPage==0 && win.DisplayedIds.First()==folder.Id,"Reopen resets wheel dial offset");
        var before=win.Slot(folder.Id);win.ResetForReveal();check(ReferenceEquals(before,win.Slot(folder.Id)),"Repeated first-page opens reuse existing visuals");win.Close();
        var primary=new Rect(0,0,1920,1080);var secondary=new Rect(-2560,-320,2560,1440);
        var leftButton=new Rect(-1300,1080,48,40);var rightButton=new Rect(800,1040,48,40);
        check(TaskbarAnchor.Choose(new[]{rightButton},new Point(-1276,1100),secondary)==null,"Taskbar cache never chooses an icon on another monitor");
        check(TaskbarAnchor.Choose(new[]{rightButton,leftButton},new Point(-1276,1100),secondary)==leftButton,"Negative monitor coordinates select the clicked monitor's icon");
        foreach(double scale in new[]{1.0,1.25,1.5,2.0})
        {
            var display=new Native.DisplayContext(new Point(-1276,1100),secondary,new Rect(-2560,-320,2560,1400),scale,scale);
            var p=TaskbarAnchor.PhysicalPlacement(display,leftButton);
            check(Math.Abs(p.X+p.Width/2-(-1276))<1 && p.Left>=display.Work.Left && p.Right<=display.Work.Right+1 && Math.Abs(p.Bottom-display.Work.Bottom)<2,"Physical placement keeps the anchor and bounds at DPI scale "+scale);
            var fallback=TaskbarAnchor.PhysicalPlacement(display,rightButton);check(fallback.Right<=display.Work.Right+1 && fallback.Left>=display.Work.Left,"Wrong-monitor anchor falls back on the requested display at scale "+scale);
        }
        var live=Native.DisplayAtCursor();var physical=TaskbarAnchor.PhysicalPlacement(live,null);var hidden=new LauncherWindow(store,lib,true);
        Native.PlacePhysical(hidden,physical);var actual=Native.WindowBounds(new System.Windows.Interop.WindowInteropHelper(hidden).Handle);
        check(actual.HasValue && Math.Abs(actual.Value.X-physical.X)<=1 && Math.Abs(actual.Value.Y-physical.Y)<=1,"Native window placement uses physical coordinates before showing");hidden.Close();
        foreach(var display in Native.Displays())
        {
            var probe=new LauncherWindow(store,lib,true);var expected=TaskbarAnchor.PhysicalPlacement(display,null);Native.PlacePhysical(probe,expected);
            var bounds=Native.WindowBounds(new System.Windows.Interop.WindowInteropHelper(probe).Handle);
            check(bounds.HasValue && Math.Abs(bounds.Value.X-expected.X)<=1 && Math.Abs(bounds.Value.Y-expected.Y)<=1 && Math.Abs(bounds.Value.Width-expected.Width)<=1,"Hidden native placement verified on connected monitor at "+display.Bounds+" (scale "+display.ScaleX+")");probe.Close();
        }
        string root=Path.Combine(output,"package-fixture","WindowsApps"),oldRoot=Path.Combine(root,"Claude_2.37337.1.0_x64__pzs8sxrjxfjjc"),newRoot=Path.Combine(root,"Claude_9.0.0.0_x64__pzs8sxrjxfjjc");
        string oldPath=Path.Combine(oldRoot,"app","claude.exe"),newPath=Path.Combine(newRoot,"app","claude.exe");Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);File.WriteAllText(newPath,"fixture only; never executed");
        var package=PackagedApps.Parse(oldPath);check(package?.Family=="Claude_pzs8sxrjxfjjc" && package.RelativePath==@"app\claude.exe","Versioned WindowsApps path resolves to a stable package family");
        string resolved=PackagedApps.Resolve(oldPath,_=>new[]{newRoot});check(resolved==newPath && !File.Exists(oldPath),"Updated package executable resolves even after the old version folder disappears");
        var entry=new Entry{Name="Claude",Target=oldPath,Arguments="--name \"two words\"",WorkingDirectory=Path.Combine(oldRoot,"app"),Note="보존"};
        var plan=Launch.Prepare(entry,resolve:p=>PackagedApps.Resolve(p,_=>new[]{newRoot}));
        check(plan.FileName==newPath && plan.Arguments==entry.Arguments && plan.WorkingDirectory==Path.Combine(newRoot,"app"),"Package updates preserve launch arguments and rebase package working directories");
        check(entry.Target==oldPath && entry.Note=="보존","Package resolution does not rewrite saved targets or notes");
        entry.WorkingDirectory=output;check(Launch.Prepare(entry,resolve:_=>newPath).WorkingDirectory==output,"Custom working directories outside the package remain unchanged");
        check(PackagedApps.Parse(@"C:\Tools\Claude_2.0.0.0_x64__publisher\claude.exe")==null && PackagedApps.Parse("https://example.com/WindowsApps/app.exe")==null,"Ordinary paths and URLs are never treated as packaged apps");
        check(PackagedApps.Resolve(oldPath,_=>Array.Empty<string>())==oldPath,"Missing packages do not silently select unrelated executables");
        check(Launch.Prepare(new Entry{Target="https://example.com",Arguments=""}).FileName=="https://example.com","Regular URL launch behavior remains intact");
        string sample=@"C:\Program Files\WindowsApps\Claude_2.37337.1.0_x64__pzs8sxrjxfjjc\app\claude.exe";string liveTarget=PackagedApps.Resolve(sample);
        File.WriteAllText(Path.Combine(output,"WindowsApps-診断.json"),JsonSerializer.Serialize(new{Original=sample,Resolved=liveTarget,Exists=File.Exists(liveTarget),RegisteredRoots=PackagedApps.InstalledRoots(PackagedApps.Parse(sample)!).ToArray(),Display=live},new JsonSerializerOptions{WriteIndented=true}));
    }
}
