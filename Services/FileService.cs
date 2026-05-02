using AutoNext.Platform.Blob.API.Models;
using AutoNext.Platform.Blob.API.Models.DTOs;
using AutoNext.Platform.Blob.API.Models.Entities;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace AutoNext.Platform.Blob.API.Services
{
    public class FileService : IFileService
    {
        private readonly MongoDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;
        private readonly long _maxFileSize;
        private readonly List<string> _allowedExtensions;
        private readonly string _baseUrl;

        public FileService(
            MongoDbContext db,
            IWebHostEnvironment env,
            ILogger<FileService> logger,
            IConfiguration configuration)
        {
            _db = db;
            _env = env;
            _logger = logger;
            _configuration = configuration;

            _uploadPath = _configuration["FileStorage:UploadPath"] ?? "uploads";
            _maxFileSize = long.Parse(_configuration["FileStorage:MaxFileSizeBytes"] ?? "52428800");
            _allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<List<string>>() ?? new();
            _baseUrl = _configuration["FileStorage:BaseUrl"] ?? "https://localhost:5001";
        }

        public async Task<FileUploadResponseDto> UploadAsync(IFormFile file, Client client, Dictionary<string, string>? metadata = null)
        {
            using var activity = new System.Diagnostics.Activity("FileService.Upload").Start();

            try
            {
                _logger.LogInformation("Upload started for client {ClientId}, file: {FileName}", client.ClientId, file.FileName);

                // Validate file
                ValidateFile(file, client);

                var fileId = Guid.NewGuid();
                var fileHash = await ComputeFileHashAsync(file);

                // Check for duplicate
                var existingFile = await _db.Files
                    .Find(f => f.FileHash == fileHash && f.ClientId == client.ClientId && !f.IsDeleted)
                    .FirstOrDefaultAsync();

                if (existingFile != null)
                {
                    _logger.LogInformation("Duplicate file detected for client {ClientId}, returning existing file", client.ClientId);
                    return MapToResponseDto(existingFile);
                }

                // Check storage quota
                await CheckStorageQuotaAsync(client, file.Length);

                // Save file
                var relativePath = await SavePhysicalFileAsync(file, fileId);
                var fileUrl = $"{_baseUrl}{relativePath}";
                var etag = ComputeETag(fileHash);

                // Create metadata
                var fileMetadata = new FileMetadata
                {
                    FileId = fileId,
                    ClientId = client.ClientId,
                    FileName = $"{fileId}{Path.GetExtension(file.FileName)}",
                    OriginalName = file.FileName,
                    FilePath = relativePath,
                    FileUrl = fileUrl,
                    FileSize = file.Length,
                    ContentType = file.ContentType,
                    FileHash = fileHash,
                    UploadedAt = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    DownloadCount = 0,
                    CustomMetadata = metadata ?? new(),
                    ETag = etag
                };

                // Save to database
                await _db.Files.InsertOneAsync(fileMetadata);

                // Update client storage
                await UpdateClientStorageAsync(client.ClientId, file.Length);

                _logger.LogInformation("Upload completed for client {ClientId}, fileId: {FileId}", client.ClientId, fileId);

                return MapToResponseDto(fileMetadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed for client {ClientId}, file: {FileName}", client.ClientId, file.FileName);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid fileId, Client client)
        {
            using var activity = new System.Diagnostics.Activity("FileService.Delete").Start();

            try
            {
                var file = await _db.Files
                    .Find(f => f.FileId == fileId && f.ClientId == client.ClientId && !f.IsDeleted)
                    .FirstOrDefaultAsync();

                if (file == null) return false;

                // Soft delete - mark as deleted
                var update = Builders<FileMetadata>.Update
                    .Set(f => f.IsDeleted, true)
                    .Set(f => f.DeletedAt, DateTime.UtcNow);

                await _db.Files.UpdateOneAsync(f => f.FileId == fileId, update);

                // Optionally delete physical file (or keep for recovery)
                var fullPath = Path.Combine(_env.WebRootPath, file.FilePath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    // Move to deleted folder instead of deleting
                    var deletedPath = fullPath.Replace(_uploadPath, $"{_uploadPath}/deleted");
                    var deletedDir = Path.GetDirectoryName(deletedPath);
                    if (!Directory.Exists(deletedDir))
                        Directory.CreateDirectory(deletedDir!);
                    File.Move(fullPath, deletedPath);
                }

                // Update client storage
                await UpdateClientStorageAsync(client.ClientId, -file.FileSize);

                _logger.LogInformation("File deleted: {FileId} for client {ClientId}", fileId, client.ClientId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete failed for fileId: {FileId}", fileId);
                throw;
            }
        }

        public async Task<FileMetadata?> GetMetadataAsync(Guid fileId, Client client)
        {
            return await _db.Files
                .Find(f => f.FileId == fileId && f.ClientId == client.ClientId && !f.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<(Stream FileStream, string ContentType, string FileName)> DownloadAsync(Guid fileId, Client client)
        {
            var file = await _db.Files
                .Find(f => f.FileId == fileId && f.ClientId == client.ClientId && !f.IsDeleted)
                .FirstOrDefaultAsync();

            if (file == null)
                throw new FileNotFoundException("File not found");

            var fullPath = Path.Combine(_env.WebRootPath, file.FilePath.TrimStart('/'));
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Physical file not found");

            // Update access metrics
            await _db.Files.UpdateOneAsync(
                f => f.FileId == fileId,
                Builders<FileMetadata>.Update
                    .Set(f => f.LastAccessed, DateTime.UtcNow)
                    .Inc(f => f.DownloadCount, 1)
            );

            var stream = File.OpenRead(fullPath);
            return (stream, file.ContentType, file.OriginalName);
        }

        public async Task<List<FileMetadata>> ListFilesAsync(Client client, int page = 1, int pageSize = 50)
        {
            var skip = (page - 1) * pageSize;

            return await _db.Files
                .Find(f => f.ClientId == client.ClientId && !f.IsDeleted)
                .SortByDescending(f => f.UploadedAt)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<Client> ValidateClientAsync(string clientId, string clientSecret)
        {
            var client = await _db.Clients
                .Find(c => c.ClientId == clientId && c.IsActive)
                .FirstOrDefaultAsync();

            if (client == null)
                throw new UnauthorizedAccessException("Invalid client ID");

            if (!BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecret))
                throw new UnauthorizedAccessException("Invalid client secret");

            // Update last accessed
            await _db.Clients.UpdateOneAsync(
                c => c.ClientId == clientId,
                Builders<Client>.Update.Set(c => c.LastAccessed, DateTime.UtcNow)
            );

            return client;
        }

        private void ValidateFile(IFormFile file, Client client)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided");

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!client.AllowedFileTypes.Contains(extension) && !_allowedExtensions.Contains(extension))
                throw new InvalidOperationException($"File type {extension} not allowed");

            if (file.Length > Math.Min(client.MaxFileSize, _maxFileSize))
                throw new InvalidOperationException($"File size exceeds limit of {Math.Min(client.MaxFileSize, _maxFileSize)} bytes");
        }

        private async Task<string> SavePhysicalFileAsync(IFormFile file, Guid fileId)
        {
            var extension = Path.GetExtension(file.FileName);
            var yearMonth = DateTime.UtcNow.ToString("yyyy/MM");
            var folder = Path.Combine(_uploadPath, yearMonth);
            var fullPath = Path.Combine(_env.WebRootPath, folder);

            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            var fileName = $"{fileId}{extension}";
            var filePath = Path.Combine(fullPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/{folder}/{fileName}".Replace("\\", "/");
        }

        private async Task<string> ComputeFileHashAsync(IFormFile file)
        {
            using var sha256 = SHA256.Create();
            using var stream = file.OpenReadStream();
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToBase64String(hash);
        }

        private string ComputeETag(string hash)
        {
            return $"\"{Convert.ToBase64String(Encoding.UTF8.GetBytes(hash.Substring(0, 16)))}\"";
        }

        private async Task CheckStorageQuotaAsync(Client client, long fileSize)
        {
            if (client.UsedStorage + fileSize > client.StorageQuota)
                throw new InvalidOperationException($"Storage quota exceeded. Used: {client.UsedStorage}, Quota: {client.StorageQuota}");
        }

        private async Task UpdateClientStorageAsync(string clientId, long delta)
        {
            await _db.Clients.UpdateOneAsync(
                c => c.ClientId == clientId,
                Builders<Client>.Update.Inc(c => c.UsedStorage, delta)
            );
        }

        private FileUploadResponseDto MapToResponseDto(FileMetadata metadata)
        {
            return new FileUploadResponseDto
            {
                FileId = metadata.FileId,
                FileName = metadata.FileName,
                OriginalName = metadata.OriginalName,
                FilePath = metadata.FilePath,
                FileUrl = metadata.FileUrl,
                FileSize = metadata.FileSize,
                ContentType = metadata.ContentType,
                ETag = metadata.ETag,
                UploadedAt = metadata.UploadedAt,
                Metadata = metadata.CustomMetadata
            };
        }
    }
}
