using AutoNext.Platform.Blob.API.Models.DTOs;
using System.Net;
using System.Text.Json;

namespace AutoNext.Platform.Blob.API.Middlewares
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ErrorResponseDto
            {
                RequestId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow
            };

            switch (exception)
            {
                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Error = exception.Message;
                    errorResponse.ErrorCode = "UNAUTHORIZED";
                    errorResponse.Status = 401;
                    break;

                case ArgumentException:
                case InvalidOperationException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Error = exception.Message;
                    errorResponse.ErrorCode = "BAD_REQUEST";
                    errorResponse.Status = 400;
                    break;

                case FileNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Error = exception.Message;
                    errorResponse.ErrorCode = "NOT_FOUND";
                    errorResponse.Status = 404;
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Error = "An internal error occurred";
                    errorResponse.ErrorCode = "INTERNAL_ERROR";
                    errorResponse.Status = 500;
                    break;
            }

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await response.WriteAsync(json);
        }
    }
}
