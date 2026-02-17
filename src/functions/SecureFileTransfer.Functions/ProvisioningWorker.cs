using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SecureFileTransfer.Functions.Services;

namespace SecureFileTransfer.Functions;

/// <summary>
/// Processes provisioning/deprovisioning requests from the Storage Queue.
/// Creates containers, app registrations, and RBAC assignments.
///
/// Uses user-assigned managed identity: id-sft-func-provision
/// </summary>
public class ProvisioningWorker
{
    private readonly TableServiceClient _tableClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IGraphProvisioningService _graphService;
    private readonly IRbacService _rbacService;
    private readonly ILogger<ProvisioningWorker> _logger;

    public ProvisioningWorker(
        TableServiceClient tableClient,
        BlobServiceClient blobServiceClient,
        IGraphProvisioningService graphService,
        IRbacService rbacService,
        ILogger<ProvisioningWorker> logger)
    {
        _tableClient = tableClient;
        _blobServiceClient = blobServiceClient;
        _graphService = graphService;
        _rbacService = rbacService;
        _logger = logger;
    }

    [Function(nameof(ProvisioningWorker))]
    public async Task Run(
        [QueueTrigger("sft-provisioning", Connection = "AzureWebJobsStorage")] string message)
    {
        _logger.LogInformation("Provisioning message received");

        var request = JsonSerializer.Deserialize<ProvisioningRequest>(message, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (request is null)
        {
            _logger.LogError("Failed to deserialize provisioning request");
            return;
        }

        if (request.Action == "deprovision")
        {
            await DeprovisionAsync(request);
            return;
        }

        await ProvisionAsync(request);
    }

    private async Task ProvisionAsync(ProvisioningRequest request)
    {
        _logger.LogInformation("Provisioning third party: {Company} ({Container})",
            request.CompanyName, request.ContainerName);

        // Step 1: Create blob container
        var containerClient = _blobServiceClient.GetBlobContainerClient(request.ContainerName);
        await containerClient.CreateIfNotExistsAsync();

        var inboundBlob = containerClient.GetBlobClient("inbound/.keep");
        var outboundBlob = containerClient.GetBlobClient("outbound/.keep");
        await inboundBlob.UploadAsync(new BinaryData(""), overwrite: true);
        await outboundBlob.UploadAsync(new BinaryData(""), overwrite: true);

        _logger.LogInformation("Container created: {Container}", request.ContainerName);

        // Step 2: Create app registration (if automation enabled)
        AppRegistrationResult? appResult = null;
        if (request.AutomationEnabled)
        {
            appResult = await _graphService.CreateAppRegistrationAsync(
                request.CompanyName, request.ContainerName);
            _logger.LogInformation(
                "App registration created for {Company}: AppId={AppId}, SPId={SpId}",
                request.CompanyName, appResult.ApplicationId, appResult.ServicePrincipalObjectId);
        }

        // Step 3: Assign RBAC
        var apiIdentityObjectId = Environment.GetEnvironmentVariable("ApiManagedIdentityObjectId")
            ?? throw new InvalidOperationException("ApiManagedIdentityObjectId is required");

        await _rbacService.AssignStorageBlobDataContributorAsync(
            request.ContainerName, apiIdentityObjectId, "ServicePrincipal");

        if (appResult is not null)
        {
            await _rbacService.AssignStorageBlobDataContributorAsync(
                request.ContainerName, appResult.ServicePrincipalObjectId, "ServicePrincipal");
        }

        // Step 4: Update registry status to active
        var registryTable = _tableClient.GetTableClient("SftRegistry");
        try
        {
            var entity = await registryTable.GetEntityAsync<TableEntity>(
                "ThirdParty", request.ThirdPartyId);
            entity.Value["Status"] = "active";

            if (appResult is not null)
            {
                entity.Value["AppRegistrationId"] = appResult.ApplicationId;
                entity.Value["ServicePrincipalObjectId"] = appResult.ServicePrincipalObjectId;
                entity.Value["CertificateThumbprint"] = appResult.CertificateThumbprint;
            }

            await registryTable.UpdateEntityAsync(
                entity.Value, entity.Value.ETag, TableUpdateMode.Replace);
            _logger.LogInformation("Third party {Id} status updated to active", request.ThirdPartyId);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError("Third party record {Id} not found in registry", request.ThirdPartyId);
        }
    }

    private async Task DeprovisionAsync(ProvisioningRequest request)
    {
        _logger.LogInformation("Deprovisioning third party: {Container}", request.ContainerName);

        // Step 1: Delete app registration if exists
        if (!string.IsNullOrEmpty(request.AppRegistrationId))
        {
            await _graphService.DeleteAppRegistrationAsync(request.AppRegistrationId);
            _logger.LogInformation("App registration {AppId} deleted", request.AppRegistrationId);
        }

        // Step 2: Remove RBAC assignments
        await _rbacService.RemoveAllRoleAssignmentsAsync(request.ContainerName);

        // Step 3: Soft-delete the container (retained per policy)
        var containerClient = _blobServiceClient.GetBlobContainerClient(request.ContainerName);
        await containerClient.DeleteIfExistsAsync();

        // Step 4: Update registry status
        var registryTable = _tableClient.GetTableClient("SftRegistry");
        try
        {
            var entity = await registryTable.GetEntityAsync<TableEntity>(
                "ThirdParty", request.ThirdPartyId);
            entity.Value["Status"] = "inactive";
            await registryTable.UpdateEntityAsync(
                entity.Value, entity.Value.ETag, TableUpdateMode.Replace);
            _logger.LogInformation("Third party {Id} status updated to inactive", request.ThirdPartyId);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogError("Third party record {Id} not found in registry", request.ThirdPartyId);
        }
    }
}

public record ProvisioningRequest(
    string ThirdPartyId,
    string CompanyName,
    string ContainerName,
    string ContactEmail,
    bool AutomationEnabled,
    string? Action = null,
    string? AppRegistrationId = null);
