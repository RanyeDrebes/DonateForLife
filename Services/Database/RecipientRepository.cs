using DonateForLife.Models;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DonateForLife.Services.Database
{
    /// <summary>
    /// Repository for Recipient-related database operations
    /// </summary>
    public class RecipientRepository(PostgresConnectionHelper db)
    {
        private readonly PostgresConnectionHelper _db = db ?? throw new ArgumentNullException(nameof(db));

        public async Task<List<Recipient>> GetAllRecipientsAsync()
        {
            const string query = @"
                SELECT r.id, r.first_name, r.last_name, r.date_of_birth, r.blood_type, r.hla_type, 
                       r.medical_history, r.hospital, r.country, r.registered_date, r.urgency_score,
                       r.waiting_since, r.status
                FROM recipients r
                ORDER BY r.last_name, r.first_name";

            var recipients = await _db.ExecuteQueryAsync(query, reader => new Recipient
            {
                Id = reader.GetGuid(0).ToString(),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                DateOfBirth = reader.GetDateTime(3),
                BloodType = reader.GetString(4),
                HlaType = reader.GetString(5),
                MedicalHistory = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Hospital = reader.GetString(7),
                Country = reader.GetString(8),
                RegisteredDate = reader.GetDateTime(9),
                UrgencyScore = reader.GetInt32(10),
                WaitingSince = reader.GetDateTime(11),
                Status = Enum.Parse<RecipientStatus>(reader.GetString(12))
            });

            // Load organ requests for each recipient
            foreach (var recipient in recipients)
            {
                recipient.OrganRequests = await GetOrganRequestsForRecipientAsync(recipient.Id);
            }

            return recipients;
        }

        public async Task<Recipient> GetRecipientByIdAsync(string id)
        {
            const string query = @"
                SELECT r.id, r.first_name, r.last_name, r.date_of_birth, r.blood_type, r.hla_type, 
                       r.medical_history, r.hospital, r.country, r.registered_date, r.urgency_score,
                       r.waiting_since, r.status
                FROM recipients r
                WHERE r.id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            var recipient = await _db.ExecuteQuerySingleAsync(query, reader => new Recipient
            {
                Id = reader.GetGuid(0).ToString(),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                DateOfBirth = reader.GetDateTime(3),
                BloodType = reader.GetString(4),
                HlaType = reader.GetString(5),
                MedicalHistory = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Hospital = reader.GetString(7),
                Country = reader.GetString(8),
                RegisteredDate = reader.GetDateTime(9),
                UrgencyScore = reader.GetInt32(10),
                WaitingSince = reader.GetDateTime(11),
                Status = Enum.Parse<RecipientStatus>(reader.GetString(12))
            }, parameters);

            if (recipient != null)
            {
                recipient.OrganRequests = await GetOrganRequestsForRecipientAsync(recipient.Id);
            }

            return recipient;
        }

        private async Task<List<OrganRequest>> GetOrganRequestsForRecipientAsync(string recipientId)
        {
            const string query = @"
                SELECT id, organ_type, request_date, medical_reason, priority, status
                FROM organ_requests
                WHERE recipient_id = @recipient_id";

            var parameters = new Dictionary<string, object>
            {
                { "@recipient_id", Guid.Parse(recipientId) }
            };

            return await _db.ExecuteQueryAsync(query, reader => new OrganRequest
            {
                Id = reader.GetGuid(0).ToString(),
                OrganType = Enum.Parse<OrganType>(reader.GetString(1)),
                RequestDate = reader.GetDateTime(2),
                MedicalReason = reader.GetString(3),
                Priority = reader.GetInt32(4),
                Status = Enum.Parse<OrganRequestStatus>(reader.GetString(5))
            }, parameters);
        }

        public async Task<string> AddRecipientAsync(Recipient recipient)
        {
            // Use a transaction to ensure both recipient and their organ requests are added together
            string recipientId = null;

            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                const string insertRecipientQuery = @"
                    INSERT INTO recipients (
                        id, first_name, last_name, date_of_birth, blood_type, hla_type, 
                        medical_history, hospital, country, registered_date, urgency_score, 
                        waiting_since, status)
                    VALUES (
                        @id, @first_name, @last_name, @date_of_birth, @blood_type, @hla_type, 
                        @medical_history, @hospital, @country, @registered_date, @urgency_score, 
                        @waiting_since, @status)
                    RETURNING id";

                var id = recipient.Id;
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();
                }

                using var cmd = new NpgsqlCommand(insertRecipientQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@id", Guid.Parse(id));
                cmd.Parameters.AddWithValue("@first_name", recipient.FirstName);
                cmd.Parameters.AddWithValue("@last_name", recipient.LastName);
                cmd.Parameters.AddWithValue("@date_of_birth", recipient.DateOfBirth);
                cmd.Parameters.AddWithValue("@blood_type", recipient.BloodType);
                cmd.Parameters.AddWithValue("@hla_type", recipient.HlaType);
                cmd.Parameters.AddWithValue("@medical_history", recipient.MedicalHistory ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@hospital", recipient.Hospital);
                cmd.Parameters.AddWithValue("@country", recipient.Country);
                cmd.Parameters.AddWithValue("@registered_date", recipient.RegisteredDate);
                cmd.Parameters.AddWithValue("@urgency_score", recipient.UrgencyScore);
                cmd.Parameters.AddWithValue("@waiting_since", recipient.WaitingSince);
                cmd.Parameters.AddWithValue("@status", recipient.Status.ToString());

                recipientId = ((Guid)await cmd.ExecuteScalarAsync()).ToString();

                // Insert organ requests
                if (recipient.OrganRequests != null && recipient.OrganRequests.Any())
                {
                    const string insertRequestQuery = @"
                        INSERT INTO organ_requests (
                            id, recipient_id, organ_type, request_date, medical_reason, 
                            priority, status)
                        VALUES (
                            @id, @recipient_id, @organ_type, @request_date, @medical_reason, 
                            @priority, @status)";

                    foreach (var request in recipient.OrganRequests)
                    {
                        var requestId = string.IsNullOrEmpty(request.Id)
                            ? Guid.NewGuid().ToString()
                            : request.Id;

                        using var requestCmd = new NpgsqlCommand(insertRequestQuery, connection, transaction);
                        requestCmd.Parameters.AddWithValue("@id", Guid.Parse(requestId));
                        requestCmd.Parameters.AddWithValue("@recipient_id", Guid.Parse(recipientId));
                        requestCmd.Parameters.AddWithValue("@organ_type", request.OrganType.ToString());
                        requestCmd.Parameters.AddWithValue("@request_date", request.RequestDate);
                        requestCmd.Parameters.AddWithValue("@medical_reason", request.MedicalReason);
                        requestCmd.Parameters.AddWithValue("@priority", request.Priority);
                        requestCmd.Parameters.AddWithValue("@status", request.Status.ToString());

                        await requestCmd.ExecuteNonQueryAsync();
                    }
                }
            });

            return recipientId;
        }

        public async Task UpdateRecipientAsync(Recipient recipient)
        {
            // Use a transaction to update both recipient and organ requests
            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                const string updateRecipientQuery = @"
                    UPDATE recipients
                    SET first_name = @first_name,
                        last_name = @last_name,
                        date_of_birth = @date_of_birth,
                        blood_type = @blood_type,
                        hla_type = @hla_type,
                        medical_history = @medical_history,
                        hospital = @hospital,
                        country = @country,
                        urgency_score = @urgency_score,
                        waiting_since = @waiting_since,
                        status = @status
                    WHERE id = @id";

                using var cmd = new NpgsqlCommand(updateRecipientQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@id", Guid.Parse(recipient.Id));
                cmd.Parameters.AddWithValue("@first_name", recipient.FirstName);
                cmd.Parameters.AddWithValue("@last_name", recipient.LastName);
                cmd.Parameters.AddWithValue("@date_of_birth", recipient.DateOfBirth);
                cmd.Parameters.AddWithValue("@blood_type", recipient.BloodType);
                cmd.Parameters.AddWithValue("@hla_type", recipient.HlaType);
                cmd.Parameters.AddWithValue("@medical_history", recipient.MedicalHistory ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@hospital", recipient.Hospital);
                cmd.Parameters.AddWithValue("@country", recipient.Country);
                cmd.Parameters.AddWithValue("@urgency_score", recipient.UrgencyScore);
                cmd.Parameters.AddWithValue("@waiting_since", recipient.WaitingSince);
                cmd.Parameters.AddWithValue("@status", recipient.Status.ToString());

                await cmd.ExecuteNonQueryAsync();

                // Get current organ requests to identify which ones to add, update, or delete
                const string getRequestsQuery = "SELECT id FROM organ_requests WHERE recipient_id = @recipient_id";
                using var getCmd = new NpgsqlCommand(getRequestsQuery, connection, transaction);
                getCmd.Parameters.AddWithValue("@recipient_id", Guid.Parse(recipient.Id));

                using var reader = await getCmd.ExecuteReaderAsync();
                var existingRequestIds = new List<string>();
                while (await reader.ReadAsync())
                {
                    existingRequestIds.Add(reader.GetGuid(0).ToString());
                }
                reader.Close();

                // Identify which requests to add, update, or delete
                var currentRequestIds = recipient.OrganRequests?.Select(r => r.Id).ToList() ?? new List<string>();
                var requestsToDelete = existingRequestIds.Where(id => !currentRequestIds.Contains(id)).ToList();
                var requestsToAddOrUpdate = recipient.OrganRequests ?? new List<OrganRequest>();

                // Delete removed requests
                if (requestsToDelete.Any())
                {
                    const string deleteRequestQuery = "DELETE FROM organ_requests WHERE id = @id";
                    foreach (var id in requestsToDelete)
                    {
                        using var deleteCmd = new NpgsqlCommand(deleteRequestQuery, connection, transaction);
                        deleteCmd.Parameters.AddWithValue("@id", Guid.Parse(id));
                        await deleteCmd.ExecuteNonQueryAsync();
                    }
                }

                // Add or update current requests
                foreach (var request in requestsToAddOrUpdate)
                {
                    var isNew = string.IsNullOrEmpty(request.Id) || !existingRequestIds.Contains(request.Id);

                    if (isNew)
                    {
                        // Insert new request
                        const string insertRequestQuery = @"
                            INSERT INTO organ_requests (
                                id, recipient_id, organ_type, request_date, medical_reason, 
                                priority, status)
                            VALUES (
                                @id, @recipient_id, @organ_type, @request_date, @medical_reason, 
                                @priority, @status)";

                        var requestId = string.IsNullOrEmpty(request.Id)
                            ? Guid.NewGuid().ToString()
                            : request.Id;

                        using var insertCmd = new NpgsqlCommand(insertRequestQuery, connection, transaction);
                        insertCmd.Parameters.AddWithValue("@id", Guid.Parse(requestId));
                        insertCmd.Parameters.AddWithValue("@recipient_id", Guid.Parse(recipient.Id));
                        insertCmd.Parameters.AddWithValue("@organ_type", request.OrganType.ToString());
                        insertCmd.Parameters.AddWithValue("@request_date", request.RequestDate);
                        insertCmd.Parameters.AddWithValue("@medical_reason", request.MedicalReason);
                        insertCmd.Parameters.AddWithValue("@priority", request.Priority);
                        insertCmd.Parameters.AddWithValue("@status", request.Status.ToString());

                        await insertCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Update existing request
                        const string updateRequestQuery = @"
                            UPDATE organ_requests
                            SET organ_type = @organ_type,
                                request_date = @request_date,
                                medical_reason = @medical_reason,
                                priority = @priority,
                                status = @status
                            WHERE id = @id";

                        using var updateCmd = new NpgsqlCommand(updateRequestQuery, connection, transaction);
                        updateCmd.Parameters.AddWithValue("@id", Guid.Parse(request.Id));
                        updateCmd.Parameters.AddWithValue("@organ_type", request.OrganType.ToString());
                        updateCmd.Parameters.AddWithValue("@request_date", request.RequestDate);
                        updateCmd.Parameters.AddWithValue("@medical_reason", request.MedicalReason);
                        updateCmd.Parameters.AddWithValue("@priority", request.Priority);
                        updateCmd.Parameters.AddWithValue("@status", request.Status.ToString());

                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
            });
        }

        public async Task DeleteRecipientAsync(string id)
        {
            // No need to delete organ requests separately due to CASCADE delete
            const string query = "DELETE FROM recipients WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
