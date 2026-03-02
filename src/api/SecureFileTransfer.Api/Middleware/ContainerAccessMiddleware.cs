using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Middleware;

/// <summary>
/// Validates that the authenticated caller has access to the requested container.
/// Checks Entra ID security group membership and service principal identity
/// against the third-party registry.
/// </summary>
public static class ContainerAccessExtensions
{
    /// <summary>
    /// Checks the current user's access to the specified container.
    /// Returns a <see cref="ContainerAccessResult"/> indicating access level and delete permission.
    /// </summary>
    public static async Task<ContainerAccessResult> CheckContainerAccessAsync(
        this HttpContext context,
        IActivityService activityService,
        string containerName,
        ILogger logger)
    {
        var userId = context.User.FindFirst("oid")?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Container access denied: no user identity in token for container {Container}", containerName);
            return ContainerAccessResult.None;
        }

        var isAdmin = context.User.IsInRole("SFT.Admin");

        // Extract groups claim (Entra ID sends as multiple "groups" claims)
        var userGroups = context.User.FindAll("groups")
            .Select(c => c.Value)
            .ToList();

        var result = await activityService.UserHasContainerAccessAsync(
            userId, userGroups, containerName, isAdmin);

        if (!result.HasAccess)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
            logger.LogWarning(
                "Container access denied: user {UserId} attempted access to {Container}. CorrelationId: {CorrelationId}",
                userId, containerName, correlationId);
        }

        return result;
    }
}
