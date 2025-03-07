using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonateForLife.Models;

namespace DonateForLife.Services
{
    public class DataService
    {
        private static DataService? _instance;
        public static DataService Instance => _instance ??= new DataService();

        // Mock data storage - would be replaced with database access
        private List<Donor> _donors = new List<Donor>();
        private List<Recipient> _recipients = new List<Recipient>();
        private List<Organ> _organs = new List<Organ>();
        private List<Match> _matches = new List<Match>();
        private List<Transplantation> _transplantations = new List<Transplantation>();

        // Statistics
        public int TotalDonors => _donors.Count;
        public int TotalRecipients => _recipients.Count;
        public int AvailableOrgans => _organs.Count(o => o.Status == OrganStatus.Available);
        public int CompleteTransplantations => _transplantations.Count(t => t.Status == TransplantationStatus.Completed);
        public int PendingMatches => _matches.Count(m => m.Status == MatchStatus.Pending || m.Status == MatchStatus.Notified);

        // Recent activity for dashboard
        private List<ActivityLog> _activityLogs = new List<ActivityLog>();

        private DataService()
        {
            // Initialize with some sample data
            GenerateSampleData();
        }

        private void GenerateSampleData()
        {
            // Create some donors
            var donors = new List<Donor>
            {
                new Donor
                {
                    FirstName = "Thomas",
                    LastName = "Müller",
                    DateOfBirth = new DateTime(1980, 5, 15),
                    BloodType = "O+",
                    HlaType = "A*01,02; B*07,08",
                    Country = "Germany",
                    Hospital = "Charité Berlin",
                    Status = DonorStatus.Available
                },
                new Donor
                {
                    FirstName = "Sophie",
                    LastName = "Dupont",
                    DateOfBirth = new DateTime(1975, 3, 22),
                    BloodType = "A+",
                    HlaType = "A*03,24; B*15,35",
                    Country = "France",
                    Hospital = "Hôpital Saint-Louis",
                    Status = DonorStatus.Available
                },
                new Donor
                {
                    FirstName = "Marco",
                    LastName = "Rossi",
                    DateOfBirth = new DateTime(1968, 11, 7),
                    BloodType = "B+",
                    HlaType = "A*02,11; B*27,44",
                    Country = "Italy",
                    Hospital = "Policlinico Gemelli",
                    Status = DonorStatus.InProcess
                }
            };

            // Add some organs to donors
            donors[0].AvailableOrgans.Add(new Organ(OrganType.Kidney)
            {
                DonorId = donors[0].Id,
                BloodType = donors[0].BloodType,
                HlaType = donors[0].HlaType
            });

            donors[1].AvailableOrgans.Add(new Organ(OrganType.Liver)
            {
                DonorId = donors[1].Id,
                BloodType = donors[1].BloodType,
                HlaType = donors[1].HlaType
            });

            donors[2].AvailableOrgans.Add(new Organ(OrganType.Heart)
            {
                DonorId = donors[2].Id,
                BloodType = donors[2].BloodType,
                HlaType = donors[2].HlaType
            });

            // Add donors to the list
            _donors.AddRange(donors);

            // Extract organs to the organs list
            foreach (var donor in donors)
            {
                _organs.AddRange(donor.AvailableOrgans);
            }

            // Create some recipients
            var recipients = new List<Recipient>
            {
                new Recipient
                {
                    FirstName = "Anna",
                    LastName = "Schmidt",
                    DateOfBirth = new DateTime(1982, 8, 10),
                    BloodType = "O+",
                    HlaType = "A*01,03; B*07,13",
                    Country = "Austria",
                    Hospital = "Allgemeines Krankenhaus Wien",
                    UrgencyScore = 8,
                    WaitingSince = DateTime.Now.AddDays(-180),
                    Status = RecipientStatus.Waiting
                },
                new Recipient
                {
                    FirstName = "Jean",
                    LastName = "Lambert",
                    DateOfBirth = new DateTime(1970, 4, 30),
                    BloodType = "A+",
                    HlaType = "A*24,33; B*15,27",
                    Country = "Belgium",
                    Hospital = "UZ Leuven",
                    UrgencyScore = 9,
                    WaitingSince = DateTime.Now.AddDays(-365),
                    Status = RecipientStatus.Waiting
                },
                new Recipient
                {
                    FirstName = "Maria",
                    LastName = "Bianchi",
                    DateOfBirth = new DateTime(1965, 12, 3),
                    BloodType = "B+",
                    HlaType = "A*02,24; B*27,35",
                    Country = "Italy",
                    Hospital = "Ospedale Niguarda",
                    UrgencyScore = 7,
                    WaitingSince = DateTime.Now.AddDays(-90),
                    Status = RecipientStatus.Waiting
                }
            };

            // Add organ requests to recipients
            recipients[0].OrganRequests.Add(new OrganRequest
            {
                OrganType = OrganType.Kidney,
                MedicalReason = "End-stage renal disease",
                Priority = 9
            });

            recipients[1].OrganRequests.Add(new OrganRequest
            {
                OrganType = OrganType.Liver,
                MedicalReason = "Cirrhosis",
                Priority = 8
            });

            recipients[2].OrganRequests.Add(new OrganRequest
            {
                OrganType = OrganType.Heart,
                MedicalReason = "Congestive heart failure",
                Priority = 10
            });

            // Add recipients to the list
            _recipients.AddRange(recipients);

            // Create some matches
            var matches = new List<Match>
            {
                new Match
                {
                    OrganId = _organs[0].Id,
                    DonorId = donors[0].Id,
                    RecipientId = recipients[0].Id,
                    CompatibilityScore = 87.5,
                    RankingScore = 92.1,
                    Status = MatchStatus.Approved,
                    MatchingFactors = new List<MatchFactor>
                    {
                        new MatchFactor { FactorName = "Blood Type", Weight = 0.35, Score = 100, Description = "Direct match" },
                        new MatchFactor { FactorName = "HLA Compatibility", Weight = 0.3, Score = 85, Description = "6/8 antigens match" },
                        new MatchFactor { FactorName = "Age Difference", Weight = 0.1, Score = 70, Description = "2 years difference" },
                        new MatchFactor { FactorName = "Waiting Time", Weight = 0.15, Score = 90, Description = "180 days on waiting list" },
                        new MatchFactor { FactorName = "Urgency", Weight = 0.1, Score = 80, Description = "High urgency (8/10)" }
                    },
                    ApprovalDate = DateTime.Now.AddDays(-2),
                    ApprovedBy = "Dr. Klaus Weber"
                },
                new Match
                {
                    OrganId = _organs[1].Id,
                    DonorId = donors[1].Id,
                    RecipientId = recipients[1].Id,
                    CompatibilityScore = 92.3,
                    RankingScore = 95.7,
                    Status = MatchStatus.Notified,
                    MatchingFactors = new List<MatchFactor>
                    {
                        new MatchFactor { FactorName = "Blood Type", Weight = 0.35, Score = 100, Description = "Direct match" },
                        new MatchFactor { FactorName = "HLA Compatibility", Weight = 0.3, Score = 90, Description = "7/8 antigens match" },
                        new MatchFactor { FactorName = "Age Difference", Weight = 0.1, Score = 85, Description = "5 years difference" },
                        new MatchFactor { FactorName = "Waiting Time", Weight = 0.15, Score = 95, Description = "365 days on waiting list" },
                        new MatchFactor { FactorName = "Urgency", Weight = 0.1, Score = 90, Description = "Very high urgency (9/10)" }
                    }
                }
            };

            // Set references in the matches
            foreach (var match in matches)
            {
                match.Organ = _organs.First(o => o.Id == match.OrganId);
                match.Donor = _donors.First(d => d.Id == match.DonorId);
                match.Recipient = _recipients.First(r => r.Id == match.RecipientId);
            }

            // Add matches to the list
            _matches.AddRange(matches);

            // Create a transplantation from the approved match
            var transplantation = new Transplantation
            {
                MatchId = matches[0].Id,
                OrganId = matches[0].OrganId,
                DonorId = matches[0].DonorId,
                RecipientId = matches[0].RecipientId,
                Hospital = recipients[0].Hospital,
                SurgeonName = "Dr. Ingrid Schwarzenegger",
                ScheduledDate = DateTime.Now.AddDays(1),
                Status = TransplantationStatus.Scheduled
            };

            // Set references in the transplantation
            transplantation.Match = matches[0];
            transplantation.Organ = _organs.First(o => o.Id == transplantation.OrganId);
            transplantation.Donor = _donors.First(d => d.Id == transplantation.DonorId);
            transplantation.Recipient = _recipients.First(r => r.Id == transplantation.RecipientId);

            // Add transplantation to the list
            _transplantations.Add(transplantation);

            // Create some activity logs
            _activityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.Now.AddDays(-5),
                ActivityType = ActivityType.NewDonor,
                Description = "New donor registered: Thomas Müller",
                RelatedId = donors[0].Id
            });

            _activityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.Now.AddDays(-4),
                ActivityType = ActivityType.NewOrgan,
                Description = "New organ available: Kidney (O+)",
                RelatedId = _organs[0].Id
            });

            _activityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.Now.AddDays(-3),
                ActivityType = ActivityType.MatchFound,
                Description = "Match found for Kidney (O+) to Anna Schmidt",
                RelatedId = matches[0].Id
            });

            _activityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.Now.AddDays(-2),
                ActivityType = ActivityType.MatchApproved,
                Description = "Match approved by Dr. Klaus Weber",
                RelatedId = matches[0].Id
            });

            _activityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.Now.AddDays(-1),
                ActivityType = ActivityType.TransplantationScheduled,
                Description = "Transplantation scheduled for tomorrow",
                RelatedId = transplantation.Id
            });
        }

        #region Donor Operations

        public List<Donor> GetAllDonors()
        {
            return _donors.ToList();
        }

        public Donor? GetDonorById(string id)
        {
            return _donors.FirstOrDefault(d => d.Id == id);
        }

        public void AddDonor(Donor donor)
        {
            _donors.Add(donor);
            LogActivity(ActivityType.NewDonor, $"New donor registered: {donor.FullName}", donor.Id);
        }

        public void UpdateDonor(Donor donor)
        {
            var index = _donors.FindIndex(d => d.Id == donor.Id);
            if (index >= 0)
            {
                _donors[index] = donor;
                LogActivity(ActivityType.DonorUpdated, $"Donor updated: {donor.FullName}", donor.Id);
            }
        }

        public void DeleteDonor(string id)
        {
            var donor = GetDonorById(id);
            if (donor != null)
            {
                _donors.Remove(donor);
                LogActivity(ActivityType.DonorRemoved, $"Donor removed: {donor.FullName}", donor.Id);
            }
        }

        #endregion

        #region Recipient Operations

        public List<Recipient> GetAllRecipients()
        {
            return _recipients.ToList();
        }

        public Recipient? GetRecipientById(string id)
        {
            return _recipients.FirstOrDefault(r => r.Id == id);
        }

        public void AddRecipient(Recipient recipient)
        {
            _recipients.Add(recipient);
            LogActivity(ActivityType.NewRecipient, $"New recipient registered: {recipient.FullName}", recipient.Id);
        }

        public void UpdateRecipient(Recipient recipient)
        {
            var index = _recipients.FindIndex(r => r.Id == recipient.Id);
            if (index >= 0)
            {
                _recipients[index] = recipient;
                LogActivity(ActivityType.RecipientUpdated, $"Recipient updated: {recipient.FullName}", recipient.Id);
            }
        }

        public void DeleteRecipient(string id)
        {
            var recipient = GetRecipientById(id);
            if (recipient != null)
            {
                _recipients.Remove(recipient);
                LogActivity(ActivityType.RecipientRemoved, $"Recipient removed: {recipient.FullName}", recipient.Id);
            }
        }

        #endregion

        #region Organ Operations

        public List<Organ> GetAllOrgans()
        {
            return _organs.ToList();
        }

        public Organ? GetOrganById(string id)
        {
            return _organs.FirstOrDefault(o => o.Id == id);
        }

        public void AddOrgan(Organ organ)
        {
            _organs.Add(organ);

            // Add to donor's available organs if applicable
            var donor = GetDonorById(organ.DonorId);
            if (donor != null)
            {
                donor.AvailableOrgans.Add(organ);
            }

            LogActivity(ActivityType.NewOrgan, $"New organ available: {organ.Type} ({organ.BloodType})", organ.Id);
        }

        public void UpdateOrgan(Organ organ)
        {
            var index = _organs.FindIndex(o => o.Id == organ.Id);
            if (index >= 0)
            {
                _organs[index] = organ;
                LogActivity(ActivityType.OrganUpdated, $"Organ updated: {organ.Type} ({organ.BloodType})", organ.Id);
            }
        }

        public void DeleteOrgan(string id)
        {
            var organ = GetOrganById(id);
            if (organ != null)
            {
                // Remove from donor's available organs if applicable
                var donor = GetDonorById(organ.DonorId);
                if (donor != null)
                {
                    donor.AvailableOrgans.RemoveAll(o => o.Id == organ.Id);
                }

                _organs.Remove(organ);
                LogActivity(ActivityType.OrganRemoved, $"Organ removed: {organ.Type} ({organ.BloodType})", organ.Id);
            }
        }

        #endregion

        #region Match Operations

        public List<Match> GetAllMatches()
        {
            return _matches.ToList();
        }

        public Match? GetMatchById(string id)
        {
            return _matches.FirstOrDefault(m => m.Id == id);
        }

        public void AddMatch(Match match)
        {
            _matches.Add(match);
            LogActivity(ActivityType.MatchFound, $"Match found for {match.Organ?.Type} to {match.Recipient?.FullName}", match.Id);
        }

        public void UpdateMatch(Match match)
        {
            var index = _matches.FindIndex(m => m.Id == match.Id);
            if (index >= 0)
            {
                var oldStatus = _matches[index].Status;
                _matches[index] = match;

                // Log status changes
                if (oldStatus != match.Status)
                {
                    switch (match.Status)
                    {
                        case MatchStatus.Notified:
                            LogActivity(ActivityType.HospitalNotified, $"Hospital notified about match for {match.Recipient?.FullName}", match.Id);
                            break;
                        case MatchStatus.Reviewing:
                            LogActivity(ActivityType.MatchReviewing, $"Match for {match.Recipient?.FullName} is under review", match.Id);
                            break;
                        case MatchStatus.Approved:
                            LogActivity(ActivityType.MatchApproved, $"Match approved by {match.ApprovedBy}", match.Id);
                            break;
                        case MatchStatus.Rejected:
                            LogActivity(ActivityType.MatchRejected, $"Match for {match.Recipient?.FullName} was rejected", match.Id);
                            break;
                        case MatchStatus.Completed:
                            LogActivity(ActivityType.MatchCompleted, $"Match for {match.Recipient?.FullName} was completed", match.Id);
                            break;
                        case MatchStatus.Cancelled:
                            LogActivity(ActivityType.MatchCancelled, $"Match for {match.Recipient?.FullName} was cancelled", match.Id);
                            break;
                    }
                }
            }
        }

        public void DeleteMatch(string id)
        {
            var match = GetMatchById(id);
            if (match != null)
            {
                _matches.Remove(match);
                LogActivity(ActivityType.MatchRemoved, $"Match removed", match.Id);
            }
        }

        #endregion

        #region Transplantation Operations

        public List<Transplantation> GetAllTransplantations()
        {
            return _transplantations.ToList();
        }

        public Transplantation? GetTransplantationById(string id)
        {
            return _transplantations.FirstOrDefault(t => t.Id == id);
        }

        public void AddTransplantation(Transplantation transplantation)
        {
            _transplantations.Add(transplantation);
            LogActivity(ActivityType.TransplantationScheduled, $"Transplantation scheduled for {transplantation.Recipient?.FullName}", transplantation.Id);
        }

        public void UpdateTransplantation(Transplantation transplantation)
        {
            var index = _transplantations.FindIndex(t => t.Id == transplantation.Id);
            if (index >= 0)
            {
                var oldStatus = _transplantations[index].Status;
                _transplantations[index] = transplantation;

                // Log status changes
                if (oldStatus != transplantation.Status)
                {
                    switch (transplantation.Status)
                    {
                        case TransplantationStatus.InProgress:
                            LogActivity(ActivityType.TransplantationStarted, $"Transplantation for {transplantation.Recipient?.FullName} has started", transplantation.Id);
                            break;
                        case TransplantationStatus.Completed:
                            LogActivity(ActivityType.TransplantationCompleted, $"Transplantation for {transplantation.Recipient?.FullName} completed successfully", transplantation.Id);
                            break;
                        case TransplantationStatus.Cancelled:
                            LogActivity(ActivityType.TransplantationCancelled, $"Transplantation for {transplantation.Recipient?.FullName} was cancelled", transplantation.Id);
                            break;
                        case TransplantationStatus.Delayed:
                            LogActivity(ActivityType.TransplantationDelayed, $"Transplantation for {transplantation.Recipient?.FullName} was delayed", transplantation.Id);
                            break;
                        case TransplantationStatus.Failed:
                            LogActivity(ActivityType.TransplantationFailed, $"Transplantation for {transplantation.Recipient?.FullName} failed", transplantation.Id);
                            break;
                    }
                }
            }
        }

        public void DeleteTransplantation(string id)
        {
            var transplantation = GetTransplantationById(id);
            if (transplantation != null)
            {
                _transplantations.Remove(transplantation);
                LogActivity(ActivityType.TransplantationRemoved, $"Transplantation record removed", transplantation.Id);
            }
        }

        #endregion

        #region Activity Log

        public List<ActivityLog> GetRecentActivity(int count = 10)
        {
            return _activityLogs
                .OrderByDescending(log => log.Timestamp)
                .Take(count)
                .ToList();
        }

        private void LogActivity(ActivityType activityType, string description, string relatedId)
        {
            _activityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.Now,
                ActivityType = activityType,
                Description = description,
                RelatedId = relatedId
            });
        }

        #endregion

        #region Matching Engine

        public async Task<List<Match>> FindMatchesForOrgan(string organId)
        {
            // Simulate async processing
            await Task.Delay(1000);

            var organ = GetOrganById(organId);
            if (organ == null) return new List<Match>();

            var potentialRecipients = _recipients
                .Where(r => r.Status == RecipientStatus.Waiting)
                .Where(r => r.OrganRequests.Any(req => req.OrganType == organ.Type && req.Status == OrganRequestStatus.Waiting))
                .ToList();

            var matches = new List<Match>();

            foreach (var recipient in potentialRecipients)
            {
                // Apply matching function
                var compatibilityScore = CalculateCompatibilityScore(organ, recipient);

                // Apply ranking function
                var rankingScore = CalculateRankingScore(organ, recipient, compatibilityScore);

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
                        MatchingFactors = GenerateMatchingFactors(organ, recipient),
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

        private double CalculateCompatibilityScore(Organ organ, Recipient recipient)
        {
            // Simple matching algorithm - would be much more complex in reality
            double score = 0;

            // Blood type compatibility (simplified)
            if (organ.BloodType == recipient.BloodType)
            {
                score += 40; // Perfect match
            }
            else if (organ.BloodType == "O+") // Universal donor for other positive types
            {
                if (recipient.BloodType.EndsWith("+"))
                {
                    score += 30;
                }
            }
            else if (organ.BloodType == "O-") // Universal donor for all
            {
                score += 35;
            }
            else
            {
                score += 10; // Some compatibility, but not ideal
            }

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
            double hlaScore = (matchedHla / (double)Math.Max(donorHla.Length, 1)) * 40;
            score += hlaScore;

            // Age compatibility (simplified)
            var donor = GetDonorById(organ.DonorId);
            if (donor != null)
            {
                int ageDifference = Math.Abs(donor.Age - recipient.Age);
                if (ageDifference <= 5)
                {
                    score += 20;
                }
                else if (ageDifference <= 10)
                {
                    score += 15;
                }
                else if (ageDifference <= 20)
                {
                    score += 10;
                }
                else
                {
                    score += 5;
                }
            }

            return Math.Min(score, 100); // Cap at 100
        }

        private double CalculateRankingScore(Organ organ, Recipient recipient, double compatibilityScore)
        {
            // Ranking function - determines final priority among compatible matches
            double score = compatibilityScore * 0.5; // 50% based on compatibility

            // Urgency factor (30%)
            score += (recipient.UrgencyScore / 10.0) * 30;

            // Waiting time factor (20%)
            int waitingDays = recipient.WaitingDays;
            double waitingTimeScore = Math.Min(waitingDays / 365.0, 1.0) * 20; // Cap at 1 year
            score += waitingTimeScore;

            return Math.Min(score, 100); // Cap at 100
        }

        private List<MatchFactor> GenerateMatchingFactors(Organ organ, Recipient recipient)
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
                Weight = 0.35,
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
                Weight = 0.3,
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
                    Weight = 0.1,
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
                Weight = 0.15,
                Score = waitingTimeScore,
                Description = $"{waitingDays} days on waiting list"
            });

            // Urgency factor
            factors.Add(new MatchFactor
            {
                FactorName = "Urgency",
                Weight = 0.1,
                Score = recipient.UrgencyScore * 10,
                Description = $"{(recipient.UrgencyScore >= 8 ? "High" : "Medium")} urgency ({recipient.UrgencyScore}/10)"
            });

            return factors;
        }

        #endregion
    }

    public class ActivityLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public ActivityType ActivityType { get; set; }
        public string Description { get; set; } = string.Empty;
        public string RelatedId { get; set; } = string.Empty;
    }

    public enum ActivityType
    {
        NewDonor,
        DonorUpdated,
        DonorRemoved,
        NewRecipient,
        RecipientUpdated,
        RecipientRemoved,
        NewOrgan,
        OrganUpdated,
        OrganRemoved,
        MatchFound,
        HospitalNotified,
        MatchReviewing,
        MatchApproved,
        MatchRejected,
        MatchCompleted,
        MatchCancelled,
        MatchRemoved,
        TransplantationScheduled,
        TransplantationStarted,
        TransplantationCompleted,
        TransplantationCancelled,
        TransplantationDelayed,
        TransplantationFailed,
        TransplantationRemoved,
        SystemAlert
    }
}