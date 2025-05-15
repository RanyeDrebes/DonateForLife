using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using DonateForLife.Models;

namespace DonateForLife.Services.Database
{
    /// <summary>
    /// Repository for user authentication and management
    /// </summary>
    public class UserRepository
    {
        private readonly PostgresConnectionHelper _db;

        public UserRepository(PostgresConnectionHelper db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            const string query = @"
                SELECT id, username, password_hash, full_name, email, role, 
                       hospital, is_active, last_login
                FROM users
                WHERE username = @username";

            var parameters = new Dictionary<string, object>
            {
                { "@username", username }
            };

            return await _db.ExecuteQuerySingleAsync(query, reader => new User
            {
                Id = reader.GetGuid(0).ToString(),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                FullName = reader.GetString(3),
                Email = reader.GetString(4),
                Role = reader.GetString(5),
                Hospital = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsActive = reader.GetBoolean(7),
                LastLogin = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8)
            }, parameters);
        }

        public async Task<bool> ValidateUserCredentialsAsync(string username, string passwordHash)
        {
            const string query = @"
                SELECT COUNT(*)
                FROM users
                WHERE username = @username AND password_hash = @password_hash AND is_active = TRUE";

            var parameters = new Dictionary<string, object>
            {
                { "@username", username },
                { "@password_hash", passwordHash }
            };

            var count = await _db.ExecuteScalarAsync<long>(query, parameters);
            return count > 0;
        }

        public async Task<bool> UpdateUserLastLoginAsync(string username)
        {
            const string query = @"
                UPDATE users
                SET last_login = CURRENT_TIMESTAMP
                WHERE username = @username";

            var parameters = new Dictionary<string, object>
            {
                { "@username", username }
            };

            var rowsAffected = await _db.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateUserPasswordAsync(string userId, string newPasswordHash)
        {
            const string query = @"
                UPDATE users
                SET password_hash = @password_hash
                WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(userId) },
                { "@password_hash", newPasswordHash }
            };

            var rowsAffected = await _db.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            const string query = @"
                SELECT id, username, password_hash, full_name, email, role, 
                       hospital, is_active, last_login
                FROM users
                ORDER BY username";

            return await _db.ExecuteQueryAsync(query, reader => new User
            {
                Id = reader.GetGuid(0).ToString(),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                FullName = reader.GetString(3),
                Email = reader.GetString(4),
                Role = reader.GetString(5),
                Hospital = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsActive = reader.GetBoolean(7),
                LastLogin = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8)
            });
        }

        public async Task<string> AddUserAsync(User user, string passwordHash)
        {
            const string query = @"
                INSERT INTO users (
                    id, username, password_hash, full_name, email, role, 
                    hospital, is_active)
                VALUES (
                    @id, @username, @password_hash, @full_name, @email, @role,
                    @hospital, @is_active)
                RETURNING id";

            var id = string.IsNullOrEmpty(user.Id) ? Guid.NewGuid().ToString() : user.Id;

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) },
                { "@username", user.Username },
                { "@password_hash", passwordHash },
                { "@full_name", user.FullName },
                { "@email", user.Email },
                { "@role", user.Role },
                { "@hospital", user.Hospital ?? (object)DBNull.Value },
                { "@is_active", user.IsActive }
            };

            var result = await _db.ExecuteScalarAsync<Guid>(query, parameters);
            return result.ToString();
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            const string query = @"
                UPDATE users
                SET username = @username,
                    full_name = @full_name,
                    email = @email,
                    role = @role,
                    hospital = @hospital,
                    is_active = @is_active
                WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(user.Id) },
                { "@username", user.Username },
                { "@full_name", user.FullName },
                { "@email", user.Email },
                { "@role", user.Role },
                { "@hospital", user.Hospital ?? (object)DBNull.Value },
                { "@is_active", user.IsActive }
            };

            var rowsAffected = await _db.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            const string query = "DELETE FROM users WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            var rowsAffected = await _db.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }
    }
}