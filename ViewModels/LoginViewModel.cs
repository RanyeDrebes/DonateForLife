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
    }
}