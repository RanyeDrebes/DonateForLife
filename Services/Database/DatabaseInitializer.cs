using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using System.IO;
using System.Reflection;

namespace DonateForLife.Services.Database
{
    public class DatabaseInitializer(
        string connectionString,
        string adminUsername,
        string adminPassword,
        string adminEmail,
        string adminFullName,
        string pepper)
    {
        private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        private readonly string _adminUsername = adminUsername ?? throw new ArgumentNullException(nameof(adminUsername));
        private readonly string _adminPassword = adminPassword ?? throw new ArgumentNullException(nameof(adminPassword));
        private readonly string _adminEmail = adminEmail ?? throw new ArgumentNullException(nameof(adminEmail));
        private readonly string _adminFullName = adminFullName ?? throw new ArgumentNullException(nameof(adminFullName));
        private readonly string _pepper = pepper ?? throw new ArgumentNullException(nameof(pepper));

        public async Task<bool> InitializeDatabaseAsync()
        {
            try
            {
                // 1. Create the database if it doesn't exist
                await CreateDatabaseIfNotExistsAsync();

                // 2. Create the schema (tables, indexes, etc.)
                await CreateSchemaAsync();

                // 3. Seed initial data
                await SeedInitialDataAsync();

                return true;
            }
            catch (Exception ex)
            {
                // Log the error in a real application
                Console.WriteLine($"Database initialization error: {ex.Message}");
                return false;
            }
        }

        private async Task CreateDatabaseIfNotExistsAsync()
        {
            // Extract the database name from the connection string
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            var databaseName = builder.Database;

            // Create a connection to the 'postgres' database to check if our database exists
            builder.Database = "postgres";
            var connectionString = builder.ConnectionString;

            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Check if the database exists
            using var checkCmd = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = @dbname",
                connection);
            checkCmd.Parameters.AddWithValue("@dbname", databaseName);
            var exists = await checkCmd.ExecuteScalarAsync();

            if (exists == null)
            {
                // Create the database if it doesn't exist
                using var createCmd = new NpgsqlCommand(
                    $"CREATE DATABASE \"{databaseName}\"",
                    connection);
                await createCmd.ExecuteNonQueryAsync();
            }
        }

        private async Task CreateSchemaAsync()
        {
            // Load the SQL schema script from a resource or file
            var schemaScript = GetSchemaScript();

            // Execute the script
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                // Split the script into separate commands (typically separated by semicolons)
                var commands = schemaScript.Split(
                    new[] { "\r\nGO\r\n", "\nGO\n", ";\r\n", ";\n" },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var command in commands)
                {
                    if (string.IsNullOrWhiteSpace(command))
                        continue;

                    using var cmd = new NpgsqlCommand(command, connection, transaction);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task SeedInitialDataAsync()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                // Check if users table is empty
                using var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM users",
                    connection,
                    transaction);
                var count = (long)await checkCmd.ExecuteScalarAsync();

                if (count == 0)
                {
                    // Add admin user
                    var adminId = Guid.NewGuid();
                    var passwordHash = HashPassword(_adminPassword, _adminUsername, _pepper);

                    using var insertCmd = new NpgsqlCommand(
                        @"INSERT INTO users (
                            id, username, password_hash, full_name, email, role, 
                            hospital, is_active, last_login)
                        VALUES (
                            @id, @username, @password_hash, @full_name, @email, @role,
                            @hospital, @is_active, @last_login)",
                        connection,
                        transaction);
                    insertCmd.Parameters.AddWithValue("@id", adminId);
                    insertCmd.Parameters.AddWithValue("@username", _adminUsername);
                    insertCmd.Parameters.AddWithValue("@password_hash", passwordHash);
                    insertCmd.Parameters.AddWithValue("@full_name", _adminFullName);
                    insertCmd.Parameters.AddWithValue("@email", _adminEmail);
                    insertCmd.Parameters.AddWithValue("@role", "admin");
                    insertCmd.Parameters.AddWithValue("@hospital", DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@is_active", true);
                    insertCmd.Parameters.AddWithValue("@last_login", DBNull.Value);

                    await insertCmd.ExecuteNonQueryAsync();

                    // Add system activity log
                    var logId = Guid.NewGuid();
                    using var logCmd = new NpgsqlCommand(
                        @"INSERT INTO activity_logs (
                            id, timestamp, activity_type, description)
                        VALUES (
                            @id, @timestamp, @activity_type, @description)",
                        connection,
                        transaction);
                    logCmd.Parameters.AddWithValue("@id", logId);
                    logCmd.Parameters.AddWithValue("@timestamp", DateTime.Now);
                    logCmd.Parameters.AddWithValue("@activity_type", "SystemAlert");
                    logCmd.Parameters.AddWithValue("@description", "Database initialized with admin account");

                    await logCmd.ExecuteNonQueryAsync();
                }

                // Add other seed data like configuration values if needed
                await SeedConfigurationValuesAsync(connection, transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task SeedConfigurationValuesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            // Check if configuration table has any values
            using var checkCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM system_configuration",
                connection,
                transaction);
            var count = (long)await checkCmd.ExecuteScalarAsync();

            if (count == 0)
            {
                // Seed matching algorithm configuration values
                var configValues = new[]
                {
                    ("blood_type_weight", "35", "Weight for blood type compatibility in matching algorithm"),
                    ("hla_weight", "30", "Weight for HLA compatibility in matching algorithm"),
                    ("age_weight", "10", "Weight for age difference in matching algorithm"),
                    ("waiting_time_weight", "15", "Weight for waiting time in matching algorithm"),
                    ("urgency_weight", "10", "Weight for medical urgency in matching algorithm"),
                    
                    // Add other configuration values as needed
                    ("matching_algorithm_version", "1.0", "Current version of the matching algorithm"),
                    ("email_notifications_enabled", "true", "Whether email notifications are enabled"),
                    ("auto_matching_enabled", "true", "Whether automatic matching is enabled for new organs")
                };

                foreach (var (key, value, description) in configValues)
                {
                    var configId = Guid.NewGuid();
                    using var insertCmd = new NpgsqlCommand(
                        @"INSERT INTO system_configuration (
                            id, key, value, description)
                        VALUES (
                            @id, @key, @value, @description)",
                        connection,
                        transaction);
                    insertCmd.Parameters.AddWithValue("@id", configId);
                    insertCmd.Parameters.AddWithValue("@key", key);
                    insertCmd.Parameters.AddWithValue("@value", value);
                    insertCmd.Parameters.AddWithValue("@description", description);

                    await insertCmd.ExecuteNonQueryAsync();
                }
            }
        }

        private string GetSchemaScript()
        {
            // In a real application, you might load this from a file or embedded resource
            // For now, we'll just return the schema script as a string
            return @"
-- Database schema for DonateForLife Organ Donation Management System

-- Create extension for UUID generation
CREATE EXTENSION IF NOT EXISTS ""uuid-ossp"";

-- Donor table
CREATE TABLE IF NOT EXISTS donors (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    date_of_birth DATE NOT NULL,
    blood_type VARCHAR(10) NOT NULL,
    hla_type VARCHAR(255) NOT NULL,
    medical_history TEXT,
    hospital VARCHAR(255) NOT NULL,
    country VARCHAR(100) NOT NULL,
    registered_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(20) NOT NULL DEFAULT 'Available',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Recipient table
CREATE TABLE IF NOT EXISTS recipients (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    date_of_birth DATE NOT NULL,
    blood_type VARCHAR(10) NOT NULL,
    hla_type VARCHAR(255) NOT NULL,
    medical_history TEXT,
    hospital VARCHAR(255) NOT NULL,
    country VARCHAR(100) NOT NULL,
    registered_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    urgency_score INTEGER NOT NULL CHECK (urgency_score BETWEEN 1 AND 10),
    waiting_since TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(20) NOT NULL DEFAULT 'Waiting',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Organ request table
CREATE TABLE IF NOT EXISTS organ_requests (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    recipient_id UUID NOT NULL REFERENCES recipients(id) ON DELETE CASCADE,
    organ_type VARCHAR(20) NOT NULL,
    request_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    medical_reason TEXT NOT NULL,
    priority INTEGER NOT NULL CHECK (priority BETWEEN 1 AND 10),
    status VARCHAR(20) NOT NULL DEFAULT 'Waiting',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Organ table
CREATE TABLE IF NOT EXISTS organs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    donor_id UUID NOT NULL REFERENCES donors(id) ON DELETE CASCADE,
    organ_type VARCHAR(20) NOT NULL,
    blood_type VARCHAR(10) NOT NULL,
    hla_type VARCHAR(255) NOT NULL,
    harvested_time TIMESTAMP NOT NULL,
    expiry_time TIMESTAMP NOT NULL,
    storage_location VARCHAR(255),
    medical_notes TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'Available',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Organ quality assessment table
CREATE TABLE IF NOT EXISTS organ_quality_assessments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organ_id UUID NOT NULL REFERENCES organs(id) ON DELETE CASCADE,
    functionality_score INTEGER NOT NULL CHECK (functionality_score BETWEEN 1 AND 10),
    structural_integrity_score INTEGER NOT NULL CHECK (structural_integrity_score BETWEEN 1 AND 10),
    risk_score INTEGER NOT NULL CHECK (risk_score BETWEEN 1 AND 10),
    assessment_notes TEXT,
    assessment_time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    assessed_by VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Match table
CREATE TABLE IF NOT EXISTS matches (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organ_id UUID NOT NULL REFERENCES organs(id),
    donor_id UUID NOT NULL REFERENCES donors(id),
    recipient_id UUID NOT NULL REFERENCES recipients(id),
    match_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    compatibility_score DECIMAL(5,2) NOT NULL,
    ranking_score DECIMAL(5,2) NOT NULL,
    matching_algorithm_version VARCHAR(20) NOT NULL,
    matching_criteria TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    approval_date TIMESTAMP,
    approved_by VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Match factors table
CREATE TABLE IF NOT EXISTS match_factors (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    match_id UUID NOT NULL REFERENCES matches(id) ON DELETE CASCADE,
    factor_name VARCHAR(100) NOT NULL,
    weight DECIMAL(5,2) NOT NULL,
    score DECIMAL(5,2) NOT NULL,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Transplantation table
CREATE TABLE IF NOT EXISTS transplantations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    match_id UUID NOT NULL REFERENCES matches(id),
    organ_id UUID NOT NULL REFERENCES organs(id),
    donor_id UUID NOT NULL REFERENCES donors(id),
    recipient_id UUID NOT NULL REFERENCES recipients(id),
    hospital VARCHAR(255) NOT NULL,
    surgeon_name VARCHAR(255) NOT NULL,
    scheduled_date TIMESTAMP NOT NULL,
    actual_start_date TIMESTAMP,
    actual_end_date TIMESTAMP,
    status VARCHAR(20) NOT NULL DEFAULT 'Scheduled',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Transplantation outcomes table
CREATE TABLE IF NOT EXISTS transplantation_outcomes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    transplantation_id UUID NOT NULL REFERENCES transplantations(id) ON DELETE CASCADE,
    outcome_type VARCHAR(50) NOT NULL,
    assessment_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_positive BOOLEAN NOT NULL,
    notes TEXT,
    assessed_by VARCHAR(255) NOT NULL,
    days_after_transplant INTEGER,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Activity log table
CREATE TABLE IF NOT EXISTS activity_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    activity_type VARCHAR(50) NOT NULL,
    description TEXT NOT NULL,
    related_id UUID,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Users table for authentication
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    role VARCHAR(50) NOT NULL,
    hospital VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_login TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Configuration table for algorithm parameters
CREATE TABLE IF NOT EXISTS system_configuration (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    key VARCHAR(100) UNIQUE NOT NULL,
    value TEXT NOT NULL,
    description TEXT,
    updated_by UUID REFERENCES users(id),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Audit log for algorithm adjustments
CREATE TABLE IF NOT EXISTS algorithm_audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id),
    parameter_key VARCHAR(100) NOT NULL,
    old_value TEXT,
    new_value TEXT NOT NULL,
    reason TEXT NOT NULL,
    timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_donors_blood_type ON donors(blood_type);
CREATE INDEX IF NOT EXISTS idx_recipients_blood_type ON recipients(blood_type);
CREATE INDEX IF NOT EXISTS idx_recipients_status ON recipients(status);
CREATE INDEX IF NOT EXISTS idx_organs_type_status ON organs(organ_type, status);
CREATE INDEX IF NOT EXISTS idx_organs_expiry ON organs(expiry_time);
CREATE INDEX IF NOT EXISTS idx_matches_status ON matches(status);
CREATE INDEX IF NOT EXISTS idx_transplantations_status ON transplantations(status);
CREATE INDEX IF NOT EXISTS idx_activity_logs_timestamp ON activity_logs(timestamp);

-- Create function for updating timestamps
CREATE OR REPLACE FUNCTION update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
   NEW.updated_at = CURRENT_TIMESTAMP;
   RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create triggers for updating timestamps
DROP TRIGGER IF EXISTS update_donors_timestamp ON donors;
CREATE TRIGGER update_donors_timestamp BEFORE UPDATE ON donors FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_recipients_timestamp ON recipients;
CREATE TRIGGER update_recipients_timestamp BEFORE UPDATE ON recipients FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_organ_requests_timestamp ON organ_requests;
CREATE TRIGGER update_organ_requests_timestamp BEFORE UPDATE ON organ_requests FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_organs_timestamp ON organs;
CREATE TRIGGER update_organs_timestamp BEFORE UPDATE ON organs FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_organ_quality_assessments_timestamp ON organ_quality_assessments;
CREATE TRIGGER update_organ_quality_assessments_timestamp BEFORE UPDATE ON organ_quality_assessments FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_matches_timestamp ON matches;
CREATE TRIGGER update_matches_timestamp BEFORE UPDATE ON matches FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_match_factors_timestamp ON match_factors;
CREATE TRIGGER update_match_factors_timestamp BEFORE UPDATE ON match_factors FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_transplantations_timestamp ON transplantations;
CREATE TRIGGER update_transplantations_timestamp BEFORE UPDATE ON transplantations FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_transplantation_outcomes_timestamp ON transplantation_outcomes;
CREATE TRIGGER update_transplantation_outcomes_timestamp BEFORE UPDATE ON transplantation_outcomes FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_users_timestamp ON users;
CREATE TRIGGER update_users_timestamp BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION update_timestamp();

DROP TRIGGER IF EXISTS update_system_configuration_timestamp ON system_configuration;
CREATE TRIGGER update_system_configuration_timestamp BEFORE UPDATE ON system_configuration FOR EACH ROW EXECUTE FUNCTION update_timestamp();
";
        }

        private static string HashPassword(string password, string username, string pepper)
        {
            // Combine the password with the pepper
            var passwordWithPepper = password + pepper;

            // Create a unique salt for each user
            var salt = Encoding.UTF8.GetBytes(username ?? "default_salt");

            // Use PBKDF2 for secure password hashing
            using var pbkdf2 = new Rfc2898DeriveBytes(
                passwordWithPepper,
                salt,
                10000, // Number of iterations
                HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(32); // 256 bits

            // Convert the hash to a base64 string
            return Convert.ToBase64String(hashBytes);
        }
    }
}