using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WpfPath = System.Windows.Shapes.Path;

namespace DeckLauncher;

/// <summary>
/// The six launcher looks. Everything is vector art tinted from the active palette, so any color theme works with any design.
/// classic: 부채꼴 · cat: 냥냥 포켓 · neon: 사이버 HUD · paper: 다이어리 스크랩 · modern: 글래스 도크 · space: 별빛 궤도.
/// All coordinates live on the 920×454 canvas; x = 460 is the mirror line.
/// </summary>
public sealed partial class LauncherWindow
{
    private bool AlternateLayout => Theme.Design is "modern" or "cat" or "neon" or "paper" or "space";
    /// <summary>Icons sit on an arc as round/hex nodes with a name pill underneath.</summary>
    private bool OrbitLayout => Theme.Design is "neon" or "space";
    /// <summary>Icons sit in rectangular tiles (rows or a grid).</summary>
    private bool TileLayout => AlternateLayout && !OrbitLayout;
    private double HoverScale => Theme.Design == "modern" ? 1.2 : 1.09;

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Color Mix(Color a, Color b, double t) => Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    private static Color Alpha(Color c, double a) { c.A = (byte)Math.Round(255 * a); return c; }
    private static readonly Color White = Colors.White, Black = Colors.Black;
    private Color Bg => C(Theme.Background);
    private Color Sf => C(Theme.Surface);
    private Color Ac => C(Theme.Accent);
    private Color Tx => C(Theme.Text);
    /// <summary>How much the soft cat/pillow highlights lift toward white: strong on light palettes, subtle on dark ones.</summary>
    private double Lift => Skin.IsLight(Theme.Background) ? .45 : .1;
    private static SolidColorBrush Solid(Color c) => new(c);
    private static LinearGradientBrush Vertical(params (Color Color, double At)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        foreach (var (color, at) in stops) brush.GradientStops.Add(new GradientStop(color, at));
        return brush;
    }
    private static Geometry Polygon(params Point[] points)
    {
        var shape=new StreamGeometry();using(var dc=shape.Open()){dc.BeginFigure(points[0],true,true);dc.PolyLineTo(points.Skip(1).ToList(),true,false);}shape.Freeze();return shape;
    }
    private static Geometry Shape(string data) { var g = Geometry.Parse(data); g.Freeze(); return g; }
    private static Geometry Hexagon(Point c, double r) => Polygon(Enumerable.Range(0, 6).Select(k => new Point(c.X + r * Math.Cos((-90 + 60 * k) * Math.PI / 180), c.Y + r * Math.Sin((-90 + 60 * k) * Math.PI / 180))).ToArray());
    /// <summary>Mirrors a path (lines and curves only, no arcs) across x = 460 so left/right art is exactly symmetric.</summary>
    private static string MirrorPath(string data) => System.Text.RegularExpressions.Regex.Replace(data, @"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)", m => $"{920 - double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)},{m.Groups[2].Value}");
    private void Add(Canvas canvas, UIElement element) { if (element is FrameworkElement f) f.IsHitTestVisible = false; canvas.Children.Add(element); }

    // ── Cat head outline: big rounded top corners, small bottom corners, ears joined by union ──
    private const string CatHead = "M 112,418 L 112,244 A 110,110 0 0 1 222,134 L 698,134 A 110,110 0 0 1 808,244 L 808,418 A 36,36 0 0 1 772,454 L 148,454 A 36,36 0 0 1 112,418 Z";
    private const string CatEar = "M 140,214 C 132,150 150,86 184,56 Q 198,44 214,54 C 254,82 296,118 322,152 Z";
    private const string CatInnerEar = "M 168,188 C 166,144 178,104 196,84 Q 204,77 212,84 C 240,108 266,134 284,158 Z";

    internal Geometry ShellGeometry()
    {
        switch (Theme.Design)
        {
            case "modern": return new CombinedGeometry(GeometryCombineMode.Union, new RectangleGeometry(new Rect(250, 204, 420, 84), 42, 42), new RectangleGeometry(new Rect(56, 300, 808, 154), 36, 36));
            case "paper": return new RectangleGeometry(new Rect(126, 100, 668, 354), 16, 16);
            case "neon": return Polygon(new(70, 125), new(125, 70), new(795, 70), new(850, 125), new(850, 454), new(70, 454));
            case "space": return Shape("M 40,454 L 40,330 A 420,250 0 0 1 880,330 L 880,454 Z");
            case "cat":
                return new CombinedGeometry(GeometryCombineMode.Union, Shape(CatHead), new GeometryGroup { Children = new GeometryCollection { Shape(CatEar), Shape(MirrorPath(CatEar)) } });
            default: return Sector(.01, 390, -180, 0);
        }
    }
    private Geometry HubGeometry() => Theme.Design switch
    {
        "cat" => new RectangleGeometry(new Rect(146, 402, 628, 52), 26, 26),
        "neon" => Polygon(new(160, 454), new(160, 420), new(182, 398), new(738, 398), new(760, 420), new(760, 454)),
        "paper" => new RectangleGeometry(new Rect(140, 402, 640, 52), 10, 10),
        "modern" => new RectangleGeometry(new Rect(80, 400, 760, 48), 24, 24),
        "space" => new EllipseGeometry(new Point(460, 640), 300, 250),
        _ => Sector(.01, 205, -180, 0)
    };
    private (double X, double Y, double Width) InfoBox => Theme.Design switch
    {
        "modern" => (250, 212, 420), "cat" => (250, 214, 420), "paper" => (190, 124, 540), "space" => (310, 286, 300), _ => (305, 278, 310)
    };

    /// <summary>Angle of node <paramref name="index"/> on the orbit arc, spread symmetrically around the top (-90°).</summary>
    private double OrbitAngle(int index, int count) => Theme.Design == "space"
        ? (count == 1 ? -90 : -165 + 150.0 * index / (count - 1))
        : (count == 1 ? -90 : -153 + 126.0 * index / (count - 1));
    private Point OrbitPoint(double angle)
    {
        if (Theme.Design != "space") return Polar(Theme.Radius, angle);
        double a = angle * Math.PI / 180; return new Point(460 + 350 * Math.Cos(a), 456 + 260 * Math.Sin(a));
    }
    private Geometry LayoutSlot(int index,int count)
    {
        if (OrbitLayout)
        {
            var p = OrbitPoint(OrbitAngle(index, count)); var center = new Point(p.X, p.Y - 3);
            Geometry node = Theme.Design == "neon" ? Hexagon(center, 42) : new EllipseGeometry(center, 38, 38);
            return new GeometryGroup { Children = new GeometryCollection { node, new RectangleGeometry(new Rect(p.X - 49, p.Y + 40, 98, 22), Theme.Design == "neon" ? 4 : 11, Theme.Design == "neon" ? 4 : 11) } };
        }
        if (Theme.Design == "modern")
        {
            double width = Math.Min(96, (760.0 - 14 * (count - 1)) / Math.Max(count, 1)), left = 460 - (width * count + 14 * (count - 1)) / 2;
            return new RectangleGeometry(new Rect(left + index * (width + 14), 310, width, 84), 20, 20);
        }
        if (Theme.Design == "cat")
        {
            int columns = count <= 3 ? Math.Max(1, count) : count <= 6 ? 3 : count <= 8 ? 4 : 5, rows = (count + columns - 1) / columns;
            double gap = 10, w = (600 - gap * (columns - 1)) / columns, h = 52, top = rows == 1 ? 314 : 284, x = 460 - (columns * w + (columns - 1) * gap) / 2;
            return new RectangleGeometry(new Rect(x + (index % columns) * (w + gap), top + (index / columns) * (h + 8), w, h), h / 2, h / 2);
        }
        // paper: sticky notes in up to four columns
        int cols = Math.Min(4, Math.Max(1, count)), lines = Math.Max(1, (count + cols - 1) / cols);
        double nw = Math.Min(140, (600.0 - (cols - 1) * 14) / cols), nh = lines > 2 ? 62 : 92, ny = lines > 2 ? 196 : 200, nx = 460 - (cols * nw + (cols - 1) * 14) / 2;
        return new RectangleGeometry(new Rect(nx + (index % cols) * (nw + 14), ny + (index / cols) * (nh + 8), nw, nh), 4, 4);
    }
    private Point IconPoint(Geometry geometry, double angle)
    {
        if (TileLayout) return new(geometry.Bounds.X + geometry.Bounds.Width / 2, geometry.Bounds.Y + geometry.Bounds.Height / 2);
        if (OrbitLayout && geometry is GeometryGroup group) { var node = group.Children[0].Bounds; return new(node.X + node.Width / 2, node.Y + node.Height / 2 + 3); }
        return Polar(Theme.Radius, angle);
    }
    private DropShadowEffect Glow(double blur=12,double opacity=.45)=>new(){Color=Ac,BlurRadius=blur,ShadowDepth=0,Opacity=opacity};

    private static readonly string[] NoteColors = { "#FFF0A6", "#FFD3DC", "#CDEFD9", "#D3E6FF", "#FFE0C2" };
    private static readonly string[] PlanetColors = { "#FFB86B", "#7FD1FF", "#C59BFF", "#7CF2B5", "#FF8FB1", "#FFE27A" };
    private Color NoteColor(int index) => Mix(C(NoteColors[index % NoteColors.Length]), Sf, .35);
    private Color PlanetColor(int index) => Mix(C(PlanetColors[index % PlanetColors.Length]), Sf, .45);

    // ── Shell ──
    private void StyleShell(WpfPath shell)
    {
        switch (Theme.Design)
        {
            case "classic":
                shell.Fill = new RadialGradientBrush { MappingMode = BrushMappingMode.Absolute, Center = new Point(460, 453), GradientOrigin = new Point(460, 453), RadiusX = 392, RadiusY = 392, GradientStops = { new(Mix(Bg, Sf, .32), 0), new(Mix(Bg, Sf, .16), .7), new(Mix(Bg, Ac, .1), 1) } };
                break;
            case "cat":
                shell.Fill = Vertical((Mix(Bg, White, Lift), 0), (Bg, .55), (Mix(Bg, Sf, .45), 1)); shell.Stroke = Solid(Mix(Ac, Bg, .45)); shell.StrokeThickness = 3; shell.Effect = Shadow(22, .2);
                break;
            case "neon":
                shell.Fill = Vertical((Mix(Bg, Ac, .08), 0), (Bg, .35), (Mix(Bg, Black, .25), 1)); shell.Stroke = T(Theme.Accent); shell.StrokeThickness = 1.4; shell.Effect = Glow(16, .65);
                break;
            case "paper":
                shell.Fill = Solid(Mix(Bg, Sf, .75)); shell.Stroke = Solid(Mix(Tx, Bg, .78)); shell.StrokeThickness = 1; shell.Effect = Shadow(16, .22);
                break;
            case "modern":
                shell.Fill = Vertical((Alpha(Mix(Sf, White, .5), .94), 0), (Alpha(Mix(Bg, Sf, .55), .93), .55), (Alpha(Bg, .96), 1)); shell.Stroke = Solid(Alpha(White, .95)); shell.StrokeThickness = 1.2; shell.Effect = Shadow(30, .22);
                break;
            case "space":
                shell.Fill = Vertical((Mix(Bg, Black, .35), 0), (Bg, .55), (Mix(Bg, Ac, .14), 1)); shell.Stroke = Solid(Mix(Ac, Bg, .55)); shell.StrokeThickness = 1; shell.Effect = Shadow(26, .45);
                break;
        }
    }
    /// <summary>Art behind the shell that may stick out of it (cat tail, the notebook's back page).</summary>
    private void DrawBackdrop()
    {
        if (Theme.Design == "cat")
        {
            var tail = Shape("M 792,412 C 852,416 888,372 880,320 C 876,286 848,268 830,288");
            fan.Children.Add(new WpfPath { Data = tail, Stroke = Solid(Mix(Ac, Bg, .45)), StrokeThickness = 21, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false, Effect = Shadow(10, .12) });
            fan.Children.Add(new WpfPath { Data = tail, Stroke = Solid(Mix(Bg, Sf, .3)), StrokeThickness = 15, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false });
            fan.Children.Add(new WpfPath { Data = Shape("M 836,286 C 846,276 860,276 868,288"), Stroke = Solid(Alpha(Mix(Ac, Bg, .2), .55)), StrokeThickness = 6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false });
        }
        if (Theme.Design == "paper")
        {
            var back = new Rectangle { Width = 668, Height = 362, RadiusX = 16, RadiusY = 16, Fill = Solid(Mix(Bg, Tx, .08)), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(-1.6), IsHitTestVisible = false, Effect = Shadow(14, .14) };
            Place(fan, back, 132, 96);
        }
    }
    private void DrawShellDecorations()
    {
        if(!AlternateLayout)return;
        var decor=new Canvas {Width=920,Height=454,Clip=ShellGeometry(),IsHitTestVisible=false,Tag="design-"+Theme.Design};
        switch (Theme.Design)
        {
            case "cat": DrawCatFace(decor); break;
            case "neon": DrawHud(decor); break;
            case "paper": DrawNotebook(decor); break;
            case "modern":
                Add(decor, new Line { X1 = 92, X2 = 828, Y1 = 301.5, Y2 = 301.5, Stroke = Solid(Alpha(White, .95)), StrokeThickness = 1.2 });
                Add(decor, new Line { X1 = 292, X2 = 628, Y1 = 205.5, Y2 = 205.5, Stroke = Solid(Alpha(White, .95)), StrokeThickness = 1.2 });
                var sheen = new Rectangle { Width = 808, Height = 50, Fill = Vertical((Alpha(White, .35), 0), (Alpha(White, 0), 1)) }; Place(decor, sheen, 56, 302);
                break;
            case "space": DrawStarfield(decor); break;
        }
        fan.Children.Add(decor);
        if (Theme.Design == "paper") DrawSpiral();
    }
    private void DrawCatFace(Canvas decor)
    {
        var innerEar = Solid(Alpha(Mix(Ac, White, .35), .75));
        Add(decor, new WpfPath { Data = Shape(CatInnerEar), Fill = innerEar }); Add(decor, new WpfPath { Data = Shape(MirrorPath(CatInnerEar)), Fill = innerEar });
        // Forehead tabby stripes
        var stripe = Solid(Alpha(Mix(Ac, Bg, .35), .55));
        foreach (var (x1, x2) in new[] { (450.0, 452.0), (460.0, 460.0), (470.0, 468.0) }) Add(decor, new Line { X1 = x1, Y1 = 141, X2 = x2, Y2 = x1 == 460 ? 158 : 154, Stroke = stripe, StrokeThickness = 3.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        // Eyes with two catch-lights each (same light direction on both sides)
        foreach (double cx in new[] { 396.0, 524.0 })
        {
            Place(decor, new Ellipse { Width = 24, Height = 28, Fill = Solid(Mix(Tx, Black, .25)), IsHitTestVisible = false }, cx - 12, 152);
            Place(decor, new Ellipse { Width = 9, Height = 9, Fill = Solid(White), IsHitTestVisible = false }, cx - 9, 156);
            Place(decor, new Ellipse { Width = 4, Height = 4, Fill = Solid(Alpha(White, .9)), IsHitTestVisible = false }, cx + 3, 170);
        }
        foreach (double cx in new[] { 336.0, 584.0 }) Place(decor, new Ellipse { Width = 42, Height = 18, Fill = Solid(Alpha(Mix(Ac, White, .15), .42)), IsHitTestVisible = false }, cx - 21, 177);
        Add(decor, new WpfPath { Data = Shape("M 452,181 Q 460,177 468,181 Q 466,188 460,190 Q 454,188 452,181 Z"), Fill = Solid(Mix(Ac, Tx, .2)) });
        Add(decor, new WpfPath { Data = Shape("M 446,194 Q 452,203 460,195 Q 468,203 474,194"), Stroke = Solid(Mix(Tx, Bg, .1)), StrokeThickness = 2.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        var whisker = Solid(Alpha(Mix(Tx, Bg, .45), .8));
        foreach (int side in new[] { -1, 1 })
            foreach (var (y1, y2) in new[] { (181.0, 175.0), (189.0, 189.0), (197.0, 203.0) })
                Add(decor, new Line { X1 = 460 + side * 160, Y1 = y1, X2 = 460 + side * 224, Y2 = y2, Stroke = whisker, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
    }
    private void DrawHud(Canvas decor)
    {
        var grid = Solid(Alpha(Ac, .06));
        for (double x = 82; x <= 840; x += 28) Add(decor, new Line { X1 = x, X2 = x, Y1 = 70, Y2 = 454, Stroke = grid, StrokeThickness = 1 });
        for (double y = 82; y <= 454; y += 28) Add(decor, new Line { X1 = 70, X2 = 850, Y1 = y, Y2 = y, Stroke = grid, StrokeThickness = 1 });
        foreach (double r in new[] { 130.0, 215.0 }) Add(decor, new WpfPath { Data = Sector(r, r + .8, -178, -2), Fill = Solid(Alpha(Ac, .18)) });
        Add(decor, new WpfPath { Data = Sector(300, 301, -165, -15), Stroke = T(Theme.Accent), StrokeThickness = .7, Opacity = .2 });
        for (int angle = -170; angle <= -10; angle += 5)
        {
            double length = angle % 20 == 0 ? 10 : 5; var a = Polar(352, angle); var b = Polar(352 + length, angle);
            Add(decor, new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = T(Theme.Accent), StrokeThickness = 1.2, Opacity = angle % 20 == 0 ? .55 : .25 });
        }
        for (int angle = -160; angle <= -20; angle += 20) { var p = Polar(Theme.Radius, angle); Add(decor, new Line { X1 = 460, Y1 = 409, X2 = p.X, Y2 = p.Y, Stroke = T(Theme.Accent), StrokeThickness = .8, Opacity = .12 }); }
        // Corner brackets, mirrored left/right
        foreach (int side in new[] { -1, 1 })
        {
            double x = 460 + side * 360; string d = side < 0 ? $"M {x},{150} L {x},{124} L {x + 26},{98}" : $"M {x},{150} L {x},{124} L {x - 26},{98}";
            Add(decor, new WpfPath { Data = Shape(d), Stroke = T(Theme.Accent), StrokeThickness = 2, Opacity = .7 });
            var stamp = UI.Text(side < 0 ? "SYS · ONLINE" : "DECK · v8", 10, T(Theme.Accent)); stamp.Width = 120; stamp.TextAlignment = TextAlignment.Center; stamp.Opacity = .55; stamp.FontFamily = new FontFamily("Consolas"); Place(decor, stamp, 460 + side * 330 - 60, 376);
        }
        var label = UI.Text("O R B I T   /   L A U N C H", 12, T(Theme.Accent)); label.Width = 400; label.TextAlignment = TextAlignment.Center; Place(decor, label, 260, 84);
        Add(decor, new Rectangle { Width = 780, Height = 384, Fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 4), SpreadMethod = GradientSpreadMethod.Repeat, MappingMode = BrushMappingMode.Absolute, GradientStops = { new(Alpha(Black, 0), 0), new(Alpha(Black, 0), .5), new(Alpha(Black, .12), .5), new(Alpha(Black, .12), 1) } } });
        Canvas.SetLeft(decor.Children[^1], 70); Canvas.SetTop(decor.Children[^1], 70);
    }
    private void DrawNotebook(Canvas decor)
    {
        var rule = Solid(Mix(Ac, Mix(Bg, Sf, .75), .82));
        for (double y = 196; y <= 446; y += 25) Add(decor, new Line { X1 = 126, X2 = 794, Y1 = y + .5, Y2 = y + .5, Stroke = rule, StrokeThickness = 1 });
        Add(decor, new Line { X1 = 172.5, X2 = 172.5, Y1 = 100, Y2 = 454, Stroke = Solid(Alpha(C("#E46A6A"), .45)), StrokeThickness = 1.2 });
        Add(decor, new Line { X1 = 747.5, X2 = 747.5, Y1 = 100, Y2 = 454, Stroke = Solid(Alpha(C("#E46A6A"), .45)), StrokeThickness = 1.2 });
        foreach (double x in SpiralX()) Place(decor, new Ellipse { Width = 11, Height = 11, Fill = Solid(Mix(Bg, Tx, .55)), IsHitTestVisible = false }, x - 5.5, 111);
        // Doodles: a star on the left, a heart on the right, same height and distance from the center
        var ink = Solid(Alpha(Ac, .75));
        var star = new StreamGeometry(); using (var dc = star.Open()) { var pts = Enumerable.Range(0, 10).Select(i => { double r = i % 2 == 0 ? 13 : 5.5, a = (-90 + 36 * i) * Math.PI / 180; return new Point(222 + r * Math.Cos(a), 152 + r * Math.Sin(a)); }).ToList(); dc.BeginFigure(pts[0], false, true); dc.PolyLineTo(pts.Skip(1).ToList(), true, true); }
        Add(decor, new WpfPath { Data = star, Stroke = ink, StrokeThickness = 1.8, StrokeLineJoin = PenLineJoin.Round });
        Add(decor, new WpfPath { Data = Shape("M 698,162 C 684,152 684,140 692,138 C 696,137 698,140 698,143 C 698,140 700,137 704,138 C 712,140 712,152 698,162 Z"), Stroke = ink, StrokeThickness = 1.8, StrokeLineJoin = PenLineJoin.Round });
    }
    private static IEnumerable<double> SpiralX() => Enumerable.Range(0, 14).Select(k => 460 + (k - 6.5) * 44);
    private void DrawSpiral()
    {
        var wire = Solid(Mix(Tx, Bg, .35));
        foreach (double x in SpiralX()) Place(fan, new Rectangle { Width = 9, Height = 30, RadiusX = 4.5, RadiusY = 4.5, Stroke = wire, StrokeThickness = 2.4, IsHitTestVisible = false }, x - 4.5, 88);
    }
    private void DrawStarfield(Canvas decor)
    {
        var random = new Random(8); var star = Tx;
        for (int i = 0; i < 90; i++)
        {
            double size = .8 + random.NextDouble() * 1.8, x = 50 + random.NextDouble() * 820, y = 86 + random.NextDouble() * 300;
            Place(decor, new Ellipse { Width = size, Height = size, Fill = Solid(Alpha(star, .25 + random.NextDouble() * .6)), IsHitTestVisible = false }, x, y);
        }
        foreach (var (x, y, r) in new[] { (180.0, 146.0, 7.0), (740.0, 146.0, 7.0), (300.0, 104.0, 5.0), (620.0, 104.0, 5.0) })
        {
            var sparkle = Shape($"M {x},{y - r} Q {x},{y} {x + r},{y} Q {x},{y} {x},{y + r} Q {x},{y} {x - r},{y} Q {x},{y} {x},{y - r} Z");
            Add(decor, new WpfPath { Data = sparkle, Fill = T(Theme.Accent), Opacity = .85, Effect = Glow(8, .6) });
        }
        Add(decor, new WpfPath { Data = Shape("M 110,456 A 350,260 0 0 1 810,456"), Stroke = Solid(Alpha(Ac, .28)), StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 3, 4 } });
        Add(decor, new WpfPath { Data = Shape("M 62,456 A 398,300 0 0 1 858,456"), Stroke = Solid(Alpha(Tx, .08)), StrokeThickness = 1 });
        Add(decor, new Line { X1 = 640, Y1 = 132, X2 = 700, Y2 = 112, Stroke = new LinearGradientBrush(Alpha(Tx, 0), Alpha(Tx, .8), 0), StrokeThickness = 1.6, StrokeEndLineCap = PenLineCap.Round });
    }

    // ── Hub ──
    private void StyleHub(WpfPath inner)
    {
        switch (Theme.Design)
        {
            case "cat": inner.Fill = Solid(Mix(Bg, Sf, .55)); inner.Stroke = Solid(Mix(Ac, Bg, .45)); inner.StrokeThickness = 2.5; break;
            case "neon": inner.Fill = Solid(Mix(Bg, Black, .2)); inner.Stroke = T(Theme.Accent); inner.StrokeThickness = 1; inner.Effect = Glow(10, .5); break;
            case "paper": inner.Fill = Solid(Mix(Bg, Sf, .45)); inner.Stroke = Solid(Mix(Tx, Bg, .6)); inner.StrokeThickness = 1.2; inner.StrokeDashArray = new DoubleCollection { 4, 3 }; break;
            case "modern": inner.Fill = Solid(Alpha(White, .45)); inner.Stroke = Solid(Alpha(White, .9)); inner.StrokeThickness = 1; break;
            case "space":
                inner.Fill = new RadialGradientBrush { Center = new Point(.5, .1), GradientOrigin = new Point(.42, 0), RadiusX = .7, RadiusY = .6, GradientStops = { new(Mix(Sf, Ac, .35), 0), new(Sf, .45), new(Mix(Bg, Black, .2), 1) } };
                inner.Stroke = Solid(Alpha(Mix(Ac, White, .3), .7)); inner.StrokeThickness = 1.4; inner.Effect = Glow(18, .35); break;
        }
    }
    private void DrawHubDecorations()
    {
        if (Theme.Design == "cat")
        {
            // Front paws resting on the bar, mirrored 250px from the center
            foreach (double cx in new[] { 210.0, 710.0 })
            {
                Place(fan, new Rectangle { Width = 62, Height = 34, RadiusX = 17, RadiusY = 17, Fill = Solid(Mix(Bg, White, Lift * .6)), Stroke = Solid(Mix(Ac, Bg, .45)), StrokeThickness = 2.5, IsHitTestVisible = false }, cx - 31, 394);
                foreach (double dx in new[] { -10.0, 10.0 }) Place(fan, new Line { X1 = 0, Y1 = 0, X2 = 0, Y2 = 9, Stroke = Solid(Mix(Ac, Bg, .45)), StrokeThickness = 2.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false }, cx + dx, 403);
            }
        }
        if (Theme.Design == "space")
        {
            var clip = new RectangleGeometry(new Rect(0, 0, 920, 454));
            foreach (var (x, y, r) in new[] { (392.0, 430.0, 9.0), (528.0, 430.0, 9.0), (460.0, 446.0, 6.0) })
                Place(fan, new Ellipse { Width = r * 2, Height = r * 1.2, Fill = Solid(Alpha(Black, .16)), IsHitTestVisible = false }, x - r, y);
            fan.Children.Add(new WpfPath { Data = new EllipseGeometry(new Point(460, 428), 250, 20), Stroke = Solid(Alpha(Mix(Ac, White, .25), .55)), StrokeThickness = 3, IsHitTestVisible = false, Clip = clip });
        }
        if (Theme.Design == "neon") fan.Children.Add(new Line { X1 = 182, X2 = 738, Y1 = 398, Y2 = 398, Stroke = T(Theme.Accent), StrokeThickness = 2, Effect = Glow(10, .9), IsHitTestVisible = false });
    }

    // ── Slots ──
    private Brush SlotFill(int index) => Theme.Design switch
    {
        "classic" => new RadialGradientBrush { MappingMode = BrushMappingMode.Absolute, Center = new Point(460, 453), GradientOrigin = new Point(460, 453), RadiusX = 382, RadiusY = 382, GradientStops = { new(Mix(Sf, Bg, .3), .55), new(Mix(Sf, Tx, .05), 1) } },
        "cat" => Vertical((Mix(Sf, White, Lift), 0), (Sf, 1)),
        "neon" => Solid(Alpha(Mix(Bg, Ac, .06), .96)),
        "paper" => Solid(NoteColor(index)),
        "modern" => Solid(Alpha(White, .01)),
        "space" => new RadialGradientBrush { GradientOrigin = new Point(.32, .22), Center = new Point(.42, .3), RadiusX = .75, RadiusY = .75, GradientStops = { new(Mix(PlanetColor(index), White, .35), 0), new(PlanetColor(index), .45), new(Mix(PlanetColor(index), Bg, .65), 1) } },
        _ => T(Theme.Surface)
    };
    private void StyleSlot(WpfPath shape, int index, bool active = false)
    {
        shape.Fill = SlotFill(index); shape.Stroke = AccentTone(.2); shape.StrokeThickness = .8; shape.Effect = null;
        switch (Theme.Design)
        {
            case "cat": shape.Stroke = Solid(Mix(Ac, Sf, .5)); shape.StrokeThickness = 1.6; break;
            case "neon": shape.Stroke = T(Theme.Accent); shape.StrokeThickness = 1.4; shape.Effect = Glow(10, .5); break;
            case "paper": shape.Stroke = Solid(Mix(NoteColor(index), Tx, .15)); shape.StrokeThickness = .8; shape.Effect = Shadow(5, .14); break;
            case "modern": shape.Stroke = Brushes.Transparent; break;
            case "space": shape.Stroke = Solid(Alpha(Mix(PlanetColor(index), White, .4), .8)); shape.StrokeThickness = 1.2; shape.Effect = Glow(12, .3); break;
        }
        if (active)
        {
            shape.Stroke = T(Theme.Accent); shape.StrokeThickness = Theme.Design == "modern" ? 0 : 1.8;
            shape.Fill = Theme.Design switch
            {
                "modern" => Solid(Alpha(Mix(Ac, White, .55), .45)),
                "paper" => Solid(Mix(NoteColor(index), White, .4)),
                "space" => shape.Fill,
                "neon" => Solid(Alpha(Mix(Bg, Ac, .2), .96)),
                "cat" => Vertical((Mix(Sf, White, Lift + .1), 0), (Mix(Sf, Ac, .22), 1)),
                _ => AccentTone(.32)
            };
            if (Theme.Design is "space" or "neon") shape.Effect = Glow(18, .85);
        }
        if (backgroundImage != null) shape.Fill.Opacity = active ? .68 : .38;
    }
    private void DrawSlotDecorations(Canvas canvas, Geometry geometry, Point point, int index, bool folder)
    {
        var b = geometry.Bounds;
        switch (Theme.Design)
        {
            case "paper":
                // Masking tape across the top edge (alternating tilt) and a folded bottom-right corner
                var tape = new Rectangle { Width = 46, Height = 15, Fill = Solid(Alpha(Mix(Ac, White, .5), .55)), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(index % 2 == 0 ? -4 : 4), IsHitTestVisible = false };
                Place(canvas, tape, b.X + b.Width / 2 - 23, b.Y - 6);
                Add(canvas, new WpfPath { Data = Polygon(new(b.Right - 15, b.Bottom), new(b.Right, b.Bottom - 15), new(b.Right - 15, b.Bottom - 15)), Fill = Solid(Mix(NoteColor(index), Tx, .16)) });
                Add(canvas, new WpfPath { Data = Polygon(new(b.Right, b.Bottom - 15), new(b.Right, b.Bottom), new(b.Right - 15, b.Bottom)), Fill = Solid(Mix(Bg, Sf, .75)) });
                break;
            case "neon":
                var node = ((GeometryGroup)geometry).Children[0].Bounds; var center = new Point(node.X + node.Width / 2, node.Y + node.Height / 2);
                Add(canvas, new WpfPath { Data = Hexagon(center, 33), Stroke = T(Theme.Accent), StrokeThickness = 1, Opacity = .35 });
                break;
            case "space":
                var planet = ((GeometryGroup)geometry).Children[0].Bounds;
                foreach (var (dx, dy, r) in new[] { (.68, .64, 6.0), (.3, .74, 4.0) }) Place(canvas, new Ellipse { Width = r * 2, Height = r * 2, Fill = Solid(Alpha(Black, .14)), IsHitTestVisible = false }, planet.X + planet.Width * dx - r, planet.Y + planet.Height * dy - r);
                break;
            case "cat" when !folder:
                // Tiny paw print (folders show their item count there instead) near the right end of each cushion
                double px = b.Right - 30, py = b.Y + b.Height / 2 + 2; var pawInk = Solid(Alpha(Mix(Ac, Sf, .3), .22));
                Place(canvas, new Ellipse { Width = 11, Height = 9, Fill = pawInk, IsHitTestVisible = false }, px - 5.5, py - 2);
                foreach (var (dx, dy) in new[] { (-7.0, -8.0), (-2.5, -11.0), (2.5, -11.0), (7.0, -8.0) }) Place(canvas, new Ellipse { Width = 4, Height = 5, Fill = pawInk, IsHitTestVisible = false }, px + dx - 2, py + dy);
                break;
        }
    }
    private void StyleIconFrame(Border frame)
    {
        switch (Theme.Design)
        {
            case "modern": frame.Background = frame.BorderBrush = Brushes.Transparent; frame.Effect = Shadow(10, .22); break;
            case "paper": frame.Background = frame.BorderBrush = Brushes.Transparent; frame.Effect = null; break;
            case "cat": frame.Background = Solid(Skin.IsLight(Theme.Background) ? Mix(Bg, White, .55) : Mix(Bg, Sf, .5)); frame.BorderBrush = Solid(Mix(Ac, Sf, .55)); frame.BorderThickness = new Thickness(1.2); frame.CornerRadius = new CornerRadius(frame.Width / 2); frame.Effect = null; break;
            case "neon": case "space": frame.Background = frame.BorderBrush = Brushes.Transparent; frame.Effect = Shadow(8, .3); break;
        }
        if(Theme.Frame=="minimal" && !OrbitLayout){frame.Background=frame.BorderBrush=Brushes.Transparent;frame.Effect=null;}
    }
    private void AddAlternateSlots(List<Entry> visible)
    {
        dialHost.Clip=ShellGeometry();dialSpan=178.0/Math.Max(visible.Count,5);
        for(int i=0;i<visible.Count;i++)AddSlot(visible[i],LayoutSlot(i,visible.Count),OrbitLayout?OrbitAngle(i,visible.Count):0);
    }
}
