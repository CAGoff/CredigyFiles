namespace SecureFileTransfer.Api.Models;

public class FileValidationOptions
{
    public const string Section = "FileValidation";

    public long MaxSizeBytes { get; set; } = 52_428_800; // 50MB
    public string[] AllowedExtensions { get; set; } = [".pdf", ".xlsx", ".xls", ".csv", ".txt"];

    /// <summary>
    /// Maps file extensions to expected magic byte signatures.
    /// Used to verify file content matches the declared extension.
    /// </summary>
    public static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        [".pdf"] = [new byte[] { 0x25, 0x50, 0x44, 0x46 }], // %PDF
        [".xlsx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }], // PK (ZIP archive)
        [".xls"] = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }], // OLE2 compound document
        [".csv"] = [],  // Text files — no magic bytes, validated by extension only
        [".txt"] = [],
    };
}
