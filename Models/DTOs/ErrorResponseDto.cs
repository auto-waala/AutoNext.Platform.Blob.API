namespace AutoNext.Platform.Blob.API.Models.DTOs
{
    public class ErrorResponseDto
    {
        public bool Success { get; set; } = false;
        public string Error { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public int Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string RequestId { get; set; } = string.Empty;
        public Dictionary<string, List<string>>? ValidationErrors { get; set; }
    }
}
