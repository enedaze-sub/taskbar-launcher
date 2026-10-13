using System.IO;
using System.Windows.Markup;
using System.Windows.Media;

namespace DeckLauncher;

/// <summary>User-selected font families. Empty values use the built-in defaults.</summary>
public sealed class FontSettings
{
    public string Launcher { get; set; } = "";
    public string Interface { get; set; } = "";
    public FontSettings Copy() => new() { Launcher = Launcher, Interface = Interface };
    public void Validate()
    {
        Launcher ??= ""; Interface ??= "";
        if (Launcher.Length > 120 || Interface.Length > 120) throw new InvalidDataException("글꼴 이름이 올바르지 않습니다.");
    }
}

internal static class FontCatalog
{
    internal sealed record Choice(string Key, string Display, FontFamily Family, string Origin)
    {
        public override string ToString() => Display;
    }
    private static readonly XmlLanguage Korean = XmlLanguage.GetLanguage("ko-kr"), English = XmlLanguage.GetLanguage("en-us");
    private static List<Choice>? all;
    private static readonly object gate = new();

    internal static string UserFontDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
    internal static string BundledFontDirectory => Path.Combine(AppContext.BaseDirectory, "Fonts");

    /// <summary>Settings text: Noto Sans KR ships with Windows 11 and has real Medium/SemiBold/Bold weights.</summary>
    internal static FontFamily DefaultInterface => new("Noto Sans KR, Malgun Gothic, Segoe UI");
    /// <summary>Launcher text keeps the rounded display look: Cafe24 Ssurround when installed, otherwise bundled Jua.</summary>
    internal static FontFamily DefaultLauncher
    {
        get
        {
            if (File.Exists(Path.Combine(UserFontDirectory, "Cafe24Ssurround-v2.0.ttf"))) return new FontFamily(new Uri(UserFontDirectory + Path.DirectorySeparatorChar), "./#Cafe24 Ssurround");
            return Bundled("Jua") ?? new FontFamily("Malgun Gothic");
        }
    }
    internal static FontFamily? Bundled(string family)
        => Directory.Exists(BundledFontDirectory) ? new FontFamily(new Uri(BundledFontDirectory + Path.DirectorySeparatorChar), "./#" + family) : null;

    internal static FontFamily Resolve(string? key, FontFamily fallback)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        foreach (var directory in new[] { BundledFontDirectory, UserFontDirectory })
        {
            try
            {
                if (!Directory.Exists(directory)) continue;
                var match = Fonts.GetFontFamilies(new Uri(directory + Path.DirectorySeparatorChar)).FirstOrDefault(f => f.FamilyNames.Values.Contains(key));
                if (match != null) return new FontFamily(new Uri(directory + Path.DirectorySeparatorChar), "./#" + key);
            }
            catch { }
        }
        return Fonts.SystemFontFamilies.Any(f => f.FamilyNames.Values.Contains(key)) ? new FontFamily(key + ", Malgun Gothic") : fallback;
    }

    /// <summary>Every installed or bundled family that can draw Hangul, with Korean display names when available.</summary>
    internal static IReadOnlyList<Choice> All
    {
        get
        {
            lock (gate)
            {
                if (all != null) return all;
                var found = new Dictionary<string, Choice>(StringComparer.OrdinalIgnoreCase);
                void Scan(IEnumerable<FontFamily> families, string origin, string? directory)
                {
                    foreach (var family in families)
                    {
                        try
                        {
                            string key = family.FamilyNames.TryGetValue(English, out var en) ? en : family.FamilyNames.Values.FirstOrDefault() ?? family.Source;
                            if (found.ContainsKey(key) || !SupportsHangul(family)) continue;
                            string display = family.FamilyNames.TryGetValue(Korean, out var ko) ? ko : KnownNames.TryGetValue(key, out var known) ? known : key;
                            var usable = directory == null ? new FontFamily(key) : new FontFamily(new Uri(directory + Path.DirectorySeparatorChar), "./#" + key);
                            found[key] = new Choice(key, display, usable, origin);
                        }
                        catch { }
                    }
                }
                try { if (Directory.Exists(BundledFontDirectory)) Scan(Fonts.GetFontFamilies(new Uri(BundledFontDirectory + Path.DirectorySeparatorChar)), "동봉", BundledFontDirectory); } catch { }
                try { if (Directory.Exists(UserFontDirectory)) Scan(Fonts.GetFontFamilies(new Uri(UserFontDirectory + Path.DirectorySeparatorChar)), "내 PC", UserFontDirectory); } catch { }
                try { Scan(Fonts.SystemFontFamilies, "Windows", null); } catch { }
                // Names that start with a symbol (e.g. "♠H2") go after the regular Korean and Latin names.
                all = found.Values.OrderBy(c => char.IsLetterOrDigit(c.Display.FirstOrDefault()) ? 0 : 1).ThenBy(c => c.Display, StringComparer.Create(new System.Globalization.CultureInfo("ko-KR"), true)).ToList();
                return all;
            }
        }
    }
    /// <summary>Korean names for common families whose files only carry an English name.</summary>
    private static readonly Dictionary<string, string> KnownNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jua"] = "주아", ["Gowun Dodum"] = "고운돋움", ["Gowun Batang"] = "고운바탕", ["Noto Sans KR"] = "Noto Sans KR (본고딕)", ["Noto Serif KR"] = "Noto Serif KR (본명조)",
        ["Cafe24 Ssurround"] = "카페24 써라운드", ["Cafe24 Ssurround air"] = "카페24 써라운드 에어", ["NanumGothic"] = "나눔고딕", ["Malgun Gothic"] = "맑은 고딕"
    };
    private static bool SupportsHangul(FontFamily family)
    {
        foreach (var typeface in family.GetTypefaces())
            if (typeface.TryGetGlyphTypeface(out var glyphs))
            {
                // Symbol fonts sometimes map a handful of syllables; a usable Korean font covers at least the 2,350 KS X 1001 syllables.
                var map = glyphs.CharacterToGlyphMap;
                return map.ContainsKey(0xAC00) && map.ContainsKey(0xD55C) && map.ContainsKey('A') && map.Keys.Count(k => k >= 0xAC00 && k <= 0xD7A3) >= 2000;
            }
        return false;
    }
}
