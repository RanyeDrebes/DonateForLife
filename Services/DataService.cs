using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonateForLife.Models;
using DonateForLife.Services.Database;

namespace DonateForLife.Services
{
    /// <summary>
    /// Service that provides data access for the application, backed by PostgreSQL database
    /// </summary>
    public class DataService
    {
        // Repositories for database access
        private readonly DonorRepository _donorRepository;
        private readonly RecipientRepository _recipientRepository;
        private readonly OrganRepository _organRepository;
        private readonly MatchRepository _matchRepository;
        private readonly TransplantationRepository _transplantationRepository;
        private readonly ActivityLogRepository _activityLogRepository;
        private readonly ConfigurationRepository _configurationRepository;

        // Caches for efficiency
        private List<Donor> _donors;
        private List<Recipient> _recipients;
        private List<Organ> _organs;
        private List<Match> _matches;
        private List<Transplantation> _transplantations;
        private List<ActivityLog> _activityLogs;

        // Constructor that accepts dependencies
        public DataService(
            DonorRepository donorRepository,
            RecipientRepository recipientRepository,
            OrganRepository organRepository,
            MatchRepository matchRepository,
            TransplantationRepository transplantationRepository,
            ActivityLogRepository activityLogRepository,
            ConfigurationRepository configurationRepository)
        {
            _donorRepository = donorRepository ?? throw new ArgumentNullException(nameof(donorRepository));
            _recipientRepository = recipientRepository ?? throw new ArgumentNullException(nameof(recipientRepository));
            _organRepository = organRepository ?? throw new ArgumentNullException(nameof(organRepository));
            _matchRepository = matchRepository ?? throw new ArgumentNullException(nameof(matchRepository));
            _transplantationRepository = transplantationRepository ?? throw new ArgumentNullException(nameof(transplantationRepository));
            _activityLogRepository = activityLogRepository ?? throw new ArgumentNullException(nameof(activityLogRepository));
            _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));

            // Initialize cache
            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                // Load all data from database
                _donors = await _donorRepository.GetAllDonorsAsync();
                _recipients = await _recipientRepository.GetAllRecipientsAsync();
                _organs = await _organRepository.GetAllOrgansAsync();
                _matches = await _matchRepository.GetAllMatchesAsync();
                _transplantations = await _transplantationRepository.GetAllTransplantationsAsync();
                _activityLogs = await _activityLogRepository.GetRecentActivityLogsAsync(100);

                // Establish relationships between entities
                EstablishRelationships();
            }
            catch (Exception ex)
            {
                // Log the error in a real application
                Console.WriteLine($"Error loading data: {ex.Message}");

                // Initialize with empty lists to avoid null reference exceptions
                _donors = [];
                _recipients = [];
                _organs = [];
                _matches = [];
                _transplantations = [];
                _activityLogs = [];
            }
        }

        // Rest of the class remains the same...
        // Keep all other methods as they are

        private void EstablishRelationships()
        {
            // Establish relationships between entities based on their IDs
            foreach (var match in _matches)
            {
                match.Organ = _organs.FirstOrDefault(o => o.Id == match.OrganId);
                match.Donor = _donors.FirstOrDefault(d => d.Id == match.DonorId);
                match.Recipient = _recipients.FirstOrDefault(r => r.Id == match.RecipientId);
            }

            foreach (var transplantation in _transplantations)
            {
                transplantation.Match = _matches.FirstOrDefault(m => m.Id == transplantation.MatchId);
                transplantation.Organ = _organs.FirstOrDefault(o => o.Id == transplantation.OrganId);
                transplantation.Donor = _donors.FirstOrDefault(d => d.Id == transplantation.DonorId);
                transplantation.Recipient = _recipients.FirstOrDefault(r => r.Id == transplantation.RecipientId);
            }

            foreach (var donor in _donors)
            {
                donor.AvailableOrgans = _organs.Where(o => o.DonorId == donor.Id).ToList();
            }
        }

        // Statistics calculated from the loaded data
        public int TotalDonors => _donors?.Count ?? 0;
        public int TotalRecipients => _recipients?.Count ?? 0;
        public int AvailableOrgans => _organs?.Count(o => o.Status == OrganStatus.Available) ?? 0;
        public int CompleteTransplantations => _transplantations?.Count(t => t.Status == TransplantationStatus.Completed) ?? 0;
        public int PendingMatches => _matches?.Count(m => m.Status == MatchStatus.Pending || m.Status == MatchStatus.Notified) ?? 0;

        #region Donor Operations

        public List<Donor> GetAllDonors()
        {
            return _donors?.ToList() ?? new List<Donor>();
        }

        public async Task<List<Donor>> GetAllDonorsAsync()
        {
            return await _donorRepository.GetAllDonorsAsync();
        }

        public Donor GetDonorById(string id)
        {
            return _donors?.FirstOrDefault(d => d.Id == id);
        }

        public async Task<Donor> GetDonorByIdAsync(string id)
        {
            return await _donorRepository.GetDonorByIdAsync(id);
        }

        public async Task AddDonorAsync(Donor donor)
        {
            var id = await _donorRepository.AddDonorAsync(donor);
            donor.Id = id;

            // Add to cache
            _donors?.Add(donor);

            // Log activity
            await LogActivityAsync(ActivityType.NewDonor, $"New donor registered: {donor.FullName}", donor.Id);
        }

        public async Task UpdateDonorAsync(Donor donor)
        {
            await _donorRepository.UpdateDonorAsync(donor);

            // Update cache
            var index = _donors?.FindIndex(d => d.Id == donor.Id) ?? -1;
            if (index >= 0 && _donors != null)
            {
                _donors[index] = donor;
            }

            // Log activity
            await LogActivityAsync(ActivityType.DonorUpdated, $"Donor updated: {donor.FullName}", donor.Id);
        }

        public async Task DeleteDonorAsync(string id)
        {
            var donor = GetDonorById(id);
            if (donor != null)
            {
                await _donorRepository.DeleteDonorAsync(id);

                // Update cache
                _donors?.Remove(donor);

                // Log activity
                await LogActivityAsync(ActivityType.DonorRemoved, $"Donor removed: {donor.FullName}", donor.Id);
            }
        }

        #endregion

        #region Recipient Operations

        public List<Recipient> GetAllRecipients()
        {
            return _recipients?.ToList() ?? new List<Recipient>();
        }

        public async Task<List<Recipient>> GetAllRecipientsAsync()
        {
            return await _recipientRepository.GetAllRecipientsAsync();
        }

        public Recipient GetRecipientById(string id)
        {
            return _recipients?.FirstOrDefault(r => r.Id == id);
        }

        public async Task<Recipient> GetRecipientByIdAsync(string id)
        {
            return await _recipientRepository.GetRecipientByIdAsync(id);
        }

        public async Task AddRecipientAsync(Recipient recipient)
        {
            var id = await _recipientRepository.AddRecipientAsync(recipient);
            recipient.Id = id;

            // Add to cache
            _recipients?.Add(recipient);

            // Log activity
            await LogActivityAsync(ActivityType.NewRecipient, $"New recipient registered: {recipient.FullName}", recipient.Id);
        }

        public async Task UpdateRecipientAsync(Recipient recipient)
        {
            await _recipientRepository.UpdateRecipientAsync(recipient);

            // Update cache
            var index = _recipients?.FindIndex(r => r.Id == recipient.Id) ?? -1;
            if (index >= 0 && _recipients != null)
            {
                _recipients[index] = recipient;
            }

            // Log activity
            await LogActivityAsync(ActivityType.RecipientUpdated, $"Recipient updated: {recipient.FullName}", recipient.Id);
        }

        public async Task DeleteRecipientAsync(string id)
        {
            var recipient = GetRecipientById(id);
            if (recipient != null)
            {
                await _recipientRepository.DeleteRecipientAsync(id);

                // Update cache
                _recipients?.Remove(recipient);

                // Log activity
                await LogActivityAsync(ActivityType.RecipientRemoved, $"Recipient removed: {recipient.FullName}", recipient.Id);
            }
        }

        #endregion

        #region Organ Operations

        public List<Organ> GetAllOrgans()
        {
            return _organs?.ToList() ?? new List<Organ>();
        }

        public async Task<List<Organ>> GetAllOrgansAsync()
        {
            return await _organRepository.GetAllOrgansAsync();
        }

        public Organ GetOrganById(string id)
        {
            return _organs?.FirstOrDefault(o => o.Id == id);
        }

        public async Task<Organ> GetOrganByIdAsync(string id)
        {
            return await _organRepository.GetOrganByIdAsync(id);
        }

        public async Task AddOrganAsync(Organ organ)
        {
            var id = await _organRepository.AddOrganAsync(organ);
            organ.Id = id;

            // Add to cache
            _organs?.Add(organ);

            // Add to donor's available organs if applicable
            var donor = GetDonorById(organ.DonorId);
            if (donor != null)
            {
                donor.AvailableOrgans.Add(organ);
            }

            // Log activity
            await LogActivityAsync(ActivityType.NewOrgan, $"New organ available: {organ.Type} ({organ.BloodType})", organ.Id);
        }

        public async Task UpdateOrganAsync(Organ organ)
        {
            await _organRepository.UpdateOrganAsync(organ);

            // Update cache
            var index = _organs?.FindIndex(o => o.Id == organ.Id) ?? -1;
            if (index >= 0 && _organs != null)
            {
                _organs[index] = organ;
            }

            // Log activity
            await LogActivityAsync(ActivityType.OrganUpdated, $"Organ updated: {organ.Type} ({organ.BloodType})", organ.Id);
        }

        public async Task DeleteOrganAsync(string id)
        {
            var organ = GetOrganById(id);
            if (organ != null)
            {
                await _organRepository.DeleteOrganAsync(id);

                // Update cache
                _organs?.Remove(organ);

                // Remove from donor's available organs if applicable
                var donor = GetDonorById(organ.DonorId);
                if (donor != null)
                {
                    donor.AvailableOrgans.RemoveAll(o => o.Id == organ.Id);
                }

                // Log activity
                await LogActivityAsync(ActivityType.OrganRemoved, $"Organ removed: {organ.Type} ({organ.BloodType})", organ.Id);
            }
        }

        #endregion

        #region Match Operations

        public List<Match> GetAllMatches()
        {
            return _matches?.ToList() ?? new List<Match>();
        }

        public async Task<List<Match>> GetAllMatchesAsync()
        {
            return await _matchRepository.GetAllMatchesAsync();
        }

        public Match GetMatchById(string id)
        {
            return _matches?.FirstOrDefault(m => m.Id == id);
        }

        public async Task<Match> GetMatchByIdAsync(string id)
        {
            return await _matchRepository.GetMatchByIdAsync(id);
        }

        public async Task AddMatchAsync(Match match)
        {
            var id = await _matchRepository.AddMatchAsync(match);
            match.Id = id;

            // Add to cache
            _matches?.Add(match);

            // Set references
            match.Organ = GetOrganById(match.OrganId);
            match.Donor = GetDonorById(match.DonorId);
            match.Recipient = GetRecipientById(match.RecipientId);

            // Log activity
            await LogActivityAsync(ActivityType.MatchFound, $"Match found for {match.Organ?.Type} to {match.Recipient?.FullName}", match.Id);
        }

        public async Task UpdateMatchAsync(Match match)
        {
            var oldMatch = GetMatchById(match.Id);
            var oldStatus = oldMatch?.Status;

            await _matchRepository.UpdateMatchAsync(match);

            // Update cache
            var index = _matches?.FindIndex(m => m.Id == match.Id) ?? -1;
            if (index >= 0 && _matches != null)
            {
                _matches[index] = match;
            }

            // Log status changes
            if (oldStatus != match.Status)
            {
                switch (match.Status)
                {
                    case MatchStatus.Notified:
                        await LogActivityAsync(ActivityType.HospitalNotified, $"Hospital notified about match for {match.Recipient?.FullName}", match.Id);
                        break;
                    case MatchStatus.Reviewing:
                        await LogActivityAsync(ActivityType.MatchReviewing, $"Match for {match.Recipient?.FullName} is under review", match.Id);
                        break;
                    case MatchStatus.Approved:
                        await LogActivityAsync(ActivityType.MatchApproved, $"Match approved by {match.ApprovedBy}", match.Id);
                        break;
                    case MatchStatus.Rejected:
                        await LogActivityAsync(ActivityType.MatchRejected, $"Match for {match.Recipient?.FullName} was rejected", match.Id);
                        break;
                    case MatchStatus.Completed:
                        await LogActivityAsync(ActivityType.MatchCompleted, $"Match for {match.Recipient?.FullName} was completed", match.Id);
                        break;
                    case MatchStatus.Cancelled:
                        await LogActivityAsync(ActivityType.MatchCancelled, $"Match for {match.Recipient?.FullName} was cancelled", match.Id);
                        break;
                }
            }
        }

        public async Task DeleteMatchAsync(string id)
        {
            var match = GetMatchById(id);
            if (match != null)
            {
                await _matchRepository.DeleteMatchAsync(id);

                // Update cache
                _matches?.Remove(match);

                // Log activity
                await LogActivityAsync(ActivityType.MatchRemoved, $"Match removed", match.Id);
            }
        }

        #endregion

        #region Transplantation Operations

        public List<Transplantation> GetAllTransplantations()
        {
            return _transplantations?.ToList() ?? new List<Transplantation>();
        }

        public async Task<List<Transplantation>> GetAllTransplantationsAsync()
        {
            return await _transplantationRepository.GetAllTransplantationsAsync();
        }

        public Transplantation GetTransplantationById(string id)
        {
            return _transplantations?.FirstOrDefault(t => t.Id == id);
        }

        public async Task<Transplantation> GetTransplantationByIdAsync(string id)
        {
            return await _transplantationRepository.GetTransplantationByIdAsync(id);
        }

        public async Task AddTransplantationAsync(Transplantation transplantation)
        {
            var id = await _transplantationRepository.AddTransplantationAsync(transplantation);
            transplantation.Id = id;

            // Add to cache
            _transplantations?.Add(transplantation);

            // Set references
            transplantation.Match = GetMatchById(transplantation.MatchId);
            transplantation.Organ = GetOrganById(transplantation.OrganId);
            transplantation.Donor = GetDonorById(transplantation.DonorId);
            transplantation.Recipient = GetRecipientById(transplantation.RecipientId);

            // Log activity
            await LogActivityAsync(ActivityType.TransplantationScheduled, $"Transplantation scheduled for {transplantation.Recipient?.FullName}", transplantation.Id);
        }

        public async Task UpdateTransplantationAsync(Transplantation transplantation)
        {
            var oldTransplantation = GetTransplantationById(transplantation.Id);
            var oldStatus = oldTransplantation?.Status;

            await _transplantationRepository.UpdateTransplantationAsync(transplantation);

            // Update cache
            var index = _transplantations?.FindIndex(t => t.Id == transplantation.Id) ?? -1;
            if (index >= 0 && _transplantations != null)
            {
                _transplantations[index] = transplantation;
            }

            // Log status changes
            if (oldStatus != transplantation.Status)
            {
                switch (transplantation.Status)
                {
                    case TransplantationStatus.InProgress:
                        await LogActivityAsync(ActivityType.TransplantationStarted, $"Transplantation for {transplantation.Recipient?.FullName} has started", transplantation.Id);
                        break;
                    case TransplantationStatus.Completed:
                        await LogActivityAsync(ActivityType.TransplantationCompleted, $"Transplantation for {transplantation.Recipient?.FullName} completed successfully", transplantation.Id);
                        break;
                    case TransplantationStatus.Cancelled:
                        await LogActivityAsync(ActivityType.TransplantationCancelled, $"Transplantation for {transplantation.Recipient?.FullName} was cancelled", transplantation.Id);
                        break;
                    case TransplantationStatus.Delayed:
                        await LogActivityAsync(ActivityType.TransplantationDelayed, $"Transplantation for {transplantation.Recipient?.FullName} was delayed", transplantation.Id);
                        break;
                    case TransplantationStatus.Failed:
                        await LogActivityAsync(ActivityType.TransplantationFailed, $"Transplantation for {transplantation.Recipient?.FullName} failed", transplantation.Id);
                        break;
                }
            }
        }

        public async Task DeleteTransplantationAsync(string id)
        {
            var transplantation = GetTransplantationById(id);
            if (transplantation != null)
            {
                await _transplantationRepository.DeleteTransplantationAsync(id);

                // Update cache
                _transplantations?.Remove(transplantation);

                // Log activity
                await LogActivityAsync(ActivityType.TransplantationRemoved, $"Transplantation record removed", transplantation.Id);
            }
        }

        #endregion

        #region Activity Log

        public List<ActivityLog> GetRecentActivity(int count = 10)
        {
            return _activityLogs?
                .OrderByDescending(log => log.Timestamp)
                .Take(count)
                .ToList() ?? new List<ActivityLog>();
        }

        public async Task<List<ActivityLog>> GetRecentActivityAsync(int count = 10)
        {
            return await _activityLogRepository.GetRecentActivityLogsAsync(count);
        }

        public async Task LogActivityAsync(ActivityType activityType, string description, string relatedId)
        {
            var log = new ActivityLog
            {
                Timestamp = DateTime.Now,
                ActivityType = activityType,
                Description = description,
                RelatedId = relatedId
            };

            await _activityLogRepository.AddActivityLogAsync(log);

            // Add to cache
            _activityLogs?.Add(log);
        }

        #endregion

        #region Matching Engine

        public async Task<List<Match>> FindMatchesForOrgan(string organId)
        {
            // Simulate async processing
            await Task.Delay(100);

            var organ = GetOrganById(organId);
            if (organ == null) return new List<Match>();

            var potentialRecipients = _recipients?
                .Where(r => r.Status == RecipientStatus.Waiting)
                .Where(r => r.OrganRequests.Any(req => req.OrganType == organ.Type && req.Status == OrganRequestStatus.Waiting))
                .ToList() ?? new List<Recipient>();

            var matches = new List<Match>();

            // Get algorithm weights from configuration
            var config = await _configurationRepository.GetAllConfigurationValuesAsync();
            var bloodTypeWeight = ParseConfigDouble(config, "blood_type_weight", 35);
            var hlaWeight = ParseConfigDouble(config, "hla_weight", 30);
            var ageWeight = ParseConfigDouble(config, "age_weight", 10);
            var waitingTimeWeight = ParseConfigDouble(config, "waiting_time_weight", 15);
            var urgencyWeight = ParseConfigDouble(config, "urgency_weight", 10);

            foreach (var recipient in potentialRecipients)
            {
                // Apply matching function with configured weights
                var compatibilityScore = CalculateCompatibilityScore(organ, recipient, bloodTypeWeight, hlaWeight, ageWeight);

                // Apply ranking function
                var rankingScore = CalculateRankingScore(organ, recipient, compatibilityScore, waitingTimeWeight, urgencyWeight);

                // Create a match if compatible enough
                if (compatibilityScore >= 50) // Minimum threshold
                {
                    var match = new Match
                    {
                        OrganId = organ.Id,
                        DonorId = organ.DonorId,
                        RecipientId = recipient.Id,
                        CompatibilityScore = compatibilityScore,
                        RankingScore = rankingScore,
                        Status = MatchStatus.Pending,
                        MatchingAlgorithmVersion = "1.0",
                        MatchingFactors = GenerateMatchingFactors(organ, recipient, bloodTypeWeight, hlaWeight, ageWeight, waitingTimeWeight, urgencyWeight),
                        Organ = organ,
                        Donor = GetDonorById(organ.DonorId),
                        Recipient = recipient
                    };

                    matches.Add(match);
                }
            }

            // Sort by ranking score (highest first)
            return matches.OrderByDescending(m => m.RankingScore).ToList();
        }

        private double ParseConfigDouble(Dictionary<string, string> config, string key, double defaultValue)
        {
            if (config != null && config.TryGetValue(key, out var value) && double.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        private double CalculateCompatibilityScore(Organ organ, Recipient recipient, double bloodTypeWeight, double hlaWeight, double ageWeight)
        {
            // Adapted simple matching algorithm
            double score = 0;
            double totalWeight = bloodTypeWeight + hlaWeight + ageWeight;

            // Normalize weights
            bloodTypeWeight = bloodTypeWeight / totalWeight * 100;
            hlaWeight = hlaWeight / totalWeight * 100;
            ageWeight = ageWeight / totalWeight * 100;

            // Blood type compatibility (simplified)
            double bloodTypeScore = 0;
            if (organ.BloodType == recipient.BloodType)
            {
                bloodTypeScore = 100; // Perfect match
            }
            else if (organ.BloodType == "O+") // Universal donor for other positive types
            {
                if (recipient.BloodType.EndsWith("+"))
                {
                    bloodTypeScore = 75;
                }
            }
            else if (organ.BloodType == "O-") // Universal donor for all
            {
                bloodTypeScore = 90;
            }
            else
            {
                bloodTypeScore = 25; // Some compatibility, but not ideal
            }

            score += bloodTypeScore * (bloodTypeWeight / 100);

            // HLA matching (simplified)
            var donorHla = organ.HlaType.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var recipientHla = recipient.HlaType.Split(';', StringSplitOptions.RemoveEmptyEntries);

            int matchedHla = 0;
            foreach (var dHla in donorHla)
            {
                if (recipientHla.Any(rHla => rHla.Trim() == dHla.Trim()))
                {
                    matchedHla++;
                }
            }

            // Score based on HLA matches
            double hlaScore = (matchedHla / (double)Math.Max(donorHla.Length, 1)) * 100;
            score += hlaScore * (hlaWeight / 100);

            // Age compatibility (simplified)
            var donor = GetDonorById(organ.DonorId);
            if (donor != null)
            {
                int ageDifference = Math.Abs(donor.Age - recipient.Age);
                double ageScore = 0;

                if (ageDifference <= 5)
                {
                    ageScore = 100;
                }
                else if (ageDifference <= 10)
                {
                    ageScore = 75;
                }
                else if (ageDifference <= 20)
                {
                    ageScore = 50;
                }
                else
                {
                    ageScore = 25;
                }

                score += ageScore * (ageWeight / 100);
            }

            return Math.Min(score, 100); // Cap at 100
        }

        private double CalculateRankingScore(Organ organ, Recipient recipient, double compatibilityScore, double waitingTimeWeight, double urgencyWeight)
        {
            // Ranking function - determines final priority among compatible matches
            double totalWeight = 50 + waitingTimeWeight + urgencyWeight; // 50% for compatibility

            // Normalize weights
            double compatibilityWeightNormalized = 50 / totalWeight * 100;
            double waitingTimeWeightNormalized = waitingTimeWeight / totalWeight * 100;
            double urgencyWeightNormalized = urgencyWeight / totalWeight * 100;

            double score = compatibilityScore * (compatibilityWeightNormalized / 100); // 50% based on compatibility

            // Urgency factor
            score += (recipient.UrgencyScore / 10.0) * (urgencyWeightNormalized / 100);

            // Waiting time factor
            int waitingDays = recipient.WaitingDays;
            double waitingTimeScore = Math.Min(waitingDays / 365.0, 1.0) * 100; // Cap at 1 year
            score += waitingTimeScore * (waitingTimeWeightNormalized / 100);

            return Math.Min(score, 100); // Cap at 100
        }

        private List<MatchFactor> GenerateMatchingFactors(Organ organ, Recipient recipient,
            double bloodTypeWeight, double hlaWeight, double ageWeight,
            double waitingTimeWeight, double urgencyWeight)
        {
            var factors = new List<MatchFactor>();

            // Blood type factor
            double bloodTypeScore = 0;
            string bloodTypeDesc = "";

            if (organ.BloodType == recipient.BloodType)
            {
                bloodTypeScore = 100;
                bloodTypeDesc = "Direct match";
            }
            else if (organ.BloodType == "O+")
            {
                if (recipient.BloodType.EndsWith("+"))
                {
                    bloodTypeScore = 75;
                    bloodTypeDesc = "Universal donor (positive)";
                }
            }
            else if (organ.BloodType == "O-")
            {
                bloodTypeScore = 90;
                bloodTypeDesc = "Universal donor";
            }
            else
            {
                bloodTypeScore = 25;
                bloodTypeDesc = "Partial compatibility";
            }

            factors.Add(new MatchFactor
            {
                FactorName = "Blood Type",
                Weight = bloodTypeWeight / 100,
                Score = bloodTypeScore,
                Description = bloodTypeDesc
            });

            // HLA factor
            var donorHla = organ.HlaType.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var recipientHla = recipient.HlaType.Split(';', StringSplitOptions.RemoveEmptyEntries);

            int matchedHla = 0;
            foreach (var dHla in donorHla)
            {
                if (recipientHla.Any(rHla => rHla.Trim() == dHla.Trim()))
                {
                    matchedHla++;
                }
            }

            double hlaScore = (matchedHla / (double)Math.Max(donorHla.Length, 1)) * 100;

            factors.Add(new MatchFactor
            {
                FactorName = "HLA Compatibility",
                Weight = hlaWeight / 100,
                Score = hlaScore,
                Description = $"{matchedHla}/{donorHla.Length} antigens match"
            });

            // Age factor
            var donor = GetDonorById(organ.DonorId);
            if (donor != null)
            {
                int ageDifference = Math.Abs(donor.Age - recipient.Age);
                double ageScore = 0;

                if (ageDifference <= 5)
                {
                    ageScore = 100;
                }
                else if (ageDifference <= 10)
                {
                    ageScore = 75;
                }
                else if (ageDifference <= 20)
                {
                    ageScore = 50;
                }
                else
                {
                    ageScore = 25;
                }

                factors.Add(new MatchFactor
                {
                    FactorName = "Age Difference",
                    Weight = ageWeight / 100,
                    Score = ageScore,
                    Description = $"{ageDifference} years difference"
                });
            }

            // Waiting time factor
            int waitingDays = recipient.WaitingDays;
            double waitingTimeScore = Math.Min(waitingDays / 365.0, 1.0) * 100; // Cap at 1 year

            factors.Add(new MatchFactor
            {
                FactorName = "Waiting Time",
                Weight = waitingTimeWeight / 100,
                Score = waitingTimeScore,
                Description = $"{waitingDays} days on waiting list"
            });

            // Urgency factor
            factors.Add(new MatchFactor
            {
                FactorName = "Urgency",
                Weight = urgencyWeight / 100,
                Score = recipient.UrgencyScore * 10,
                Description = $"{(recipient.UrgencyScore >= 8 ? "High" : "Medium")} urgency ({recipient.UrgencyScore}/10)"
            });

            return factors;
        }

        #endregion

        #region Configuration Operations

        public async Task<Dictionary<string, string>> GetAllConfigurationValuesAsync()
        {
            return await _configurationRepository.GetAllConfigurationValuesAsync();
        }

        public async Task<string> GetConfigurationValueAsync(string key)
        {
            return await _configurationRepository.GetConfigurationValueAsync(key);
        }

        public async Task SetConfigurationValueAsync(string key, string value, string userId)
        {
            await _configurationRepository.SetConfigurationValueAsync(key, value, userId);
        }

        #endregion

        #region Refresh Data

        public async Task RefreshDataAsync()
        {
            // Reload all data from database
            _donors = await _donorRepository.GetAllDonorsAsync();
            _recipients = await _recipientRepository.GetAllRecipientsAsync();
            _organs = await _organRepository.GetAllOrgansAsync();
            _matches = await _matchRepository.GetAllMatchesAsync();
            _transplantations = await _transplantationRepository.GetAllTransplantationsAsync();
            _activityLogs = await _activityLogRepository.GetRecentActivityLogsAsync(100);

            // Establish relationships between entities
            EstablishRelationships();
        }

        #endregion

        #region Synchronous Wrapper Methods

        // Match synchronous methods
        public void UpdateMatch(Match match)
        {
            // Call the async method and wait for it synchronously
            // Not ideal, but maintains compatibility with existing code
            Task.Run(async () => await UpdateMatchAsync(match)).GetAwaiter().GetResult();
        }

        // Transplantation synchronous methods
        public void UpdateTransplantation(Transplantation transplantation)
        {
            // Call the async method and wait for it synchronously
            Task.Run(async () => await UpdateTransplantationAsync(transplantation)).GetAwaiter().GetResult();
        }

        // Add similar wrappers for other methods as needed
        public void AddDonor(Donor donor)
        {
            Task.Run(async () => await AddDonorAsync(donor)).GetAwaiter().GetResult();
        }

        public void UpdateDonor(Donor donor)
        {
            Task.Run(async () => await UpdateDonorAsync(donor)).GetAwaiter().GetResult();
        }

        public void DeleteDonor(string id)
        {
            Task.Run(async () => await DeleteDonorAsync(id)).GetAwaiter().GetResult();
        }

        public void AddRecipient(Recipient recipient)
        {
            Task.Run(async () => await AddRecipientAsync(recipient)).GetAwaiter().GetResult();
        }

        public void UpdateRecipient(Recipient recipient)
        {
            Task.Run(async () => await UpdateRecipientAsync(recipient)).GetAwaiter().GetResult();
        }

        public void DeleteRecipient(string id)
        {
            Task.Run(async () => await DeleteRecipientAsync(id)).GetAwaiter().GetResult();
        }

        public void AddOrgan(Organ organ)
        {
            Task.Run(async () => await AddOrganAsync(organ)).GetAwaiter().GetResult();
        }

        public void UpdateOrgan(Organ organ)
        {
            Task.Run(async () => await UpdateOrganAsync(organ)).GetAwaiter().GetResult();
        }

        public void DeleteOrgan(string id)
        {
            Task.Run(async () => await DeleteOrganAsync(id)).GetAwaiter().GetResult();
        }

        public void AddMatch(Match match)
        {
            Task.Run(async () => await AddMatchAsync(match)).GetAwaiter().GetResult();
        }

        public void DeleteMatch(string id)
        {
            Task.Run(async () => await DeleteMatchAsync(id)).GetAwaiter().GetResult();
        }

        public void AddTransplantation(Transplantation transplantation)
        {
            Task.Run(async () => await AddTransplantationAsync(transplantation)).GetAwaiter().GetResult();
        }

        public void DeleteTransplantation(string id)
        {
            Task.Run(async () => await DeleteTransplantationAsync(id)).GetAwaiter().GetResult();
        }

        #endregion
    }
}