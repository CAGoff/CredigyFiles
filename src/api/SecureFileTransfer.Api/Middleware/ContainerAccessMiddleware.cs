using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Middleware;

/// <summary>
/// Validates that the authenticated caller has access to the requested container.
/// Checks the caller's identity against the third-party registry.
/// </summary>
public static class ContainerAccessExtensions
{
    /// <summary>
    /// Verifies the current user has access to the specified container.
    /// Returns true if access is granted, false otherwise.
    /// </summary>
    public static async Task<bool> HasContainerAccessAsync(
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
            return false;
        }

        var isAdmin = context.User.IsInRole("SFT.Admin");
        var isOrgUser = context.User.IsInRole("SFT.User");

        var hasAccess = await activityService.UserHasContainerAccessAsync(
            userId, containerName, isAdmin, isOrgUser);

        if (!hasAccess)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
            logger.LogWarning(
                "Container access denied: user {UserId} attempted access to {Container}. CorrelationId: {CorrelationId}",
                userId, containerName, correlationId);
        }

        return hasAccess;
    }
}
