
using System;

namespace DonateForLife.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty; // Only used internally
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "admin", "doctor", "coordinator", "researcher"
        public string? Hospital { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLogin { get; set; }

        // Password is not stored in the model, only used for transient operations
        public string Password { get; set; } = string.Empty;
    }
}