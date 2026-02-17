using Azure;
using Azure.Data.Tables;

namespace SecureFileTransfer.Api.Models;

public class ThirdParty : ITableEntity
{
    /// <summary>PartitionKey is a fixed value "ThirdParty" for easy querying.</summary>
    public string PartitionKey { get; set; } = "ThirdParty";

    /// <summary>RowKey is the third-party ID (e.g., "tp-001").</summary>
    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? AppRegistrationId { get; set; }
    public string? ServicePrincipalObjectId { get; set; }
    public string? CertificateThumbprint { get; set; }
    public bool AutomationEnabled { get; set; }
    public string Status { get; set; } = "provisioning"; // provisioning, active, deprovisioning, inactive
}

public record ThirdPartyCreateRequest(
    string CompanyName,
    string ContactEmail,
    bool EnableAutomation);

public record ThirdPartyResponse(
    string Id,
    string CompanyName,
    string ContainerName,
    string? AppRegistrationId,
    string Status,
    DateTimeOffset CreatedAt);
