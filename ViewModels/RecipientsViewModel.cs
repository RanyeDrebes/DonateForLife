using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using DonateForLife.Models;
using DonateForLife.Services;
using ReactiveUI;

namespace DonateForLife.ViewModels
{
    public class RecipientsViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private ObservableCollection<Recipient> _recipients;
        private string _searchQuery = "";
        private bool _noRecipientsFound;
        private int _totalRecipients;
        private int _waitingRecipients;
        private int _matchedRecipients;
        private int _totalOrganRequests;
        private string _selectedCountry;
        private string _selectedSortOption;
        private ObservableCollection<string> _countryList;
        private ObservableCollection<string> _sortOptions;
        private string _paginationInfo = "Showing 1-10 of 10 recipients";
        private bool _canGoToPreviousPage;
        private bool _canGoToNextPage;

        // Filter properties
        private bool _filterAPlus;
        private bool _filterAMinus;
        private bool _filterBPlus;
        private bool _filterBMinus;
        private bool _filterABPlus;
        private bool _filterABMinus;
        private bool _filterOPlus;
        private bool _filterOMinus;
        private bool _filterStatusWaiting = true;
        private bool _filterStatusMatched;
        private bool _filterStatusTransplanted;
        private int _minUrgencyScore = 0;
        private int _maxUrgencyScore = 10;

        public RecipientsViewModel(DataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));

            // Initialize collections
            Recipients = new ObservableCollection<Recipient>(_dataService.GetAllRecipients());
            CountryList = new ObservableCollection<string>(
                _dataService.GetAllRecipients()
                    .Select(r => r.Country)
                    .Distinct()
                    .OrderBy(c => c)
            );
            SortOptions = new ObservableCollection<string>
            {
                "Name (A-Z)",
                "Name (Z-A)",
                "Blood Type",
                "Status",
                "Urgency (Highest First)",
                "Urgency (Lowest First)",
                "Waiting Time (Longest First)",
                "Waiting Time (Shortest First)"
            };
            SelectedSortOption = SortOptions[0];

            // Update stats
            UpdateStats();

            // Commands
            AddRecipientCommand = ReactiveCommand.Create(AddRecipient);
            RefreshCommand = ReactiveCommand.Create(RefreshData);
            ViewRecipientDetailsCommand = ReactiveCommand.Create<Recipient>(ViewRecipientDetails);
            EditRecipientCommand = ReactiveCommand.Create<Recipient>(EditRecipient);
            ApplyFiltersCommand = ReactiveCommand.Create(ApplyFilters);
            ResetFiltersCommand = ReactiveCommand.Create(ResetFilters);
            ExportDataCommand = ReactiveCommand.Create(ExportData);
            PreviousPageCommand = ReactiveCommand.Create(GotoPreviousPage);
            NextPageCommand = ReactiveCommand.Create(GotoNextPage);

            // Set initial filter state
            ResetFilters();
        }

        #region Properties

        public ObservableCollection<Recipient> Recipients
        {
            get => _recipients;
            set => this.RaiseAndSetIfChanged(ref _recipients, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                this.RaiseAndSetIfChanged(ref _searchQuery, value);
                ApplyFilters();
            }
        }

        public bool NoRecipientsFound
        {
            get => _noRecipientsFound;
            set => this.RaiseAndSetIfChanged(ref _noRecipientsFound, value);
        }

        public int TotalRecipients
        {
            get => _totalRecipients;
            set => this.RaiseAndSetIfChanged(ref _totalRecipients, value);
        }

        public int WaitingRecipients
        {
            get => _waitingRecipients;
            set => this.RaiseAndSetIfChanged(ref _waitingRecipients, value);
        }

        public int MatchedRecipients
        {
            get => _matchedRecipients;
            set => this.RaiseAndSetIfChanged(ref _matchedRecipients, value);
        }

        public int TotalOrganRequests
        {
            get => _totalOrganRequests;
            set => this.RaiseAndSetIfChanged(ref _totalOrganRequests, value);
        }

        public ObservableCollection<string> CountryList
        {
            get => _countryList;
            set => this.RaiseAndSetIfChanged(ref _countryList, value);
        }

        public string SelectedCountry
        {
            get => _selectedCountry;
            set => this.RaiseAndSetIfChanged(ref _selectedCountry, value);
        }

        public ObservableCollection<string> SortOptions
        {
            get => _sortOptions;
            set => this.RaiseAndSetIfChanged(ref _sortOptions, value);
        }

        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSortOption, value);
                ApplyFilters();
            }
        }

        public string PaginationInfo
        {
            get => _paginationInfo;
            set => this.RaiseAndSetIfChanged(ref _paginationInfo, value);
        }

        public bool CanGoToPreviousPage
        {
            get => _canGoToPreviousPage;
            set => this.RaiseAndSetIfChanged(ref _canGoToPreviousPage, value);
        }

        public bool CanGoToNextPage
        {
            get => _canGoToNextPage;
            set => this.RaiseAndSetIfChanged(ref _canGoToNextPage, value);
        }

        // Filter properties
        public bool FilterAPlus
        {
            get => _filterAPlus;
            set => this.RaiseAndSetIfChanged(ref _filterAPlus, value);
        }

        public bool FilterAMinus
        {
            get => _filterAMinus;
            set => this.RaiseAndSetIfChanged(ref _filterAMinus, value);
        }

        public bool FilterBPlus
        {
            get => _filterBPlus;
            set => this.RaiseAndSetIfChanged(ref _filterBPlus, value);
        }

        public bool FilterBMinus
        {
            get => _filterBMinus;
            set => this.RaiseAndSetIfChanged(ref _filterBMinus, value);
        }

        public bool FilterABPlus
        {
            get => _filterABPlus;
            set => this.RaiseAndSetIfChanged(ref _filterABPlus, value);
        }

        public bool FilterABMinus
        {
            get => _filterABMinus;
            set => this.RaiseAndSetIfChanged(ref _filterABMinus, value);
        }

        public bool FilterOPlus
        {
            get => _filterOPlus;
            set => this.RaiseAndSetIfChanged(ref _filterOPlus, value);
        }

        public bool FilterOMinus
        {
            get => _filterOMinus;
            set => this.RaiseAndSetIfChanged(ref _filterOMinus, value);
        }

        public bool FilterStatusWaiting
        {
            get => _filterStatusWaiting;
            set => this.RaiseAndSetIfChanged(ref _filterStatusWaiting, value);
        }

        public bool FilterStatusMatched
        {
            get => _filterStatusMatched;
            set => this.RaiseAndSetIfChanged(ref _filterStatusMatched, value);
        }

        public bool FilterStatusTransplanted
        {
            get => _filterStatusTransplanted;
            set => this.RaiseAndSetIfChanged(ref _filterStatusTransplanted, value);
        }

        public int MinUrgencyScore
        {
            get => _minUrgencyScore;
            set => this.RaiseAndSetIfChanged(ref _minUrgencyScore, value);
        }

        public int MaxUrgencyScore
        {
            get => _maxUrgencyScore;
            set => this.RaiseAndSetIfChanged(ref _maxUrgencyScore, value);
        }

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> AddRecipientCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Recipient, Unit> ViewRecipientDetailsCommand { get; }
        public ReactiveCommand<Recipient, Unit> EditRecipientCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportDataCommand { get; }
        public ReactiveCommand<Unit, Unit> PreviousPageCommand { get; }
        public ReactiveCommand<Unit, Unit> NextPageCommand { get; }

        #endregion

        #region Methods

        private void AddRecipient()
        {
            // In a real implementation, this would open a dialog or navigate to a form
            // For the prototype, we'll just log this action
            Console.WriteLine("Add recipient action triggered");
        }

        private void RefreshData()
        {
            // Reload data from service
            Recipients.Clear();
            foreach (var recipient in _dataService.GetAllRecipients())
            {
                Recipients.Add(recipient);
            }

            UpdateStats();
            ApplyFilters();
        }

        private void ViewRecipientDetails(Recipient recipient)
        {
            // In a real implementation, this would navigate to a details view
            Console.WriteLine($"View details for recipient: {recipient.FullName}");
        }

        private void EditRecipient(Recipient recipient)
        {
            // In a real implementation, this would open an edit form
            Console.WriteLine($"Edit recipient: {recipient.FullName}");
        }

        private void ApplyFilters()
        {
            var allRecipients = _dataService.GetAllRecipients();

            // Apply search query filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string query = SearchQuery.ToLowerInvariant();
                allRecipients = allRecipients.Where(r =>
                    r.FirstName.ToLowerInvariant().Contains(query) ||
                    r.LastName.ToLowerInvariant().Contains(query) ||
                    r.Id.ToLowerInvariant().Contains(query) ||
                    r.BloodType.ToLowerInvariant().Contains(query) ||
                    r.Hospital.ToLowerInvariant().Contains(query)
                ).ToList();
            }

            // Apply blood type filters
            if (FilterAPlus || FilterAMinus || FilterBPlus || FilterBMinus ||
                FilterABPlus || FilterABMinus || FilterOPlus || FilterOMinus)
            {
                var bloodTypes = new List<string>();
                if (FilterAPlus) bloodTypes.Add("A+");
                if (FilterAMinus) bloodTypes.Add("A-");
                if (FilterBPlus) bloodTypes.Add("B+");
                if (FilterBMinus) bloodTypes.Add("B-");
                if (FilterABPlus) bloodTypes.Add("AB+");
                if (FilterABMinus) bloodTypes.Add("AB-");
                if (FilterOPlus) bloodTypes.Add("O+");
                if (FilterOMinus) bloodTypes.Add("O-");

                allRecipients = allRecipients.Where(r => bloodTypes.Contains(r.BloodType)).ToList();
            }

            // Apply status filters
            if (FilterStatusWaiting || FilterStatusMatched || FilterStatusTransplanted)
            {
                var statuses = new List<RecipientStatus>();
                if (FilterStatusWaiting) statuses.Add(RecipientStatus.Waiting);
                if (FilterStatusMatched) statuses.Add(RecipientStatus.Matched);
                if (FilterStatusTransplanted) statuses.Add(RecipientStatus.Transplanted);

                allRecipients = allRecipients.Where(r => statuses.Contains(r.Status)).ToList();
            }

            // Apply urgency score filter
            allRecipients = allRecipients.Where(r =>
                r.UrgencyScore >= MinUrgencyScore && r.UrgencyScore <= MaxUrgencyScore
            ).ToList();

            // Apply country filter
            if (!string.IsNullOrEmpty(SelectedCountry))
            {
                allRecipients = allRecipients.Where(r => r.Country == SelectedCountry).ToList();
            }

            // Apply sorting
            switch (SelectedSortOption)
            {
                case "Name (A-Z)":
                    allRecipients = allRecipients.OrderBy(r => r.LastName).ThenBy(r => r.FirstName).ToList();
                    break;
                case "Name (Z-A)":
                    allRecipients = allRecipients.OrderByDescending(r => r.LastName).ThenByDescending(r => r.FirstName).ToList();
                    break;
                case "Blood Type":
                    allRecipients = allRecipients.OrderBy(r => r.BloodType).ToList();
                    break;
                case "Status":
                    allRecipients = allRecipients.OrderBy(r => r.Status).ToList();
                    break;
                case "Urgency (Highest First)":
                    allRecipients = allRecipients.OrderByDescending(r => r.UrgencyScore).ToList();
                    break;
                case "Urgency (Lowest First)":
                    allRecipients = allRecipients.OrderBy(r => r.UrgencyScore).ToList();
                    break;
                case "Waiting Time (Longest First)":
                    allRecipients = allRecipients.OrderByDescending(r => r.WaitingDays).ToList();
                    break;
                case "Waiting Time (Shortest First)":
                    allRecipients = allRecipients.OrderBy(r => r.WaitingDays).ToList();
                    break;
            }

            // Update the collection
            Recipients.Clear();
            foreach (var recipient in allRecipients)
            {
                Recipients.Add(recipient);
            }

            // Update no results status
            NoRecipientsFound = !Recipients.Any();

            // Update pagination
            UpdatePagination();
        }

        private void ResetFilters()
        {
            SearchQuery = "";
            FilterAPlus = false;
            FilterAMinus = false;
            FilterBPlus = false;
            FilterBMinus = false;
            FilterABPlus = false;
            FilterABMinus = false;
            FilterOPlus = false;
            FilterOMinus = false;
            FilterStatusWaiting = true;
            FilterStatusMatched = false;
            FilterStatusTransplanted = false;
            SelectedCountry = null;
            SelectedSortOption = "Name (A-Z)";
            MinUrgencyScore = 0;
            MaxUrgencyScore = 10;

            // Reapply filters (will use the reset values)
            ApplyFilters();
        }

        private void ExportData()
        {
            // In a real implementation, this would generate a CSV or PDF export
            Console.WriteLine("Export data triggered");
        }

        private void GotoPreviousPage()
        {
            // In a real implementation, this would handle pagination
            Console.WriteLine("Previous page triggered");
        }

        private void GotoNextPage()
        {
            // In a real implementation, this would handle pagination
            Console.WriteLine("Next page triggered");
        }

        private void UpdateStats()
        {
            var allRecipients = _dataService.GetAllRecipients();

            TotalRecipients = allRecipients.Count;
            WaitingRecipients = allRecipients.Count(r => r.Status == RecipientStatus.Waiting);
            MatchedRecipients = allRecipients.Count(r => r.Status == RecipientStatus.Matched);

            // Count all organ requests
            TotalOrganRequests = allRecipients.Sum(r => r.OrganRequests.Count);
        }

        private void UpdatePagination()
        {
            int total = Recipients.Count;
            PaginationInfo = $"Showing 1-{total} of {total} recipients";

            // For the prototype, we won't implement actual pagination
            CanGoToPreviousPage = false;
            CanGoToNextPage = false;
        }

        #endregion
    }
}