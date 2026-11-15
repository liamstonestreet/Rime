using System.Windows;
using System.Windows.Markup;

// One scrollbar/gutter implementation for the control center and Spotlight.
static class ScrollChrome {
 public static void Install(ResourceDictionary resources){var dictionary=(ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='ScrollBar'>
  <Setter Property='Width' Value='6'/><Setter Property='MinWidth' Value='0'/><Setter Property='Background' Value='Transparent'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True'>
   <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>
   <Track.Thumb><Thumb MinHeight='24'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='{DynamicResource ThemeTrack}' CornerRadius='3' Margin='1,0'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
   <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>
  </Track></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style x:Key='RiceScrollViewer' TargetType='ScrollViewer'>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollViewer'><Grid>
   <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='6'/></Grid.ColumnDefinitions>
   <ScrollContentPresenter x:Name='PART_ScrollContentPresenter' Grid.Column='0' Margin='{TemplateBinding Padding}' Content='{TemplateBinding Content}' ContentTemplate='{TemplateBinding ContentTemplate}' CanContentScroll='{TemplateBinding CanContentScroll}'/>
   <ScrollBar x:Name='PART_VerticalScrollBar' Grid.Column='1' Width='6' MinWidth='0' MaxWidth='6' Orientation='Vertical' Minimum='0' Maximum='{TemplateBinding ScrollableHeight}' ViewportSize='{TemplateBinding ViewportHeight}' Value='{Binding VerticalOffset, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}' Visibility='{TemplateBinding ComputedVerticalScrollBarVisibility}'/>
  </Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
</ResourceDictionary>");resources.MergedDictionaries.Add(dictionary);}
}
