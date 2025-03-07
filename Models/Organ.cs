using System;

namespace DonateForLife.Models
{
    public class Organ
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DonorId { get; set; } = string.Empty;
        public OrganType Type { get; set; }
        public string BloodType { get; set; } = string.Empty;
        public string HlaType { get; set; } = string.Empty;
        public DateTime HarvestedTime { get; set; }
        public DateTime ExpiryTime { get; set; }
        public string StorageLocation { get; set; } = string.Empty;
        public string MedicalNotes { get; set; } = string.Empty;
        public OrganStatus Status { get; set; } = OrganStatus.Available;
        public QualityAssessment Quality { get; set; } = new QualityAssessment();

        // Calculated properties
        public TimeSpan RemainingViability => ExpiryTime - DateTime.Now;
        public bool IsStillViable => DateTime.Now < ExpiryTime;

        public Organ(OrganType type)
        {
            Type = type;
            // Set default expiry time based on organ type
            HarvestedTime = DateTime.Now;
            ExpiryTime = HarvestedTime.Add(GetDefaultPreservationTime(type));
        }

        private TimeSpan GetDefaultPreservationTime(OrganType type)
        {
            return type switch
            {
                OrganType.Heart => TimeSpan.FromHours(4),
                OrganType.Lung => TimeSpan.FromHours(6),
                OrganType.Liver => TimeSpan.FromHours(12),
                OrganType.Kidney => TimeSpan.FromHours(24),
                OrganType.Pancreas => TimeSpan.FromHours(12),
                OrganType.Intestine => TimeSpan.FromHours(8),
                _ => TimeSpan.FromHours(12)
            };
        }
    }

    public class QualityAssessment
    {
        public int FunctionalityScore { get; set; } = 10; // 1-10, 10 being best
        public int StructuralIntegrityScore { get; set; } = 10; // 1-10, 10 being best
        public int RiskScore { get; set; } = 1; // 1-10, 1 being lowest risk
        public string AssessmentNotes { get; set; } = string.Empty;
        public DateTime AssessmentTime { get; set; } = DateTime.Now;
        public string AssessedBy { get; set; } = string.Empty;

        // Overall quality score (weighted average)
        public double OverallQualityScore =>
            (FunctionalityScore * 0.4) +
            (StructuralIntegrityScore * 0.4) +
            ((11 - RiskScore) * 0.2); // Inverting risk score for calculation
    }

    public enum OrganType
    {
        Heart,
        Lung,
        Liver,
        Kidney,
        Pancreas,
        Intestine
    }

    public enum OrganStatus
    {
        Available,
        Reserved,
        InTransit,
        Transplanted,
        Expired,
        Discarded
    }
}