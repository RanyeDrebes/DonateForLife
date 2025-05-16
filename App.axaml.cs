using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
                    try
                    {
                        // Get the data service - log each step
                        Console.WriteLine("Getting DataService...");
                        var dataService = serviceProvider.GetRequiredService<DataService>();
                        Console.WriteLine("DataService obtained successfully");

                        // Create the main window view model
                        Console.WriteLine("Creating MainWindowViewModel...");
                        var mainWindowViewModel = new MainWindowViewModel(dataService);
                        Console.WriteLine("MainWindowViewModel created successfully");

                        // Create the main window
                        Console.WriteLine("Creating MainWindow...");
                        var mainWindow = new MainWindow { DataContext = mainWindowViewModel };
                        Console.WriteLine("MainWindow created successfully");

                        // Set the main window and show it
                        Console.WriteLine("Setting MainWindow as desktop.MainWindow...");
                        desktop.MainWindow = mainWindow;
                        Console.WriteLine("About to show MainWindow...");
                        mainWindow.Show();
                        Console.WriteLine("MainWindow shown successfully");

                        // Close login window
                        Console.WriteLine("Closing login window...");
                        loginWindow.Close();
                        Console.WriteLine("Login complete and main window shown");
                    }
                    catch (Exception ex)
                    {
                        var msgBox = new Window
                        {
                            Title = "Application Error",
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            Width = 500,
                            Height = 300,
                            Content = new TextBlock
                            {
                                Text = $"An error occurred starting the application:\n\n{ex.Message}\n\nSee error_log.txt for details.",
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(20)
                            }
                        };

                        desktop.MainWindow = msgBox;
                        msgBox.Show();
                    }
                };
            }
            catch (Exception ex)
            {
                var errorWindow = new Window
                {
                    Title = "Startup Error",
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Width = 500,
                    Height = 300,
                    Content = new TextBlock
                    {
                        Text = $"Application failed to start:\n\n{ex.Message}",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20)
                    }
                };

                desktop.MainWindow = errorWindow;
                errorWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}