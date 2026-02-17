using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SecureFileTransfer.Api.Controllers;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Controllers;

public class FilesControllerTests
{
    private readonly Mock<IBlobStorageService> _mockBlobStorage;
    private readonly Mock<IActivityService> _mockActivityService;
    private readonly Mock<ILogger<FilesController>> _mockLogger;
    private readonly IConfiguration _configuration;

    public FilesControllerTests()
    {
        _mockBlobStorage = new Mock<IBlobStorageService>();
        _mockActivityService = new Mock<IActivityService>();
        _mockLogger = new Mock<ILogger<FilesController>>();

        var configData = new Dictionary<string, string?>
        {
            ["Storage:ContainerPrefix"] = "sft-",
            ["FileValidation:MaxSizeBytes"] = "52428800",
            ["FileValidation:AllowedExtensions:0"] = ".pdf",
            ["FileValidation:AllowedExtensions:1"] = ".xlsx",
            ["FileValidation:AllowedExtensions:2"] = ".xls",
            ["FileValidation:AllowedExtensions:3"] = ".csv",
            ["FileValidation:AllowedExtensions:4"] = ".txt",
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    // --- ListFiles ---

    [Fact]
    public async Task ListFiles_InvalidDirectory_ReturnsBadRequest()
    {
        var controller = CreateController("admin-oid", "SFT.Admin");
        var result = await controller.ListFiles("sft-acme", "invalid");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task ListFiles_AccessDenied_ReturnsForbid()
    {
        SetupAccessDenied();

        var controller = CreateController("sp-wrong");
        var result = await controller.ListFiles("sft-acme", "inbound");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ListFiles_ValidRequest_ReturnsFiles()
    {
        SetupAccessGranted();
        var files = new List<TransferFile>
        {
            new("report.pdf", "sft-acme", "inbound", 1024, DateTimeOffset.UtcNow, "Hot")
        };
        _mockBlobStorage.Setup(x => x.ListFilesAsync("sft-acme", "inbound")).ReturnsAsync(files);

        var controller = CreateController("admin-oid", "SFT.Admin");
        var result = await controller.ListFiles("sft-acme", "inbound");

        Assert.IsType<OkObjectResult>(result);
    }

    // --- UploadFile ---

    [Fact]
    public async Task UploadFile_InvalidDirectory_ReturnsBadRequest()
    {
        var controller = CreateController("admin-oid", "SFT.Admin");
        var file = CreateMockFile("report.pdf", PdfContent());

        var result = await controller.UploadFile("sft-acme", "wrong", file);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadFile_AccessDenied_ReturnsForbid()
    {
        SetupAccessDenied();

        var controller = CreateController("sp-wrong");
        var file = CreateMockFile("report.pdf", PdfContent());

        var result = await controller.UploadFile("sft-acme", "inbound", file);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UploadFile_NullFile_ReturnsBadRequest()
    {
        SetupAccessGranted();

        var controller = CreateController("admin-oid", "SFT.Admin");
        var result = await controller.UploadFile("sft-acme", "inbound", null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadFile_DisallowedExtension_ReturnsBadRequest()
    {
        SetupAccessGranted();

        var controller = CreateController("admin-oid", "SFT.Admin");
        var file = CreateMockFile("script.exe", new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00 });

        var result = await controller.UploadFile("sft-acme", "inbound", file);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task UploadFile_MagicByteMismatch_ReturnsBadRequest()
    {
        SetupAccessGranted();

        var controller = CreateController("admin-oid", "SFT.Admin");
        // .pdf extension but MZ (executable) bytes
        var file = CreateMockFile("report.pdf", new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00 });

        var result = await controller.UploadFile("sft-acme", "inbound", file);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadFile_DuplicateFile_ReturnsConflict()
    {
        SetupAccessGranted();
        _mockBlobStorage
            .Setup(x => x.UploadFileAsync("sft-acme", "inbound", "report.pdf", It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("File 'report.pdf' already exists in sft-acme/inbound."));

        var controller = CreateController("admin-oid", "SFT.Admin");
        var file = CreateMockFile("report.pdf", PdfContent());

        var result = await controller.UploadFile("sft-acme", "inbound", file);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task UploadFile_ValidPdf_ReturnsCreated()
    {
        SetupAccessGranted();
        var response = new FileUploadResponse("report.pdf", "sft-acme", "inbound", "admin@test.com", DateTimeOffset.UtcNow, 1024);
        _mockBlobStorage
            .Setup(x => x.UploadFileAsync("sft-acme", "inbound", "report.pdf", It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        var controller = CreateController("admin-oid", "SFT.Admin");
        var file = CreateMockFile("report.pdf", PdfContent());

        var result = await controller.UploadFile("sft-acme", "inbound", file);

        Assert.IsType<CreatedResult>(result);
    }

    // --- DownloadFile ---

    [Fact]
    public async Task DownloadFile_InvalidDirectory_ReturnsBadRequest()
    {
        var controller = CreateController("admin-oid", "SFT.Admin");
        var result = await controller.DownloadFile("sft-acme", "report.pdf", "wrong");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DownloadFile_InvalidFileName_ReturnsBadRequest()
    {
        SetupAccessGranted();

        var controller = CreateController("admin-oid", "SFT.Admin");
        var result = await controller.DownloadFile("sft-acme", "...", "inbound");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- DeleteFile ---

    [Fact]
    public async Task DeleteFile_ValidRequest_ReturnsNoContent()
    {
        SetupAccessGranted();

        var controller = CreateController("admin-oid", "SFT.Admin");
        var result = await controller.DeleteFile("sft-acme", "report.pdf", "inbound");

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteFile_InvalidDirectory_ReturnsBadRequest()
    {
        var controller = CreateController("admin-oid", "SFT.Admin");
        var result = await controller.DeleteFile("sft-acme", "report.pdf", "wrong");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- Helpers ---

    private static byte[] PdfContent() => [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37];

    private static IFormFile CreateMockFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.Length).Returns(content.Length);
        mock.Setup(f => f.OpenReadStream()).Returns(() =>
        {
            stream.Position = 0;
            return stream;
        });
        return mock.Object;
    }

    private void SetupAccessGranted()
    {
        _mockActivityService
            .Setup(x => x.UserHasContainerAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
    }

    private void SetupAccessDenied()
    {
        _mockActivityService
            .Setup(x => x.UserHasContainerAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(false);
    }

    private FilesController CreateController(string userId, string? role = null)
    {
        var claims = new List<Claim>
        {
            new("oid", userId),
            new("preferred_username", "test@org.com"),
        };
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var controller = new FilesController(
            _mockBlobStorage.Object,
            _mockActivityService.Object,
            _configuration,
            _mockLogger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }
}
