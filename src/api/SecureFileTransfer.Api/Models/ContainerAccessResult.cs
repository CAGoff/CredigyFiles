namespace SecureFileTransfer.Api.Models;

/// <summary>
/// Result of a container access check. HasAccess gates all operations;
/// CanDelete additionally gates file deletion.
/// </summary>
public record ContainerAccessResult(bool HasAccess, bool CanDelete)
{
    public static readonly ContainerAccessResult None = new(false, false);
    public static readonly ContainerAccessResult ReadOnly = new(true, false);
    public static readonly ContainerAccessResult Full = new(true, true);
}
