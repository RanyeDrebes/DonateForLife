using System;
using System.Collections.Generic;

namespace DonateForLife.Models
{
    public class Recipient
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string BloodType { get; set; } = string.Empty;
        public string HlaType { get; set; } = string.Empty; // Human Leukocyte Antigen type
        public string MedicalHistory { get; set; } = string.Empty;
        public string Hospital { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public DateTime RegisteredDate { get; set; } = DateTime.Now;
        public int UrgencyScore { get; set; } // Higher score = more urgent
        public DateTime WaitingSince { get; set; } = DateTime.Now;
        public RecipientStatus Status { get; set; } = RecipientStatus.Waiting;
        public List<OrganRequest> OrganRequests { get; set; } = new List<OrganRequest>();

        // Calculated properties
        public int Age => CalculateAge(DateOfBirth);
        public string FullName => $"{FirstName} {LastName}";
        public int WaitingDays => (int)(DateTime.Now - WaitingSince).TotalDays;

        private int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    public class OrganRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public OrganType OrganType { get; set; }
        public DateTime RequestDate { get; set; } = DateTime.Now;
        public string MedicalReason { get; set; } = string.Empty;
        public int Priority { get; set; } // 1-10, 10 being highest priority
        public OrganRequestStatus Status { get; set; } = OrganRequestStatus.Waiting;
    }

    public enum RecipientStatus
    {
        Waiting,
        Matched,
        Transplanted,
        Ineligible,
        Deceased
    }

    public enum OrganRequestStatus
    {
        Waiting,
        Matched,
        Fulfilled,
        Cancelled
    }
}