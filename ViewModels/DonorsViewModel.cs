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
    public class DonorsViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private ObservableCollection<Donor> _donors;
        private string _searchQuery = "";
        private bool _noDonorsFound;
        private int _totalDonors;
        private int _availableDonors;
        private int _totalOrgans;
        private int _availableOrgans;
        private string _selectedCountry;
        private string _selectedSortOption;
        private ObservableCollection<string> _countryList;
        private ObservableCollection<string> _sortOptions;
        private string _paginationInfo = "Showing 1-10 of 10 donors";
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
        private bool _filterStatusAvailable = true;
        private bool _filterStatusInProcess;
        private bool _filterStatusCompleted;

        public DonorsViewModel()
        {
            _dataService = DataService.Instance;

            // Initialize collections
            Donors = new ObservableCollection<Donor>(_dataService.GetAllDonors());
            CountryList = new ObservableCollection<string>(
                _dataService.GetAllDonors()
                    .Select(d => d.Country)
                    .Distinct()
                    .OrderBy(c => c)
            );
            SortOptions = new ObservableCollection<string>
            {
                "Name (A-Z)",
                "Name (Z-A)",
                "Blood Type",
                "Status",
                "Date Registered (Newest)",
                "Date Registered (Oldest)"
            };
            SelectedSortOption = SortOptions[0];

            // Update stats
            UpdateStats();

            // Commands
            AddDonorCommand = ReactiveCommand.Create(AddDonor);
            RefreshCommand = ReactiveCommand.Create(RefreshData);
            ViewDonorDetailsCommand = ReactiveCommand.Create<Donor>(ViewDonorDetails);
            EditDonorCommand = ReactiveCommand.Create<Donor>(EditDonor);
            ApplyFiltersCommand = ReactiveCommand.Create(ApplyFilters);
            ResetFiltersCommand = ReactiveCommand.Create(ResetFilters);
            ExportDataCommand = ReactiveCommand.Create(ExportData);
            PreviousPageCommand = ReactiveCommand.Create(GotoPreviousPage);
            NextPageCommand = ReactiveCommand.Create(GotoNextPage);

            // Set initial filter state
            ResetFilters();
        }

        #region Properties

        public ObservableCollection<Donor> Donors
        {
            get => _donors;
            set => this.RaiseAndSetIfChanged(ref _donors, value);
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

        public bool NoDonorsFound
        {
            get => _noDonorsFound;
            set => this.RaiseAndSetIfChanged(ref _noDonorsFound, value);
        }

        public int TotalDonors
        {
            get => _totalDonors;
            set => this.RaiseAndSetIfChanged(ref _totalDonors, value);
        }

        public int AvailableDonors
        {
            get => _availableDonors;
            set => this.RaiseAndSetIfChanged(ref _availableDonors, value);
        }

        public int TotalOrgans
        {
            get => _totalOrgans;
            set => this.RaiseAndSetIfChanged(ref _totalOrgans, value);
        }

        public int AvailableOrgans
        {
            get => _availableOrgans;
            set => this.RaiseAndSetIfChanged(ref _availableOrgans, value);
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

        public bool FilterStatusAvailable
        {
            get => _filterStatusAvailable;
            set => this.RaiseAndSetIfChanged(ref _filterStatusAvailable, value);
        }

        public bool FilterStatusInProcess
        {
            get => _filterStatusInProcess;
            set => this.RaiseAndSetIfChanged(ref _filterStatusInProcess, value);
        }

        public bool FilterStatusCompleted
        {
            get => _filterStatusCompleted;
            set => this.RaiseAndSetIfChanged(ref _filterStatusCompleted, value);
        }

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> AddDonorCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Donor, Unit> ViewDonorDetailsCommand { get; }
        public ReactiveCommand<Donor, Unit> EditDonorCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportDataCommand { get; }
        public ReactiveCommand<Unit, Unit> PreviousPageCommand { get; }
        public ReactiveCommand<Unit, Unit> NextPageCommand { get; }

        #endregion

        #region Methods

        private void AddDonor()
        {
            // In a real implementation, this would open a dialog or navigate to a form
            // For the prototype, we'll just log this action
            Console.WriteLine("Add donor action triggered");
        }

        private void RefreshData()
        {
            // Reload data from service
            Donors.Clear();
            foreach (var donor in _dataService.GetAllDonors())
            {
                Donors.Add(donor);
            }

            UpdateStats();
            ApplyFilters();
        }

        private void ViewDonorDetails(Donor donor)
        {
            // In a real implementation, this would navigate to a details view
            Console.WriteLine($"View details for donor: {donor.FullName}");
        }

        private void EditDonor(Donor donor)
        {
            // In a real implementation, this would open an edit form
            Console.WriteLine($"Edit donor: {donor.FullName}");
        }

        private void ApplyFilters()
        {
            var allDonors = _dataService.GetAllDonors();

            // Apply search query filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string query = SearchQuery.ToLowerInvariant();
                allDonors = allDonors.Where(d =>
                    d.FirstName.ToLowerInvariant().Contains(query) ||
                    d.LastName.ToLowerInvariant().Contains(query) ||
                    d.Id.ToLowerInvariant().Contains(query) ||
                    d.BloodType.ToLowerInvariant().Contains(query)
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

                allDonors = allDonors.Where(d => bloodTypes.Contains(d.BloodType)).ToList();
            }

            // Apply status filters
            if (FilterStatusAvailable || FilterStatusInProcess || FilterStatusCompleted)
            {
                var statuses = new List<DonorStatus>();
                if (FilterStatusAvailable) statuses.Add(DonorStatus.Available);
                if (FilterStatusInProcess) statuses.Add(DonorStatus.InProcess);
                if (FilterStatusCompleted) statuses.Add(DonorStatus.Completed);

                allDonors = allDonors.Where(d => statuses.Contains(d.Status)).ToList();
            }

            // Apply country filter
            if (!string.IsNullOrEmpty(SelectedCountry))
            {
                allDonors = allDonors.Where(d => d.Country == SelectedCountry).ToList();
            }

            // Apply sorting
            switch (SelectedSortOption)
            {
                case "Name (A-Z)":
                    allDonors = allDonors.OrderBy(d => d.LastName).ThenBy(d => d.FirstName).ToList();
                    break;
                case "Name (Z-A)":
                    allDonors = allDonors.OrderByDescending(d => d.LastName).ThenByDescending(d => d.FirstName).ToList();
                    break;
                case "Blood Type":
                    allDonors = allDonors.OrderBy(d => d.BloodType).ToList();
                    break;
                case "Status":
                    allDonors = allDonors.OrderBy(d => d.Status).ToList();
                    break;
                case "Date Registered (Newest)":
                    allDonors = allDonors.OrderByDescending(d => d.RegisteredDate).ToList();
                    break;
                case "Date Registered (Oldest)":
                    allDonors = allDonors.OrderBy(d => d.RegisteredDate).ToList();
                    break;
            }

            // Update the collection
            Donors.Clear();
            foreach (var donor in allDonors)
            {
                Donors.Add(donor);
            }

            // Update no results status
            NoDonorsFound = !Donors.Any();

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
            FilterStatusAvailable = true;
            FilterStatusInProcess = false;
            FilterStatusCompleted = false;
            SelectedCountry = null;
            SelectedSortOption = "Name (A-Z)";

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
            var allDonors = _dataService.GetAllDonors();

            TotalDonors = allDonors.Count;
            AvailableDonors = allDonors.Count(d => d.Status == DonorStatus.Available);

            // Count all organs
            TotalOrgans = allDonors.Sum(d => d.AvailableOrgans.Count);

            // Count available organs
            AvailableOrgans = _dataService.GetAllOrgans().Count(o => o.Status == OrganStatus.Available);
        }

        private void UpdatePagination()
        {
            int total = Donors.Count;
            PaginationInfo = $"Showing 1-{total} of {total} donors";

            // For the prototype, we won't implement actual pagination
            CanGoToPreviousPage = false;
            CanGoToNextPage = false;
        }

        #endregion
    }
}