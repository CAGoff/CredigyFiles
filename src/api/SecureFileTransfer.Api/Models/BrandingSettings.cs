using Azure;
using Azure.Data.Tables;

namespace SecureFileTransfer.Api.Models;

public class BrandingSettings : ITableEntity
{
    public const string DefaultAppName = "Credigy Files";
    public const string DefaultPrimaryColor = "#2563eb";

    /// <summary>PartitionKey is a fixed value "Branding".</summary>
    public string PartitionKey { get; set; } = "Branding";

    /// <summary>RowKey is a fixed value "settings" (single-row config).</summary>
    public string RowKey { get; set; } = "settings";

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string AppName { get; set; } = DefaultAppName;
    public string PrimaryColor { get; set; } = DefaultPrimaryColor;
    public string? LogoBlobName { get; set; }
    public string? FaviconBlobName { get; set; }
}

public record BrandingUpdateRequest(string AppName, string PrimaryColor);

public record BrandingResponse(
    string AppName,
    string PrimaryColor,
    string? LogoUrl,
    string? FaviconUrl);
