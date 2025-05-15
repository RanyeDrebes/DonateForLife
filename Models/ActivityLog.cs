using System;

namespace DonateForLife.Models
{
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