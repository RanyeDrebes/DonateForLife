
using System;
using Microsoft.Extensions.DependencyInjection;

namespace DonateForLife.Services
{
    public static class ViewModelLocator
    {
        /// <summary>
        /// Gets a service of type T from the DI container
        /// </summary>
        public static T GetService<T>() where T : class
        {
            if (Program.ServiceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider is not initialized");
            }

            return Program.ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Creates a view model of type T using dependency injection
        /// </summary>
        public static T GetViewModel<T>() where T : class
        {
            if (Program.ServiceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider is not initialized");
            }

            return Program.ServiceProvider.GetRequiredService<T>();
        }
    }
}