using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public partial class OnboardingService : IOnboardingService
{
    private const string RegistryTable = "SftRegistry";

    private readonly TableServiceClient _tableServiceClient;
    private readonly QueueServiceClient _queueServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OnboardingService> _logger;

    public OnboardingService(
        TableServiceClient tableServiceClient,
        QueueServiceClient queueServiceClient,
        IConfiguration configuration,
        ILogger<OnboardingService> logger)
    {
        _tableServiceClient = tableServiceClient;
        _queueServiceClient = queueServiceClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ThirdPartyResponse> RequestProvisioningAsync(ThirdPartyCreateRequest request)
    {
        var tableClient = _tableServiceClient.GetTableClient(RegistryTable);
        await tableClient.CreateIfNotExistsAsync();

        var id = $"tp-{Guid.NewGuid():N}"[..10];
        var containerName = $"{_configuration["Storage:ContainerPrefix"]}{SanitizeCompanyName(request.CompanyName)}";

        var entity = new ThirdParty
        {
            RowKey = id,
            CompanyName = request.CompanyName,
            ContainerName = containerName,
            ContactEmail = request.ContactEmail,
            AutomationEnabled = request.EnableAutomation,
            Status = "provisioning"
        };

        await tableClient.AddEntityAsync(entity);

        // Enqueue provisioning request for the Provisioning Function
        var queueName = _configuration["Storage:ProvisioningQueueName"] ?? "sft-provisioning";
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        await queueClient.CreateIfNotExistsAsync();

        var message = JsonSerializer.Serialize(new
        {
            ThirdPartyId = id,
            entity.CompanyName,
            entity.ContainerName,
            entity.ContactEmail,
            entity.AutomationEnabled
        });
        await queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message)));

        _logger.LogInformation("Provisioning requested for {Company} (container: {Container})", request.CompanyName, containerName);

        return new ThirdPartyResponse(id, request.CompanyName, containerName, null, "provisioning", DateTimeOffset.UtcNow);
    }

    public async Task<ThirdParty?> GetThirdPartyAsync(string id)
    {
        var tableClient = _tableServiceClient.GetTableClient(RegistryTable);
        await tableClient.CreateIfNotExistsAsync();

        try
        {
            var response = await tableClient.GetEntityAsync<ThirdParty>("ThirdParty", id);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ThirdParty>> ListThirdPartiesAsync(int take = 100)
    {
        var tableClient = _tableServiceClient.GetTableClient(RegistryTable);
        await tableClient.CreateIfNotExistsAsync();

        var parties = new List<ThirdParty>();
        await foreach (var entity in tableClient.QueryAsync<ThirdParty>(
            filter: "PartitionKey eq 'ThirdParty'", maxPerPage: take))
        {
            parties.Add(entity);
            if (parties.Count >= take) break;
        }
        return parties;
    }

    public async Task UpdateThirdPartyAsync(ThirdParty thirdParty)
    {
        var tableClient = _tableServiceClient.GetTableClient(RegistryTable);
        await tableClient.UpsertEntityAsync(thirdParty, TableUpdateMode.Replace);
    }

    public async Task RequestDeprovisioningAsync(string id)
    {
        var entity = await GetThirdPartyAsync(id);
        if (entity is null) return;

        entity.Status = "deprovisioning";
        await UpdateThirdPartyAsync(entity);

        // Enqueue deprovisioning request
        var queueName = _configuration["Storage:ProvisioningQueueName"] ?? "sft-provisioning";
        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        await queueClient.CreateIfNotExistsAsync();

        var message = JsonSerializer.Serialize(new
        {
            Action = "deprovision",
            ThirdPartyId = id,
            entity.ContainerName,
            entity.AppRegistrationId
        });
        await queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message)));

        _logger.LogInformation("Deprovisioning requested for {Id} (container: {Container})", id, entity.ContainerName);
    }

    private static string SanitizeCompanyName(string name)
    {
        // Azure container names: lowercase, alphanumeric + hyphens, 3-63 chars
        var sanitized = CompanyNameRegex().Replace(name.ToLowerInvariant(), "-").Trim('-');
        return sanitized.Length > 50 ? sanitized[..50] : sanitized;
    }

    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex CompanyNameRegex();
}
