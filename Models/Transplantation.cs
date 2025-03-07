using System;
using System.Collections.Generic;

namespace DonateForLife.Models
{
    public class Transplantation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MatchId { get; set; } = string.Empty;
        public string OrganId { get; set; } = string.Empty;
        public string DonorId { get; set; } = string.Empty;
        public string RecipientId { get; set; } = string.Empty;
        public string Hospital { get; set; } = string.Empty;
        public string SurgeonName { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public TransplantationStatus Status { get; set; } = TransplantationStatus.Scheduled;
        public List<TransplantationOutcome> Outcomes { get; set; } = new List<TransplantationOutcome>();

        // References to related entities
        public Match? Match { get; set; }
        public Organ? Organ { get; set; }
        public Donor? Donor { get; set; }
        public Recipient? Recipient { get; set; }

        // Calculated properties
        public TimeSpan? SurgeryDuration =>
            ActualStartDate.HasValue && ActualEndDate.HasValue
                ? ActualEndDate.Value - ActualStartDate.Value
                : null;

        public bool IsSuccessful =>
            Status == TransplantationStatus.Completed &&
            Outcomes.Exists(o => o.Type == OutcomeType.InitialFunction && o.IsPositive);
    }

    public class TransplantationOutcome
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TransplantationId { get; set; } = string.Empty;
        public OutcomeType Type { get; set; }
        public DateTime AssessmentDate { get; set; } = DateTime.Now;
        public bool IsPositive { get; set; } = true;
        public string Notes { get; set; } = string.Empty;
        public string AssessedBy { get; set; } = string.Empty;

        // For long-term outcomes
        public int DaysAfterTransplant { get; set; }
    }

    public enum TransplantationStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Cancelled,
        Delayed,
        Failed
    }

    public enum OutcomeType
    {
        InitialFunction,              // Immediate post-op organ function
        EarlyComplications,           // Complications within first week
        AcuteRejection,               // Rejection episodes
        Infection,                    // Infections
        GraftFunction30Day,           // 30-day function assessment
        GraftFunction90Day,           // 90-day function assessment
        GraftFunction1Year,           // 1-year function assessment
        GraftFunction5Year,           // 5-year function assessment
        PatientSurvival1Year,         // 1-year patient survival
        PatientSurvival5Year,         // 5-year patient survival
        QualityOfLife,                // Patient reported quality of life
        MedicationAdherence,          // Patient medication adherence
        SecondaryComplication,        // Secondary complications
        LongTermFunction              // Long-term function beyond 5 years
    }
}