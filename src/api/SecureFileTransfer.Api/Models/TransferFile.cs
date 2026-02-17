namespace SecureFileTransfer.Api.Models;

public record TransferFile(
    string FileName,
    string Container,
    string Directory,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string AccessTier);

public record FileUploadResponse(
    string FileName,
    string Container,
    string Directory,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    long SizeBytes);

public record FileListResponse(
    string Container,
    string Directory,
    IReadOnlyList<TransferFile> Files);
