using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public interface IActivityService
{
    Task LogActivityAsync(string containerName, string action, string fileName, string directory, string performedBy, long sizeBytes, string correlationId);
    Task<IReadOnlyList<ActivityRecord>> GetActivityAsync(string containerName, int take = 50);
    Task<IReadOnlyList<ActivityRecord>> GetAllActivityAsync(int take = 100);
    Task<ContainerAccessResult> UserHasContainerAccessAsync(string userId, IReadOnlyList<string> userGroups, string containerName, bool isAdmin);
    Task<IReadOnlyList<string>> GetAccessibleContainersAsync(string userId, IReadOnlyList<string> userGroups, bool isAdmin);
}
