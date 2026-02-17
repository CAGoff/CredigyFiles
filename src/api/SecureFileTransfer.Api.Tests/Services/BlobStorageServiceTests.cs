using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Moq;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Services;

public class BlobStorageServiceTests
{
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<BlobContainerClient> _mockContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;
    private readonly Mock<ILogger<BlobStorageService>> _mockLogger;
    private readonly BlobStorageService _service;

    public BlobStorageServiceTests()
    {
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockContainerClient = new Mock<BlobContainerClient>();
        _mockBlobClient = new Mock<BlobClient>();
        _mockLogger = new Mock<ILogger<BlobStorageService>>();

        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_mockContainerClient.Object);

        _mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(_mockBlobClient.Object);

        _service = new BlobStorageService(_mockBlobServiceClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task UploadFileAsync_ExistingBlob_ThrowsFileAlreadyExists()
    {
        _mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var ex = await Assert.ThrowsAsync<FileAlreadyExistsException>(
            () => _service.UploadFileAsync("sft-acme", "inbound", "existing.pdf", Stream.Null, "user@test.com"));
        Assert.Equal("existing.pdf", ex.FileName);
    }

    [Fact]
    public async Task ContainerExistsAsync_ExistingContainer_ReturnsTrue()
    {
        _mockContainerClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var result = await _service.ContainerExistsAsync("sft-acme");

        Assert.True(result);
    }

    [Fact]
    public async Task ContainerExistsAsync_NonExistentContainer_ReturnsFalse()
    {
        _mockContainerClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        var result = await _service.ContainerExistsAsync("sft-nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteFileAsync_CallsDeleteOnBlobClient()
    {
        _mockBlobClient
            .Setup(x => x.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        await _service.DeleteFileAsync("sft-acme", "inbound", "report.pdf");

        _mockBlobClient.Verify(x => x.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadFileAsync_BlobNotFound_ReturnsNull()
    {
        _mockBlobClient
            .Setup(x => x.DownloadStreamingAsync(
                It.IsAny<BlobDownloadOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "BlobNotFound"));

        var result = await _service.DownloadFileAsync("sft-acme", "inbound", "missing.pdf");

        Assert.Null(result);
    }

    [Fact]
    public void ConstructsBlobPath_WithDirectoryPrefix()
    {
        _service.DeleteFileAsync("sft-acme", "inbound", "report.pdf");

        _mockContainerClient.Verify(x => x.GetBlobClient("inbound/report.pdf"), Times.Once);
    }
}
