namespace AutoNext.Platform.Blob.API.Configurations
{
    public class MongoDbSettings
    {
        public string DatabaseName { get; set; } = string.Empty;
        public string FileMetadataCollection { get; set; } = string.Empty;
        public string ClientsCollection { get; set; } = string.Empty;
        public string LogsCollection { get; set; } = string.Empty;
    }
}
