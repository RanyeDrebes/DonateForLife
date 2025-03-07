using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Reactive;
using ReactiveUI;
using DonateForLife.Models;
using DonateForLife.Services;

namespace DonateForLife.ViewModels
{
    public class MatchingViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private Organ? _selectedOrgan;
        private ObservableCollection<Organ> _availableOrgans;
        private ObservableCollection<Match> _potentialMatches;
        private string _matchingStatus;
        private bool _isMatchingInProgress;
        private bool _hasSelectedOrgan;
        private ObservableCollection<string> _algorithmVersions;
        private string _selectedAlgorithmVersion;

        // Algorithm weights
        private double _bloodTypeWeight = 35;
        private double _hlaWeight = 30;
        private double _ageWeight = 10;
        private double _waitingTimeWeight = 15;
        private double _urgencyWeight = 10;

        public MatchingViewModel()
        {
            _dataService = DataService.Instance;

            // Initialize collections
            AvailableOrgans = new ObservableCollection<Organ>(_dataService.GetAllOrgans().Where(o => o.Status == OrganStatus.Available));
            PotentialMatches = new ObservableCollection<Match>();
            AlgorithmVersions = new ObservableCollection<string>
            {
                "Standard v1.0",
                "Enhanced v1.2",
                "Research v2.0 (Beta)"
            };
            SelectedAlgorithmVersion = AlgorithmVersions[0];

            // Initial status
            MatchingStatus = "Ready to run matching algorithm";
            HasSelectedOrgan = false;

            // Initialize commands
            RunMatchingCommand = ReactiveCommand.CreateFromTask(RunMatchingAsync);
            RefreshCommand = ReactiveCommand.Create(RefreshData);
            ViewMatchDetailsCommand = ReactiveCommand.Create<Match>(ViewMatchDetails);
            ApproveMatchCommand = ReactiveCommand.Create<Match>(ApproveMatch);
            SaveAlgorithmConfigCommand = ReactiveCommand.Create(SaveAlgorithmConfig);
        }

        #region Properties

        public ObservableCollection<Organ> AvailableOrgans
        {
            get => _availableOrgans;
            set => this.RaiseAndSetIfChanged(ref _availableOrgans, value);
        }

        public Organ? SelectedOrgan
        {
            get => _selectedOrgan;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedOrgan, value);
                HasSelectedOrgan = value != null;
                if (value != null)
                {
                    LoadPotentialMatches(value.Id);
                }
                else
                {
                    PotentialMatches.Clear();
                }
            }
        }

        public ObservableCollection<Match> PotentialMatches
        {
            get => _potentialMatches;
            set => this.RaiseAndSetIfChanged(ref _potentialMatches, value);
        }

        public string MatchingStatus
        {
            get => _matchingStatus;
            set => this.RaiseAndSetIfChanged(ref _matchingStatus, value);
        }

        public bool IsMatchingInProgress
        {
            get => _isMatchingInProgress;
            set => this.RaiseAndSetIfChanged(ref _isMatchingInProgress, value);
        }

        public bool HasSelectedOrgan
        {
            get => _hasSelectedOrgan;
            set => this.RaiseAndSetIfChanged(ref _hasSelectedOrgan, value);
        }

        public ObservableCollection<string> AlgorithmVersions
        {
            get => _algorithmVersions;
            set => this.RaiseAndSetIfChanged(ref _algorithmVersions, value);
        }

        public string SelectedAlgorithmVersion
        {
            get => _selectedAlgorithmVersion;
            set => this.RaiseAndSetIfChanged(ref _selectedAlgorithmVersion, value);
        }

        // Algorithm weights
        public double BloodTypeWeight
        {
            get => _bloodTypeWeight;
            set => this.RaiseAndSetIfChanged(ref _bloodTypeWeight, value);
        }

        public double HlaWeight
        {
            get => _hlaWeight;
            set => this.RaiseAndSetIfChanged(ref _hlaWeight, value);
        }

        public double AgeWeight
        {
            get => _ageWeight;
            set => this.RaiseAndSetIfChanged(ref _ageWeight, value);
        }

        public double WaitingTimeWeight
        {
            get => _waitingTimeWeight;
            set => this.RaiseAndSetIfChanged(ref _waitingTimeWeight, value);
        }

        public double UrgencyWeight
        {
            get => _urgencyWeight;
            set => this.RaiseAndSetIfChanged(ref _urgencyWeight, value);
        }

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> RunMatchingCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Match, Unit> ViewMatchDetailsCommand { get; }
        public ReactiveCommand<Match, Unit> ApproveMatchCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveAlgorithmConfigCommand { get; }

        #endregion

        #region Methods

        private async Task RunMatchingAsync()
        {
            if (SelectedOrgan == null)
            {
                MatchingStatus = "Please select an organ first";
                return;
            }

            IsMatchingInProgress = true;
            MatchingStatus = "Running matching algorithm...";
            PotentialMatches.Clear();

            try
            {
                // Simulate processing delay
                await Task.Delay(1000);

                // Run the matching algorithm
                var matches = await _dataService.FindMatchesForOrgan(SelectedOrgan.Id);

                foreach (var match in matches)
                {
                    PotentialMatches.Add(match);
                }

                MatchingStatus = $"Found {matches.Count} potential matches";
            }
            catch (Exception ex)
            {
                MatchingStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsMatchingInProgress = false;
            }
        }

        private void RefreshData()
        {
            AvailableOrgans.Clear();
            foreach (var organ in _dataService.GetAllOrgans().Where(o => o.Status == OrganStatus.Available))
            {
                AvailableOrgans.Add(organ);
            }

            if (SelectedOrgan != null)
            {
                // Check if the selected organ still exists and is available
                var organ = _dataService.GetOrganById(SelectedOrgan.Id);
                if (organ != null && organ.Status == OrganStatus.Available)
                {
                    LoadPotentialMatches(organ.Id);
                }
                else
                {
                    SelectedOrgan = null;
                    PotentialMatches.Clear();
                }
            }

            MatchingStatus = "Data refreshed";
        }

        private void LoadPotentialMatches(string organId)
        {
            PotentialMatches.Clear();
            MatchingStatus = "Select an organ and run matching algorithm";
        }

        private void ViewMatchDetails(Match match)
        {
            // In a real app, this would open a detail dialog or navigate to a details page
            MatchingStatus = $"Viewing details for match with {match.Recipient?.FullName}";
        }

        private void ApproveMatch(Match match)
        {
            // Update match status
            match.Status = MatchStatus.Approved;
            match.ApprovalDate = DateTime.Now;
            match.ApprovedBy = "Dr. Max Mustermann"; // Would come from authentication in a real app

            // Update in data service
            _dataService.UpdateMatch(match);

            // Update UI
            MatchingStatus = $"Match approved for {match.Recipient?.FullName}";

            // In a real app, this would trigger notifications and next steps
        }

        private void SaveAlgorithmConfig()
        {
            // Normalize weights to sum to 100
            double total = BloodTypeWeight + HlaWeight + AgeWeight + WaitingTimeWeight + UrgencyWeight;

            if (total == 0)
            {
                MatchingStatus = "Error: Total weights cannot be zero";
                return;
            }

            BloodTypeWeight = Math.Round(BloodTypeWeight / total * 100);
            HlaWeight = Math.Round(HlaWeight / total * 100);
            AgeWeight = Math.Round(AgeWeight / total * 100);
            WaitingTimeWeight = Math.Round(WaitingTimeWeight / total * 100);
            UrgencyWeight = Math.Round(UrgencyWeight / total * 100);

            // Adjust to ensure they sum to 100 exactly
            double adjustedTotal = BloodTypeWeight + HlaWeight + AgeWeight + WaitingTimeWeight + UrgencyWeight;
            if (adjustedTotal != 100)
            {
                // Add/subtract the difference to/from the largest weight
                double diff = 100 - adjustedTotal;
                double maxWeight = Math.Max(Math.Max(Math.Max(Math.Max(BloodTypeWeight, HlaWeight), AgeWeight), WaitingTimeWeight), UrgencyWeight);

                if (maxWeight == BloodTypeWeight) BloodTypeWeight += diff;
                else if (maxWeight == HlaWeight) HlaWeight += diff;
                else if (maxWeight == AgeWeight) AgeWeight += diff;
                else if (maxWeight == WaitingTimeWeight) WaitingTimeWeight += diff;
                else UrgencyWeight += diff;
            }

            MatchingStatus = "Algorithm configuration saved";

            // In a real app, this would save to configuration storage
        }

        #endregion
    }
}