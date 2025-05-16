using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DonateForLife.Services.Database;

namespace DonateForLife.Services
{
    public class AuthenticationService
    {
        private readonly UserRepository _userRepository;
        private readonly ActivityLogRepository _activityLogRepository;
        private readonly string _pepper; // An additional secret value added to password hashing
        private const int _iterations = 10000; // Number of PBKDF2 iterations

        // Current authenticated user
        private string _currentUserId;
        private string _currentUsername;
        private string _currentUserRole;

        public AuthenticationService(PostgresConnectionHelper dbHelper, string pepper)
        {
            _userRepository = new UserRepository(dbHelper);
            _activityLogRepository = new ActivityLogRepository(dbHelper);
            _pepper = pepper ?? throw new ArgumentNullException(nameof(pepper));
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(_currentUserId);
        public string CurrentUserId => _currentUserId;
        public string CurrentUsername => _currentUsername;
        public string CurrentUserRole => _currentUserRole;

        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            try
            {
                Console.WriteLine($"Attempting to authenticate user: {username}");

                // Get the user first to check if they exist
                var user = await _userRepository.GetUserByUsernameAsync(username);

                if (user == null)
                {
                    Console.WriteLine("User not found");
                    return false;
                }

                if (!user.IsActive)
                {
                    Console.WriteLine("User account is inactive");
                    return false;
                }

                // Debug: Check stored password hash
                Console.WriteLine($"Stored password hash: {user.PasswordHash}");

                // Calculate the hash for the provided password
                var passwordHash = HashPassword(password, username);

                // Debug: Check calculated password hash
                Console.WriteLine($"Calculated password hash: {passwordHash}");

                // Check if the password matches
                if (user.PasswordHash == passwordHash)
                {
                    // Authentication successful
                    _currentUserId = user.Id;
                    _currentUsername = user.Username;
                    _currentUserRole = user.Role;

                    // Update the last login timestamp
                    await _userRepository.UpdateUserLastLoginAsync(username);

                    // Log the successful login
                    await _activityLogRepository.AddActivityLogAsync(new Models.ActivityLog
                    {
                        ActivityType = Models.ActivityType.SystemAlert,
                        Description = $"User {username} logged in",
                        Timestamp = DateTime.Now
                    });

                    Console.WriteLine("Authentication successful");
                    return true;
                }
                else
                {
                    Console.WriteLine("Password hash doesn't match");

                    // Log the failed login attempt
                    await _activityLogRepository.AddActivityLogAsync(new Models.ActivityLog
                    {
                        ActivityType = Models.ActivityType.SystemAlert,
                        Description = $"Failed login attempt for username {username}",
                        Timestamp = DateTime.Now
                    });

                    return false;
                }
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Authentication error: {ex.Message}");

                // Log the failed login attempt
                await _activityLogRepository.AddActivityLogAsync(new Models.ActivityLog
                {
                    ActivityType = Models.ActivityType.SystemAlert,
                    Description = $"Failed login attempt for username {username}: {ex.Message}",
                    Timestamp = DateTime.Now
                });

                return false;
            }
        }

        public void Logout()
        {
            // Log the logout
            if (!string.IsNullOrEmpty(_currentUsername))
            {
                // We can't await here in a synchronous method, so we'll fire and forget
                _ = _activityLogRepository.AddActivityLogAsync(new Models.ActivityLog
                {
                    ActivityType = Models.ActivityType.SystemAlert,
                    Description = $"User {_currentUsername} logged out",
                    Timestamp = DateTime.Now
                });
            }

            // Clear the current user information
            _currentUserId = null;
            _currentUsername = null;
            _currentUserRole = null;
        }

        public bool HasPermission(string permission)
        {
            // Simple permission check based on role
            // In a real application, this would be more sophisticated
            if (!IsAuthenticated)
                return false;

            switch (_currentUserRole?.ToLowerInvariant())
            {
                case "admin":
                    // Admins have all permissions
                    return true;

                case "doctor":
                    // Doctors can approve matches, manage transplantations, etc.
                    return permission.StartsWith("match.") ||
                           permission.StartsWith("transplantation.") ||
                           permission.StartsWith("donor.view") ||
                           permission.StartsWith("recipient.view") ||
                           permission.StartsWith("organ.view");

                case "coordinator":
                    // Coordinators can manage donors, recipients, organs
                    return permission.StartsWith("donor.") ||
                           permission.StartsWith("recipient.") ||
                           permission.StartsWith("organ.") ||
                           permission.StartsWith("match.view");

                case "researcher":
                    // Researchers can view data but not modify it
                    return permission.EndsWith(".view");

                default:
                    return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(string oldPassword, string newPassword)
        {
            if (!IsAuthenticated)
                return false;

            // Verify old password
            var oldPasswordHash = HashPassword(oldPassword, _currentUsername);
            var isValid = await _userRepository.ValidateUserCredentialsAsync(_currentUsername, oldPasswordHash);

            if (!isValid)
                return false;

            // Update the password
            var newPasswordHash = HashPassword(newPassword, _currentUsername);
            var success = await _userRepository.UpdateUserPasswordAsync(_currentUserId, newPasswordHash);

            if (success)
            {
                // Log the password change
                await _activityLogRepository.AddActivityLogAsync(new Models.ActivityLog
                {
                    ActivityType = Models.ActivityType.SystemAlert,
                    Description = $"Password changed for user {_currentUsername}",
                    Timestamp = DateTime.Now
                });
            }

            return success;
        }

        private string HashPassword(string password, string username)
        {
            try
            {
                // Combine the password with the pepper
                var passwordWithPepper = password + _pepper;

                // Log the input to the hash function (for debugging only - remove in production)
                Console.WriteLine($"Password input: '{password}', Pepper: '{_pepper}', Username: '{username}'");
                Console.WriteLine($"PasswordWithPepper: '{passwordWithPepper}'");

                // Create a salt from the username
                var salt = Encoding.UTF8.GetBytes(username ?? "default_salt");
                Console.WriteLine($"Salt bytes length: {salt.Length}");

                // Use PBKDF2 for secure password hashing
                using var pbkdf2 = new Rfc2898DeriveBytes(
                    passwordWithPepper,
                    salt,
                    _iterations,
                    HashAlgorithmName.SHA256);

                var hashBytes = pbkdf2.GetBytes(32); // 256 bits

                // Convert the hash to a base64 string
                var passwordHash = Convert.ToBase64String(hashBytes);
                Console.WriteLine($"Generated hash: {passwordHash}");

                return passwordHash;
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error hashing password: {ex.Message}");
                throw;
            }
        }   
    }
}