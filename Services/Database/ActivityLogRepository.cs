using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DonateForLife.Models;
using Npgsql;

namespace DonateForLife.Services.Database
{
    public class ActivityLogRepository
    {
        private readonly PostgresConnectionHelper _db;

        public ActivityLogRepository(PostgresConnectionHelper db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<List<ActivityLog>> GetRecentActivityLogsAsync(int count)
        {
            const string query = @"
                SELECT id, timestamp, activity_type, description, related_id
                FROM activity_logs
                ORDER BY timestamp DESC
                LIMIT @count";

            var parameters = new Dictionary<string, object>
            {
                { "@count", count }
            };

            return await _db.ExecuteQueryAsync(query, reader => new ActivityLog
            {
                Id = reader.GetGuid(0).ToString(),
                Timestamp = reader.GetDateTime(1),
                ActivityType = Enum.Parse<ActivityType>(reader.GetString(2)),
                Description = reader.GetString(3),
                RelatedId = reader.IsDBNull(4) ? string.Empty : reader.GetGuid(4).ToString()
            }, parameters);
        }

        public async Task AddActivityLogAsync(ActivityLog log)
        {
            const string query = @"
                INSERT INTO activity_logs (id, timestamp, activity_type, description, related_id)
                VALUES (@id, @timestamp, @activity_type, @description, @related_id)";

            var id = log.Id;
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
            }

            Guid? relatedId = null;
            if (!string.IsNullOrEmpty(log.RelatedId))
            {
                relatedId = Guid.Parse(log.RelatedId);
            }

            var parameters = new Dictionary<string, object>
            {
                { "@id", Guid.Parse(id) },
                { "@timestamp", log.Timestamp },
                { "@activity_type", log.ActivityType.ToString() },
                { "@description", log.Description },
                { "@related_id", relatedId ?? (object)DBNull.Value }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}