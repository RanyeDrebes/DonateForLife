using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DonateForLife.ViewModels;
using System;
using DonateForLife.Views;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace DonateForLife.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Handle logout request
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.LogoutRequested += OnLogoutRequested;
        }
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        // Show login window again
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Create new login window
            var authService = Program.ServiceProvider.GetRequiredService<Services.AuthenticationService>();
            var loginViewModel = new LoginViewModel(authService);
            var loginWindow = new LoginWindow { DataContext = loginViewModel };

            // When login succeeds, close this window and show a new main window
            loginViewModel.LoginSuccessful += (s, args) =>
            {
                // Get the DataService from the service provider
                var dataService = Program.ServiceProvider.GetRequiredService<Services.DataService>();

                // Create the MainWindowViewModel with the DataService
                var mainWindowViewModel = new MainWindowViewModel(dataService);

                var mainWindow = new MainWindow
                {
                    DataContext = mainWindowViewModel
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                loginWindow.Close();
            };

            // Show login window
            desktop.MainWindow = loginWindow;
            loginWindow.Show();
        }

        // Close this window
        Close();
    }
}