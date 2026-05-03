using AutoNext.Platform.Blob.API.Models.DTOs;
using AutoNext.Platform.Blob.API.Models.Entities;
using AutoNext.Platform.Blob.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AutoNext.Platform.Blob.API.Controllers
{
    [ApiController]
    [Route("api/v1/files")]
    [Produces("application/json")]
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<FilesController> _logger;

        public FilesController(IFileService fileService, ILogger<FilesController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(FileUploadResponseDto), 201)]
        [ProducesResponseType(typeof(ErrorResponseDto), 400)]
        [ProducesResponseType(typeof(ErrorResponseDto), 401)]
        public async Task<IActionResult> Upload(
            IFormFile file,
            [FromForm] string? metadata = null)
        {
            var client = HttpContext.Items["Client"] as Client;

            if (client == null)
                return Unauthorized(new ErrorResponseDto { Error = "Invalid client" });

            _logger.LogInformation("Upload request from client: {ClientId}, file: {FileName}",
                client.ClientId, file.FileName);

            Dictionary<string, string>? metadataDict = null;
            if (!string.IsNullOrEmpty(metadata))
            {
                metadataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
            }

            var result = await _fileService.UploadAsync(file, client, metadataDict);

            return StatusCode(201, new
            {
                success = true,
                status = 201,
                data = result
            });
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(FileMetadata), 200)]
        [ProducesResponseType(typeof(ErrorResponseDto), 404)]
        public async Task<IActionResult> GetMetadata(Guid id)
        {
            var client = HttpContext.Items["Client"] as Client;
            var metadata = await _fileService.GetMetadataAsync(id, client);

            if (metadata == null)
                return NotFound(new ErrorResponseDto { Error = "File not found" });

            return Ok(new { success = true, data = metadata });
        }

        [HttpGet("{id}/download")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(ErrorResponseDto), 404)]
        public async Task<IActionResult> Download(Guid id)
        {
            var client = HttpContext.Items["Client"] as Client;
            var (stream, contentType, fileName) = await _fileService.DownloadAsync(id, client);

            return File(stream, contentType, fileName);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(ErrorResponseDto), 404)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = HttpContext.Items["Client"] as Client;
            var deleted = await _fileService.DeleteAsync(id, client);

            if (!deleted)
                return NotFound(new ErrorResponseDto { Error = "File not found" });

            return Ok(new { success = true, message = "File deleted successfully" });
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<FileMetadata>), 200)]
        public async Task<IActionResult> ListFiles(int page = 1, int pageSize = 50)
        {
            var client = HttpContext.Items["Client"] as Client;
            var files = await _fileService.ListFilesAsync(client, page, pageSize);

            return Ok(new
            {
                success = true,
                data = files,
                pagination = new
                {
                    page,
                    pageSize,
                    total = files.Count
                }
            });
        }
    }
}