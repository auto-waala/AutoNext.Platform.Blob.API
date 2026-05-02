using AutoNext.Platform.Blob.API.Models.DTOs;
using AutoNext.Platform.Blob.API.Models.Entities;

namespace AutoNext.Platform.Blob.API.Services
{
    public interface IFileService
    {
        Task<FileUploadResponseDto> UploadAsync(IFormFile file, Client client, Dictionary<string, string>? metadata = null);
        Task<bool> DeleteAsync(Guid fileId, Client client);
        Task<FileMetadata?> GetMetadataAsync(Guid fileId, Client client);
        Task<(Stream FileStream, string ContentType, string FileName)> DownloadAsync(Guid fileId, Client client);
        Task<List<FileMetadata>> ListFilesAsync(Client client, int page = 1, int pageSize = 50);
        Task<Client> ValidateClientAsync(string clientId, string clientSecret);
    }
}
