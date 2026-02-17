using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public interface IBlobStorageService
{
    Task<IReadOnlyList<string>> ListContainersAsync(string prefix);
    Task<IReadOnlyList<TransferFile>> ListFilesAsync(string containerName, string directory, int take = 100);
    Task<FileUploadResponse> UploadFileAsync(string containerName, string directory, string fileName, Stream content, string uploadedBy);
    Task<Stream?> DownloadFileAsync(string containerName, string directory, string fileName);
    Task DeleteFileAsync(string containerName, string directory, string fileName);
    Task<bool> ContainerExistsAsync(string containerName);
}
