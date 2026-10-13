using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeckLauncher;

public sealed partial class LauncherWindow
{
    private readonly List<Window> folderPreviews = new();
    private DispatcherTimer? hoverOpen, hoverClose;
    internal int PreviewDepth => folderPreviews.Count;
    internal Window? FolderPreview => folderPreviews.LastOrDefault();
    private void ScheduleFolderPreview(Entry folder, FrameworkElement anchor, int depth=0)
    {
        hoverClose?.Stop(); hoverOpen?.Stop();
        hoverOpen=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(180) };
        hoverOpen.Tick+=(_,_)=>{hoverOpen.Stop(); ShowFolderPreview(folder,anchor,depth);};hoverOpen.Start();
    }
    private void SchedulePreviewClose()
    {
        hoverOpen?.Stop(); hoverClose?.Stop();
        hoverClose=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(360)};
        hoverClose.Tick+=(_,_)=>{hoverClose.Stop();if(!folderPreviews.Any(w=>w.IsMouseOver) && !slots.Any(pair=>pair.Value.IsMouseOver && library.Find(pair.Key)?.IsFolder==true))CloseFolderPreview();};hoverClose.Start();
    }
    internal void CloseFolderPreview(int depth=0)
    {
        hoverOpen?.Stop(); hoverClose?.Stop();
        while(folderPreviews.Count>depth){var window=folderPreviews[^1];folderPreviews.RemoveAt(folderPreviews.Count-1);window.Close();}
    }
    internal void ShowFolderPreview(Entry folder, FrameworkElement anchor, int depth=0)
    {
        if(previewMode)return;
        if(folderPreviews.Count>depth && folderPreviews[depth].Tag as string==folder.Id){CloseFolderPreview(depth+1);return;}
        CloseFolderPreview(depth);
        var entries=library.Children(folder.Id).ToList();
        var popup=new Window { Title=folder.Name+" · 빠른 펼치기",Tag=folder.Id,Owner=this,ShowInTaskbar=false,ShowActivated=false,Topmost=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Width=284,Height=Math.Min(390,100+Math.Max(1,entries.Count)*59),FontSize=13 };
        UI.Theme(popup,Theme);popup.Background=Brushes.Transparent;popup.FontFamily=LFont;
        var panel=new Grid {Margin=new Thickness(5)};panel.RowDefinitions.Add(new(){Height=GridLength.Auto});panel.RowDefinitions.Add(new());
        popup.Content=new Border {Background=T(Theme.Background),BorderBrush=AccentTone(.6),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(18),Padding=new Thickness(14),Child=panel};
        var header=new StackPanel {Margin=new Thickness(3,1,3,12)};var name=UI.Text(folder.Name,19,T(Theme.Text));name.FontFamily=LFont;header.Children.Add(name);header.Children.Add(UI.Text(entries.Count+"개 항목 · 바로 선택하세요",11,MutedTone));panel.Children.Add(header);
        var rows=new StackPanel();var scroll=new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);panel.Children.Add(scroll);
        if(entries.Count==0)rows.Children.Add(UI.Text("아직 등록된 항목이 없어요",13,MutedTone));
        foreach(var entry in entries)
        {
            var row=new Button {Padding=new Thickness(8),Margin=new Thickness(0,0,0,5),Background=T(Theme.Surface),Foreground=T(Theme.Text),HorizontalContentAlignment=HorizontalAlignment.Stretch,Height=53,Tag=entry.Id,ToolTip=string.IsNullOrWhiteSpace(entry.Note)?null:entry.Note};
            var layout=new Grid();layout.ColumnDefinitions.Add(new(){Width=new GridLength(38)});layout.ColumnDefinitions.Add(new());layout.ColumnDefinitions.Add(new(){Width=new GridLength(15)});
            layout.Children.Add(new Image {Source=IconFor(entry),Width=30,Height=30,Stretch=Stretch.Uniform});var caption=new TextBlock {Text=entry.Name,FontFamily=LFont,FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(8,0,4,0)};Grid.SetColumn(caption,1);layout.Children.Add(caption);
            if(entry.IsFolder){var arrow=UI.Text("›",20,T(Theme.Accent));Grid.SetColumn(arrow,2);layout.Children.Add(arrow);}
            row.Content=layout;row.Click+=(_,_)=>{CloseFolderPreview();Open(entry);};
            row.MouseEnter+=(_,_)=>{hoverClose?.Stop();ShowNote(entry);if(entry.IsFolder)ScheduleFolderPreview(entry,row,depth+1);else CloseFolderPreview(depth+1);};
            row.MouseLeave+=(_,_)=>SchedulePreviewClose(); rows.Children.Add(row);
        }
        popup.MouseEnter+=(_,_)=>{hoverClose?.Stop();};popup.MouseLeave+=(_,_)=>SchedulePreviewClose();
        popup.Deactivated+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{if(!IsActive && !folderPreviews.Any(w=>w.IsActive))HandleFocusLoss();}),DispatcherPriority.Background);
        popup.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){CloseFolderPreview();e.Handled=true;}};
        Point at;
        if(depth==0 && slotShapes.TryGetValue(folder.Id,out var shape)) {var rect=shape.Bounds;at=anchor.PointToScreen(new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2));}
        else at=anchor.PointToScreen(new Point(anchor.ActualWidth,anchor.ActualHeight/2));
        var dpi=VisualTreeHelper.GetDpi(this);var physical=Native.WorkAreaAt(at);var work=new Rect(physical.X/dpi.DpiScaleX,physical.Y/dpi.DpiScaleY,physical.Width/dpi.DpiScaleX,physical.Height/dpi.DpiScaleY);at=new Point(at.X/dpi.DpiScaleX,at.Y/dpi.DpiScaleY);
        double left=depth==0?at.X+42:at.X+8;double top=at.Y-55;
        if(left+popup.Width>work.Right)left=depth==0?at.X-popup.Width-42:folderPreviews[depth-1].Left-popup.Width-8;
        if(Left>-10000){left=Math.Clamp(left,work.Left+8,work.Right-popup.Width-8);top=Math.Clamp(top,work.Top+8,work.Bottom-popup.Height-4);}
        popup.Left=left;popup.Top=top;folderPreviews.Add(popup);popup.Show();
    }
    private void SetupSliceDrop(Button target, Entry anchor, double centerAngle)
    {
        if(previewMode)return;target.AllowDrop=true;
        (bool Into,bool After) Location(DragEventArgs e)
        {
            var point=e.GetPosition(target);
            if(AlternateLayout && slotShapes.TryGetValue(anchor.Id,out var geometry)) {var rect=geometry.Bounds;return(anchor.IsFolder && Math.Abs(point.X-(rect.X+rect.Width/2))<rect.Width*.26,point.X>rect.X+rect.Width/2);}
            double angle=Math.Atan2(point.Y-453,point.X-460)*180/Math.PI;
            double span=178.0/Math.Max(5,RenderedSlots);
            return(anchor.IsFolder && Math.Abs(angle-centerAngle)<span*.28,angle>centerAngle);
        }
        target.DragOver+=(_,e)=>
        {
            CloseFolderPreview();var position=Location(e);bool allowed=e.Data.GetData(LibraryWindow.EntryDragFormat) is string[] ids && ids.All(id=>library.Find(id) is Entry entry && entry!=anchor && library.CanMove(entry,position.Into?anchor.Id:anchor.ParentId));
            e.Effects=allowed?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;target.Opacity=allowed?.75:1;target.ToolTip=position.Into?"폴더 안으로 넣기":position.After?"이 칸 뒤로 이동":"이 칸 앞으로 이동";
        };
        target.DragLeave+=(_,_)=>target.Opacity=1;
        target.Drop+=(_,e)=>
        {
            e.Handled=true;target.Opacity=1;var position=Location(e);
            if(e.Data.GetData(LibraryWindow.EntryDragFormat) is string[] ids)
            {
                bool changed=position.Into?library.MoveEntries(ids,anchor.Id):library.ReorderEntries(ids,anchor.Id,position.After);
                if(changed){store.Save(library);Refresh();}
            }
        };
    }
}
