using System;
using System.Collections.Generic;

namespace DonateForLife.Models
{
    public class Match
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganId { get; set; } = string.Empty;
        public string DonorId { get; set; } = string.Empty;
        public string RecipientId { get; set; } = string.Empty;
        public DateTime MatchDate { get; set; } = DateTime.Now;
        public double CompatibilityScore { get; set; } // 0-100, higher is better
        public double RankingScore { get; set; } // The final score after applying the ranking function
        public string MatchingAlgorithmVersion { get; set; } = "1.0";
        public string MatchingCriteria { get; set; } = string.Empty; // JSON or formatted string of the criteria used
        public List<MatchFactor> MatchingFactors { get; set; } = new List<MatchFactor>();
        public MatchStatus Status { get; set; } = MatchStatus.Pending;
        public DateTime? ApprovalDate { get; set; }
        public string ApprovedBy { get; set; } = string.Empty;

        // References to related entities
        public Organ? Organ { get; set; }
        public Donor? Donor { get; set; }
        public Recipient? Recipient { get; set; }

        // Calculated property
        public TimeSpan TimeToTransplant =>
            ApprovalDate.HasValue ? ApprovalDate.Value - MatchDate : TimeSpan.Zero;
    }

    public class MatchFactor
    {
        public string FactorName { get; set; } = string.Empty;
        public double Weight { get; set; } // Importance of this factor in the match
        public double Score { get; set; } // 0-100, how well this factor matched
        public string Description { get; set; } = string.Empty;

        // Calculated property
        public double WeightedScore => Weight * Score;
    }

    public enum MatchStatus
    {
        Pending,        // Match found, notification not yet sent
        Notified,       // Hospital notified
        Reviewing,      // Doctor is reviewing the match
        Approved,       // Doctor approved, organ requested
        Rejected,       // Doctor rejected the match
        Completed,      // Transplantation completed
        Cancelled       // Match cancelled for any reason
    }
}