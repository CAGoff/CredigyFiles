using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ListContainersAsync(string prefix)
    {
        var containers = new List<string>();
        await foreach (var container in _blobServiceClient.GetBlobContainersAsync(prefix: prefix))
        {
            containers.Add(container.Name);
        }
        return containers;
    }

    public async Task<IReadOnlyList<TransferFile>> ListFilesAsync(string containerName, string directory)
    {
        var client = _blobServiceClient.GetBlobContainerClient(containerName);
        var files = new List<TransferFile>();
        var prefix = $"{directory}/";

        await foreach (var blob in client.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, default))
        {
            if (blob.Properties.AccessTier == AccessTier.Hot)
            {
                files.Add(new TransferFile(
                    FileName: blob.Name[(prefix.Length)..],
                    Container: containerName,
                    Directory: directory,
                    SizeBytes: blob.Properties.ContentLength ?? 0,
                    UploadedAt: blob.Properties.LastModified ?? DateTimeOffset.MinValue,
                    AccessTier: blob.Properties.AccessTier?.ToString() ?? "Hot"));
            }
        }
        return files;
    }

    public async Task<FileUploadResponse> UploadFileAsync(
        string containerName, string directory, string fileName,
        Stream content, string uploadedBy)
    {
        var client = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobPath = $"{directory}/{fileName}";
        var blobClient = client.GetBlobClient(blobPath);

        // Reject if blob already exists (prevent silent overwrites)
        if (await blobClient.ExistsAsync())
        {
            throw new InvalidOperationException($"File '{fileName}' already exists in {containerName}/{directory}.");
        }

        var response = await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/octet-stream",
                ContentDisposition = $"attachment; filename=\"{fileName}\""
            }
        });

        var properties = await blobClient.GetPropertiesAsync();

        _logger.LogInformation("File uploaded: {Container}/{Path} by {User}", containerName, blobPath, uploadedBy);

        return new FileUploadResponse(
            FileName: fileName,
            Container: containerName,
            Directory: directory,
            UploadedBy: uploadedBy,
            UploadedAt: properties.Value.LastModified,
            SizeBytes: properties.Value.ContentLength);
    }

    public async Task<Stream> DownloadFileAsync(string containerName, string directory, string fileName)
    {
        var client = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = client.GetBlobClient($"{directory}/{fileName}");
        var download = await blobClient.DownloadStreamingAsync();
        return download.Value.Content;
    }

    public async Task DeleteFileAsync(string containerName, string directory, string fileName)
    {
        var client = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = client.GetBlobClient($"{directory}/{fileName}");
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

        _logger.LogInformation("File deleted: {Container}/{Directory}/{FileName}", containerName, directory, fileName);
    }

    public async Task<bool> ContainerExistsAsync(string containerName)
    {
        var client = _blobServiceClient.GetBlobContainerClient(containerName);
        return await client.ExistsAsync();
    }
}
