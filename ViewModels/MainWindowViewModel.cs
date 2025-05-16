using ReactiveUI;
using System;
using System.Reactive;
using System.Windows.Input;
using DonateForLife.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DonateForLife.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private object _currentPage;
        private string _searchQuery = "";
        private string _systemStatus = "System online. Database connected.";
        private bool _isDashboardSelected = true;
        private bool _isDonorsSelected = false;
        private bool _isRecipientsSelected = false;
        private bool _isMatchingSelected = false;
        private bool _isTransplantationsSelected = false;
        private bool _isSettingsSelected = false;
        private string _currentUserName;
        private string _currentUserRole;

        // Get the AuthService instance
        private readonly AuthenticationService _authService;

        public MainWindowViewModel()
        {
            // Initialize with Dashboard
            CurrentPage = new DashboardViewModel();

            // Get services from the DI container
            _authService = Program.ServiceProvider.GetRequiredService<AuthenticationService>();

            // Get current user info
            CurrentUserName = _authService.CurrentUsername ?? "Unknown User";
            CurrentUserRole = _authService.CurrentUserRole ?? "Unknown Role";

            // Update system status with login info
            SystemStatus = $"Logged in as {CurrentUserName} ({CurrentUserRole}). System online.";

            // Commands
            NavigateToPageCommand = ReactiveCommand.Create<string>(NavigateToPage);
            ShowNotificationsCommand = ReactiveCommand.Create(ShowNotifications);
            ShowUserMenuCommand = ReactiveCommand.Create(ShowUserMenu);
            LogoutCommand = ReactiveCommand.Create(Logout);
        }

        public object CurrentPage
        {
            get => _currentPage;
            set => this.RaiseAndSetIfChanged(ref _currentPage, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
        }

        public string SystemStatus
        {
            get => _systemStatus;
            set => this.RaiseAndSetIfChanged(ref _systemStatus, value);
        }

        public string CurrentUserName
        {
            get => _currentUserName;
            set => this.RaiseAndSetIfChanged(ref _currentUserName, value);
        }

        public string CurrentUserRole
        {
            get => _currentUserRole;
            set => this.RaiseAndSetIfChanged(ref _currentUserRole, value);
        }

        // Navigation selection state
        public bool IsDashboardSelected
        {
            get => _isDashboardSelected;
            set => this.RaiseAndSetIfChanged(ref _isDashboardSelected, value);
        }

        public bool IsDonorsSelected
        {
            get => _isDonorsSelected;
            set => this.RaiseAndSetIfChanged(ref _isDonorsSelected, value);
        }

        public bool IsRecipientsSelected
        {
            get => _isRecipientsSelected;
            set => this.RaiseAndSetIfChanged(ref _isRecipientsSelected, value);
        }

        public bool IsMatchingSelected
        {
            get => _isMatchingSelected;
            set => this.RaiseAndSetIfChanged(ref _isMatchingSelected, value);
        }

        public bool IsTransplantationsSelected
        {
            get => _isTransplantationsSelected;
            set => this.RaiseAndSetIfChanged(ref _isTransplantationsSelected, value);
        }

        public bool IsSettingsSelected
        {
            get => _isSettingsSelected;
            set => this.RaiseAndSetIfChanged(ref _isSettingsSelected, value);
        }

        // Commands
        public ReactiveCommand<string, Unit> NavigateToPageCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowNotificationsCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowUserMenuCommand { get; }
        public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

        // Event for logout
        public event EventHandler LogoutRequested;

        private void NavigateToPage(string pageName)
        {
            // Reset all selections
            IsDashboardSelected = false;
            IsDonorsSelected = false;
            IsRecipientsSelected = false;
            IsMatchingSelected = false;
            IsTransplantationsSelected = false;
            IsSettingsSelected = false;

            // Set the selected page
            switch (pageName)
            {
                case "Dashboard":
                    CurrentPage = new DashboardViewModel();
                    IsDashboardSelected = true;
                    break;
                case "Donors":
                    CurrentPage = new DonorsViewModel();
                    IsDonorsSelected = true;
                    break;
                case "Recipients":
                    CurrentPage = new RecipientsViewModel();
                    IsRecipientsSelected = true;
                    break;
                case "Matching":
                    CurrentPage = new MatchingViewModel();
                    IsMatchingSelected = true;
                    break;
                case "Transplantations":
                    CurrentPage = new TransplantationsViewModel();
                    IsTransplantationsSelected = true;
                    break;
                case "Settings":
                    CurrentPage = new SettingsViewModel();
                    IsSettingsSelected = true;
                    break;
                default:
                    CurrentPage = new DashboardViewModel();
                    IsDashboardSelected = true;
                    break;
            }
        }

        private void ShowNotifications()
        {
            // This would show a notifications flyout in a real app
            SystemStatus = "Notifications checked: " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ShowUserMenu()
        {
            // This would show a user menu flyout in a real app
            SystemStatus = "User menu accessed: " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void Logout()
        {
            // Log the user out
            _authService.Logout();

            // Notify the app that logout was requested
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}