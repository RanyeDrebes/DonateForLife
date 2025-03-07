using System;
using System.Collections.ObjectModel;
using DonateForLife.Models;
using DonateForLife.Services;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Linq;

namespace DonateForLife.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private int _totalDonors;
        private int _totalRecipients;
        private int _availableOrgans;
        private int _completeTransplantations;
        private int _pendingMatches;
        private ObservableCollection<ActivityLog> _recentActivity = new ObservableCollection<ActivityLog>();

        public DashboardViewModel()
        {
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
            var dataService = DataService.Instance;

            TotalDonors = dataService.TotalDonors;
            TotalRecipients = dataService.TotalRecipients;
            AvailableOrgans = dataService.AvailableOrgans;
            CompleteTransplantations = dataService.CompleteTransplantations;
            PendingMatches = dataService.PendingMatches;

            // Update recent activity
            var activity = dataService.GetRecentActivity();
            RecentActivity.Clear();
            foreach (var log in activity)
            {
                RecentActivity.Add(log);
            }
        }

        // We'll implement simpler approach without converters for now
    }
}