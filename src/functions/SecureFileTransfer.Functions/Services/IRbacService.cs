namespace SecureFileTransfer.Functions.Services;

public interface IRbacService
{
    Task AssignStorageBlobDataContributorAsync(string containerName, string principalId, string principalType);
    Task RemoveAllRoleAssignmentsAsync(string containerName);
}
