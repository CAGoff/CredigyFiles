using System.Text.RegularExpressions;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public partial class BrandingService : IBrandingService
{
    private const string BrandingTable = "SftBranding";
    private const string BrandingContainer = "sft-branding";

    private static readonly HashSet<string> AllowedLogoExtensions = [".png", ".svg", ".jpg", ".jpeg"];
    private static readonly HashSet<string> AllowedFaviconExtensions = [".png", ".ico"];

    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BrandingService> _logger;

    public BrandingService(
        TableServiceClient tableServiceClient,
        BlobServiceClient blobServiceClient,
        ILogger<BrandingService> logger)
    {
        _tableServiceClient = tableServiceClient;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<BrandingResponse> GetBrandingAsync()
    {
        var settings = await GetSettingsAsync();
        return ToResponse(settings);
    }

    public async Task<BrandingResponse> UpdateBrandingAsync(BrandingUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AppName))
            throw new ArgumentException("App name is required.");

        if (!HexColorRegex().IsMatch(request.PrimaryColor))
            throw new ArgumentException("Primary color must be a valid hex color (e.g., #2563eb).");

        var settings = await GetSettingsAsync();
        settings.AppName = request.AppName.Trim();
        settings.PrimaryColor = request.PrimaryColor.Trim();

        var tableClient = _tableServiceClient.GetTableClient(BrandingTable);
        await tableClient.UpsertEntityAsync(settings, TableUpdateMode.Replace);

        _logger.LogInformation("Branding updated: AppName={AppName}, PrimaryColor={Color}",
            settings.AppName, settings.PrimaryColor);

        return ToResponse(settings);
    }

    public async Task<BrandingResponse> UploadLogoAsync(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedLogoExtensions.Contains(extension))
            throw new ArgumentException($"Logo must be one of: {string.Join(", ", AllowedLogoExtensions)}");

        var blobName = $"logo{extension}";
        await UploadBlobAsync(blobName, stream, GetContentType(extension));

        var settings = await GetSettingsAsync();
        settings.LogoBlobName = blobName;

        var tableClient = _tableServiceClient.GetTableClient(BrandingTable);
        await tableClient.UpsertEntityAsync(settings, TableUpdateMode.Replace);

        _logger.LogInformation("Logo uploaded: {BlobName}", blobName);
        return ToResponse(settings);
    }

    public async Task<BrandingResponse> UploadFaviconAsync(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedFaviconExtensions.Contains(extension))
            throw new ArgumentException($"Favicon must be one of: {string.Join(", ", AllowedFaviconExtensions)}");

        var blobName = $"favicon{extension}";
        await UploadBlobAsync(blobName, stream, GetContentType(extension));

        var settings = await GetSettingsAsync();
        settings.FaviconBlobName = blobName;

        var tableClient = _tableServiceClient.GetTableClient(BrandingTable);
        await tableClient.UpsertEntityAsync(settings, TableUpdateMode.Replace);

        _logger.LogInformation("Favicon uploaded: {BlobName}", blobName);
        return ToResponse(settings);
    }

    private async Task<BrandingSettings> GetSettingsAsync()
    {
        var tableClient = _tableServiceClient.GetTableClient(BrandingTable);
        await tableClient.CreateIfNotExistsAsync();

        try
        {
            var response = await tableClient.GetEntityAsync<BrandingSettings>("Branding", "settings");
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return new BrandingSettings();
        }
    }

    private async Task UploadBlobAsync(string blobName, Stream stream, string contentType)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(BrandingContainer);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = containerClient.GetBlobClient(blobName);
        stream.Position = 0;

        await blobClient.UploadAsync(stream, new BlobHttpHeaders
        {
            ContentType = contentType,
            CacheControl = "public, max-age=3600"
        });
    }

    private BrandingResponse ToResponse(BrandingSettings settings)
    {
        string? logoUrl = null;
        string? faviconUrl = null;

        if (!string.IsNullOrEmpty(settings.LogoBlobName))
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(BrandingContainer);
            logoUrl = containerClient.GetBlobClient(settings.LogoBlobName).Uri.ToString();
        }

        if (!string.IsNullOrEmpty(settings.FaviconBlobName))
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(BrandingContainer);
            faviconUrl = containerClient.GetBlobClient(settings.FaviconBlobName).Uri.ToString();
        }

        return new BrandingResponse(
            settings.AppName,
            settings.PrimaryColor,
            logoUrl,
            faviconUrl);
    }

    private static string GetContentType(string extension) => extension switch
    {
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorRegex();
}
