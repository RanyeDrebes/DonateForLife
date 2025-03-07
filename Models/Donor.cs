using System;
using System.Collections.Generic;

namespace DonateForLife.Models
{
    public class Donor
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
        public DonorStatus Status { get; set; } = DonorStatus.Available;
        public List<Organ> AvailableOrgans { get; set; } = new List<Organ>();

        // Calculated properties
        public int Age => CalculateAge(DateOfBirth);
        public string FullName => $"{FirstName} {LastName}";

        private int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    public enum DonorStatus
    {
        Available,
        InProcess,
        Completed,
        Ineligible
    }
}