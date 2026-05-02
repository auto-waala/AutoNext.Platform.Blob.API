using AutoNext.Platform.Blob.API.Configurations;
using AutoNext.Platform.Blob.API.Middlewares;
using AutoNext.Platform.Blob.API.Models;
using AutoNext.Platform.Blob.API.Models.Entities;
using AutoNext.Platform.Blob.API.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Serilog;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Application", "FileStorageAPI")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 3. Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Autonext File Storage API",
        Version = "v1",
        Description = "Production-ready file storage API with MongoDB"
    });

    // Add client credentials authentication to swagger
    c.AddSecurityDefinition("ClientId", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = "X-Client-Id",
        In = ParameterLocation.Header,
        Description = "Enter your Client ID"
    });

    c.AddSecurityDefinition("ClientSecret", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = "X-Client-Secret",
        In = ParameterLocation.Header,
        Description = "Enter your Client Secret"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ClientId"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ClientSecret"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 4. Configure MongoDB settings
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));
builder.Services.AddSingleton<MongoDbContext>();

// 5. Add application services
builder.Services.AddScoped<IFileService, FileService>();

// 6. Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins("https://autonext-blob.services.azurewebsites.betalen.in", "https://localhost:3000", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("X-Total-Count", "X-Page", "Content-Disposition");
    });
});

// 7. Configure response caching
builder.Services.AddResponseCaching();

// 8. Configure health checks
builder.Services.AddHealthChecks();

// 9. Configure JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

// 10. Configure form options for file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

var app = builder.Build();

// 11. Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Autonext File Storage API v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCaching();
app.UseCors("AllowSpecificOrigins");

// Serve static files (for uploaded files)
app.UseStaticFiles();

// Custom middleware
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<ClientAuthMiddleware>();

// Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("ClientId", httpContext.Request.Headers["X-Client-Id"].FirstOrDefault());
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
    };
});

app.MapControllers();
app.MapHealthChecks("/health");

// 12. Create uploads directory if it doesn't exist (Fixed version)
var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? app.Environment.ContentRootPath, "uploads");

// Ensure wwwroot exists if using WebRootPath
if (app.Environment.WebRootPath == null)
{
    var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    if (!Directory.Exists(wwwrootPath))
    {
        Directory.CreateDirectory(wwwrootPath);
    }
    app.Environment.WebRootPath = wwwrootPath;
    uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
}

if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    Log.Information("Created uploads directory at {Path}", uploadsPath);
}

// 13. Seed database with sample client (Run once)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Check if client exists
        var existingClient = await db.Clients.Find(c => c.ClientId == "autonext_web_app").FirstOrDefaultAsync();
        if (existingClient == null)
        {
            var hashedSecret = BCrypt.Net.BCrypt.HashPassword("a6cb4b9a-35a7-418d-bb8e-0c4a5527684b", 12);
            var client = new Client
            {
                ClientId = "autonext_web_app",
                ClientSecret = hashedSecret,
                ClientName = "Autonext Web Application",
                CreatedAt = DateTime.UtcNow,
                StorageQuota = 5_368_709_120, // 5GB
                UsedStorage = 0,
                AllowedFileTypes = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".mp4", ".mov", ".zip", ".doc", ".docx" },
                MaxFileSize = 104_857_600, // 100MB
                RateLimitPerMinute = 200,
                IsActive = true,
                IpWhitelist = new List<string>(),
                Environment = app.Environment.EnvironmentName
            };
            await db.Clients.InsertOneAsync(client);
            logger.LogInformation("Sample client created with ClientId: autonext_web_app");

            Console.WriteLine("=".PadRight(50, '='));
            Console.WriteLine("Sample Client Created:");
            Console.WriteLine($"Client ID: autonext_web_app");
            Console.WriteLine($"Client Secret: a6cb4b9a-35a7-418d-bb8e-0c4a5527684b");
            Console.WriteLine("=".PadRight(50, '='));
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding database");
    }
}

// 14. Run application
try
{
    Log.Information("Starting up the File Storage API");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}