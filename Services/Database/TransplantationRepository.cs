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
    /// Repository for Transplantation-related database operations
    /// </summary>
    public class TransplantationRepository(PostgresConnectionHelper db)
    {
        private readonly PostgresConnectionHelper _db = db ?? throw new ArgumentNullException(nameof(db));

        public async Task<List<Transplantation>> GetAllTransplantationsAsync()
        {
            const string query = @"
                SELECT t.id, t.match_id, t.organ_id, t.donor_id, t.recipient_id, 
                       t.hospital, t.surgeon_name, t.scheduled_date, t.actual_start_date, 
                       t.actual_end_date, t.status
                FROM transplantations t
                ORDER BY t.scheduled_date DESC";

            var transplantations = await _db.ExecuteQueryAsync(query, reader => new Transplantation
            {
                Id = reader.GetGuid(0).ToString(),
                MatchId = reader.GetGuid(1).ToString(),
                OrganId = reader.GetGuid(2).ToString(),
                DonorId = reader.GetGuid(3).ToString(),
                RecipientId = reader.GetGuid(4).ToString(),
                Hospital = reader.GetString(5),
                SurgeonName = reader.GetString(6),
                ScheduledDate = reader.GetDateTime(7),
                ActualStartDate = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8),
                ActualEndDate = reader.IsDBNull(9) ? null : (DateTime?)reader.GetDateTime(9),
                Status = Enum.Parse<TransplantationStatus>(reader.GetString(10))
            });

            // Load outcomes for each transplantation
            foreach (var transplantation in transplantations)
            {
                transplantation.Outcomes = await GetTransplantationOutcomesAsync(transplantation.Id);
            }

            return transplantations;
        }

        public async Task<Transplantation> GetTransplantationByIdAsync(string id)
        {
            const string query = @"
                SELECT t.id, t.match_id, t.organ_id, t.donor_id, t.recipient_id, 
                       t.hospital, t.surgeon_name, t.scheduled_date, t.actual_start_date, 
                       t.actual_end_date, t.status
                FROM transplantations t
                WHERE t.id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            var transplantation = await _db.ExecuteQuerySingleAsync(query, reader => new Transplantation
            {
                Id = reader.GetGuid(0).ToString(),
                MatchId = reader.GetGuid(1).ToString(),
                OrganId = reader.GetGuid(2).ToString(),
                DonorId = reader.GetGuid(3).ToString(),
                RecipientId = reader.GetGuid(4).ToString(),
                Hospital = reader.GetString(5),
                SurgeonName = reader.GetString(6),
                ScheduledDate = reader.GetDateTime(7),
                ActualStartDate = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8),
                ActualEndDate = reader.IsDBNull(9) ? null : (DateTime?)reader.GetDateTime(9),
                Status = Enum.Parse<TransplantationStatus>(reader.GetString(10))
            }, parameters);

            if (transplantation != null)
            {
                transplantation.Outcomes = await GetTransplantationOutcomesAsync(transplantation.Id);
            }

            return transplantation;
        }

        private async Task<List<TransplantationOutcome>> GetTransplantationOutcomesAsync(string transplantationId)
        {
            const string query = @"
                SELECT id, outcome_type, assessment_date, is_positive, notes, assessed_by, days_after_transplant
                FROM transplantation_outcomes
                WHERE transplantation_id = @transplantation_id
                ORDER BY assessment_date";

            var parameters = new Dictionary<string, object>
            {
                { "@transplantation_id", Guid.Parse(transplantationId) }
            };

            return await _db.ExecuteQueryAsync(query, reader => new TransplantationOutcome
            {
                Id = reader.GetGuid(0).ToString(),
                TransplantationId = transplantationId,
                Type = Enum.Parse<OutcomeType>(reader.GetString(1)),
                AssessmentDate = reader.GetDateTime(2),
                IsPositive = reader.GetBoolean(3),
                Notes = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                AssessedBy = reader.GetString(5),
                DaysAfterTransplant = reader.GetInt32(6)
            }, parameters);
        }

        public async Task<string> AddTransplantationAsync(Transplantation transplantation)
        {
            // Use a transaction to ensure both transplantation and outcomes are added together
            string transplantationId = null;

            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                const string insertTransplantationQuery = @"
                    INSERT INTO transplantations (
                        id, match_id, organ_id, donor_id, recipient_id, 
                        hospital, surgeon_name, scheduled_date, actual_start_date, 
                        actual_end_date, status)
                    VALUES (
                        @id, @match_id, @organ_id, @donor_id, @recipient_id, 
                        @hospital, @surgeon_name, @scheduled_date, @actual_start_date, 
                        @actual_end_date, @status)
                    RETURNING id";

                var id = transplantation.Id;
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();
                }

                using var cmd = new NpgsqlCommand(insertTransplantationQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@id", Guid.Parse(id));
                cmd.Parameters.AddWithValue("@match_id", Guid.Parse(transplantation.MatchId));
                cmd.Parameters.AddWithValue("@organ_id", Guid.Parse(transplantation.OrganId));
                cmd.Parameters.AddWithValue("@donor_id", Guid.Parse(transplantation.DonorId));
                cmd.Parameters.AddWithValue("@recipient_id", Guid.Parse(transplantation.RecipientId));
                cmd.Parameters.AddWithValue("@hospital", transplantation.Hospital);
                cmd.Parameters.AddWithValue("@surgeon_name", transplantation.SurgeonName);
                cmd.Parameters.AddWithValue("@scheduled_date", transplantation.ScheduledDate);
                cmd.Parameters.AddWithValue("@actual_start_date",
                    transplantation.ActualStartDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@actual_end_date",
                    transplantation.ActualEndDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", transplantation.Status.ToString());

                transplantationId = ((Guid)await cmd.ExecuteScalarAsync()).ToString();

                // Insert outcomes if any
                if (transplantation.Outcomes != null && transplantation.Outcomes.Any())
                {
                    const string insertOutcomeQuery = @"
                        INSERT INTO transplantation_outcomes (
                            id, transplantation_id, outcome_type, assessment_date, 
                            is_positive, notes, assessed_by, days_after_transplant)
                        VALUES (
                            @id, @transplantation_id, @outcome_type, @assessment_date, 
                            @is_positive, @notes, @assessed_by, @days_after_transplant)";

                    foreach (var outcome in transplantation.Outcomes)
                    {
                        var outcomeId = string.IsNullOrEmpty(outcome.Id)
                            ? Guid.NewGuid().ToString()
                            : outcome.Id;

                        using var outcomeCmd = new NpgsqlCommand(insertOutcomeQuery, connection, transaction);
                        outcomeCmd.Parameters.AddWithValue("@id", Guid.Parse(outcomeId));
                        outcomeCmd.Parameters.AddWithValue("@transplantation_id", Guid.Parse(transplantationId));
                        outcomeCmd.Parameters.AddWithValue("@outcome_type", outcome.Type.ToString());
                        outcomeCmd.Parameters.AddWithValue("@assessment_date", outcome.AssessmentDate);
                        outcomeCmd.Parameters.AddWithValue("@is_positive", outcome.IsPositive);
                        outcomeCmd.Parameters.AddWithValue("@notes", outcome.Notes ?? (object)DBNull.Value);
                        outcomeCmd.Parameters.AddWithValue("@assessed_by", outcome.AssessedBy);
                        outcomeCmd.Parameters.AddWithValue("@days_after_transplant", outcome.DaysAfterTransplant);

                        await outcomeCmd.ExecuteNonQueryAsync();
                    }
                }
            });

            return transplantationId;
        }

        public async Task UpdateTransplantationAsync(Transplantation transplantation)
        {
            // Use a transaction to update both transplantation and outcomes
            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                const string updateTransplantationQuery = @"
                    UPDATE transplantations
                    SET match_id = @match_id,
                        organ_id = @organ_id,
                        donor_id = @donor_id,
                        recipient_id = @recipient_id,
                        hospital = @hospital,
                        surgeon_name = @surgeon_name,
                        scheduled_date = @scheduled_date,
                        actual_start_date = @actual_start_date,
                        actual_end_date = @actual_end_date,
                        status = @status
                    WHERE id = @id";

                using var cmd = new NpgsqlCommand(updateTransplantationQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@id", Guid.Parse(transplantation.Id));
                cmd.Parameters.AddWithValue("@match_id", Guid.Parse(transplantation.MatchId));
                cmd.Parameters.AddWithValue("@organ_id", Guid.Parse(transplantation.OrganId));
                cmd.Parameters.AddWithValue("@donor_id", Guid.Parse(transplantation.DonorId));
                cmd.Parameters.AddWithValue("@recipient_id", Guid.Parse(transplantation.RecipientId));
                cmd.Parameters.AddWithValue("@hospital", transplantation.Hospital);
                cmd.Parameters.AddWithValue("@surgeon_name", transplantation.SurgeonName);
                cmd.Parameters.AddWithValue("@scheduled_date", transplantation.ScheduledDate);
                cmd.Parameters.AddWithValue("@actual_start_date",
                    transplantation.ActualStartDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@actual_end_date",
                    transplantation.ActualEndDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", transplantation.Status.ToString());

                await cmd.ExecuteNonQueryAsync();

                // Get current outcomes to identify which ones to add, update, or delete
                const string getOutcomesQuery = "SELECT id FROM transplantation_outcomes WHERE transplantation_id = @transplantation_id";
                using var getCmd = new NpgsqlCommand(getOutcomesQuery, connection, transaction);
                getCmd.Parameters.AddWithValue("@transplantation_id", Guid.Parse(transplantation.Id));

                using var reader = await getCmd.ExecuteReaderAsync();
                var existingOutcomeIds = new List<string>();
                while (await reader.ReadAsync())
                {
                    existingOutcomeIds.Add(reader.GetGuid(0).ToString());
                }
                reader.Close();

                // Identify which outcomes to add, update, or delete
                var currentOutcomeIds = transplantation.Outcomes?.Select(o => o.Id).ToList() ?? new List<string>();
                var outcomesToDelete = existingOutcomeIds.Where(id => !currentOutcomeIds.Contains(id)).ToList();
                var outcomesToAddOrUpdate = transplantation.Outcomes ?? new List<TransplantationOutcome>();

                // Delete removed outcomes
                if (outcomesToDelete.Any())
                {
                    const string deleteOutcomeQuery = "DELETE FROM transplantation_outcomes WHERE id = @id";
                    foreach (var id in outcomesToDelete)
                    {
                        using var deleteCmd = new NpgsqlCommand(deleteOutcomeQuery, connection, transaction);
                        deleteCmd.Parameters.AddWithValue("@id", Guid.Parse(id));
                        await deleteCmd.ExecuteNonQueryAsync();
                    }
                }

                // Add or update current outcomes
                foreach (var outcome in outcomesToAddOrUpdate)
                {
                    var isNew = string.IsNullOrEmpty(outcome.Id) || !existingOutcomeIds.Contains(outcome.Id);

                    if (isNew)
                    {
                        // Insert new outcome
                        const string insertOutcomeQuery = @"
                            INSERT INTO transplantation_outcomes (
                                id, transplantation_id, outcome_type, assessment_date, 
                                is_positive, notes, assessed_by, days_after_transplant)
                            VALUES (
                                @id, @transplantation_id, @outcome_type, @assessment_date, 
                                @is_positive, @notes, @assessed_by, @days_after_transplant)";

                        var outcomeId = string.IsNullOrEmpty(outcome.Id)
                            ? Guid.NewGuid().ToString()
                            : outcome.Id;

                        using var insertCmd = new NpgsqlCommand(insertOutcomeQuery, connection, transaction);
                        insertCmd.Parameters.AddWithValue("@id", Guid.Parse(outcomeId));
                        insertCmd.Parameters.AddWithValue("@transplantation_id", Guid.Parse(transplantation.Id));
                        insertCmd.Parameters.AddWithValue("@outcome_type", outcome.Type.ToString());
                        insertCmd.Parameters.AddWithValue("@assessment_date", outcome.AssessmentDate);
                        insertCmd.Parameters.AddWithValue("@is_positive", outcome.IsPositive);
                        insertCmd.Parameters.AddWithValue("@notes", outcome.Notes ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@assessed_by", outcome.AssessedBy);
                        insertCmd.Parameters.AddWithValue("@days_after_transplant", outcome.DaysAfterTransplant);

                        await insertCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Update existing outcome
                        const string updateOutcomeQuery = @"
                            UPDATE transplantation_outcomes
                            SET outcome_type = @outcome_type,
                                assessment_date = @assessment_date,
                                is_positive = @is_positive,
                                notes = @notes,
                                assessed_by = @assessed_by,
                                days_after_transplant = @days_after_transplant
                            WHERE id = @id";

                        using var updateCmd = new NpgsqlCommand(updateOutcomeQuery, connection, transaction);
                        updateCmd.Parameters.AddWithValue("@id", Guid.Parse(outcome.Id));
                        updateCmd.Parameters.AddWithValue("@outcome_type", outcome.Type.ToString());
                        updateCmd.Parameters.AddWithValue("@assessment_date", outcome.AssessmentDate);
                        updateCmd.Parameters.AddWithValue("@is_positive", outcome.IsPositive);
                        updateCmd.Parameters.AddWithValue("@notes", outcome.Notes ?? (object)DBNull.Value);
                        updateCmd.Parameters.AddWithValue("@assessed_by", outcome.AssessedBy);
                        updateCmd.Parameters.AddWithValue("@days_after_transplant", outcome.DaysAfterTransplant);

                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
            });
        }

        public async Task DeleteTransplantationAsync(string id)
        {
            // No need to delete outcomes separately due to CASCADE delete
            const string query = "DELETE FROM transplantations WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
