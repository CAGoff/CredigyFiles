using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;
using SecureFileTransfer.Api.Services;
using SecureFileTransfer.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Authentication — Layer 2 (independent JWT validation)
if (builder.Configuration.GetValue<bool>("Authentication:UseDevelopmentAuth"))
{
    builder.Services.AddAuthentication("DevAuth")
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevAuth", null);
    builder.Services.AddAuthorization();
}
else
{
    builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
}

// Azure SDK clients — connection string (Azurite) or Managed Identity (Azure)
var connectionString = builder.Configuration["Storage:ConnectionString"];
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddSingleton(new BlobServiceClient(connectionString));
    builder.Services.AddSingleton(new TableServiceClient(connectionString));
    builder.Services.AddSingleton(new QueueServiceClient(connectionString));
}
else
{
    var credential = new DefaultAzureCredential();
    var storageUri = builder.Configuration["Storage:AccountUri"]
        ?? throw new InvalidOperationException("Storage:AccountUri or Storage:ConnectionString is required");

    builder.Services.AddSingleton(new BlobServiceClient(new Uri(storageUri), credential));
    builder.Services.AddSingleton(new TableServiceClient(new Uri(storageUri.Replace(".blob.", ".table.")), credential));
    builder.Services.AddSingleton(new QueueServiceClient(new Uri(storageUri.Replace(".blob.", ".queue.")), credential));
}

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
