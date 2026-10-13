using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WpfPath = System.Windows.Shapes.Path;

namespace DeckLauncher;

public sealed partial class LauncherWindow
{
    private DropShadowEffect Glow(double blur = 15, double opacity = .55) => new() { Color = (Color)ColorConverter.ConvertFromString(Theme.Accent), BlurRadius = blur, ShadowDepth = 0, Opacity = opacity };
    private Brush SlotFill()
    {
        if (Theme.Design == "glass")
        {
            var a = (Color)ColorConverter.ConvertFromString(Theme.Surface); var b = (Color)ColorConverter.ConvertFromString(Theme.Background);
            a.A = 205; b.A = 160; return new LinearGradientBrush(a, b, new Point(0, 0), new Point(1, 1));
        }
        return Tone(1);
    }
    private void StyleShell(WpfPath shell)
    {
        if (Theme.Design == "modern") { shell.Fill = T(Theme.Background); shell.Stroke = Theme.Blend(Theme.Text, Theme.Background, .86); shell.Effect = Shadow(16, .15); }
        if (Theme.Design == "cat") { shell.Fill = Gradient(Theme.Background, Theme.Surface); shell.Stroke = AccentTone(.35); shell.StrokeThickness = 1.6; shell.Effect = Shadow(18, .2); }
        if (Theme.Design == "neon") { shell.Fill = T(Theme.Background); shell.Stroke = T(Theme.Accent); shell.StrokeThickness = 1.7; shell.Effect = Glow(18, .6); }
        if (Theme.Design == "glass") { shell.Fill = Gradient(Theme.Surface, Theme.Background); shell.Fill.Opacity = .9; shell.Stroke = T(Theme.Accent); shell.Effect = Shadow(25, .35); }
    }
    private void StyleSlot(WpfPath shape)
    {
        shape.Fill = SlotFill();
        if (backgroundImage != null) shape.Fill.Opacity = .35;
        if (Theme.Design == "modern") { shape.Stroke = Theme.Blend(Theme.Text, Theme.Surface, .91); shape.StrokeThickness = .8; }
        if (Theme.Design == "neon") { shape.Stroke = T(Theme.Accent); shape.StrokeThickness = 1.3; shape.Effect = Glow(10, .4); }
        if (Theme.Design == "glass") { shape.Stroke = T(Theme.Accent); shape.Stroke.Opacity = .5; shape.StrokeThickness = 1; }
    }
    private void StyleHub(WpfPath inner)
    {
        if (Theme.Design == "modern") { inner.Fill = T(Theme.Background); inner.Stroke = Theme.Blend(Theme.Text, Theme.Background, .92); }
        if (Theme.Design == "cat") { inner.Fill = T(Theme.Background); inner.Stroke = AccentTone(.3); }
        if (Theme.Design == "neon") { inner.Fill = T(Theme.Background); inner.Stroke = T(Theme.Accent); inner.Effect = Glow(13, .5); }
        if (Theme.Design == "glass") { inner.Fill = Gradient(Theme.Surface, Theme.Background); inner.Fill.Opacity = .9; inner.Stroke = T(Theme.Accent); }
    }
    private void StyleIconFrame(Border frame)
    {
        if (Theme.Design == "modern") { frame.Background = Theme.Blend(Theme.Surface, Theme.Background, .6); frame.BorderBrush = Theme.Blend(Theme.Text, Theme.Surface, .92); frame.Effect = null; }
        if (Theme.Design == "cat") { frame.Background = Theme.Blend(Theme.Surface, Theme.Background, .5); frame.BorderBrush = AccentTone(.5); frame.Effect = Shadow(7, .15); }
        if (Theme.Design == "neon") { frame.Background = T(Theme.Background); frame.BorderBrush = T(Theme.Accent); frame.BorderThickness = new Thickness(1.4); frame.Effect = Glow(15, .6); }
        if (Theme.Design == "glass") { frame.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)); frame.BorderBrush = new SolidColorBrush(Color.FromArgb(155, 235, 248, 255)); frame.Effect = Shadow(10, .2); }
        if (Theme.Frame == "minimal") { frame.Background = frame.BorderBrush = Brushes.Transparent; frame.Effect = null; }
    }
    private void DrawShellDecorations()
    {
        if (Theme.Design == "classic" || Theme.Design == "modern") return;
        var decor = new Canvas { Width = 920, Height = 454, Clip = Sector(.01, 385, -180, 0), IsHitTestVisible = false, Tag = "design-" + Theme.Design };
        if (Theme.Design == "neon")
        {
            for (int x = 80; x < 860; x += 42) decor.Children.Add(new Line { X1 = x, X2 = x, Y1 = 70, Y2 = 454, Stroke = T(Theme.Accent), StrokeThickness = .45, Opacity = .12 });
            for (int y = 90; y < 454; y += 42) decor.Children.Add(new Line { X1 = 70, X2 = 850, Y1 = y, Y2 = y, Stroke = T(Theme.Accent), StrokeThickness = .45, Opacity = .12 });
        }
        if (Theme.Design == "glass")
        {
            var gleam = new Ellipse { Width = 590, Height = 200, Fill = new LinearGradientBrush(Color.FromArgb(75, 255, 255, 255), Colors.Transparent, 90), RenderTransform = new RotateTransform(-18), Opacity = .55 };
            Place(decor, gleam, 190, 20);
            var line = new WpfPath { Data = Sector(368, 369, -150, -60), Fill = new SolidColorBrush(Color.FromArgb(85, 255, 255, 255)) }; decor.Children.Add(line);
        }
        if (Theme.Design == "cat")
        {
            foreach (var (radius, angle) in new[] { (370.0, -155.0), (373.0, -114.0), (372.0, -64.0), (374.0, -24.0) })
            { var point = Polar(radius, angle); Paw(decor, point.X, point.Y, .45); }
        }
        fan.Children.Add(decor);
    }
    private void DrawHubDecorations()
    {
        if (Theme.Design != "cat") return;
        var decor = new Canvas { Width = 920, Height = 454, IsHitTestVisible = false, Tag = "cat-hub" };
        var face = new Ellipse { Width = 44, Height = 26, Fill = AccentTone(.3) }; Place(decor, face, 438, 249);
        foreach (var points in new[] { new PointCollection { new(439, 257), new(440, 240), new(453, 252) }, new PointCollection { new(467, 252), new(480, 240), new(481, 257) } })
            decor.Children.Add(new Polygon { Points = points, Fill = AccentTone(.3), Stroke = AccentTone(.5), StrokeThickness = .6 });
        foreach (double x in new[] { 449.0, 468.0 }) Place(decor, new Ellipse { Width = 3, Height = 3, Fill = T(Theme.Text) }, x, 258);
        var nose = new Polygon { Points = new PointCollection { new(457, 263), new(463, 263), new(460, 266) }, Fill = T(Theme.Accent) }; decor.Children.Add(nose);
        foreach (int side in new[] { -1, 1 }) foreach (int y in new[] { 260, 266 }) decor.Children.Add(new Line { X1 = 460 + side * 17, X2 = 460 + side * 30, Y1 = y, Y2 = y - 2, Stroke = AccentTone(.6), StrokeThickness = .8 });
        Paw(decor, 328, 393, .55); Paw(decor, 591, 393, .55); fan.Children.Add(decor);
    }
    private void Paw(Canvas canvas, double x, double y, double opacity)
    {
        var pad = new Ellipse { Width = 13, Height = 10, Fill = T(Theme.Accent), Opacity = opacity }; Place(canvas, pad, x - 6.5, y);
        foreach (var pair in new[] { (-8.0, -3.0), (-3.5, -7.0), (2.0, -7.0), (6.5, -3.0) }) Place(canvas, new Ellipse { Width = 4, Height = 5, Fill = T(Theme.Accent), Opacity = opacity }, x + pair.Item1, y + pair.Item2);
    }
    private void DrawSlotDecorations(Canvas contents, Point point)
    {
        if (Theme.Design == "cat" && Theme.Frame != "minimal")
        {
            double x = point.X, y = point.Y - 38;
            foreach (var points in new[] { new PointCollection { new(x - 27, y + 8), new(x - 25, y - 8), new(x - 10, y + 2) }, new PointCollection { new(x + 10, y + 2), new(x + 25, y - 8), new(x + 27, y + 8) } })
                contents.Children.Add(new Polygon { Points = points, Fill = Theme.Blend(Theme.Surface, Theme.Background, .5), Stroke = AccentTone(.5), StrokeThickness = .7, IsHitTestVisible = false });
        }
        if (Theme.Design == "glass")
        {
            var shine = new Line { X1 = point.X - 17, X2 = point.X + 9, Y1 = point.Y - 34, Y2 = point.Y - 34, Stroke = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), StrokeThickness = 1, IsHitTestVisible = false }; contents.Children.Add(shine);
        }
    }
}
