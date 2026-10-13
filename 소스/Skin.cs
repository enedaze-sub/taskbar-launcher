using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace DeckLauncher;

internal static class Skin
{
    internal static void Apply(Window window,ThemeSettings theme)
    {
        window.FontFamily=UI.RoundedFont;window.FontSize=14;TextOptions.SetTextFormattingMode(window,TextFormattingMode.Display);
        window.Resources=(ResourceDictionary)XamlReader.Parse(Styles);Palette(window,theme);
        window.SetResourceReference(Window.BackgroundProperty,"SkinBackground");window.SetResourceReference(Window.ForegroundProperty,"SkinText");
    }
    internal static void Palette(Window window,ThemeSettings theme)
    {
        var colors=new Dictionary<string,Brush> {
            ["SkinBackground"]=UI.Brush(theme.Background),["SkinPanel"]=theme.Blend(theme.Background,theme.Surface,.55),
            ["SkinCard"]=UI.Brush(theme.Surface),["SkinControl"]=theme.Blend(theme.Background,theme.Surface,.8),
            ["SkinBorder"]=theme.Blend(theme.Background,theme.Text,.2),["SkinText"]=UI.Brush(theme.Text),
            ["SkinMuted"]=theme.Blend(theme.Text,theme.Background,.22),["SkinAccent"]=UI.Brush(theme.Accent),
            ["SkinButton"]=theme.Blend(theme.Background,theme.Surface,.75),["SkinHover"]=theme.Blend(theme.Surface,theme.Accent,.15)
        };
        foreach(var pair in colors)window.Resources[pair.Key]=pair.Value;
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
    private const string Styles=@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='Button'>
 <Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinButton}'/><Setter Property='BorderBrush' Value='{DynamicResource SkinBorder}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='14,10'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Margin' Value='3'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='12' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Opacity' Value='.82'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource SkinAccent}'/><Setter TargetName='box' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='TextBox'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinControl}'/><Setter Property='BorderBrush' Value='{DynamicResource SkinBorder}'/><Setter Property='Padding' Value='11,9'/><Setter Property='CaretBrush' Value='{DynamicResource SkinAccent}'/><Setter Property='SelectionBrush' Value='{DynamicResource SkinHover}'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
<Style TargetType='CheckBox'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Margin' Value='5,11'/></Style>
<Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Padding' Value='11,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='item' CornerRadius='7' Padding='{TemplateBinding Padding}' Background='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource SkinHover}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ComboBox'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinControl}'/><Setter Property='Padding' Value='10,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='{DynamicResource SkinControl}' BorderBrush='{DynamicResource SkinBorder}' BorderThickness='1' CornerRadius='9'><TextBlock Text='⌄' Foreground='{DynamicResource SkinMuted}' HorizontalAlignment='Right' Margin='0,0,11,0' VerticalAlignment='Center'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' IsHitTestVisible='False' Margin='11,9,30,9'/><Popup Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'><Border Background='{DynamicResource SkinControl}' BorderBrush='{DynamicResource SkinBorder}' BorderThickness='1' CornerRadius='11' Padding='5' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}'><ScrollViewer MaxHeight='320'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ContextMenu'><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Background' Value='{DynamicResource SkinControl}'/><Setter Property='BorderBrush' Value='{DynamicResource SkinBorder}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='13' Padding='7'><StackPanel IsItemsHost='True'/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='MenuItem'><Setter Property='Padding' Value='13,9'/><Setter Property='Foreground' Value='{DynamicResource SkinText}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'><Border x:Name='row' Background='Transparent' Padding='{TemplateBinding Padding}' CornerRadius='8'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='20'/><ColumnDefinition/><ColumnDefinition Width='10'/></Grid.ColumnDefinitions><TextBlock x:Name='check' Text='✓' Visibility='Collapsed' Foreground='{DynamicResource SkinAccent}'/><ContentPresenter Grid.Column='1' ContentSource='Header'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='row' Property='Background' Value='{DynamicResource SkinHover}'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='check' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
}
