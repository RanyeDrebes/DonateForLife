using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Linq;
using DonateForLife.Models;
using DonateForLife.Services;
using ReactiveUI;

namespace DonateForLife.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly DataService _dataService;

        private int _totalDonors;
        private int _totalRecipients;
        private int _availableOrgans;
        private int _completeTransplantations;
        private int _pendingMatches;
        private ObservableCollection<ActivityLog> _recentActivity = new ObservableCollection<ActivityLog>();

        public DashboardViewModel(DataService dataService)
        {
            // Get data service from constructor injection
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));

            // Get stats from data service
            UpdateStats();

            // Setup commands
            RefreshCommand = ReactiveCommand.Create(UpdateStats);
        }

        public int TotalDonors
        {
            get => _totalDonors;
            set => this.RaiseAndSetIfChanged(ref _totalDonors, value);
        }

        public int TotalRecipients
        {
            get => _totalRecipients;
            set => this.RaiseAndSetIfChanged(ref _totalRecipients, value);
        }

        public int AvailableOrgans
        {
            get => _availableOrgans;
            set => this.RaiseAndSetIfChanged(ref _availableOrgans, value);
        }

        public int CompleteTransplantations
        {
            get => _completeTransplantations;
            set => this.RaiseAndSetIfChanged(ref _completeTransplantations, value);
        }

        public int PendingMatches
        {
            get => _pendingMatches;
            set => this.RaiseAndSetIfChanged(ref _pendingMatches, value);
        }

        public ObservableCollection<ActivityLog> RecentActivity
        {
            get => _recentActivity;
            set => this.RaiseAndSetIfChanged(ref _recentActivity, value);
        }

        // Commands
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        private void UpdateStats()
        {
            TotalDonors = _dataService.TotalDonors;
            TotalRecipients = _dataService.TotalRecipients;
            AvailableOrgans = _dataService.AvailableOrgans;
            CompleteTransplantations = _dataService.CompleteTransplantations;
            PendingMatches = _dataService.PendingMatches;

            // Update recent activity
            var activity = _dataService.GetRecentActivity(10);
            RecentActivity.Clear();
            foreach (var log in activity)
            {
                RecentActivity.Add(log);
            }
        }
    }
}