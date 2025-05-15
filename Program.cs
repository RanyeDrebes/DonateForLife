using Avalonia;
using Avalonia.ReactiveUI;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace DonateForLife;

sealed class Program
{
    // Add a service provider field to store the application's services
    public static ServiceProvider ServiceProvider { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        try
        {
            // Configure services
            ServiceProvider = ConfigureServices();

            // Initialize the database
            await AppConfiguration.InitializeDatabaseAsync(ServiceProvider);

            // Start the Avalonia application
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Application startup error: {ex}");
            throw;
        }
        finally
        {
            // Dispose of the service provider on application exit
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static ServiceProvider ConfigureServices()
    {
        // Build configuration
        var configuration = AppConfiguration.BuildConfiguration();

        // Configure services
        var services = new ServiceCollection();
        AppConfiguration.ConfigureServices(services, configuration);

        // Build service provider
        return services.BuildServiceProvider();
    }
}