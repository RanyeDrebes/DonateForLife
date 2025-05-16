using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DonateForLife.Services;
using DonateForLife.ViewModels;
using DonateForLife.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DonateForLife;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                // Get the service provider
                var serviceProvider = Program.ServiceProvider;
                if (serviceProvider == null)
                {
                    throw new InvalidOperationException("Service provider is not initialized");
                }

                // Get the authentication service
                var authService = serviceProvider.GetRequiredService<AuthenticationService>();

                // Create login view model directly (not from DI to avoid circular dependency)
                var loginViewModel = new LoginViewModel(authService);

                // Create the login window
                var loginWindow = new LoginWindow { DataContext = loginViewModel };

                // Set the main window to the login window initially
                desktop.MainWindow = loginWindow;

                // When login is successful, show the main application
                loginViewModel.LoginSuccessful += (sender, args) =>
                {
                    ShowMainWindow(desktop, serviceProvider);
                };
            }
            catch (Exception ex)
            {
                // In a real app, you would log this error
                Console.WriteLine($"Initialization error: {ex}");
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider serviceProvider)
    {
        try
        {
            // Get the data service
            var dataService = serviceProvider.GetRequiredService<DataService>();

            // Refresh data from the database
            dataService.RefreshDataAsync().Wait();

            // Create the main window view model with the dataService
            var mainWindowViewModel = new MainWindowViewModel(dataService);

            // Create and show the main window
            var mainWindow = new MainWindow { DataContext = mainWindowViewModel };

            // Store reference to the login window
            var loginWindow = desktop.MainWindow as LoginWindow;

            // Set the main window and show it
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            // Close the login window
            loginWindow?.Close();
        }
        catch (Exception ex)
        {
            // Improved error handling - show the actual exception details
            Console.WriteLine($"Error showing main window: {ex}");

            // Display an error message box
            var messageBox = new Window
            {
                Title = "Error",
                Content = new TextBlock { Text = $"Application error: {ex.Message}\n\n{ex.StackTrace}" },
                Width = 600,
                Height = 400
            };
            messageBox.Show();
            throw;
        }
    }
}