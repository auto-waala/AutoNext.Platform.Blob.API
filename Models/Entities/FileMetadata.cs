using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AutoNext.Platform.Blob.API.Models.Entities
{
    public class FileMetadata
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("file_id")]
        public Guid FileId { get; set; }

        [BsonElement("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [BsonElement("file_name")]
        public string FileName { get; set; } = string.Empty;

        [BsonElement("original_name")]
        public string OriginalName { get; set; } = string.Empty;

        [BsonElement("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [BsonElement("file_url")]
        public string FileUrl { get; set; } = string.Empty;

        [BsonElement("file_size")]
        public long FileSize { get; set; }

        [BsonElement("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [BsonElement("file_hash")]
        public string FileHash { get; set; } = string.Empty;

        [BsonElement("uploaded_at")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("last_accessed")]
        public DateTime LastAccessed { get; set; }

        [BsonElement("download_count")]
        public int DownloadCount { get; set; } = 0;

        [BsonElement("metadata")]
        public Dictionary<string, string> CustomMetadata { get; set; } = new();

        [BsonElement("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [BsonElement("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        [BsonElement("etag")]
        public string ETag { get; set; } = string.Empty;
    }
}
