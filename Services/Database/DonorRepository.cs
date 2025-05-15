using DonateForLife.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DonateForLife.Services.Database
{
    /// <summary>
    /// Repository for Donor-related database operations
    /// </summary>
    public class DonorRepository(PostgresConnectionHelper db)
    {
        private readonly PostgresConnectionHelper _db = db ?? throw new ArgumentNullException(nameof(db));

        public async Task<List<Donor>> GetAllDonorsAsync()
        {
            const string query = @"
                SELECT id, first_name, last_name, date_of_birth, blood_type, hla_type, 
                       medical_history, hospital, country, registered_date, status
                FROM donors
                ORDER BY last_name, first_name";

            return await _db.ExecuteQueryAsync(query, reader => new Donor
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
                Status = Enum.Parse<DonorStatus>(reader.GetString(10))
            });
        }

        public async Task<Donor> GetDonorByIdAsync(string id)
        {
            const string query = @"
                SELECT id, first_name, last_name, date_of_birth, blood_type, hla_type, 
                       medical_history, hospital, country, registered_date, status
                FROM donors
                WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            return await _db.ExecuteQuerySingleAsync(query, reader => new Donor
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
                Status = Enum.Parse<DonorStatus>(reader.GetString(10))
            }, parameters);
        }

        public async Task<string> AddDonorAsync(Donor donor)
        {
            const string query = @"
                INSERT INTO donors (id, first_name, last_name, date_of_birth, blood_type, hla_type, 
                                   medical_history, hospital, country, registered_date, status)
                VALUES (@id, @first_name, @last_name, @date_of_birth, @blood_type, @hla_type, 
                        @medical_history, @hospital, @country, @registered_date, @status)
                RETURNING id";

            var id = donor.Id;
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
            }

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) },
                { "@first_name", donor.FirstName },
                { "@last_name", donor.LastName },
                { "@date_of_birth", donor.DateOfBirth },
                { "@blood_type", donor.BloodType },
                { "@hla_type", donor.HlaType },
                { "@medical_history", donor.MedicalHistory },
                { "@hospital", donor.Hospital },
                { "@country", donor.Country },
                { "@registered_date", donor.RegisteredDate },
                { "@status", donor.Status.ToString() }
            };

            var result = await _db.ExecuteScalarAsync<Guid>(query, parameters);
            return result.ToString();
        }

        public async Task UpdateDonorAsync(Donor donor)
        {
            const string query = @"
                UPDATE donors
                SET first_name = @first_name,
                    last_name = @last_name,
                    date_of_birth = @date_of_birth,
                    blood_type = @blood_type,
                    hla_type = @hla_type,
                    medical_history = @medical_history,
                    hospital = @hospital,
                    country = @country,
                    status = @status
                WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(donor.Id) },
                { "@first_name", donor.FirstName },
                { "@last_name", donor.LastName },
                { "@date_of_birth", donor.DateOfBirth },
                { "@blood_type", donor.BloodType },
                { "@hla_type", donor.HlaType },
                { "@medical_history", donor.MedicalHistory },
                { "@hospital", donor.Hospital },
                { "@country", donor.Country },
                { "@status", donor.Status.ToString() }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }

        public async Task DeleteDonorAsync(string id)
        {
            const string query = "DELETE FROM donors WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
