using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
namespace DeckLauncher;
internal static class TaskbarAnchor
{
    private delegate bool EnumCallback(IntPtr handle,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback,IntPtr parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr handle,StringBuilder name,int count);
    private sealed class State {internal DateTime Updated=DateTime.MinValue;internal Rect[] Matches=Array.Empty<Rect>();internal Task<Rect[]>? Pending;}
    private static readonly object gate=new();
    private static readonly Dictionary<Rect,State> screens=new();
    internal static async Task<Rect?> FindAsync(Native.DisplayContext display)
    {
        State state;Task<Rect[]> query;
        lock(gate)
        {
            if(!screens.TryGetValue(display.Bounds,out state!))screens[display.Bounds]=state=new();
            if(DateTime.UtcNow-state.Updated<TimeSpan.FromSeconds(5))return Choose(state.Matches,display.Cursor,display.Bounds);
            if(state.Pending==null || state.Pending.IsCompleted)state.Pending=Task.Run(()=>{var found=Find(display.Bounds);lock(gate){state.Matches=found;state.Updated=DateTime.UtcNow;}return found;});
            query=state.Pending;
        }
        // A slow Explorer query only updates the cache; it cannot move a visible window.
        if(await Task.WhenAny(query,Task.Delay(75))==query)return Choose(await query,display.Cursor,display.Bounds);
        lock(gate)return Choose(state.Matches,display.Cursor,display.Bounds);
    }
    private static Rect[] Find(Rect screen)
    {
        try
        {
            var handles=new List<IntPtr>();
            EnumWindows((handle,_)=>{var name=new StringBuilder(128);GetClassName(handle,name,name.Capacity);
                if(name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"){var b=Native.WindowBounds(handle);if(b.HasValue && screen.Contains(new Point(b.Value.X+b.Value.Width/2,b.Value.Y+b.Value.Height/2)))handles.Add(handle);}return true;},IntPtr.Zero);
            var matches=new List<Rect>();
            foreach(var handle in handles)
            {
                var root=AutomationElement.FromHandle(handle);
                var buttons=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
                foreach(AutomationElement element in buttons)
                {
                    try{var s=element.Current;if(!s.AutomationId.Contains("DeckLauncher",StringComparison.OrdinalIgnoreCase) && !s.Name.Contains("작업표시줄 런처") && !s.Name.Contains("DeckLauncher",StringComparison.OrdinalIgnoreCase))continue;
                        var r=s.BoundingRectangle;if(!r.IsEmpty && r.Width>0 && r.Height>0 && !s.IsOffscreen)matches.Add(r);}
                    catch(ElementNotAvailableException){}
                }
            }
            return matches.ToArray();
        }
        catch{return Array.Empty<Rect>();}
    }
    internal static Rect? Choose(IEnumerable<Rect> matches,Point cursor,Rect screen)
    {
        var valid=matches.Where(r=>!r.IsEmpty && r.Width>0 && r.Height>0 && screen.Contains(new Point(r.X+r.Width/2,r.Y+r.Height/2))).OrderBy(r=>Math.Pow(r.X+r.Width/2-cursor.X,2)+Math.Pow(r.Y+r.Height/2-cursor.Y,2)).ToArray();
        return valid.Length==0?null:valid[0];
    }
    internal static Rect PhysicalPlacement(Native.DisplayContext display,Rect? anchor)
    {
        anchor=anchor.HasValue?Choose(new[]{anchor.Value},display.Cursor,display.Bounds):null;
        double center=anchor.HasValue?anchor.Value.X+anchor.Value.Width/2:display.Cursor.X;
        double sx=display.ScaleX>0?display.ScaleX:1,sy=display.ScaleY>0?display.ScaleY:1;
        var work=new Rect(0,0,display.Work.Width/sx,display.Work.Height/sy);
        center=Math.Clamp((center-display.Work.Left)/sx,Math.Min(220,work.Width/2),work.Right-Math.Min(220,work.Width/2));
        var p=Placement(work,center);return new(display.Work.Left+p.X*sx,display.Work.Top+p.Y*sy,p.Width*sx,p.Height*sy);
    }
    internal static Rect Placement(Rect work,double centerX)
    {
        double room=2*Math.Min(centerX-work.Left,work.Right-centerX);
        double width=Math.Min(work.Width,Math.Min(920,Math.Max(440,room)));width=Math.Min(width,Math.Max(1,work.Height-10)*920/454);
        double scale=width/920;return new Rect(Math.Clamp(centerX-width/2,work.Left,Math.Max(work.Left,work.Right-width)),work.Bottom-453.5*scale,width,454*scale);
    }
}
