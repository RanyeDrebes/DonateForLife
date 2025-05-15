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
    /// Repository for Organ-related database operations
    /// </summary>
    public class OrganRepository(PostgresConnectionHelper db)
    {
        private readonly PostgresConnectionHelper _db = db ?? throw new ArgumentNullException(nameof(db));

        public async Task<List<Organ>> GetAllOrgansAsync()
        {
            const string query = @"
                SELECT o.id, o.donor_id, o.organ_type, o.blood_type, o.hla_type, 
                       o.harvested_time, o.expiry_time, o.storage_location, o.medical_notes, o.status,
                       q.functionality_score, q.structural_integrity_score, q.risk_score, 
                       q.assessment_notes, q.assessment_time, q.assessed_by
                FROM organs o
                LEFT JOIN organ_quality_assessments q ON o.id = q.organ_id
                ORDER BY o.harvested_time DESC";

            return await _db.ExecuteQueryAsync(query, reader =>
            {
                var organType = Enum.Parse<OrganType>(reader.GetString(2));
                var organ = new Organ(organType)
                {
                    Id = reader.GetGuid(0).ToString(),
                    DonorId = reader.GetGuid(1).ToString(),
                    BloodType = reader.GetString(3),
                    HlaType = reader.GetString(4),
                    HarvestedTime = reader.GetDateTime(5),
                    ExpiryTime = reader.GetDateTime(6),
                    StorageLocation = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    MedicalNotes = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Status = Enum.Parse<OrganStatus>(reader.GetString(9))
                };

                // If there's quality assessment data
                if (!reader.IsDBNull(10))
                {
                    organ.Quality = new QualityAssessment
                    {
                        FunctionalityScore = reader.GetInt32(10),
                        StructuralIntegrityScore = reader.GetInt32(11),
                        RiskScore = reader.GetInt32(12),
                        AssessmentNotes = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                        AssessmentTime = reader.GetDateTime(14),
                        AssessedBy = reader.GetString(15)
                    };
                }

                return organ;
            });
        }

        public async Task<Organ> GetOrganByIdAsync(string id)
        {
            const string query = @"
                SELECT o.id, o.donor_id, o.organ_type, o.blood_type, o.hla_type, 
                       o.harvested_time, o.expiry_time, o.storage_location, o.medical_notes, o.status,
                       q.functionality_score, q.structural_integrity_score, q.risk_score, 
                       q.assessment_notes, q.assessment_time, q.assessed_by
                FROM organs o
                LEFT JOIN organ_quality_assessments q ON o.id = q.organ_id
                WHERE o.id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            return await _db.ExecuteQuerySingleAsync(query, reader =>
            {
                var organType = Enum.Parse<OrganType>(reader.GetString(2));
                var organ = new Organ(organType)
                {
                    Id = reader.GetGuid(0).ToString(),
                    DonorId = reader.GetGuid(1).ToString(),
                    BloodType = reader.GetString(3),
                    HlaType = reader.GetString(4),
                    HarvestedTime = reader.GetDateTime(5),
                    ExpiryTime = reader.GetDateTime(6),
                    StorageLocation = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    MedicalNotes = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Status = Enum.Parse<OrganStatus>(reader.GetString(9))
                };

                // If there's quality assessment data
                if (!reader.IsDBNull(10))
                {
                    organ.Quality = new QualityAssessment
                    {
                        FunctionalityScore = reader.GetInt32(10),
                        StructuralIntegrityScore = reader.GetInt32(11),
                        RiskScore = reader.GetInt32(12),
                        AssessmentNotes = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                        AssessmentTime = reader.GetDateTime(14),
                        AssessedBy = reader.GetString(15)
                    };
                }

                return organ;
            }, parameters);
        }

        public async Task<string> AddOrganAsync(Organ organ)
        {
            // Use a transaction to ensure both organ and quality assessment are added together
            string organId = null;

            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                const string insertOrganQuery = @"
                    INSERT INTO organs (
                        id, donor_id, organ_type, blood_type, hla_type, 
                        harvested_time, expiry_time, storage_location, medical_notes, status)
                    VALUES (
                        @id, @donor_id, @organ_type, @blood_type, @hla_type, 
                        @harvested_time, @expiry_time, @storage_location, @medical_notes, @status)
                    RETURNING id";

                var id = organ.Id;
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();
                }

                using var cmd = new NpgsqlCommand(insertOrganQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@id", Guid.Parse(id));
                cmd.Parameters.AddWithValue("@donor_id", Guid.Parse(organ.DonorId));
                cmd.Parameters.AddWithValue("@organ_type", organ.Type.ToString());
                cmd.Parameters.AddWithValue("@blood_type", organ.BloodType);
                cmd.Parameters.AddWithValue("@hla_type", organ.HlaType);
                cmd.Parameters.AddWithValue("@harvested_time", organ.HarvestedTime);
                cmd.Parameters.AddWithValue("@expiry_time", organ.ExpiryTime);
                cmd.Parameters.AddWithValue("@storage_location", organ.StorageLocation ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@medical_notes", organ.MedicalNotes ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", organ.Status.ToString());

                organId = ((Guid)await cmd.ExecuteScalarAsync()).ToString();

                // Insert quality assessment if it exists
                if (organ.Quality != null)
                {
                    const string insertQualityQuery = @"
                        INSERT INTO organ_quality_assessments (
                            organ_id, functionality_score, structural_integrity_score, risk_score, 
                            assessment_notes, assessment_time, assessed_by)
                        VALUES (
                            @organ_id, @functionality_score, @structural_integrity_score, @risk_score, 
                            @assessment_notes, @assessment_time, @assessed_by)";

                    using var qualityCmd = new NpgsqlCommand(insertQualityQuery, connection, transaction);
                    qualityCmd.Parameters.AddWithValue("@organ_id", Guid.Parse(organId));
                    qualityCmd.Parameters.AddWithValue("@functionality_score", organ.Quality.FunctionalityScore);
                    qualityCmd.Parameters.AddWithValue("@structural_integrity_score", organ.Quality.StructuralIntegrityScore);
                    qualityCmd.Parameters.AddWithValue("@risk_score", organ.Quality.RiskScore);
                    qualityCmd.Parameters.AddWithValue("@assessment_notes",
                        organ.Quality.AssessmentNotes ?? (object)DBNull.Value);
                    qualityCmd.Parameters.AddWithValue("@assessment_time", organ.Quality.AssessmentTime);
                    qualityCmd.Parameters.AddWithValue("@assessed_by", organ.Quality.AssessedBy);

                    await qualityCmd.ExecuteNonQueryAsync();
                }
            });

            return organId;
        }

        public async Task UpdateOrganAsync(Organ organ)
        {
            // Use a transaction to update both organ and quality assessment
            await _db.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                const string updateOrganQuery = @"
                    UPDATE organs
                    SET donor_id = @donor_id,
                        organ_type = @organ_type,
                        blood_type = @blood_type,
                        hla_type = @hla_type,
                        harvested_time = @harvested_time,
                        expiry_time = @expiry_time,
                        storage_location = @storage_location,
                        medical_notes = @medical_notes,
                        status = @status
                    WHERE id = @id";

                using var cmd = new NpgsqlCommand(updateOrganQuery, connection, transaction);
                cmd.Parameters.AddWithValue("@id", Guid.Parse(organ.Id));
                cmd.Parameters.AddWithValue("@donor_id", Guid.Parse(organ.DonorId));
                cmd.Parameters.AddWithValue("@organ_type", organ.Type.ToString());
                cmd.Parameters.AddWithValue("@blood_type", organ.BloodType);
                cmd.Parameters.AddWithValue("@hla_type", organ.HlaType);
                cmd.Parameters.AddWithValue("@harvested_time", organ.HarvestedTime);
                cmd.Parameters.AddWithValue("@expiry_time", organ.ExpiryTime);
                cmd.Parameters.AddWithValue("@storage_location", organ.StorageLocation ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@medical_notes", organ.MedicalNotes ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", organ.Status.ToString());

                await cmd.ExecuteNonQueryAsync();

                // Check if quality assessment exists
                const string checkQualityQuery = @"
                    SELECT COUNT(*) FROM organ_quality_assessments WHERE organ_id = @organ_id";
                using var checkCmd = new NpgsqlCommand(checkQualityQuery, connection, transaction);
                checkCmd.Parameters.AddWithValue("@organ_id", Guid.Parse(organ.Id));
                var count = (long)await checkCmd.ExecuteScalarAsync();

                if (organ.Quality != null)
                {
                    if (count > 0)
                    {
                        // Update existing quality assessment
                        const string updateQualityQuery = @"
                            UPDATE organ_quality_assessments
                            SET functionality_score = @functionality_score,
                                structural_integrity_score = @structural_integrity_score,
                                risk_score = @risk_score,
                                assessment_notes = @assessment_notes,
                                assessment_time = @assessment_time,
                                assessed_by = @assessed_by
                            WHERE organ_id = @organ_id";

                        using var updateQualityCmd = new NpgsqlCommand(updateQualityQuery, connection, transaction);
                        updateQualityCmd.Parameters.AddWithValue("@organ_id", Guid.Parse(organ.Id));
                        updateQualityCmd.Parameters.AddWithValue("@functionality_score", organ.Quality.FunctionalityScore);
                        updateQualityCmd.Parameters.AddWithValue("@structural_integrity_score", organ.Quality.StructuralIntegrityScore);
                        updateQualityCmd.Parameters.AddWithValue("@risk_score", organ.Quality.RiskScore);
                        updateQualityCmd.Parameters.AddWithValue("@assessment_notes",
                            organ.Quality.AssessmentNotes ?? (object)DBNull.Value);
                        updateQualityCmd.Parameters.AddWithValue("@assessment_time", organ.Quality.AssessmentTime);
                        updateQualityCmd.Parameters.AddWithValue("@assessed_by", organ.Quality.AssessedBy);

                        await updateQualityCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Insert new quality assessment
                        const string insertQualityQuery = @"
                            INSERT INTO organ_quality_assessments (
                                organ_id, functionality_score, structural_integrity_score, risk_score, 
                                assessment_notes, assessment_time, assessed_by)
                            VALUES (
                                @organ_id, @functionality_score, @structural_integrity_score, @risk_score, 
                                @assessment_notes, @assessment_time, @assessed_by)";

                        using var insertQualityCmd = new NpgsqlCommand(insertQualityQuery, connection, transaction);
                        insertQualityCmd.Parameters.AddWithValue("@organ_id", Guid.Parse(organ.Id));
                        insertQualityCmd.Parameters.AddWithValue("@functionality_score", organ.Quality.FunctionalityScore);
                        insertQualityCmd.Parameters.AddWithValue("@structural_integrity_score", organ.Quality.StructuralIntegrityScore);
                        insertQualityCmd.Parameters.AddWithValue("@risk_score", organ.Quality.RiskScore);
                        insertQualityCmd.Parameters.AddWithValue("@assessment_notes",
                            organ.Quality.AssessmentNotes ?? (object)DBNull.Value);
                        insertQualityCmd.Parameters.AddWithValue("@assessment_time", organ.Quality.AssessmentTime);
                        insertQualityCmd.Parameters.AddWithValue("@assessed_by", organ.Quality.AssessedBy);

                        await insertQualityCmd.ExecuteNonQueryAsync();
                    }
                }
                else if (count > 0)
                {
                    // Delete quality assessment if it exists in DB but not in the object
                    const string deleteQualityQuery = @"
                        DELETE FROM organ_quality_assessments WHERE organ_id = @organ_id";
                    using var deleteQualityCmd = new NpgsqlCommand(deleteQualityQuery, connection, transaction);
                    deleteQualityCmd.Parameters.AddWithValue("@organ_id", Guid.Parse(organ.Id));
                    await deleteQualityCmd.ExecuteNonQueryAsync();
                }
            });
        }

        public async Task DeleteOrganAsync(string id)
        {
            // No need to delete quality assessment separately due to CASCADE delete
            const string query = "DELETE FROM organs WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
