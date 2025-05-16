using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DonateForLife.Services;
using DonateForLife.Services.Database;

namespace DonateForLife
{
    public static class AppConfiguration
    {
        // Create configuration
        public static IConfiguration BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }

        // Configure services
        public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add database connection
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddSingleton<PostgresConnectionHelper>(sp => new PostgresConnectionHelper(connectionString));

            // Add repositories
            services.AddSingleton<DonorRepository>();
            services.AddSingleton<RecipientRepository>();
            services.AddSingleton<OrganRepository>();
            services.AddSingleton<MatchRepository>();
            services.AddSingleton<TransplantationRepository>();
            services.AddSingleton<ActivityLogRepository>();
            services.AddSingleton<ConfigurationRepository>();
            services.AddSingleton<UserRepository>();

            // Add services
            services.AddSingleton<DataService>();
            services.AddSingleton<AuthenticationService>(sp =>
            {
                var dbHelper = sp.GetRequiredService<PostgresConnectionHelper>();
                var pepper = configuration["Security:PasswordPepper"];
                return new AuthenticationService(dbHelper, pepper);
            });

            // Add database initializer
            services.AddSingleton<DatabaseInitializer>(sp =>
            {
                var adminConfig = configuration.GetSection("Admin");
                return new DatabaseInitializer(
                    connectionString,
                    adminConfig["Username"],
                    adminConfig["Password"],
                    adminConfig["Email"],
                    adminConfig["FullName"],
                    configuration["Security:PasswordPepper"]);
            });

            return services;
        }
        
        // Initialize database
        public static async System.Threading.Tasks.Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
        {
            var initializer = serviceProvider.GetRequiredService<DatabaseInitializer>();
            var success = await initializer.InitializeDatabaseAsync();

            if (!success)
            {
                throw new ApplicationException("Failed to initialize the database.");
            }
        }
    }
}