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
    public class TransplantationsViewModel : ViewModelBase
    {
        private readonly DataService _dataService;
        private ObservableCollection<Transplantation> _transplantations;
        private Transplantation? _selectedTransplantation;
        private string _searchQuery = "";
        private bool _noTransplantationsFound;
        private int _totalTransplantations;
        private int _scheduledTransplantations;
        private int _completedTransplantations;
        private int _successfulTransplantations;
        private int _cancelledTransplantations;
        private string _selectedHospital;
        private string _selectedSortOption;
        private ObservableCollection<string> _hospitalList;
        private ObservableCollection<string> _sortOptions;
        private string _paginationInfo = "Showing 1-10 of 10 transplantations";
        private bool _canGoToPreviousPage;
        private bool _canGoToNextPage;
        private ObservableCollection<string> _statusList;
        private string _selectedStatusFilter;
        private ObservableCollection<string> _surgeonList;
        private string _selectedSurgeon;
        private DateTime? _startDateFilter;
        private DateTime? _endDateFilter;
        private ObservableCollection<TransplantationOutcome> _selectedTransplantationOutcomes;

        public TransplantationsViewModel()
        {
            _dataService = DataService.Instance;

            // Initialize collections
            Transplantations = new ObservableCollection<Transplantation>(_dataService.GetAllTransplantations());
            SelectedTransplantationOutcomes = new ObservableCollection<TransplantationOutcome>();

            // Populate filter lists
            var allTransplantations = _dataService.GetAllTransplantations();
            HospitalList = new ObservableCollection<string>(
                allTransplantations
                    .Select(t => t.Hospital)
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Distinct()
                    .OrderBy(h => h)
            );

            SurgeonList = new ObservableCollection<string>(
                allTransplantations
                    .Select(t => t.SurgeonName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .OrderBy(s => s)
            );

            StatusList = new ObservableCollection<string>(
                Enum.GetNames(typeof(TransplantationStatus))
            );

            SortOptions = new ObservableCollection<string>
            {
                "Date (Newest First)",
                "Date (Oldest First)",
                "Status",
                "Hospital",
                "Surgeon"
            };
            SelectedSortOption = SortOptions[0];

            // Update stats
            UpdateStats();

            // Commands
            ScheduleTransplantationCommand = ReactiveCommand.Create(ScheduleTransplantation);
            RefreshCommand = ReactiveCommand.Create(RefreshData);
            ViewTransplantationDetailsCommand = ReactiveCommand.Create<Transplantation>(ViewTransplantationDetails);
            UpdateTransplantationStatusCommand = ReactiveCommand.Create<Transplantation>(UpdateTransplantationStatus);
            RecordOutcomeCommand = ReactiveCommand.Create<Transplantation>(RecordOutcome);
            ApplyFiltersCommand = ReactiveCommand.Create(ApplyFilters);
            ResetFiltersCommand = ReactiveCommand.Create(ResetFilters);
            ExportDataCommand = ReactiveCommand.Create(ExportData);
            PreviousPageCommand = ReactiveCommand.Create(GotoPreviousPage);
            NextPageCommand = ReactiveCommand.Create(GotoNextPage);
            PrintReportCommand = ReactiveCommand.Create(PrintReport);
        }

        #region Properties

        public ObservableCollection<Transplantation> Transplantations
        {
            get => _transplantations;
            set => this.RaiseAndSetIfChanged(ref _transplantations, value);
        }

        public Transplantation? SelectedTransplantation
        {
            get => _selectedTransplantation;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTransplantation, value);
                if (value != null)
                {
                    LoadTransplantationOutcomes(value);
                }
                else
                {
                    SelectedTransplantationOutcomes.Clear();
                }
            }
        }

        public ObservableCollection<TransplantationOutcome> SelectedTransplantationOutcomes
        {
            get => _selectedTransplantationOutcomes;
            set => this.RaiseAndSetIfChanged(ref _selectedTransplantationOutcomes, value);
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

        public bool NoTransplantationsFound
        {
            get => _noTransplantationsFound;
            set => this.RaiseAndSetIfChanged(ref _noTransplantationsFound, value);
        }

        public int TotalTransplantations
        {
            get => _totalTransplantations;
            set => this.RaiseAndSetIfChanged(ref _totalTransplantations, value);
        }

        public int ScheduledTransplantations
        {
            get => _scheduledTransplantations;
            set => this.RaiseAndSetIfChanged(ref _scheduledTransplantations, value);
        }

        public int CompletedTransplantations
        {
            get => _completedTransplantations;
            set => this.RaiseAndSetIfChanged(ref _completedTransplantations, value);
        }

        public int SuccessfulTransplantations
        {
            get => _successfulTransplantations;
            set => this.RaiseAndSetIfChanged(ref _successfulTransplantations, value);
        }

        public int CancelledTransplantations
        {
            get => _cancelledTransplantations;
            set => this.RaiseAndSetIfChanged(ref _cancelledTransplantations, value);
        }

        public ObservableCollection<string> HospitalList
        {
            get => _hospitalList;
            set => this.RaiseAndSetIfChanged(ref _hospitalList, value);
        }

        public string SelectedHospital
        {
            get => _selectedHospital;
            set => this.RaiseAndSetIfChanged(ref _selectedHospital, value);
        }

        public ObservableCollection<string> StatusList
        {
            get => _statusList;
            set => this.RaiseAndSetIfChanged(ref _statusList, value);
        }

        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set => this.RaiseAndSetIfChanged(ref _selectedStatusFilter, value);
        }

        public ObservableCollection<string> SurgeonList
        {
            get => _surgeonList;
            set => this.RaiseAndSetIfChanged(ref _surgeonList, value);
        }

        public string SelectedSurgeon
        {
            get => _selectedSurgeon;
            set => this.RaiseAndSetIfChanged(ref _selectedSurgeon, value);
        }

        public DateTime? StartDateFilter
        {
            get => _startDateFilter;
            set => this.RaiseAndSetIfChanged(ref _startDateFilter, value);
        }

        public DateTime? EndDateFilter
        {
            get => _endDateFilter;
            set => this.RaiseAndSetIfChanged(ref _endDateFilter, value);
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

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> ScheduleTransplantationCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Transplantation, Unit> ViewTransplantationDetailsCommand { get; }
        public ReactiveCommand<Transplantation, Unit> UpdateTransplantationStatusCommand { get; }
        public ReactiveCommand<Transplantation, Unit> RecordOutcomeCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetFiltersCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportDataCommand { get; }
        public ReactiveCommand<Unit, Unit> PreviousPageCommand { get; }
        public ReactiveCommand<Unit, Unit> NextPageCommand { get; }
        public ReactiveCommand<Unit, Unit> PrintReportCommand { get; }

        #endregion

        #region Methods

        private void ScheduleTransplantation()
        {
            // In a real implementation, this would open a dialog or navigate to a form
            // For the prototype, we'll just log this action
            Console.WriteLine("Schedule new transplantation action triggered");
        }

        private void RefreshData()
        {
            // Reload data from service
            Transplantations.Clear();
            foreach (var transplantation in _dataService.GetAllTransplantations())
            {
                Transplantations.Add(transplantation);
            }

            UpdateStats();
            ApplyFilters();
        }

        private void ViewTransplantationDetails(Transplantation transplantation)
        {
            // In a real implementation, this would navigate to a details view
            // For now, just set as selected
            SelectedTransplantation = transplantation;
            Console.WriteLine($"View details for transplantation: {transplantation.Id}");
        }

        private void UpdateTransplantationStatus(Transplantation transplantation)
        {
            // In a real implementation, this would open a dialog to select the new status
            // For the prototype, we'll just toggle between common statuses
            switch (transplantation.Status)
            {
                case TransplantationStatus.Scheduled:
                    transplantation.Status = TransplantationStatus.InProgress;
                    transplantation.ActualStartDate = DateTime.Now;
                    break;
                case TransplantationStatus.InProgress:
                    transplantation.Status = TransplantationStatus.Completed;
                    transplantation.ActualEndDate = DateTime.Now;
                    break;
                case TransplantationStatus.Completed:
                    transplantation.Status = TransplantationStatus.Scheduled;
                    transplantation.ActualStartDate = null;
                    transplantation.ActualEndDate = null;
                    break;
                default:
                    transplantation.Status = TransplantationStatus.Scheduled;
                    transplantation.ActualStartDate = null;
                    transplantation.ActualEndDate = null;
                    break;
            }

            // Update in the data service
            _dataService.UpdateTransplantation(transplantation);
            UpdateStats();
        }

        private void RecordOutcome(Transplantation transplantation)
        {
            // In a real implementation, this would open a dialog to record outcome details
            Console.WriteLine($"Record outcome for transplantation: {transplantation.Id}");

            // For the prototype, we'll add a simple "success" outcome
            var outcome = new TransplantationOutcome
            {
                TransplantationId = transplantation.Id,
                Type = OutcomeType.InitialFunction,
                AssessmentDate = DateTime.Now,
                IsPositive = true,
                Notes = "Initial function successful",
                AssessedBy = "Dr. Max Mustermann"
            };

            transplantation.Outcomes.Add(outcome);
            _dataService.UpdateTransplantation(transplantation);

            // Refresh outcomes display if this is the selected transplantation
            if (SelectedTransplantation?.Id == transplantation.Id)
            {
                LoadTransplantationOutcomes(transplantation);
            }
        }

        private void ApplyFilters()
        {
            var allTransplantations = _dataService.GetAllTransplantations();

            // Apply search query filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string query = SearchQuery.ToLowerInvariant();
                allTransplantations = allTransplantations.Where(t =>
                    (t.Donor?.FullName?.ToLowerInvariant().Contains(query) ?? false) ||
                    (t.Recipient?.FullName?.ToLowerInvariant().Contains(query) ?? false) ||
                    t.Hospital.ToLowerInvariant().Contains(query) ||
                    t.SurgeonName.ToLowerInvariant().Contains(query) ||
                    t.Id.ToLowerInvariant().Contains(query)
                ).ToList();
            }

            // Apply hospital filter
            if (!string.IsNullOrEmpty(SelectedHospital))
            {
                allTransplantations = allTransplantations.Where(t => t.Hospital == SelectedHospital).ToList();
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(SelectedStatusFilter))
            {
                if (Enum.TryParse<TransplantationStatus>(SelectedStatusFilter, out var status))
                {
                    allTransplantations = allTransplantations.Where(t => t.Status == status).ToList();
                }
            }

            // Apply surgeon filter
            if (!string.IsNullOrEmpty(SelectedSurgeon))
            {
                allTransplantations = allTransplantations.Where(t => t.SurgeonName == SelectedSurgeon).ToList();
            }

            // Apply date filters
            if (StartDateFilter.HasValue)
            {
                var startDate = StartDateFilter.Value.Date;
                allTransplantations = allTransplantations.Where(t => t.ScheduledDate.Date >= startDate).ToList();
            }

            if (EndDateFilter.HasValue)
            {
                var endDate = EndDateFilter.Value.Date.AddDays(1).AddSeconds(-1); // End of the day
                allTransplantations = allTransplantations.Where(t => t.ScheduledDate.Date <= endDate.Date).ToList();
            }

            // Apply sorting
            switch (SelectedSortOption)
            {
                case "Date (Newest First)":
                    allTransplantations = allTransplantations.OrderByDescending(t => t.ScheduledDate).ToList();
                    break;
                case "Date (Oldest First)":
                    allTransplantations = allTransplantations.OrderBy(t => t.ScheduledDate).ToList();
                    break;
                case "Status":
                    allTransplantations = allTransplantations.OrderBy(t => t.Status).ToList();
                    break;
                case "Hospital":
                    allTransplantations = allTransplantations.OrderBy(t => t.Hospital).ToList();
                    break;
                case "Surgeon":
                    allTransplantations = allTransplantations.OrderBy(t => t.SurgeonName).ToList();
                    break;
            }

            // Update the collection
            Transplantations.Clear();
            foreach (var transplantation in allTransplantations)
            {
                Transplantations.Add(transplantation);
            }

            // Update no results status
            NoTransplantationsFound = !Transplantations.Any();

            // Update pagination
            UpdatePagination();
        }

        private void ResetFilters()
        {
            SearchQuery = "";
            SelectedHospital = null;
            SelectedStatusFilter = null;
            SelectedSurgeon = null;
            StartDateFilter = null;
            EndDateFilter = null;
            SelectedSortOption = "Date (Newest First)";

            // Reapply filters (will use the reset values)
            ApplyFilters();
        }

        private void ExportData()
        {
            // In a real implementation, this would generate a CSV or PDF export
            Console.WriteLine("Export transplantation data triggered");
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

        private void PrintReport()
        {
            // In a real implementation, this would generate a printable report
            Console.WriteLine("Print report triggered");
        }

        private void UpdateStats()
        {
            var allTransplantations = _dataService.GetAllTransplantations();

            TotalTransplantations = allTransplantations.Count;
            ScheduledTransplantations = allTransplantations.Count(t => t.Status == TransplantationStatus.Scheduled);
            CompletedTransplantations = allTransplantations.Count(t => t.Status == TransplantationStatus.Completed);
            SuccessfulTransplantations = allTransplantations.Count(t => t.IsSuccessful);
            CancelledTransplantations = allTransplantations.Count(t => t.Status == TransplantationStatus.Cancelled);
        }

        private void UpdatePagination()
        {
            int total = Transplantations.Count;
            PaginationInfo = $"Showing 1-{total} of {total} transplantations";

            // For the prototype, we won't implement actual pagination
            CanGoToPreviousPage = false;
            CanGoToNextPage = false;
        }

        private void LoadTransplantationOutcomes(Transplantation transplantation)
        {
            SelectedTransplantationOutcomes.Clear();
            foreach (var outcome in transplantation.Outcomes)
            {
                SelectedTransplantationOutcomes.Add(outcome);
            }
        }

        #endregion
    }
}