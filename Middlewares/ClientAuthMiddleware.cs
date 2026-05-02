using AutoNext.Platform.Blob.API.Models.DTOs;
using AutoNext.Platform.Blob.API.Services;
using System.Net;
using System.Text.Json;

namespace AutoNext.Platform.Blob.API.Middlewares
{
    public class ClientAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ClientAuthMiddleware> _logger;
        private readonly List<string> _publicPaths = new() { "/health", "/metrics", "/swagger" };

        public ClientAuthMiddleware(RequestDelegate next, ILogger<ClientAuthMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IFileService fileService)
        {
            // Skip auth for public paths
            if (_publicPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
            {
                await _next(context);
                return;
            }

            // Extract credentials
            if (!context.Request.Headers.TryGetValue("X-Client-Id", out var clientId) ||
                !context.Request.Headers.TryGetValue("X-Client-Secret", out var clientSecret))
            {
                await SendUnauthorizedResponse(context, "Missing client credentials");
                return;
            }

            try
            {
                // Validate client
                var client = await fileService.ValidateClientAsync(clientId!, clientSecret!);
                context.Items["Client"] = client;

                // Rate limiting (add Redis or in-memory cache for production)
                await CheckRateLimit(context, client);

                // IP whitelist check
                await CheckIpWhitelist(context, client);

                await _next(context);
            }
            catch (UnauthorizedAccessException ex)
            {
                await SendUnauthorizedResponse(context, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication error for client: {ClientId}", clientId);
                await SendErrorResponse(context, "Authentication failed", HttpStatusCode.InternalServerError);
            }
        }

        private async Task CheckRateLimit(HttpContext context, Models.Entities.Client client)
        {
            // Implement rate limiting logic here
            // Use IDistributedCache for production
            await Task.CompletedTask;
        }

        private async Task CheckIpWhitelist(HttpContext context, Models.Entities.Client client)
        {
            if (client.IpWhitelist.Any())
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString();
                if (!client.IpWhitelist.Contains(remoteIp))
                {
                    _logger.LogWarning("Access denied for IP {RemoteIp} for client {ClientId}", remoteIp, client.ClientId);
                    throw new UnauthorizedAccessException("IP not whitelisted");
                }
            }
        }

        private async Task SendUnauthorizedResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";

            var error = new ErrorResponseDto
            {
                Error = message,
                ErrorCode = "UNAUTHORIZED",
                Status = 401,
                RequestId = context.TraceIdentifier
            };

            var json = JsonSerializer.Serialize(error);
            await context.Response.WriteAsync(json);
        }

        private async Task SendErrorResponse(HttpContext context, string message, HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var error = new ErrorResponseDto
            {
                Error = message,
                ErrorCode = "AUTH_ERROR",
                Status = (int)statusCode,
                RequestId = context.TraceIdentifier
            };

            var json = JsonSerializer.Serialize(error);
            await context.Response.WriteAsync(json);
        }
    }
}
