using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Identity.Web;
using SecureFileTransfer.Api.Services;
using SecureFileTransfer.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Authentication — Layer 2 (independent JWT validation)
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

// Azure SDK clients via Managed Identity
var credential = new DefaultAzureCredential();
var storageUri = builder.Configuration["Storage:AccountUri"]
    ?? throw new InvalidOperationException("Storage:AccountUri is required");

builder.Services.AddSingleton(new BlobServiceClient(new Uri(storageUri), credential));
builder.Services.AddSingleton(new TableServiceClient(new Uri(storageUri.Replace(".blob.", ".table.")), credential));
builder.Services.AddSingleton(new QueueServiceClient(new Uri(storageUri.Replace(".blob.", ".queue.")), credential));

// Application services
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();

// Controllers + OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaPolicy", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("SpaPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CorrelationIdMiddleware>();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
