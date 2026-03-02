using Azure.Data.Tables;
using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public class ActivityService : IActivityService
{
    private const string ActivityTable = "SftActivity";
    private const string RegistryTable = "SftRegistry";

    private readonly TableServiceClient _tableServiceClient;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(TableServiceClient tableServiceClient, ILogger<ActivityService> logger)
    {
        _tableServiceClient = tableServiceClient;
        _logger = logger;
    }

    public async Task LogActivityAsync(
        string containerName, string action, string fileName,
        string directory, string performedBy, long sizeBytes, string correlationId)
    {
        var tableClient = _tableServiceClient.GetTableClient(ActivityTable);
        await tableClient.CreateIfNotExistsAsync();

        var rowKey = $"{DateTimeOffset.MaxValue.Ticks - DateTimeOffset.UtcNow.Ticks:D20}_{Guid.NewGuid():N}";

        var record = new ActivityRecord
        {
            PartitionKey = containerName,
            RowKey = rowKey,
            Action = action,
            FileName = fileName,
            Directory = directory,
            PerformedBy = performedBy,
            SizeBytes = sizeBytes,
            CorrelationId = correlationId
        };

        await tableClient.AddEntityAsync(record);
        _logger.LogInformation("Activity logged: {Action} {FileName} in {Container} by {User}",
            action, fileName, containerName, performedBy);
    }

    public async Task<IReadOnlyList<ActivityRecord>> GetActivityAsync(string containerName, int take = 50)
    {
        var tableClient = _tableServiceClient.GetTableClient(ActivityTable);
        await tableClient.CreateIfNotExistsAsync();

        var records = new List<ActivityRecord>();
        await foreach (var record in tableClient.QueryAsync<ActivityRecord>(
            filter: $"PartitionKey eq '{ODataSanitizer.EscapeStringValue(containerName)}'",
            maxPerPage: take))
        {
            records.Add(record);
            if (records.Count >= take) break;
        }
        return records;
    }

    public async Task<IReadOnlyList<ActivityRecord>> GetAllActivityAsync(int take = 100)
    {
        var tableClient = _tableServiceClient.GetTableClient(ActivityTable);
        await tableClient.CreateIfNotExistsAsync();

        var records = new List<ActivityRecord>();
        await foreach (var record in tableClient.QueryAsync<ActivityRecord>(maxPerPage: take))
        {
            records.Add(record);
            if (records.Count >= take) break;
        }
        return records;
    }

    public async Task<ContainerAccessResult> UserHasContainerAccessAsync(
        string userId, IReadOnlyList<string> userGroups, string containerName, bool isAdmin)
    {
        var tableClient = _tableServiceClient.GetTableClient(RegistryTable);
        await tableClient.CreateIfNotExistsAsync();

        await foreach (var entity in tableClient.QueryAsync<ThirdParty>(
            filter: $"PartitionKey eq 'ThirdParty' and ContainerName eq '{ODataSanitizer.EscapeStringValue(containerName)}'"))
        {
            if (entity.Status != "active") return ContainerAccessResult.None;

            // SFT.Admin role → full access to any active container
            if (isAdmin) return ContainerAccessResult.Full;

            // AdminGroupId match → full access (includes delete)
            if (!string.IsNullOrEmpty(entity.AdminGroupId) && userGroups.Contains(entity.AdminGroupId))
                return ContainerAccessResult.Full;

            // UserGroupId match → read/upload/download only
            if (!string.IsNullOrEmpty(entity.UserGroupId) && userGroups.Contains(entity.UserGroupId))
                return ContainerAccessResult.ReadOnly;

            // ServicePrincipalObjectId match → full access (M2M automation)
            if (entity.ServicePrincipalObjectId == userId)
                return ContainerAccessResult.Full;

            return ContainerAccessResult.None;
        }

        return ContainerAccessResult.None;
    }

    public async Task<IReadOnlyList<string>> GetAccessibleContainersAsync(
        string userId, IReadOnlyList<string> userGroups, bool isAdmin)
    {
        var tableClient = _tableServiceClient.GetTableClient(RegistryTable);
        await tableClient.CreateIfNotExistsAsync();

        var containers = new List<string>();
        await foreach (var entity in tableClient.QueryAsync<ThirdParty>(
            filter: "PartitionKey eq 'ThirdParty'"))
        {
            if (entity.Status != "active") continue;

            if (isAdmin)
            {
                containers.Add(entity.ContainerName);
            }
            else if (!string.IsNullOrEmpty(entity.AdminGroupId) && userGroups.Contains(entity.AdminGroupId))
            {
                containers.Add(entity.ContainerName);
            }
            else if (!string.IsNullOrEmpty(entity.UserGroupId) && userGroups.Contains(entity.UserGroupId))
            {
                containers.Add(entity.ContainerName);
            }
            else if (entity.ServicePrincipalObjectId == userId)
            {
                containers.Add(entity.ContainerName);
            }
        }

        return containers;
    }
}
