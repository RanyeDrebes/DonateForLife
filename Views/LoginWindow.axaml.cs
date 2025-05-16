using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DonateForLife.ViewModels;
using System;

namespace DonateForLife.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            // Subscribe to the login successful event
            if (DataContext is LoginViewModel loginViewModel)
            {
                loginViewModel.LoginSuccessful += OnLoginSuccessful;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnLoginSuccessful(object? sender, EventArgs e)
        {
            // Close the login window when login is successful
            Close(true);
        }
    }
}