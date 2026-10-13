using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace DeckLauncher;

internal static class Skin
{
    internal static void Apply(Window window,ThemeSettings theme)
    {
        window.FontFamily=UI.InterfaceFont;window.FontSize=14;TextOptions.SetTextFormattingMode(window,TextFormattingMode.Display);
        window.Resources=(ResourceDictionary)XamlReader.Parse(Styles);Palette(window,theme);
        window.SetResourceReference(Window.BackgroundProperty,"SkinBackground");window.SetResourceReference(Window.ForegroundProperty,"SkinText");
    }
    internal static bool IsLight(string color)
    {
        var c=(Color)ColorConverter.ConvertFromString(color);return .2126*c.R+.7152*c.G+.0722*c.B>150;
    }
    internal static void Palette(Window window,ThemeSettings theme)
    {
        bool light=IsLight(theme.Background);
        var colors=new Dictionary<string,Brush> {
            ["SkinBackground"]=UI.Brush(theme.Background),["SkinPanel"]=theme.Blend(theme.Background,theme.Surface,.55),
            ["SkinSidebar"]=theme.Blend(theme.Background,theme.Surface,.28),
            ["SkinCard"]=UI.Brush(theme.Surface),["SkinControl"]=theme.Blend(theme.Background,theme.Surface,.8),
            ["SkinBorder"]=theme.Blend(theme.Background,theme.Text,.16),["SkinStrongBorder"]=theme.Blend(theme.Background,theme.Text,.3),
            ["SkinText"]=UI.Brush(theme.Text),["SkinMuted"]=theme.Blend(theme.Text,theme.Background,light?.3:.26),
            ["SkinAccent"]=UI.Brush(theme.Accent),["SkinAccentText"]=UI.Brush(IsLight(theme.Accent)?(light?theme.Text:theme.Background):"#FFFFFF"),
            ["SkinAccentSoft"]=theme.Blend(theme.Background,theme.Accent,light?.13:.2),["SkinAccentInk"]=light?theme.Blend(theme.Accent,theme.Text,.25):UI.Brush(theme.Accent),
            ["SkinButton"]=theme.Blend(theme.Background,theme.Surface,.75),["SkinHover"]=theme.Blend(theme.Surface,theme.Accent,.15),
            ["SkinDanger"]=UI.Brush(light?"#C2413B":"#FF8A80"),["SkinDangerSoft"]=theme.Blend(theme.Background,light?"#C2413B":"#FF8A80",.16),
            ["SkinOverlay"]=UI.Brush(theme.Text)
        };
        foreach(var pair in colors)window.Resources[pair.Key]=pair.Value;
        window.Resources["SkinFont"]=UI.InterfaceFont;
        Caption(window,theme);
    }
    /// <summary>Windows 11 lets the standard title bar follow the palette instead of staying white.</summary>
    private static void Caption(Window window,ThemeSettings theme)
    {
        void Paint()
        {
            var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
            Native.PaintCaption(handle,theme.Background,theme.Text,!IsLight(theme.Background));
        }
        if(new WindowInteropHelper(window).Handle!=IntPtr.Zero)Paint();else window.SourceInitialized+=(_,_)=>Paint();
    }
    internal static void Connect(DependencyObject root)
    {
        var visited=new HashSet<DependencyObject>();
        void Walk(DependencyObject obj)
        {
            if(!visited.Add(obj))return;
            if(obj is FrameworkElement node)
            {
                if(node.Tag as string=="keep-colors")return;
                var local=node.GetLocalValueEnumerator();var values=new List<(DependencyProperty,string)>();
                while(local.MoveNext())
                {
                    var entry=local.Current;
                    if(entry.Property.Name is not ("Background" or "Foreground" or "BorderBrush" or "Fill" or "Stroke") || entry.Value is not SolidColorBrush brush || brush.Color.A!=255)continue;
                    string hex=$"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                    string? key=hex switch {
                        "#101722" or "#131D2A" or "#071421"=>"SkinBackground",
                        "#182332" or "#233245"=>"SkinPanel",
                        "#213247" or "#202D3F" or "#223247" or "#243041"=>"SkinControl",
                        "#304159" or "#42546C" or "#3B526D" or "#4A6077"=>"SkinBorder",
                        "#FFFFFF" or "#EAF2FC"=>"SkinText",
                        "#93A5BA"=>"SkinMuted", "#6DE0D4"=>"SkinAccent",
                        "#285D67" or "#254B57" or "#315B68" or "#2F6871"=>"SkinHover", _=>null
                    };
                    if(key!=null)values.Add((entry.Property,key));
                }
                foreach(var value in values)node.SetResourceReference(value.Item1,value.Item2);
            }
            foreach(var child in LogicalTreeHelper.GetChildren(obj).OfType<DependencyObject>())Walk(child);
            if(obj is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(obj);i++)Walk(VisualTreeHelper.GetChild(obj,i));
        }
        Walk(root);
    }
    // Implicit control styles shared by every settings window. Icons use Segoe Fluent Icons (Windows 11) with the
    // matching Segoe MDL2 Assets code points as fallback, so glyphs stay optically centered in their boxes.
    private const string Styles=@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='Button'>
 <Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinButton}'/><Setter Property='BorderBrush' Value='{DynamicResource SkinBorder}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='14,9'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Margin' Value='3'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Grid><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='10'/><Border x:Name='shade' Background='{DynamicResource SkinOverlay}' Opacity='0' CornerRadius='10'/><Border x:Name='ring' BorderBrush='{DynamicResource SkinAccent}' BorderThickness='0' CornerRadius='10'/><ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='shade' Property='Opacity' Value='.07'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='shade' Property='Opacity' Value='.13'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='ring' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 <Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='HorizontalContentAlignment' Value='Center'/>
</Style>
<Style TargetType='TextBox'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinControl}'/><Setter Property='BorderBrush' Value='{DynamicResource SkinBorder}'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='10,8'/><Setter Property='CaretBrush' Value='{DynamicResource SkinAccent}'/><Setter Property='SelectionBrush' Value='{DynamicResource SkinAccent}'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='10'><ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='{TemplateBinding VerticalScrollBarVisibility}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource SkinStrongBorder}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource SkinAccent}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='CheckBox'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Margin' Value='0,9'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition/></Grid.ColumnDefinitions><Border x:Name='box' Width='20' Height='20' CornerRadius='6' BorderThickness='1.5' BorderBrush='{DynamicResource SkinStrongBorder}' Background='{DynamicResource SkinControl}' VerticalAlignment='Center'><TextBlock x:Name='mark' Text='&#xE73E;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='12' Foreground='{DynamicResource SkinAccentText}' HorizontalAlignment='Center' VerticalAlignment='Center' Visibility='Collapsed'/></Border><ContentPresenter Grid.Column='1' Margin='10,0,0,0' VerticalAlignment='Center'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource SkinAccent}'/><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource SkinAccent}'/><Setter TargetName='mark' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource SkinAccent}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource SkinAccent}'/><Setter TargetName='box' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='SkinSwitch' TargetType='CheckBox'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions><ContentPresenter VerticalAlignment='Center' Margin='0,0,14,0'/><Border x:Name='track' Grid.Column='1' Width='44' Height='24' CornerRadius='12' BorderThickness='1.5' BorderBrush='{DynamicResource SkinStrongBorder}' Background='{DynamicResource SkinControl}' VerticalAlignment='Center'><Ellipse x:Name='knob' Width='14' Height='14' Fill='{DynamicResource SkinMuted}' HorizontalAlignment='Left' Margin='4,0,0,0'/></Border></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='track' Property='Background' Value='{DynamicResource SkinAccent}'/><Setter TargetName='track' Property='BorderBrush' Value='{DynamicResource SkinAccent}'/><Setter TargetName='knob' Property='Fill' Value='{DynamicResource SkinAccentText}'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/><Setter TargetName='knob' Property='Margin' Value='0,0,4,0'/><Setter TargetName='knob' Property='Width' Value='16'/><Setter TargetName='knob' Property='Height' Value='16'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='track' Property='BorderBrush' Value='{DynamicResource SkinText}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Padding' Value='11,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='item' CornerRadius='7' Padding='{TemplateBinding Padding}' Background='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource SkinHover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource SkinAccentSoft}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ComboBox'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinControl}'/><Setter Property='Padding' Value='10,8'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='b' Background='{DynamicResource SkinControl}' BorderBrush='{DynamicResource SkinBorder}' BorderThickness='1' CornerRadius='10'><TextBlock Text='&#xE70D;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='11' Foreground='{DynamicResource SkinMuted}' HorizontalAlignment='Right' Margin='0,0,13,0' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource SkinStrongBorder}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' IsHitTestVisible='False' Margin='12,9,34,9' VerticalAlignment='Center'/><Popup Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' VerticalOffset='4'><Border Background='{DynamicResource SkinControl}' BorderBrush='{DynamicResource SkinBorder}' BorderThickness='1' CornerRadius='11' Padding='5' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}'><ScrollViewer MaxHeight='320'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ContextMenu'><Setter Property='FontFamily' Value='{DynamicResource SkinFont}'/><Setter Property='FontSize' Value='13.5'/><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinControl}'/><Setter Property='BorderBrush' Value='{DynamicResource SkinBorder}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='12' Padding='6'><StackPanel IsItemsHost='True'/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='Separator'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Separator'><Border Height='1' Margin='8,5' Background='{DynamicResource SkinBorder}'/></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='MenuItem'><Setter Property='Padding' Value='12,8'/><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'><Border x:Name='row' Background='Transparent' Padding='{TemplateBinding Padding}' CornerRadius='8'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='22'/><ColumnDefinition/><ColumnDefinition Width='10'/></Grid.ColumnDefinitions><TextBlock x:Name='check' Text='&#xE73E;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='12' VerticalAlignment='Center' Visibility='Collapsed' Foreground='{DynamicResource SkinAccent}'/><ContentPresenter x:Name='glyph' ContentSource='Icon' VerticalAlignment='Center' HorizontalAlignment='Left'/><ContentPresenter Grid.Column='1' ContentSource='Header' VerticalAlignment='Center'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='row' Property='Background' Value='{DynamicResource SkinHover}'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='check' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ToolTip'><Setter Property='FontFamily' Value='{DynamicResource SkinFont}'/><Setter Property='FontSize' Value='12.5'/><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinControl}'/><Setter Property='BorderBrush' Value='{DynamicResource SkinBorder}'/><Setter Property='Padding' Value='10,7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToolTip'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8' Padding='{TemplateBinding Padding}' Margin='0,0,6,6'><Border.Effect><DropShadowEffect BlurRadius='10' ShadowDepth='2' Opacity='.22'/></Border.Effect><ContentPresenter/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='ThumbStyle' TargetType='Thumb'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Thumb'><Border x:Name='t' CornerRadius='4' Background='{DynamicResource SkinStrongBorder}'/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='t' Property='Background' Value='{DynamicResource SkinMuted}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ScrollBar'><Setter Property='Background' Value='Transparent'/><Setter Property='Width' Value='12'/><Setter Property='MinWidth' Value='12'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Margin='3,4'><Track.Thumb><Thumb Style='{StaticResource ThumbStyle}'/></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter>
 <Style.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter Property='Width' Value='Auto'/><Setter Property='MinWidth' Value='0'/><Setter Property='Height' Value='12'/><Setter Property='MinHeight' Value='12'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' Margin='4,3'><Track.Thumb><Thumb Style='{StaticResource ThumbStyle}'/></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Trigger></Style.Triggers>
</Style>
<Style x:Key='SliderThumb' TargetType='Thumb'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Thumb'><Grid Width='18' Height='18'><Ellipse x:Name='o' Fill='{DynamicResource SkinAccent}'/><Ellipse Width='8' Height='8' Fill='{DynamicResource SkinAccentText}'/></Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='o' Property='Width' Value='20'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='SliderRepeat' TargetType='RepeatButton'><Setter Property='Focusable' Value='False'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'><Border Height='4' CornerRadius='2' Background='{TemplateBinding Background}' VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='Slider'><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'><Grid Height='24'><Track x:Name='PART_Track'><Track.DecreaseRepeatButton><RepeatButton Style='{StaticResource SliderRepeat}' Background='{DynamicResource SkinAccent}' Command='Slider.DecreaseLarge'/></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Style='{StaticResource SliderRepeat}' Background='{DynamicResource SkinBorder}' Command='Slider.IncreaseLarge'/></Track.IncreaseRepeatButton><Track.Thumb><Thumb Style='{StaticResource SliderThumb}'/></Track.Thumb></Track></Grid><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ListBox'><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/></Style>
<Style TargetType='ListBoxItem'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Padding' Value='12,8'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border x:Name='item' CornerRadius='9' Margin='0,0,0,2' Padding='{TemplateBinding Padding}' Background='Transparent' BorderThickness='1.5' BorderBrush='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource SkinHover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource SkinAccentSoft}'/><Setter TargetName='item' Property='BorderBrush' Value='{DynamicResource SkinAccent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
}
