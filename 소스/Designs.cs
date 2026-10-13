using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WpfPath = System.Windows.Shapes.Path;

namespace DeckLauncher;

public sealed partial class LauncherWindow
{
    private bool AlternateLayout => Theme.Design is "modern" or "cat" or "neon" or "paper";
    private Geometry Polygon(params Point[] points)
    {
        var shape=new StreamGeometry();using(var dc=shape.Open()){dc.BeginFigure(points[0],true,true);dc.PolyLineTo(points.Skip(1).ToList(),true,false);}shape.Freeze();return shape;
    }
    internal Geometry ShellGeometry()
    {
        if(Theme.Design=="modern")return new RectangleGeometry(new Rect(40,190,840,264),28,28);
        if(Theme.Design=="paper")return new RectangleGeometry(new Rect(128,94,664,360),24,24);
        if(Theme.Design=="neon")return Polygon(new(70,125),new(125,70),new(795,70),new(850,125),new(850,454),new(70,454));
        if(Theme.Design=="cat")
        {
            return new CombinedGeometry(GeometryCombineMode.Union,new RectangleGeometry(new Rect(130,184,660,270),45,45),new GeometryGroup {Children=new GeometryCollection {Polygon(new(148,240),new(206,96),new(304,194)),Polygon(new(616,194),new(714,96),new(772,240))}});
        }
        return Sector(.01,390,-180,0);
    }
    private Geometry HubGeometry() => AlternateLayout ? new RectangleGeometry(new Rect(Theme.Design=="paper"?140:Theme.Design=="modern"?52:145,400,Theme.Design=="paper"?640:Theme.Design=="modern"?816:630,54),14,14) : Sector(.01,205,-180,0);
    private (double X,double Y,double Width) InfoBox => Theme.Design switch {"modern"=>(80,207,760),"cat"=>(210,184,500),"paper"=>(190,107,540),_=>(305,278,310)};
    private Geometry LayoutSlot(int index,int count)
    {
        if(Theme.Design=="neon")
        {
            var point=Polar(Theme.Radius,count==1?-90:-153+126.0*index/(count-1));
            return new GeometryGroup {Children=new GeometryCollection {new EllipseGeometry(new Point(point.X,point.Y-3),41,41),new RectangleGeometry(new Rect(point.X-49,point.Y+40,98,22),8,8)}};
        }
        if(Theme.Design=="modern")
        {
            double width=Math.Min(114,(780.0-12*(count-1))/Math.Max(count,1)),left=460-(width*count+12*(count-1))/2;
            return new RectangleGeometry(new Rect(left+index*(width+12),302,width,92),17,17);
        }
        int columns=Theme.Design=="paper"?Math.Min(4,Math.Max(1,count)):Math.Min(3,Math.Max(1,count));
        int rows=Math.Max(1,(count+columns-1)/columns);
        double w=Theme.Design=="paper"?Math.Min(144,(610.0-(columns-1)*10)/columns):178;
        double h=Theme.Design=="paper"?(rows>2?67:96):(rows>2?42:60);
        double y=Theme.Design=="paper"?(rows>2?178:188):272;
        double x=460-(columns*w+(columns-1)*10)/2;
        return new RectangleGeometry(new Rect(x+(index%columns)*(w+10),y+(index/columns)*(h+9),w,h),Theme.Design=="paper"?12:22,Theme.Design=="paper"?12:22);
    }
    private Point IconPoint(Geometry geometry,double angle) => AlternateLayout && Theme.Design!="neon" ? new(geometry.Bounds.X+geometry.Bounds.Width/2,geometry.Bounds.Y+geometry.Bounds.Height/2) : Polar(Theme.Radius,angle);
    private DropShadowEffect Glow(double blur=12,double opacity=.45)=>new(){Color=(Color)ColorConverter.ConvertFromString(Theme.Accent),BlurRadius=blur,ShadowDepth=0,Opacity=opacity};
    private Brush SlotFill()=>T(Theme.Surface);
    private void StyleShell(WpfPath shell)
    {
        if(!AlternateLayout)return;shell.Fill=T(Theme.Background);shell.Stroke=Theme.Blend(Theme.Text,Theme.Background,.82);shell.Effect=Shadow(20,.22);
        if(Theme.Design=="neon"){shell.Stroke=T(Theme.Accent);shell.Effect=Glow(15,.6);shell.StrokeThickness=1.3;}
        if(Theme.Design=="cat")shell.Stroke=AccentTone(.4);
    }
    private void StyleSlot(WpfPath shape)
    {
        shape.Fill=SlotFill();if(backgroundImage!=null)shape.Fill.Opacity=.38;
        if(AlternateLayout){shape.Stroke=Theme.Blend(Theme.Surface,Theme.Text,.12);shape.StrokeThickness=.8;shape.Effect=Shadow(6,.08);}
        if(Theme.Design=="neon"){shape.Stroke=T(Theme.Accent);shape.StrokeThickness=1.2;shape.Effect=Glow(10,.45);}
        if(Theme.Design=="cat")shape.Stroke=AccentTone(.35);
    }
    private void StyleHub(WpfPath inner){if(AlternateLayout){inner.Fill=Theme.Blend(Theme.Background,Theme.Surface,.25);inner.Stroke=Theme.Blend(Theme.Text,Theme.Background,.86);inner.StrokeThickness=.6;}}
    private void StyleIconFrame(Border frame)
    {
        if(Theme.Design=="modern" || Theme.Design=="paper"){frame.Background=frame.BorderBrush=Brushes.Transparent;frame.Effect=null;}
        if(Theme.Design=="cat"){frame.Background=Theme.Blend(Theme.Surface,Theme.Background,.5);frame.Effect=null;}
        if(Theme.Design=="neon"){frame.Background=T(Theme.Background);frame.BorderBrush=T(Theme.Accent);frame.Effect=Glow();}
        if(Theme.Frame=="minimal"){frame.Background=frame.BorderBrush=Brushes.Transparent;frame.Effect=null;}
    }
    private void DrawShellDecorations()
    {
        if(!AlternateLayout)return;
        var decor=new Canvas {Width=920,Height=454,Clip=ShellGeometry(),IsHitTestVisible=false,Tag="design-"+Theme.Design};
        if(Theme.Design=="cat")
        {
            decor.Children.Add(new System.Windows.Shapes.Polygon {Points=new PointCollection{new(165,225),new(208,121),new(280,194)},Fill=AccentTone(.3)});
            decor.Children.Add(new System.Windows.Shapes.Polygon {Points=new PointCollection{new(640,194),new(712,121),new(755,225)},Fill=AccentTone(.3)});
            for(int side=-1;side<=1;side+=2)for(int y=241;y<=260;y+=10)decor.Children.Add(new Line {X1=460+side*288,X2=460+side*315,Y1=y,Y2=y-3,Stroke=AccentTone(.6),StrokeThickness=1.5});
            Paw(decor,151,411,.6);Paw(decor,768,411,.6);
        }
        if(Theme.Design=="neon")
        {
            decor.Children.Add(new WpfPath {Data=Sector(300,301,-165,-15),Stroke=T(Theme.Accent),StrokeThickness=.7,Opacity=.2});
            for(int angle=-160;angle<=-20;angle+=20){var p=Polar(Theme.Radius,angle);decor.Children.Add(new Line{X1=460,Y1=409,X2=p.X,Y2=p.Y,Stroke=T(Theme.Accent),StrokeThickness=.8,Opacity=.14});}
            var label=UI.Text("O R B I T   /   L A U N C H",12,T(Theme.Accent));Place(decor,label,344,99);
        }
        if(Theme.Design=="paper")
        {
            for(int x=161;x<=760;x+=20)Place(decor,new Ellipse{Width=7,Height=7,Fill=Theme.Blend(Theme.Text,Theme.Background,.7)},x,101);
            decor.Children.Add(new Line {X1=150,X2=770,Y1=171,Y2=171,Stroke=AccentTone(.2),StrokeThickness=1});
        }
        fan.Children.Add(decor);
    }
    private void Paw(Canvas canvas,double x,double y,double opacity)
    {
        Place(canvas,new Ellipse {Width=13,Height=10,Fill=T(Theme.Accent),Opacity=opacity},x-6.5,y);
        foreach(var p in new[]{(-8.0,-3.0),(-3.5,-7.0),(2.0,-7.0),(6.5,-3.0)})Place(canvas,new Ellipse{Width=4,Height=5,Fill=T(Theme.Accent),Opacity=opacity},x+p.Item1,y+p.Item2);
    }
    private void DrawHubDecorations() { }
    private void DrawSlotDecorations(Canvas canvas,Point point) { }
    private void AddAlternateSlots(List<Entry> visible)
    {
        dialHost.Clip=ShellGeometry();dialSpan=178.0/Math.Max(visible.Count,5);
        for(int i=0;i<visible.Count;i++)AddSlot(visible[i],LayoutSlot(i,visible.Count),Theme.Design=="neon"?(visible.Count==1?-90:-153+126.0*i/(visible.Count-1)):0);
    }
}
