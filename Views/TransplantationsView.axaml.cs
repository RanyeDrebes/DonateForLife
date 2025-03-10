using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DonateForLife.Views;

public partial class TransplantationsView : UserControl
{
    public TransplantationsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}