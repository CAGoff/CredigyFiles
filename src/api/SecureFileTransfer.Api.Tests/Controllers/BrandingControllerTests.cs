using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SecureFileTransfer.Api.Controllers;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Controllers;

public class BrandingControllerTests
{
    private readonly Mock<IBrandingService> _mockBrandingService;
    private readonly Mock<ILogger<BrandingController>> _mockLogger;
    private readonly BrandingController _controller;

    public BrandingControllerTests()
    {
        _mockBrandingService = new Mock<IBrandingService>();
        _mockLogger = new Mock<ILogger<BrandingController>>();
        _controller = new BrandingController(_mockBrandingService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetBranding_ReturnsOkWithDefaults()
    {
        var expected = new BrandingResponse("Credigy Files", "#2563eb", null, null);
        _mockBrandingService
            .Setup(x => x.GetBrandingAsync())
            .ReturnsAsync(expected);

        var result = await _controller.GetBranding();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<BrandingResponse>(ok.Value);
        Assert.Equal("Credigy Files", response.AppName);
        Assert.Equal("#2563eb", response.PrimaryColor);
    }

    [Fact]
    public async Task UpdateBranding_ValidRequest_ReturnsOk()
    {
        var request = new BrandingUpdateRequest("New App", "#ff0000");
        var expected = new BrandingResponse("New App", "#ff0000", null, null);

        _mockBrandingService
            .Setup(x => x.UpdateBrandingAsync(request))
            .ReturnsAsync(expected);

        var result = await _controller.UpdateBranding(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<BrandingResponse>(ok.Value);
        Assert.Equal("New App", response.AppName);
    }

    [Fact]
    public async Task UpdateBranding_InvalidInput_ReturnsBadRequest()
    {
        var request = new BrandingUpdateRequest("", "#2563eb");

        _mockBrandingService
            .Setup(x => x.UpdateBrandingAsync(request))
            .ThrowsAsync(new ArgumentException("App name is required."));

        var result = await _controller.UpdateBranding(request);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(bad.Value);
    }

    [Fact]
    public async Task UploadLogo_ValidFile_ReturnsOk()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.FileName).Returns("logo.png");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([0x89, 0x50]));

        var expected = new BrandingResponse("Credigy Files", "#2563eb", "https://storage/logo.png", null);
        _mockBrandingService
            .Setup(x => x.UploadLogoAsync(It.IsAny<Stream>(), "logo.png"))
            .ReturnsAsync(expected);

        var result = await _controller.UploadLogo(fileMock.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<BrandingResponse>(ok.Value);
        Assert.NotNull(response.LogoUrl);
    }

    [Fact]
    public async Task UploadLogo_NullFile_ReturnsBadRequest()
    {
        var result = await _controller.UploadLogo(null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadLogo_EmptyFile_ReturnsBadRequest()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(0);

        var result = await _controller.UploadLogo(fileMock.Object);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadLogo_InvalidFileType_ReturnsBadRequest()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.FileName).Returns("logo.gif");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([0x47, 0x49]));

        _mockBrandingService
            .Setup(x => x.UploadLogoAsync(It.IsAny<Stream>(), "logo.gif"))
            .ThrowsAsync(new ArgumentException("Logo must be one of: .png, .svg, .jpg, .jpeg"));

        var result = await _controller.UploadLogo(fileMock.Object);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadFavicon_ValidFile_ReturnsOk()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(512);
        fileMock.Setup(f => f.FileName).Returns("favicon.ico");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream([0x00, 0x00]));

        var expected = new BrandingResponse("Credigy Files", "#2563eb", null, "https://storage/favicon.ico");
        _mockBrandingService
            .Setup(x => x.UploadFaviconAsync(It.IsAny<Stream>(), "favicon.ico"))
            .ReturnsAsync(expected);

        var result = await _controller.UploadFavicon(fileMock.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<BrandingResponse>(ok.Value);
        Assert.NotNull(response.FaviconUrl);
    }

    [Fact]
    public async Task UploadFavicon_NullFile_ReturnsBadRequest()
    {
        var result = await _controller.UploadFavicon(null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
