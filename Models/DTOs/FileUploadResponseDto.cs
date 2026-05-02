namespace AutoNext.Platform.Blob.API.Models.DTOs
{
    public class FileUploadResponseDto
    {
        public Guid FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string ETag { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();

        // Azure Blob compatible fields
        public string BlobType => "BlockBlob";
        public string AccessTier => "Hot";
        public bool ServerEncrypted => true;
    }
}
