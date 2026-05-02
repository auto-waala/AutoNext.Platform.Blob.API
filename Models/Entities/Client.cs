using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AutoNext.Platform.Blob.API.Models.Entities
{
    public class Client
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [BsonElement("client_secret")]
        public string ClientSecret { get; set; } = string.Empty; // Hashed

        [BsonElement("client_name")]
        public string ClientName { get; set; } = string.Empty;

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("storage_quota")]
        public long StorageQuota { get; set; } = 1073741824; // 1GB

        [BsonElement("used_storage")]
        public long UsedStorage { get; set; } = 0;

        [BsonElement("allowed_file_types")]
        public List<string> AllowedFileTypes { get; set; } = new();

        [BsonElement("max_file_size")]
        public long MaxFileSize { get; set; } = 52428800; // 50MB

        [BsonElement("rate_limit_per_minute")]
        public int RateLimitPerMinute { get; set; } = 100;

        [BsonElement("ip_whitelist")]
        public List<string> IpWhitelist { get; set; } = new();

        [BsonElement("last_accessed")]
        public DateTime? LastAccessed { get; set; }

        [BsonElement("environment")]
        public string Environment { get; set; } = "Production";
    }
}
