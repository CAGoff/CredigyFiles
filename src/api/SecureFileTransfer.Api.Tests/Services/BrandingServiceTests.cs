using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Moq;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Services;

public class BrandingServiceTests
{
    private readonly Mock<TableServiceClient> _mockTableServiceClient;
    private readonly Mock<TableClient> _mockTableClient;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<BlobContainerClient> _mockContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;
    private readonly Mock<ILogger<BrandingService>> _mockLogger;
    private readonly BrandingService _service;

    public BrandingServiceTests()
    {
        _mockTableServiceClient = new Mock<TableServiceClient>();
        _mockTableClient = new Mock<TableClient>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockContainerClient = new Mock<BlobContainerClient>();
        _mockBlobClient = new Mock<BlobClient>();
        _mockLogger = new Mock<ILogger<BrandingService>>();

        _mockTableServiceClient
            .Setup(x => x.GetTableClient(It.IsAny<string>()))
            .Returns(_mockTableClient.Object);

        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_mockContainerClient.Object);

        _mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(_mockBlobClient.Object);

        _mockBlobClient
            .Setup(x => x.Uri)
            .Returns(new Uri("https://storage.blob.core.windows.net/sft-branding/logo.png"));

        _service = new BrandingService(
            _mockTableServiceClient.Object,
            _mockBlobServiceClient.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetBrandingAsync_NoRowExists_ReturnsDefaults()
    {
        _mockTableClient
            .Setup(x => x.GetEntityAsync<BrandingSettings>("Branding", "settings", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        var result = await _service.GetBrandingAsync();

        Assert.Equal(BrandingSettings.DefaultAppName, result.AppName);
        Assert.Equal(BrandingSettings.DefaultPrimaryColor, result.PrimaryColor);
        Assert.Null(result.LogoUrl);
        Assert.Null(result.FaviconUrl);
    }

    [Fact]
    public async Task GetBrandingAsync_RowExists_ReturnsStoredValues()
    {
        var settings = new BrandingSettings
        {
            AppName = "Custom App",
            PrimaryColor = "#ff0000",
            LogoBlobName = "logo.png"
        };

        _mockTableClient
            .Setup(x => x.GetEntityAsync<BrandingSettings>("Branding", "settings", null, default))
            .ReturnsAsync(Response.FromValue(settings, Mock.Of<Response>()));

        var result = await _service.GetBrandingAsync();

        Assert.Equal("Custom App", result.AppName);
        Assert.Equal("#ff0000", result.PrimaryColor);
        Assert.NotNull(result.LogoUrl);
        Assert.Null(result.FaviconUrl);
    }

    [Fact]
    public async Task UpdateBrandingAsync_ValidInput_UpsertsAndReturns()
    {
        _mockTableClient
            .Setup(x => x.GetEntityAsync<BrandingSettings>("Branding", "settings", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        var request = new BrandingUpdateRequest("New Name", "#abcdef");
        var result = await _service.UpdateBrandingAsync(request);

        Assert.Equal("New Name", result.AppName);
        Assert.Equal("#abcdef", result.PrimaryColor);

        _mockTableClient.Verify(
            x => x.UpsertEntityAsync(
                It.Is<BrandingSettings>(s => s.AppName == "New Name" && s.PrimaryColor == "#abcdef"),
                TableUpdateMode.Replace,
                default),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateBrandingAsync_BlankAppName_ThrowsArgumentException(string appName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateBrandingAsync(new BrandingUpdateRequest(appName, "#2563eb")));
        Assert.Contains("App name", ex.Message);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#xyz")]
    [InlineData("#12345")]
    [InlineData("2563eb")]
    [InlineData("#2563eb0")]
    public async Task UpdateBrandingAsync_InvalidColor_ThrowsArgumentException(string color)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateBrandingAsync(new BrandingUpdateRequest("App", color)));
        Assert.Contains("hex color", ex.Message);
    }

    [Theory]
    [InlineData("logo.png")]
    [InlineData("logo.svg")]
    [InlineData("logo.jpg")]
    [InlineData("logo.jpeg")]
    public async Task UploadLogoAsync_ValidExtension_Succeeds(string fileName)
    {
        _mockTableClient
            .Setup(x => x.GetEntityAsync<BrandingSettings>("Branding", "settings", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);
        var result = await _service.UploadLogoAsync(stream, fileName);

        Assert.NotNull(result);
        _mockContainerClient.Verify(
            x => x.GetBlobClient(It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task UploadLogoAsync_InvalidExtension_ThrowsArgumentException()
    {
        using var stream = new MemoryStream([0x00]);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UploadLogoAsync(stream, "logo.gif"));
        Assert.Contains(".png", ex.Message);
    }

    [Theory]
    [InlineData("favicon.png")]
    [InlineData("favicon.ico")]
    public async Task UploadFaviconAsync_ValidExtension_Succeeds(string fileName)
    {
        _mockTableClient
            .Setup(x => x.GetEntityAsync<BrandingSettings>("Branding", "settings", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        using var stream = new MemoryStream([0x00, 0x00, 0x01, 0x00]);
        var result = await _service.UploadFaviconAsync(stream, fileName);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UploadFaviconAsync_InvalidExtension_ThrowsArgumentException()
    {
        using var stream = new MemoryStream([0x00]);
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UploadFaviconAsync(stream, "favicon.svg"));
        Assert.Contains(".png", ex.Message);
    }
}
