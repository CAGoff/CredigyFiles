using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SecureFileTransfer.Functions.Services;

public class RbacService : IRbacService
{
    // Storage Blob Data Contributor role definition ID
    private static readonly string BlobDataContributorRoleId = "ba92f5b4-2d11-453d-a403-e96b0029c9fe";

    private readonly ArmClient _armClient;
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private readonly string _storageAccountName;
    private readonly ILogger<RbacService> _logger;

    public RbacService(
        [FromKeyedServices("provision")] DefaultAzureCredential credential,
        ILogger<RbacService> logger)
    {
        _armClient = new ArmClient(credential);
        _subscriptionId = Environment.GetEnvironmentVariable("SubscriptionId")
            ?? throw new InvalidOperationException("SubscriptionId is required");
        _resourceGroupName = Environment.GetEnvironmentVariable("ResourceGroupName")
            ?? throw new InvalidOperationException("ResourceGroupName is required");
        _storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName")
            ?? throw new InvalidOperationException("StorageAccountName is required");
        _logger = logger;
    }

    public async Task AssignStorageBlobDataContributorAsync(
        string containerName, string principalId, string principalType)
    {
        var scope = GetContainerScope(containerName);
        var scopeId = new Azure.Core.ResourceIdentifier(scope);
        var roleDefinitionId = new Azure.Core.ResourceIdentifier(
            $"/subscriptions/{_subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{BlobDataContributorRoleId}");

        var assignmentName = Guid.NewGuid().ToString();
        var assignmentResource = _armClient.GetRoleAssignmentResource(
            RoleAssignmentResource.CreateResourceIdentifier(scope, assignmentName));

        var content = new RoleAssignmentCreateOrUpdateContent(roleDefinitionId, new Guid(principalId))
        {
            PrincipalType = principalType == "ServicePrincipal"
                ? RoleManagementPrincipalType.ServicePrincipal
                : RoleManagementPrincipalType.User
        };

        await assignmentResource.UpdateAsync(Azure.WaitUntil.Completed, content);
        _logger.LogInformation(
            "RBAC assigned: {PrincipalId} ({Type}) → Blob Data Contributor on {Container}",
            principalId, principalType, containerName);
    }

    public async Task RemoveAllRoleAssignmentsAsync(string containerName)
    {
        var scope = GetContainerScope(containerName);
        var scopeId = new Azure.Core.ResourceIdentifier(scope);

        var assignments = _armClient.GetRoleAssignments(scopeId);
        var removed = 0;

        await foreach (var assignment in assignments.GetAllAsync())
        {
            if (assignment.Data.RoleDefinitionId.ToString().Contains(BlobDataContributorRoleId))
            {
                await assignment.DeleteAsync(Azure.WaitUntil.Completed);
                removed++;
            }
        }

        _logger.LogInformation(
            "Removed {Count} RBAC assignments from container {Container}",
            removed, containerName);
    }

    private string GetContainerScope(string containerName)
    {
        return $"/subscriptions/{_subscriptionId}" +
               $"/resourceGroups/{_resourceGroupName}" +
               $"/providers/Microsoft.Storage/storageAccounts/{_storageAccountName}" +
               $"/blobServices/default/containers/{containerName}";
    }
}
