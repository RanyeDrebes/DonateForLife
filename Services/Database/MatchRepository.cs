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
    /// Repository for Match-related database operations
    /// </summary>
    public class MatchRepository(PostgresConnectionHelper db)
    {
        private readonly PostgresConnectionHelper _db = db ?? throw new ArgumentNullException(nameof(db));

        public async Task<List<Match>> GetAllMatchesAsync()
        {
            const string query = @"
                SELECT m.id, m.organ_id, m.donor_id, m.recipient_id, m.match_date, 
                       m.compatibility_score, m.ranking_score, m.matching_algorithm_version, 
                       m.matching_criteria, m.status, m.approval_date, m.approved_by
                FROM matches m
                ORDER BY m.match_date DESC";

            var matches = await _db.ExecuteQueryAsync(query, reader => new Match
            {
                Id = reader.GetGuid(0).ToString(),
                OrganId = reader.GetGuid(1).ToString(),
                DonorId = reader.GetGuid(2).ToString(),
                RecipientId = reader.GetGuid(3).ToString(),
                MatchDate = reader.GetDateTime(4),
                CompatibilityScore = reader.GetDouble(5),
                RankingScore = reader.GetDouble(6),
                MatchingAlgorithmVersion = reader.GetString(7),
                MatchingCriteria = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                Status = Enum.Parse<MatchStatus>(reader.GetString(9)),
                ApprovalDate = reader.IsDBNull(10) ? null : (DateTime?)reader.GetDateTime(10),
                ApprovedBy = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
            });

            // Load matching factors for each match
            foreach (var match in matches)
            {
                match.MatchingFactors = await GetMatchingFactorsForMatchAsync(match.Id);
            }

            return matches;
        }

        public async Task<Match> GetMatchByIdAsync(string id)
        {
            const string query = @"
                SELECT m.id, m.organ_id, m.donor_id, m.recipient_id, m.match_date, 
                       m.compatibility_score, m.ranking_score, m.matching_algorithm_version, 
                       m.matching_criteria, m.status, m.approval_date, m.approved_by
                FROM matches m
                WHERE m.id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            var match = await _db.ExecuteQuerySingleAsync(query, reader => new Match
            {
                Id = reader.GetGuid(0).ToString(),
                OrganId = reader.GetGuid(1).ToString(),
                DonorId = reader.GetGuid(2).ToString(),
                RecipientId = reader.GetGuid(3).ToString(),
                MatchDate = reader.GetDateTime(4),
                CompatibilityScore = reader.GetDouble(5),
                RankingScore = reader.GetDouble(6),
                MatchingAlgorithmVersion = reader.GetString(7),
                MatchingCriteria = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                Status = Enum.Parse<MatchStatus>(reader.GetString(9)),
                ApprovalDate = reader.IsDBNull(10) ? null : (DateTime?)reader.GetDateTime(10),
                ApprovedBy = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
            }, parameters);

            if (match != null)
            {
                match.MatchingFactors = await GetMatchingFactorsForMatchAsync(match.Id);
            }

            return match;
        }

        private async Task<List<MatchFactor>> GetMatchingFactorsForMatchAsync(string matchId)
        {
            const string query = @"
                SELECT id, factor_name, weight, score, description
                FROM match_factors
                WHERE match_id = @match_id";

            var parameters = new Dictionary<string, object>
            {
                { "@match_id", Guid.Parse(matchId) }
            };

            return await _db.ExecuteQueryAsync(query, reader => new MatchFactor
            {
                FactorName = reader.GetString(1),
                Weight = reader.GetDouble(2),
                Score = reader.GetDouble(3),
                Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            }, parameters);
        }

        public async Task<string> AddMatchAsync(Match match)
        {
            // Use a transaction to ensure both match and factors are added together
            string matchId = null;

            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                const string insertMatchQuery = @"
                    INSERT INTO matches (
                        id, organ_id, donor_id, recipient_id, match_date, 
                        compatibility_score, ranking_score, matching_algorithm_version, 
                        matching_criteria, status, approval_date, approved_by)
                    VALUES (
                        @id, @organ_id, @donor_id, @recipient_id, @match_date, 
                        @compatibility_score, @ranking_score, @matching_algorithm_version, 
                        @matching_criteria, @status, @approval_date, @approved_by)
                    RETURNING id";

                var id = match.Id;
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();
                }

                using var cmd = new NpgsqlCommand(insertMatchQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@id", Guid.Parse(id));
                cmd.Parameters.AddWithValue("@organ_id", Guid.Parse(match.OrganId));
                cmd.Parameters.AddWithValue("@donor_id", Guid.Parse(match.DonorId));
                cmd.Parameters.AddWithValue("@recipient_id", Guid.Parse(match.RecipientId));
                cmd.Parameters.AddWithValue("@match_date", match.MatchDate);
                cmd.Parameters.AddWithValue("@compatibility_score", match.CompatibilityScore);
                cmd.Parameters.AddWithValue("@ranking_score", match.RankingScore);
                cmd.Parameters.AddWithValue("@matching_algorithm_version", match.MatchingAlgorithmVersion);
                cmd.Parameters.AddWithValue("@matching_criteria", match.MatchingCriteria ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", match.Status.ToString());
                cmd.Parameters.AddWithValue("@approval_date", match.ApprovalDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@approved_by", match.ApprovedBy ?? (object)DBNull.Value);

                matchId = ((Guid)await cmd.ExecuteScalarAsync()).ToString();

                // Insert matching factors
                if (match.MatchingFactors != null && match.MatchingFactors.Any())
                {
                    const string insertFactorQuery = @"
                        INSERT INTO match_factors (
                            id, match_id, factor_name, weight, score, description)
                        VALUES (
                            @id, @match_id, @factor_name, @weight, @score, @description)";

                    foreach (var factor in match.MatchingFactors)
                    {
                        var factorId = Guid.NewGuid();

                        using var factorCmd = new NpgsqlCommand(insertFactorQuery, connection, transaction);
                        factorCmd.Parameters.AddWithValue("@id", factorId);
                        factorCmd.Parameters.AddWithValue("@match_id", Guid.Parse(matchId));
                        factorCmd.Parameters.AddWithValue("@factor_name", factor.FactorName);
                        factorCmd.Parameters.AddWithValue("@weight", factor.Weight);
                        factorCmd.Parameters.AddWithValue("@score", factor.Score);
                        factorCmd.Parameters.AddWithValue("@description", factor.Description ?? (object)DBNull.Value);

                        await factorCmd.ExecuteNonQueryAsync();
                    }
                }
            });

            return matchId;
        }

        public async Task UpdateMatchAsync(Match match)
        {
            // Use a transaction to update both match and factors
            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                const string updateMatchQuery = @"
                    UPDATE matches
                    SET organ_id = @organ_id,
                        donor_id = @donor_id,
                        recipient_id = @recipient_id,
                        match_date = @match_date,
                        compatibility_score = @compatibility_score,
                        ranking_score = @ranking_score,
                        matching_algorithm_version = @matching_algorithm_version,
                        matching_criteria = @matching_criteria,
                        status = @status,
                        approval_date = @approval_date,
                        approved_by = @approved_by
                    WHERE id = @id";

                using var cmd = new NpgsqlCommand(updateMatchQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@id", Guid.Parse(match.Id));
                cmd.Parameters.AddWithValue("@organ_id", Guid.Parse(match.OrganId));
                cmd.Parameters.AddWithValue("@donor_id", Guid.Parse(match.DonorId));
                cmd.Parameters.AddWithValue("@recipient_id", Guid.Parse(match.RecipientId));
                cmd.Parameters.AddWithValue("@match_date", match.MatchDate);
                cmd.Parameters.AddWithValue("@compatibility_score", match.CompatibilityScore);
                cmd.Parameters.AddWithValue("@ranking_score", match.RankingScore);
                cmd.Parameters.AddWithValue("@matching_algorithm_version", match.MatchingAlgorithmVersion);
                cmd.Parameters.AddWithValue("@matching_criteria", match.MatchingCriteria ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", match.Status.ToString());
                cmd.Parameters.AddWithValue("@approval_date", match.ApprovalDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@approved_by", match.ApprovedBy ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                // Delete existing factors and insert updated ones
                const string deleteFactorsQuery = "DELETE FROM match_factors WHERE match_id = @match_id";
                using var deleteCmd = new NpgsqlCommand(deleteFactorsQuery, connection, transaction);
                deleteCmd.Parameters.AddWithValue("@match_id", Guid.Parse(match.Id));
                await deleteCmd.ExecuteNonQueryAsync();

                // Insert updated matching factors
                if (match.MatchingFactors != null && match.MatchingFactors.Any())
                {
                    const string insertFactorQuery = @"
                        INSERT INTO match_factors (
                            id, match_id, factor_name, weight, score, description)
                        VALUES (
                            @id, @match_id, @factor_name, @weight, @score, @description)";

                    foreach (var factor in match.MatchingFactors)
                    {
                        var factorId = Guid.NewGuid();

                        using var factorCmd = new NpgsqlCommand(insertFactorQuery, connection, transaction);
                        factorCmd.Parameters.AddWithValue("@id", factorId);
                        factorCmd.Parameters.AddWithValue("@match_id", Guid.Parse(match.Id));
                        factorCmd.Parameters.AddWithValue("@factor_name", factor.FactorName);
                        factorCmd.Parameters.AddWithValue("@weight", factor.Weight);
                        factorCmd.Parameters.AddWithValue("@score", factor.Score);
                        factorCmd.Parameters.AddWithValue("@description", factor.Description ?? (object)DBNull.Value);

                        await factorCmd.ExecuteNonQueryAsync();
                    }
                }
            });
        }

        public async Task DeleteMatchAsync(string id)
        {
            // No need to delete factors separately due to CASCADE delete
            const string query = "DELETE FROM matches WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
