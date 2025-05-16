using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

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
                // 0. Test the connection to the database
                await TestConnectionAsync();

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
                // Log the full exception with stack trace
                Console.WriteLine($"Database initialization error: {ex}");
                return false;
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                Console.WriteLine("Database connection successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database connection failed: {ex.Message}");
                throw;
            }
        }

        private async Task CreateDatabaseIfNotExistsAsync()
        {
            try
            {
                // Extract the database name from the connection string
                var builder = new NpgsqlConnectionStringBuilder(_connectionString);
                var databaseName = builder.Database;

                // Connect to postgres database first
                builder.Database = "postgres";
                var connectionString = builder.ConnectionString;

                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine("Successfully connected to PostgreSQL server");

                // Check if the database exists
                using var checkCmd = new NpgsqlCommand(
                    "SELECT 1 FROM pg_database WHERE datname = @dbname",
                    connection);
                checkCmd.Parameters.AddWithValue("@dbname", databaseName);
                var exists = await checkCmd.ExecuteScalarAsync();

                if (exists == null)
                {
                    Console.WriteLine($"Creating database '{databaseName}'...");
                    // Create the database if it doesn't exist
                    using var createCmd = new NpgsqlCommand(
                        $"CREATE DATABASE \"{databaseName}\"",
                        connection);
                    await createCmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"Database '{databaseName}' created successfully");
                }
                else
                {
                    Console.WriteLine($"Database '{databaseName}' already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating database: {ex.Message}");
                throw;
            }
        }

        private async Task CreateSchemaAsync()
        {
            try
            {
                await ExecuteBatch("Extensions", "CREATE EXTENSION IF NOT EXISTS");
                await ExecuteBatch("Tables", "CREATE TABLE");
                await ExecuteBatch("Indexes", "CREATE INDEX");
                await ExecuteBatch("Triggers", "CREATE TRIGGER");
                await ExecuteBatch("Functions", "CREATE OR REPLACE FUNCTION");
                Console.WriteLine("Schema created successfully in batches");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create schema in batches: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteBatch(string batchName, string startsWith)
        {
            Console.WriteLine($"Executing {batchName} batch...");
            var script = GetSchemaScript();
            var commands = script.Split(';')
                .Where(cmd => !string.IsNullOrWhiteSpace(cmd))
                .Select(cmd => cmd.Trim())
                .Where(cmd => cmd.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Execute the commands
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var command in commands)
            {
                try
                {
                    using var cmd = new NpgsqlCommand(command, connection);
                    await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"Successfully executed: {command.Substring(0, Math.Min(50, command.Length))}...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in {batchName} batch: {ex.Message}");
                    Console.WriteLine($"Command: {command}");
                    throw;
                }
            }
        }

        public async Task SeedInitialDataAsync()
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

                // Add sample data for testing
                await SeedSampleDataAsync(connection, transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task SeedConfigurationValuesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
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

        private async Task SeedSampleDataAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            // Check if tables already have data
            if (await HasExistingData(connection, transaction))
            {
                Console.WriteLine("Sample data already exists. Skipping sample data creation.");
                return;
            }

            Console.WriteLine("Creating sample data...");

            // Create sample donors
            var donorIds = await CreateSampleDonors(connection, transaction);

            // Create sample organs linked to the donors
            var organIds = await CreateSampleOrgans(connection, transaction, donorIds);

            // Create sample recipients
            var recipientIds = await CreateSampleRecipients(connection, transaction);

            // Create sample matches between organs and recipients
            var matchIds = await CreateSampleMatches(connection, transaction, organIds, donorIds, recipientIds);

            // Create sample transplantations
            await CreateSampleTransplantations(connection, transaction, matchIds, organIds, donorIds, recipientIds);

            // Create sample activity logs
            await CreateSampleActivityLogs(connection, transaction, donorIds, recipientIds, organIds, matchIds);

            Console.WriteLine("Sample data created successfully!");
        }

        private async Task<bool> HasExistingData(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            // Check if donors table has any entries
            using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM donors", connection, transaction);
            var count = (long)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        private async Task<List<Guid>> CreateSampleDonors(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            Console.WriteLine("Creating sample donors...");
            var donors = new List<(string FirstName, string LastName, DateTime DateOfBirth, string BloodType,
                                   string Hospital, string Country)>
    {
        ("Johannes", "Müller", new DateTime(1975, 5, 12), "A+", "Charité Berlin", "Germany"),
        ("Marie", "Schmidt", new DateTime(1980, 10, 25), "O-", "Universitätsklinikum Köln", "Germany"),
        ("Thomas", "Weber", new DateTime(1983, 3, 18), "B+", "Klinikum Frankfurt", "Germany"),
        ("Anna", "Fischer", new DateTime(1968, 7, 30), "AB+", "AKH Wien", "Austria"),
        ("Michael", "Huber", new DateTime(1977, 11, 5), "A-", "Universitätsspital Zürich", "Switzerland"),
        ("Sofia", "Garcia", new DateTime(1982, 4, 22), "O+", "Hospital Clínic Barcelona", "Spain"),
        ("Antoine", "Dubois", new DateTime(1965, 9, 8), "B-", "Hôpital Pitié-Salpêtrière", "France"),
        ("Emma", "Rossi", new DateTime(1972, 1, 15), "AB-", "Ospedale San Raffaele", "Italy"),
        ("Jan", "Kowalski", new DateTime(1979, 6, 27), "A+", "Szpital Uniwersytecki w Krakowie", "Poland"),
        ("Eva", "Novak", new DateTime(1985, 8, 3), "O+", "Nemocnice Motol", "Czech Republic")
    };

            var donorIds = new List<Guid>();
            string hlaTypes = "A*01:01;A*02:01;B*07:02;B*08:01;C*07:01;DRB1*15:01";

            foreach (var donor in donors)
            {
                var id = Guid.NewGuid();
                donorIds.Add(id);

                using var cmd = new NpgsqlCommand(@"
            INSERT INTO donors (
                id, first_name, last_name, date_of_birth, blood_type, hla_type, 
                hospital, country, registered_date, status)
            VALUES (
                @id, @first_name, @last_name, @date_of_birth, @blood_type, @hla_type, 
                @hospital, @country, @registered_date, @status)",
                    connection, transaction);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@first_name", donor.FirstName);
                cmd.Parameters.AddWithValue("@last_name", donor.LastName);
                cmd.Parameters.AddWithValue("@date_of_birth", donor.DateOfBirth);
                cmd.Parameters.AddWithValue("@blood_type", donor.BloodType);
                cmd.Parameters.AddWithValue("@hla_type", hlaTypes);
                cmd.Parameters.AddWithValue("@hospital", donor.Hospital);
                cmd.Parameters.AddWithValue("@country", donor.Country);
                cmd.Parameters.AddWithValue("@registered_date", DateTime.Now.AddDays(-new Random().Next(30, 365)));
                cmd.Parameters.AddWithValue("@status", "Available");

                await cmd.ExecuteNonQueryAsync();
            }

            return donorIds;
        }

        private async Task<List<Guid>> CreateSampleOrgans(NpgsqlConnection connection, NpgsqlTransaction transaction, List<Guid> donorIds)
        {
            Console.WriteLine("Creating sample organs...");
            var organIds = new List<Guid>();
            var random = new Random();

            var organTypes = new[] { "Heart", "Lung", "Liver", "Kidney", "Pancreas", "Intestine" };
            var statuses = new[] { "Available", "Reserved", "Transplanted" };

            // Create 15-20 organs from our donors
            for (int i = 0; i < 20; i++)
            {
                var id = Guid.NewGuid();
                organIds.Add(id);

                // Randomly select a donor
                var donorId = donorIds[random.Next(donorIds.Count)];

                // Get donor data to match blood type
                using (var donorCmd = new NpgsqlCommand(
                    "SELECT blood_type FROM donors WHERE id = @id", connection, transaction))
                {
                    donorCmd.Parameters.AddWithValue("@id", donorId);
                    var bloodType = (string)await donorCmd.ExecuteScalarAsync();

                    // Create the organ
                    var organType = organTypes[random.Next(organTypes.Length)];
                    var status = statuses[random.Next(statuses.Length)];
                    var harvestedTime = DateTime.Now.AddDays(-random.Next(1, 20));

                    // Set expiry time based on organ type
                    var expiryTime = organType switch
                    {
                        "Heart" => harvestedTime.AddHours(4),
                        "Lung" => harvestedTime.AddHours(6),
                        "Liver" => harvestedTime.AddHours(12),
                        "Kidney" => harvestedTime.AddHours(24),
                        "Pancreas" => harvestedTime.AddHours(12),
                        "Intestine" => harvestedTime.AddHours(8),
                        _ => harvestedTime.AddHours(12)
                    };

                    using var cmd = new NpgsqlCommand(@"
                INSERT INTO organs (
                    id, donor_id, organ_type, blood_type, hla_type, 
                    harvested_time, expiry_time, storage_location, status)
                VALUES (
                    @id, @donor_id, @organ_type, @blood_type, @hla_type, 
                    @harvested_time, @expiry_time, @storage_location, @status)",
                        connection, transaction);

                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@donor_id", donorId);
                    cmd.Parameters.AddWithValue("@organ_type", organType);
                    cmd.Parameters.AddWithValue("@blood_type", bloodType);
                    cmd.Parameters.AddWithValue("@hla_type", "A*01:01;A*02:01;B*07:02;B*08:01");
                    cmd.Parameters.AddWithValue("@harvested_time", harvestedTime);
                    cmd.Parameters.AddWithValue("@expiry_time", expiryTime);
                    cmd.Parameters.AddWithValue("@storage_location", $"Hospital Storage {random.Next(1, 5)}");
                    cmd.Parameters.AddWithValue("@status", status);

                    await cmd.ExecuteNonQueryAsync();

                    // Add quality assessment
                    using var qualityCmd = new NpgsqlCommand(@"
                INSERT INTO organ_quality_assessments (
                    id, organ_id, functionality_score, structural_integrity_score, risk_score, 
                    assessment_notes, assessment_time, assessed_by)
                VALUES (
                    @id, @organ_id, @functionality_score, @structural_integrity_score, @risk_score, 
                    @assessment_notes, @assessment_time, @assessed_by)",
                        connection, transaction);

                    qualityCmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                    qualityCmd.Parameters.AddWithValue("@organ_id", id);
                    qualityCmd.Parameters.AddWithValue("@functionality_score", random.Next(7, 11));
                    qualityCmd.Parameters.AddWithValue("@structural_integrity_score", random.Next(7, 11));
                    qualityCmd.Parameters.AddWithValue("@risk_score", random.Next(1, 4));
                    qualityCmd.Parameters.AddWithValue("@assessment_notes", "Standard assessment");
                    qualityCmd.Parameters.AddWithValue("@assessment_time", harvestedTime);
                    qualityCmd.Parameters.AddWithValue("@assessed_by", "Dr. Schmidt");

                    await qualityCmd.ExecuteNonQueryAsync();
                }
            }

            return organIds;
        }

        private async Task<List<Guid>> CreateSampleRecipients(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            Console.WriteLine("Creating sample recipients...");
            var recipients = new List<(string FirstName, string LastName, DateTime DateOfBirth, string BloodType,
                                       string Hospital, string Country, int Urgency)>
    {
        ("Laura", "Wagner", new DateTime(1970, 7, 15), "AB+", "Universitätsklinikum Heidelberg", "Germany", 8),
        ("Christian", "Bauer", new DateTime(1965, 3, 22), "O+", "LMU Klinikum München", "Germany", 9),
        ("Sophia", "Hoffmann", new DateTime(1982, 11, 8), "A-", "Universitätsklinikum Dresden", "Germany", 6),
        ("Daniel", "Schulz", new DateTime(1977, 5, 30), "B+", "Universitätsklinikum Hamburg-Eppendorf", "Germany", 7),
        ("Julia", "Meyer", new DateTime(1960, 9, 12), "AB-", "Inselspital Bern", "Switzerland", 10),
        ("Marco", "Berger", new DateTime(1973, 1, 25), "O-", "Universitätsspital Basel", "Switzerland", 8),
        ("Luisa", "Keller", new DateTime(1985, 8, 5), "A+", "AKH Wien", "Austria", 5),
        ("Paul", "Huber", new DateTime(1969, 4, 18), "B-", "LKH Graz", "Austria", 7),
        ("Maria", "Gruber", new DateTime(1978, 12, 3), "O+", "Allgemeines Krankenhaus Linz", "Austria", 6),
        ("Pierre", "Durand", new DateTime(1962, 6, 27), "A+", "CHU de Bordeaux", "France", 9),
        ("Sophie", "Martin", new DateTime(1974, 2, 14), "AB+", "AP-HP Hôpital Necker", "France", 8),
        ("Jean", "Bernard", new DateTime(1980, 10, 9), "O+", "Hospices Civils de Lyon", "France", 7),
        ("Francesca", "Ricci", new DateTime(1968, 7, 21), "B+", "Policlinico Gemelli", "Italy", 9),
        ("Alessandro", "Marino", new DateTime(1983, 5, 16), "A-", "Ospedale Maggiore Policlinico", "Italy", 6),
        ("Martina", "Ferrari", new DateTime(1971, 11, 30), "O-", "Azienda Ospedaliera di Padova", "Italy", 8)
    };

            var recipientIds = new List<Guid>();
            string hlaTypes = "A*03:01;A*11:01;B*15:01;B*35:01;C*04:01;DRB1*01:01";

            foreach (var recipient in recipients)
            {
                var id = Guid.NewGuid();
                recipientIds.Add(id);

                var random = new Random();
                var waitingSince = DateTime.Now.AddDays(-random.Next(30, 730)); // 1 month to 2 years

                using var cmd = new NpgsqlCommand(@"
            INSERT INTO recipients (
                id, first_name, last_name, date_of_birth, blood_type, hla_type, 
                hospital, country, registered_date, urgency_score, waiting_since, status)
            VALUES (
                @id, @first_name, @last_name, @date_of_birth, @blood_type, @hla_type, 
                @hospital, @country, @registered_date, @urgency_score, @waiting_since, @status)",
                    connection, transaction);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@first_name", recipient.FirstName);
                cmd.Parameters.AddWithValue("@last_name", recipient.LastName);
                cmd.Parameters.AddWithValue("@date_of_birth", recipient.DateOfBirth);
                cmd.Parameters.AddWithValue("@blood_type", recipient.BloodType);
                cmd.Parameters.AddWithValue("@hla_type", hlaTypes);
                cmd.Parameters.AddWithValue("@hospital", recipient.Hospital);
                cmd.Parameters.AddWithValue("@country", recipient.Country);
                cmd.Parameters.AddWithValue("@registered_date", waitingSince);
                cmd.Parameters.AddWithValue("@urgency_score", recipient.Urgency);
                cmd.Parameters.AddWithValue("@waiting_since", waitingSince);
                cmd.Parameters.AddWithValue("@status", "Waiting");

                await cmd.ExecuteNonQueryAsync();

                // Create organ requests for each recipient
                var random2 = new Random();
                var organTypes = new[] { "Heart", "Lung", "Liver", "Kidney", "Pancreas", "Intestine" };
                var requestCount = random2.Next(1, 3); // 1-2 organ requests per recipient

                for (int i = 0; i < requestCount; i++)
                {
                    using var requestCmd = new NpgsqlCommand(@"
                INSERT INTO organ_requests (
                    id, recipient_id, organ_type, request_date, medical_reason, priority, status)
                VALUES (
                    @id, @recipient_id, @organ_type, @request_date, @medical_reason, @priority, @status)",
                        connection, transaction);

                    requestCmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                    requestCmd.Parameters.AddWithValue("@recipient_id", id);
                    requestCmd.Parameters.AddWithValue("@organ_type", organTypes[random2.Next(organTypes.Length)]);
                    requestCmd.Parameters.AddWithValue("@request_date", waitingSince);
                    requestCmd.Parameters.AddWithValue("@medical_reason", "End-stage organ failure");
                    requestCmd.Parameters.AddWithValue("@priority", recipient.Urgency);
                    requestCmd.Parameters.AddWithValue("@status", "Waiting");

                    await requestCmd.ExecuteNonQueryAsync();
                }
            }

            return recipientIds;
        }

        private async Task<List<Guid>> CreateSampleMatches(NpgsqlConnection connection, NpgsqlTransaction transaction, List<Guid> organIds, List<Guid> donorIds, List<Guid> recipientIds)
        {
            Console.WriteLine("Creating sample matches...");
            var matchIds = new List<Guid>();
            var random = new Random();
            var statuses = new[] { "Pending", "Notified", "Reviewing", "Approved", "Completed" };

            // Create 10-12 matches
            for (int i = 0; i < 12; i++)
            {
                var id = Guid.NewGuid();
                matchIds.Add(id);

                // Get a random organ and its donor
                var organId = organIds[random.Next(organIds.Count)];
                Guid donorId;
                string organBloodType;
                string organType;

                using (var organCmd = new NpgsqlCommand(
                    "SELECT donor_id, blood_type, organ_type FROM organs WHERE id = @id", connection, transaction))
                {
                    organCmd.Parameters.AddWithValue("@id", organId);
                    using var reader = await organCmd.ExecuteReaderAsync();
                    await reader.ReadAsync();
                    donorId = reader.GetGuid(0);
                    organBloodType = reader.GetString(1);
                    organType = reader.GetString(2);
                }

                // Find a compatible recipient (simplified - just match blood type)
                // In reality, you'd need a more complex algorithm
                Guid recipientId;
                bool foundMatch = false;

                using (var recipientCmd = new NpgsqlCommand(
                    "SELECT id FROM recipients WHERE blood_type = @blood_type OR blood_type = 'AB+' LIMIT 1",
                    connection, transaction))
                {
                    recipientCmd.Parameters.AddWithValue("@blood_type", organBloodType);
                    var result = await recipientCmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        recipientId = (Guid)result;
                        foundMatch = true;
                    }
                    else
                    {
                        // Fallback to any recipient if no blood match
                        recipientId = recipientIds[random.Next(recipientIds.Count)];
                    }
                }

                // Create the match
                var matchDate = DateTime.Now.AddDays(-random.Next(1, 60));
                var status = statuses[random.Next(statuses.Length)];
                DateTime? approvalDate = null;
                string approvedBy = null;

                if (status == "Approved" || status == "Completed")
                {
                    approvalDate = matchDate.AddDays(random.Next(1, 5));
                    approvedBy = "Dr. Max Mustermann";
                }

                using var cmd = new NpgsqlCommand(@"
            INSERT INTO matches (
                id, organ_id, donor_id, recipient_id, match_date, 
                compatibility_score, ranking_score, matching_algorithm_version, 
                matching_criteria, status, approval_date, approved_by)
            VALUES (
                @id, @organ_id, @donor_id, @recipient_id, @match_date, 
                @compatibility_score, @ranking_score, @matching_algorithm_version, 
                @matching_criteria, @status, @approval_date, @approved_by)",
                    connection, transaction);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@organ_id", organId);
                cmd.Parameters.AddWithValue("@donor_id", donorId);
                cmd.Parameters.AddWithValue("@recipient_id", recipientId);
                cmd.Parameters.AddWithValue("@match_date", matchDate);
                cmd.Parameters.AddWithValue("@compatibility_score", foundMatch ? random.Next(75, 101) : random.Next(50, 76));
                cmd.Parameters.AddWithValue("@ranking_score", foundMatch ? random.Next(80, 101) : random.Next(60, 81));
                cmd.Parameters.AddWithValue("@matching_algorithm_version", "1.0");
                cmd.Parameters.AddWithValue("@matching_criteria", "{\"algorithm\": \"standard\", \"version\": \"1.0\"}");
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@approval_date", approvalDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@approved_by", approvedBy ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                // Create match factors
                var factorNames = new[] { "Blood Type", "HLA Compatibility", "Age Difference", "Waiting Time", "Urgency" };
                var factorWeights = new[] { 0.35, 0.30, 0.10, 0.15, 0.10 };

                for (int f = 0; f < factorNames.Length; f++)
                {
                    using var factorCmd = new NpgsqlCommand(@"
                INSERT INTO match_factors (
                    id, match_id, factor_name, weight, score, description)
                VALUES (
                    @id, @match_id, @factor_name, @weight, @score, @description)",
                        connection, transaction);

                    factorCmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                    factorCmd.Parameters.AddWithValue("@match_id", id);
                    factorCmd.Parameters.AddWithValue("@factor_name", factorNames[f]);
                    factorCmd.Parameters.AddWithValue("@weight", factorWeights[f]);

                    // If it's a blood type match and this is the blood type factor, give a high score
                    double score = factorNames[f] == "Blood Type" && foundMatch ?
                        100 : random.Next(50, 101);

                    factorCmd.Parameters.AddWithValue("@score", score);
                    factorCmd.Parameters.AddWithValue("@description", $"Factor: {factorNames[f]}");

                    await factorCmd.ExecuteNonQueryAsync();
                }
            }

            return matchIds;
        }

        private async Task CreateSampleTransplantations(NpgsqlConnection connection, NpgsqlTransaction transaction, List<Guid> matchIds, List<Guid> organIds,List<Guid> donorIds, List<Guid> recipientIds)
        {
            Console.WriteLine("Creating sample transplantations...");
            var random = new Random();
            var statuses = new[] { "Scheduled", "InProgress", "Completed", "Delayed", "Cancelled" };
            var hospitals = new[]
            {
        "Charité Berlin",
        "Universitätsklinikum Heidelberg",
        "LMU Klinikum München",
        "AKH Wien",
        "Universitätsspital Zürich"
    };
            var surgeons = new[]
            {
        "Dr. Schmidt",
        "Dr. Müller",
        "Dr. Fischer",
        "Dr. Weber",
        "Dr. Bauer"
    };

            // Create 5-8 transplantations
            for (int i = 0; i < 8; i++)
            {
                var id = Guid.NewGuid();

                // Get a random match
                var matchId = matchIds[random.Next(matchIds.Count)];

                // Get match info
                Guid organId, donorId, recipientId;

                using (var matchCmd = new NpgsqlCommand(
                    "SELECT organ_id, donor_id, recipient_id FROM matches WHERE id = @id",
                    connection, transaction))
                {
                    matchCmd.Parameters.AddWithValue("@id", matchId);
                    using var reader = await matchCmd.ExecuteReaderAsync();
                    await reader.ReadAsync();
                    organId = reader.GetGuid(0);
                    donorId = reader.GetGuid(1);
                    recipientId = reader.GetGuid(2);
                }

                // Create transplantation
                var status = statuses[random.Next(statuses.Length)];
                var scheduledDate = DateTime.Now.AddDays(random.Next(-30, 30));
                DateTime? actualStartDate = null;
                DateTime? actualEndDate = null;

                if (status == "InProgress")
                {
                    actualStartDate = scheduledDate.AddHours(random.Next(1, 5));
                }
                else if (status == "Completed")
                {
                    actualStartDate = scheduledDate.AddHours(random.Next(1, 5));
                    actualEndDate = actualStartDate.Value.AddHours(random.Next(2, 8));
                }

                using var cmd = new NpgsqlCommand(@"
            INSERT INTO transplantations (
                id, match_id, organ_id, donor_id, recipient_id, 
                hospital, surgeon_name, scheduled_date, actual_start_date, 
                actual_end_date, status)
            VALUES (
                @id, @match_id, @organ_id, @donor_id, @recipient_id, 
                @hospital, @surgeon_name, @scheduled_date, @actual_start_date, 
                @actual_end_date, @status)",
                    connection, transaction);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@match_id", matchId);
                cmd.Parameters.AddWithValue("@organ_id", organId);
                cmd.Parameters.AddWithValue("@donor_id", donorId);
                cmd.Parameters.AddWithValue("@recipient_id", recipientId);
                cmd.Parameters.AddWithValue("@hospital", hospitals[random.Next(hospitals.Length)]);
                cmd.Parameters.AddWithValue("@surgeon_name", surgeons[random.Next(surgeons.Length)]);
                cmd.Parameters.AddWithValue("@scheduled_date", scheduledDate);
                cmd.Parameters.AddWithValue("@actual_start_date", actualStartDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@actual_end_date", actualEndDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", status);

                await cmd.ExecuteNonQueryAsync();

                // For completed transplantations, add outcomes
                if (status == "Completed")
                {
                    var outcomeTypes = new[]
                    {
                "InitialFunction",
                "EarlyComplications",
                "GraftFunction30Day"
            };

                    foreach (var outcomeType in outcomeTypes)
                    {
                        using var outcomeCmd = new NpgsqlCommand(@"
                    INSERT INTO transplantation_outcomes (
                        id, transplantation_id, outcome_type, assessment_date, 
                        is_positive, notes, assessed_by, days_after_transplant)
                    VALUES (
                        @id, @transplantation_id, @outcome_type, @assessment_date, 
                        @is_positive, @notes, @assessed_by, @days_after_transplant)",
                            connection, transaction);

                        var assessmentDate = outcomeType switch
                        {
                            "InitialFunction" => actualEndDate.Value.AddDays(1),
                            "EarlyComplications" => actualEndDate.Value.AddDays(7),
                            "GraftFunction30Day" => actualEndDate.Value.AddDays(30),
                            _ => actualEndDate.Value.AddDays(1)
                        };

                        var isPositive = random.Next(0, 10) < 8; // 80% chance of positive outcome

                        outcomeCmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                        outcomeCmd.Parameters.AddWithValue("@transplantation_id", id);
                        outcomeCmd.Parameters.AddWithValue("@outcome_type", outcomeType);
                        outcomeCmd.Parameters.AddWithValue("@assessment_date", assessmentDate);
                        outcomeCmd.Parameters.AddWithValue("@is_positive", isPositive);
                        outcomeCmd.Parameters.AddWithValue("@notes", isPositive ?
                            "Good progress, patient responding well" :
                            "Some complications, monitoring required");
                        outcomeCmd.Parameters.AddWithValue("@assessed_by", surgeons[random.Next(surgeons.Length)]);
                        outcomeCmd.Parameters.AddWithValue("@days_after_transplant", (assessmentDate - actualEndDate.Value).Days);

                        await outcomeCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private async Task CreateSampleActivityLogs(NpgsqlConnection connection, NpgsqlTransaction transaction,List<Guid> donorIds, List<Guid> recipientIds, List<Guid> organIds, List<Guid> matchIds)
        {
            Console.WriteLine("Creating sample activity logs...");
            var random = new Random();

            // Create a timeline of activity logs
            var baseDate = DateTime.Now.AddDays(-90);
            var activities = new List<(DateTime Date, string Type, string Description, Guid? RelatedId)>();

            // Donor activities
            foreach (var donorId in donorIds.Take(5))
            {
                var registrationDate = baseDate.AddDays(random.Next(1, 10));
                activities.Add((registrationDate, "NewDonor", "New donor registered", donorId));
                activities.Add((registrationDate.AddDays(1), "DonorUpdated", "Donor medical history updated", donorId));
            }

            // Recipient activities
            foreach (var recipientId in recipientIds.Take(5))
            {
                var registrationDate = baseDate.AddDays(random.Next(5, 15));
                activities.Add((registrationDate, "NewRecipient", "New recipient registered", recipientId));
                activities.Add((registrationDate.AddDays(2), "RecipientUpdated", "Recipient urgency score updated", recipientId));
            }

            // Organ activities
            foreach (var organId in organIds.Take(5))
            {
                var registrationDate = baseDate.AddDays(random.Next(15, 25));
                activities.Add((registrationDate, "NewOrgan", "New organ available", organId));
                activities.Add((registrationDate.AddHours(2), "OrganUpdated", "Organ quality assessment completed", organId));
            }

            // Match activities
            foreach (var matchId in matchIds.Take(5))
            {
                var matchDate = baseDate.AddDays(random.Next(30, 40));
                activities.Add((matchDate, "MatchFound", "Potential match identified", matchId));
                activities.Add((matchDate.AddDays(1), "HospitalNotified", "Hospital notified about match", matchId));
                activities.Add((matchDate.AddDays(2), "MatchReviewing", "Match under medical review", matchId));

                if (random.Next(0, 10) < 8) // 80% chance of approval
                {
                    activities.Add((matchDate.AddDays(3), "MatchApproved", "Match approved by medical team", matchId));
                    activities.Add((matchDate.AddDays(5), "TransplantationScheduled", "Transplantation scheduled", matchId));

                    if (random.Next(0, 10) < 8) // 80% chance of completion
                    {
                        activities.Add((matchDate.AddDays(7), "TransplantationStarted", "Transplantation procedure started", matchId));
                        activities.Add((matchDate.AddDays(7).AddHours(5), "TransplantationCompleted", "Transplantation completed successfully", matchId));
                    }
                    else
                    {
                        activities.Add((matchDate.AddDays(6), "TransplantationCancelled", "Transplantation cancelled due to complications", matchId));
                    }
                }
                else
                {
                    activities.Add((matchDate.AddDays(3), "MatchRejected", "Match rejected by medical team", matchId));
                }
            }

            // System alerts
            activities.Add((baseDate, "SystemAlert", "System initialized", null));
            activities.Add((baseDate.AddDays(1), "SystemAlert", "Database backup completed", null));
            activities.Add((baseDate.AddDays(30), "SystemAlert", "System maintenance performed", null));
            activities.Add((baseDate.AddDays(60), "SystemAlert", "Algorithm parameters updated", null));

            // Sort activities by date
            activities = activities.OrderBy(a => a.Date).ToList();

            // Insert into database
            foreach (var activity in activities)
            {
                using var cmd = new NpgsqlCommand(@"
            INSERT INTO activity_logs (
                id, timestamp, activity_type, description, related_id)
            VALUES (
                @id, @timestamp, @activity_type, @description, @related_id)",
                    connection, transaction);

                cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("@timestamp", activity.Date);
                cmd.Parameters.AddWithValue("@activity_type", activity.Type);
                cmd.Parameters.AddWithValue("@description", activity.Description);
                cmd.Parameters.AddWithValue("@related_id", activity.RelatedId ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}