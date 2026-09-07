using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KanTab.Views;

public partial class StartupView : UserControl
{
    public StartupView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            var block = this.FindControl<StackPanel>("BrandBlock");
            if (block != null)
            {
                block.Opacity = 1;
                block.RenderTransform = new TranslateTransform(0, 0);
            }
        };
    }
}
