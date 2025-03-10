using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DonateForLife.Views;

public partial class DonorsView : UserControl
{
    public DonorsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}