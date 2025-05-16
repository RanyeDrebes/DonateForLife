using System;
using System.Threading.Tasks;
using System.Windows.Input;
using DonateForLife.Services;
using ReactiveUI;

namespace DonateForLife.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly AuthenticationService _authService;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoggingIn;

        public LoginViewModel(AuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            // Create commands
            LoginCommand = ReactiveCommand.CreateFromTask(LoginAsync,
                this.WhenAnyValue(
                    x => x.Username,
                    x => x.Password,
                    x => x.IsLoggingIn,
                    (username, password, isLoggingIn) =>
                        !string.IsNullOrWhiteSpace(username) &&
                        !string.IsNullOrWhiteSpace(password) &&
                        !isLoggingIn
                ));

            // Admin Auto-Login for testing (remove in production)
            AdminLoginCommand = ReactiveCommand.CreateFromTask(AdminLoginAsync);
        }

        public string Username
        {
            get => _username;
            set => this.RaiseAndSetIfChanged(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => this.RaiseAndSetIfChanged(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set => this.RaiseAndSetIfChanged(ref _isLoggingIn, value);
        }

        public ICommand LoginCommand { get; }

        // Command for automatic admin login (testing only)
        public ICommand AdminLoginCommand { get; }

        // Event raised when login is successful
        public event EventHandler<EventArgs>? LoginSuccessful;

        private async Task LoginAsync()
        {
            try
            {
                ErrorMessage = string.Empty;
                IsLoggingIn = true;

                bool success = await _authService.AuthenticateAsync(Username, Password);

                if (success)
                {
                    // Raise event for login success
                    LoginSuccessful?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ErrorMessage = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login failed: {ex.Message}";
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        // Admin auto-login for testing only
        private async Task AdminLoginAsync()
        {
            try
            {
                ErrorMessage = string.Empty;
                IsLoggingIn = true;

                // Use the test login method to bypass normal authentication
                bool success = await _authService.TestLoginAsync("admin");

                if (success)
                {
                    // Raise event for login success
                    LoginSuccessful?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ErrorMessage = "Admin test login failed. Check that the 'admin' user exists in the database.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login failed: {ex.Message}";
            }
            finally
            {
                IsLoggingIn = false;
            }
        }
    }
}