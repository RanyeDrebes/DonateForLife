using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DonateForLife.Services.Database
{
    /// <summary>
    /// Repository for system configuration settings
    /// </summary>
    public class ConfigurationRepository(PostgresConnectionHelper db)
    {
        private readonly PostgresConnectionHelper _db = db ?? throw new ArgumentNullException(nameof(db));

        public async Task<Dictionary<string, string>> GetAllConfigurationValuesAsync()
        {
            const string query = @"
                SELECT key, value
                FROM system_configuration";

            var result = new Dictionary<string, string>();
            var dataTable = await _db.ExecuteQueryAsync(query);

            foreach (System.Data.DataRow row in dataTable.Rows)
            {
                result.Add(row["key"].ToString(), row["value"].ToString());
            }

            return result;
        }

        public async Task<string> GetConfigurationValueAsync(string key)
        {
            const string query = @"
                SELECT value
                FROM system_configuration
                WHERE key = @key";

            var parameters = new Dictionary<string, object>
            {
                { "@key", key }
            };

            return await _db.ExecuteScalarAsync<string>(query, parameters);
        }

        public async Task SetConfigurationValueAsync(string key, string value, string userId)
        {
            // First check if the key exists
            const string checkQuery = @"
                SELECT COUNT(*)
                FROM system_configuration
                WHERE key = @key";

            var parameters = new Dictionary<string, object>
            {
                { "@key", key }
            };

            var count = await _db.ExecuteScalarAsync<long>(checkQuery, parameters);

            // Use a transaction to update the value and log the change
            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                // Get old value for audit log
                string oldValue = null;
                if (count > 0)
                {
                    const string getOldValueQuery = @"
                        SELECT value
                        FROM system_configuration
                        WHERE key = @key";

                    using var getOldCmd = new NpgsqlCommand(getOldValueQuery, connection, transaction);
                    getOldCmd.Parameters.AddWithValue("@key", key);
                    oldValue = (string)await getOldCmd.ExecuteScalarAsync();

                    // Update existing config
                    const string updateQuery = @"
                        UPDATE system_configuration
                        SET value = @value,
                            updated_by = @updated_by,
                            updated_at = CURRENT_TIMESTAMP
                        WHERE key = @key";

                    using var updateCmd = new NpgsqlCommand(updateQuery, connection, transaction);
                    updateCmd.Parameters.AddWithValue("@key", key);
                    updateCmd.Parameters.AddWithValue("@value", value);
                    updateCmd.Parameters.AddWithValue("@updated_by", Guid.Parse(userId));

                    await updateCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // Insert new config
                    const string insertQuery = @"
                        INSERT INTO system_configuration (key, value, updated_by)
                        VALUES (@key, @value, @updated_by)";

                    using var insertCmd = new NpgsqlCommand(insertQuery, connection, transaction);
                    insertCmd.Parameters.AddWithValue("@key", key);
                    insertCmd.Parameters.AddWithValue("@value", value);
                    insertCmd.Parameters.AddWithValue("@updated_by", Guid.Parse(userId));

                    await insertCmd.ExecuteNonQueryAsync();
                }

                // Log the change
                const string logQuery = @"
                    INSERT INTO algorithm_audit_logs (
                        id, user_id, parameter_key, old_value, new_value, reason, timestamp)
                    VALUES (
                        @id, @user_id, @parameter_key, @old_value, @new_value, @reason, CURRENT_TIMESTAMP)";

                using var logCmd = new NpgsqlCommand(logQuery, connection, transaction);
                logCmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                logCmd.Parameters.AddWithValue("@user_id", Guid.Parse(userId));
                logCmd.Parameters.AddWithValue("@parameter_key", key);
                logCmd.Parameters.AddWithValue("@old_value", oldValue ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@new_value", value);
                logCmd.Parameters.AddWithValue("@reason", "Updated via configuration interface");

                await logCmd.ExecuteNonQueryAsync();
            });
        }
    }
}
