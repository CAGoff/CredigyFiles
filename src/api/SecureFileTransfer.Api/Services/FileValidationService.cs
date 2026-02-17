using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public static partial class FileValidationService
{
    /// <summary>
    /// Validates the directory parameter is a strict enum value.
    /// </summary>
    public static bool IsValidDirectory(string? dir)
    {
        return dir is "inbound" or "outbound";
    }

    /// <summary>
    /// Sanitizes a filename: strips path components, special characters.
    /// Returns null if the filename is invalid after sanitization.
    /// </summary>
    public static string? SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        // Strip path components
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Remove characters that aren't alphanumeric, dash, underscore, or dot
        name = SafeFileNameRegex().Replace(name, "_");

        // Prevent names that are only dots or whitespace
        if (string.IsNullOrWhiteSpace(name.Replace(".", "").Replace("_", "")))
            return null;

        return name.Length > 255 ? name[..255] : name;
    }

    /// <summary>
    /// Validates file extension is in the allowlist.
    /// </summary>
    public static bool HasAllowedExtension(string fileName, FileValidationOptions options)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return options.AllowedExtensions.Contains(extension);
    }

    /// <summary>
    /// Validates file content matches the declared extension via magic bytes.
    /// Returns true if validation passes or no magic bytes are defined for the extension.
    /// </summary>
    public static async Task<bool> ValidateMagicBytesAsync(Stream content, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!FileValidationOptions.MagicBytes.TryGetValue(extension, out var signatures) || signatures.Length == 0)
            return true; // No magic bytes defined — pass

        var buffer = new byte[8];
        var originalPosition = content.Position;
        var bytesRead = await content.ReadAsync(buffer);
        content.Position = originalPosition;

        if (bytesRead < 4) return false;

        return signatures.Any(sig => buffer.AsSpan(0, sig.Length).SequenceEqual(sig));
    }

    [GeneratedRegex(@"[^a-zA-Z0-9\-_\.]")]
    private static partial Regex SafeFileNameRegex();
}
