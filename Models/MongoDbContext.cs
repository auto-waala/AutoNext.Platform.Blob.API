using AutoNext.Platform.Blob.API.Configurations;
using AutoNext.Platform.Blob.API.Models.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AutoNext.Platform.Blob.API.Models
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoDbContext> _logger;
        private readonly MongoDbSettings _settings;

        public MongoDbContext(
            IConfiguration configuration,
            IOptions<MongoDbSettings> settings,
            ILogger<MongoDbContext> logger)
        {
            _logger = logger;
            _settings = settings.Value;

            try
            {
                var connectionString = configuration.GetConnectionString("MongoDB");
                var databaseName = _settings.DatabaseName;

                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("MongoDB connection string is missing");

                if (string.IsNullOrEmpty(databaseName))
                    throw new InvalidOperationException("Database name is missing in configuration");

                _logger.LogInformation("Connecting to MongoDB...");

                var client = new MongoClient(connectionString);
                _database = client.GetDatabase(databaseName);

                // Test connection
                var pingCommand = new BsonDocument("ping", 1);
                _database.RunCommand<BsonDocument>(pingCommand);

                _logger.LogInformation("Successfully connected to MongoDB database: {DatabaseName}", databaseName);

                // Create indexes asynchronously
                Task.Run(async () => await CreateIndexesAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MongoDB");
                throw;
            }
        }

        public IMongoCollection<FileMetadata> Files =>
            _database.GetCollection<FileMetadata>(_settings.FileMetadataCollection);

        public IMongoCollection<Client> Clients =>
            _database.GetCollection<Client>(_settings.ClientsCollection);

        private async Task CreateIndexesAsync()
        {
            try
            {
                // File metadata indexes
                var fileIndexes = new[]
                {
                    new CreateIndexModel<FileMetadata>(
                        Builders<FileMetadata>.IndexKeys.Ascending(f => f.FileId),
                        new CreateIndexOptions { Unique = true }),
                    new CreateIndexModel<FileMetadata>(
                        Builders<FileMetadata>.IndexKeys.Ascending(f => f.ClientId).Descending(f => f.UploadedAt)),
                    new CreateIndexModel<FileMetadata>(
                        Builders<FileMetadata>.IndexKeys.Ascending(f => f.FileHash))
                };

                await Files.Indexes.CreateManyAsync(fileIndexes);

                // Client indexes
                var clientIndexes = new[]
                {
                    new CreateIndexModel<Client>(
                        Builders<Client>.IndexKeys.Ascending(c => c.ClientId),
                        new CreateIndexOptions { Unique = true })
                };

                await Clients.Indexes.CreateManyAsync(clientIndexes);

                _logger.LogInformation("MongoDB indexes created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to create indexes: {Message}", ex.Message);
            }
        }
    }
}
