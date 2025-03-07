using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DonateForLife.Views;

public partial class MatchingView : UserControl
{
    public MatchingView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}