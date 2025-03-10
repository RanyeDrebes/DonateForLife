using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DonateForLife.Views;

public partial class RecipientsView : UserControl
{
    public RecipientsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}