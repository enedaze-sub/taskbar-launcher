using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace DeckLauncher;

public sealed class AppearanceSettings
{
    public const string DefaultNote = "자주 쓰는 것들을 한 번에. 실행할 아이콘을 선택하세요.";
    public string Title { get; set; } = "나의 바로가기";
    public string Note { get; set; } = DefaultNote;
    public double Opacity { get; set; } = 1;
    public AppearanceSettings Copy() => new() { Title = Title, Note = Note, Opacity = Opacity };
    public void Validate()
    {
        if (Title == null || Title.Length > 60 || Note == null || Note.Length > 300 || !double.IsFinite(Opacity) || Opacity < 0 || Opacity > 1) throw new InvalidDataException("중앙 문구 또는 전체 투명도 값이 잘못되었습니다.");
    }
}

public sealed class Entry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? ParentId { get; set; }
    public string Name { get; set; } = "새 항목";
    public bool IsFolder { get; set; }
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string Note { get; set; } = "";
    public double Opacity { get; set; } = 1;
    public bool SeparateOpacity { get; set; }
    public double IconOpacity { get; set; } = 1;
    public double TextOpacity { get; set; } = 1;
    public string? IconFile { get; set; }
    public BackgroundSettings Background { get; set; } = new();
}

public sealed class Library
{
    public int Version { get; set; } = 1;
    public int UiRevision { get; set; }
    public bool HideAfterLaunch { get; set; }
    public bool WheelDialEnabled { get; set; }
    public List<SavedTheme> CustomThemes { get; set; } = new();
    public FontSettings Fonts { get; set; } = new();
    public BackgroundSettings Background { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
    public List<Entry> Entries { get; set; } = new();

    public IEnumerable<Entry> Children(string? parent) => Entries.Where(e => e.ParentId == parent);
    public Entry? Find(string? id) => Entries.FirstOrDefault(e => e.Id == id);
    public bool ApplyUiUpdate()
    {
        if (UiRevision >= 6) return false;
        if (UiRevision < 3) HideAfterLaunch = false;
        if (UiRevision < 5) WheelDialEnabled = false;
        HideAfterLaunch = true;
        if (Theme.Design == "glass") Theme.Design = "classic";
        foreach(var theme in CustomThemes) if(theme.Settings.Design == "glass") theme.Settings.Design="classic";
        if (!ThemeSettings.Presets.Concat(ThemeSettings.DesignPresets).Any(t => t.Name == Theme.Name) && !CustomThemes.Any(t => t.Settings.Name == Theme.Name))
            CustomThemes.Add(new SavedTheme { Name = Theme.Name, Settings = Theme.Copy() });
        UiRevision = 6; return true;
    }
    public bool MoveEntries(IEnumerable<string> ids, string? parent)
    {
        var entries = ids.Distinct().Select(Find).ToList();
        if (entries.Count == 0 || entries.Any(x => x == null || !CanMove(x, parent))) return false;
        bool changed = false;
        foreach (var entry in entries) { changed |= entry!.ParentId != parent; entry.ParentId = parent; }
        return changed;
    }
    public bool ReorderEntries(IEnumerable<string> ids, string anchorId, bool after)
    {
        var anchor = Find(anchorId); var selected = ids.Distinct().Select(Find).ToList();
        if (anchor == null || selected.Count == 0 || selected.Any(e => e == null || e == anchor || !CanMove(e, anchor.ParentId))) return false;
        var before = Entries.Select(e => (e.Id, e.ParentId)).ToArray();
        var moving = Entries.Where(e => selected.Contains(e)).ToList();
        Entries.RemoveAll(e => moving.Contains(e));
        foreach (var e in moving) e.ParentId = anchor.ParentId;
        Entries.InsertRange(Entries.IndexOf(anchor) + (after ? 1 : 0), moving);
        return !before.SequenceEqual(Entries.Select(e => (e.Id, e.ParentId)));
    }
    public IEnumerable<string> ImageFiles() => Entries.SelectMany(e => new[] { e.IconFile, e.Background.ImageFile }).Append(Background.ImageFile).Where(x => x != null).Cast<string>().Distinct();
    public string Trail(string? parent)
    {
        var names = new List<string>();
        var visited = new HashSet<string>();
        while (parent != null && visited.Add(parent))
        {
            var e = Find(parent); if (e == null) break;
            names.Insert(0, e.Name); parent = e.ParentId;
        }
        return names.Count == 0 ? "라이브러리" : "라이브러리 / " + string.Join(" / ", names);
    }
    public bool CanMove(Entry e, string? parent)
    {
        if (parent == null) return true;
        var visited = new HashSet<string>();
        while (parent != null)
        {
            if (parent == e.Id || !visited.Add(parent)) return false;
            var folder = Find(parent);
            if (folder == null || !folder.IsFolder) return false;
            parent = folder.ParentId;
        }
        return true;
    }
    public List<Entry> BackgroundTargets(string? parent, string scope)
    {
        if(scope=="all")return Entries.ToList();
        if(scope=="level")return Children(parent).ToList();
        if(scope!="tree")throw new InvalidDataException("알 수 없는 적용 범위입니다.");
        bool Inside(Entry e) { var id=e.ParentId;var seen=new HashSet<string>();while(id!=null && seen.Add(id)){if(id==parent)return true;id=Find(id)?.ParentId;}return parent==null; }
        return Entries.Where(Inside).ToList();
    }
    public int ApplyBackgrounds(IEnumerable<string> ids,BackgroundSettings? settings,double? opacity,bool? separateOpacity=null,double? iconOpacity=null,double? textOpacity=null)
    {
        var chosen=ids.Distinct().Select(Find).ToList();if(chosen.Count==0 || chosen.Any(e=>e==null))throw new InvalidDataException("적용할 칸이 없습니다.");
        settings?.Validate();if(opacity is double value && (!double.IsFinite(value) || value<0 || value>2))throw new InvalidDataException("칸의 투명도가 잘못되었습니다.");
        if(new[]{iconOpacity,textOpacity}.Any(v=>v.HasValue && (!double.IsFinite(v.Value) || v<0 || v>1)))throw new InvalidDataException("아이콘 또는 글자 투명도가 잘못되었습니다.");
        foreach(var e in chosen){if(separateOpacity.HasValue)e!.SeparateOpacity=separateOpacity.Value;if(iconOpacity.HasValue)e!.IconOpacity=iconOpacity.Value;if(textOpacity.HasValue)e!.TextOpacity=textOpacity.Value;if(settings!=null)e!.Background=settings.Copy();if(opacity!=null)e!.Opacity=opacity.Value;}return chosen.Count;
    }
    public void Remove(Entry e)
    {
        var ids = new HashSet<string> { e.Id };
        bool changed;
        do { changed = false; foreach (var x in Entries) if (x.ParentId != null && ids.Contains(x.ParentId)) changed |= ids.Add(x.Id); } while (changed);
        Entries.RemoveAll(x => ids.Contains(x.Id));
    }
    public void Validate()
    {
        Appearance ??= new(); Appearance.Validate();
        Background ??= new(); Background.Validate(); Theme ??= new(); Theme.Validate(); CustomThemes ??= new(); Fonts ??= new(); Fonts.Validate();
        var themeIds = new HashSet<string>();
        foreach (var saved in CustomThemes)
        {
            if (string.IsNullOrWhiteSpace(saved.Id) || !themeIds.Add(saved.Id) || string.IsNullOrWhiteSpace(saved.Name) || saved.Name.Length > 40 || saved.Settings == null) throw new InvalidDataException("커스텀 테마 정보가 올바르지 않습니다.");
            saved.Settings.Validate();
        }
        if (Version != 1) throw new InvalidDataException("지원하지 않는 라이브러리 버전입니다.");
        var ids = new HashSet<string>();
        foreach (var e in Entries)
        {
            e.Background ??= new(); e.Background.Validate();
            if (!double.IsFinite(e.Opacity) || e.Opacity < 0 || e.Opacity > 2) throw new InvalidDataException("칸의 투명도 값이 잘못되었습니다.");
            if(new[]{e.IconOpacity,e.TextOpacity}.Any(v=>!double.IsFinite(v) || v<0 || v>1))throw new InvalidDataException("아이콘 또는 글자 투명도가 잘못되었습니다.");
            if (string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id)) throw new InvalidDataException("항목 ID가 중복되었거나 비어 있습니다.");
            if (string.IsNullOrWhiteSpace(e.Name)) throw new InvalidDataException("이름이 없는 항목이 있습니다.");
            if (e.IconFile != null && (Path.GetFileName(e.IconFile) != e.IconFile || e.IconFile.Contains(':'))) throw new InvalidDataException("잘못된 아이콘 경로입니다.");
        }
        foreach (var e in Entries) if (!CanMove(e, e.ParentId)) throw new InvalidDataException("폴더 구조가 올바르지 않습니다.");
    }
}

public sealed class Store
{
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "library.json");
    public string IconDirectory => Path.Combine(DirectoryPath, "icons");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public Store(string path) => DirectoryPath = path;
    public Library Load()
    {
        Directory.CreateDirectory(IconDirectory);
        if (!File.Exists(FilePath)) return new();
        return Read(FilePath);
    }
    public static Library Read(string path)
    {
        var data = JsonSerializer.Deserialize<Library>(File.ReadAllText(path)) ?? throw new InvalidDataException("라이브러리를 읽을 수 없습니다.");
        data.Validate(); return data;
    }
    public void Save(Library library)
    {
        library.Validate();
        Directory.CreateDirectory(IconDirectory);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(library, Options));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
        else File.Move(temp, FilePath);
    }
    public string CopyIcon(string path)
    {
        Directory.CreateDirectory(IconDirectory);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(path).ToLowerInvariant();
        File.Copy(path, Path.Combine(IconDirectory, name)); return name;
    }
    public string IconPath(string name) => Path.Combine(IconDirectory, Path.GetFileName(name));
    public static string BackupFileName(DateTime? when = null) => "DeckLauncher-backup-" + (when ?? DateTime.Now).ToString("yyyyMMdd-HHmmss") + ".zip";
    public string Backup(string path, Library library)
    {
        Save(library);
        var requested = Path.GetFullPath(path); var directory = Path.GetDirectoryName(requested)!;
        var stem = Path.GetFileNameWithoutExtension(requested); var extension = Path.GetExtension(requested);
        FileStream stream; int number = 1;
        while (true)
        {
            path = number == 1 ? requested : Path.Combine(directory, $"{stem}_{number}{extension}");
            if (Directory.Exists(path)) { number++; continue; }
            try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); break; }
            catch (IOException) when (File.Exists(path) || Directory.Exists(path)) { number++; }
        }
        using var backupStream = stream; using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(FilePath, "library.json");
        foreach (var icon in library.ImageFiles())
        {
            if (!File.Exists(IconPath(icon!))) throw new FileNotFoundException("사용자 지정 아이콘을 찾을 수 없습니다.", icon);
            zip.CreateEntryFromFile(IconPath(icon!), "icons/" + icon);
        }
        return path;
    }
    public sealed record BackupContent(Library Library, Dictionary<string, byte[]> Icons);
    public static BackupContent ReadBackup(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var json = zip.GetEntry("library.json") ?? throw new InvalidDataException("등록 정보가 없는 백업입니다.");
        if (json.Length > 8 * 1024 * 1024) throw new InvalidDataException("백업 파일이 너무 큽니다.");
        Library imported;
        using (var reader = new StreamReader(json.Open())) imported = JsonSerializer.Deserialize<Library>(reader.ReadToEnd()) ?? throw new InvalidDataException("잘못된 백업입니다.");
        imported.Validate();
        var icons = new Dictionary<string, byte[]>(); long total = 0;
        foreach (var name in imported.ImageFiles())
        {
            var entry = zip.GetEntry("icons/" + name) ?? throw new InvalidDataException("아이콘 파일이 누락되었습니다.");
            total += entry.Length;
            if (entry.Length > 32 * 1024 * 1024 || total > 256 * 1024 * 1024) throw new InvalidDataException("아이콘 파일이 너무 큽니다.");
            using var source = entry.Open(); using var memory = new MemoryStream(); source.CopyTo(memory); icons[name!] = memory.ToArray();
        }
        return new BackupContent(imported, icons);
    }
    public Library Restore(BackupContent backup)
    {
        backup.Library.Validate(); Directory.CreateDirectory(IconDirectory);
        var names = new Dictionary<string, string>();
        foreach (var image in backup.Library.ImageFiles().ToArray())
        {
            string replacement = Guid.NewGuid().ToString("N") + Path.GetExtension(image);
            File.WriteAllBytes(IconPath(replacement), backup.Icons[image]); names[image] = replacement;
        }
        foreach (var e in backup.Library.Entries.Where(e => e.IconFile != null)) e.IconFile = names[e.IconFile!];
        foreach (var e in backup.Library.Entries.Where(e => e.Background.ImageFile != null)) e.Background.ImageFile = names[e.Background.ImageFile!];
        if (backup.Library.Background.ImageFile != null) backup.Library.Background.ImageFile = names[backup.Library.Background.ImageFile];
        Save(backup.Library); return backup.Library;
    }
}
