using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SecureFileTransfer.Api.Controllers;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Controllers;

public class ContainersControllerTests
{
    private readonly Mock<IBlobStorageService> _mockBlobStorage;
    private readonly Mock<IActivityService> _mockActivityService;
    private readonly Mock<ILogger<ContainersController>> _mockLogger;
    private readonly IConfiguration _configuration;

    public ContainersControllerTests()
    {
        _mockBlobStorage = new Mock<IBlobStorageService>();
        _mockActivityService = new Mock<IActivityService>();
        _mockLogger = new Mock<ILogger<ContainersController>>();

        var configData = new Dictionary<string, string?>
        {
            ["Storage:ContainerPrefix"] = "sft-"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public async Task ListContainers_AdminUser_SeesAllAccessibleContainers()
    {
        var allContainers = new List<string> { "sft-acme", "sft-globex", "sft-admin" };
        var accessibleContainers = new List<string> { "sft-acme", "sft-globex", "sft-admin" };

        _mockBlobStorage
            .Setup(x => x.ListContainersAsync("sft-"))
            .ReturnsAsync(allContainers);

        _mockActivityService
            .Setup(x => x.GetAccessibleContainersAsync("admin-oid", It.IsAny<IReadOnlyList<string>>(), true))
            .ReturnsAsync(accessibleContainers);

        var controller = CreateController("admin-oid", "SFT.Admin");
        var result = await controller.ListContainers();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ListContainers_UserWithGroups_SeesOnlyGroupContainers()
    {
        var allContainers = new List<string> { "sft-acme", "sft-globex" };
        var accessibleContainers = new List<string> { "sft-acme" };

        _mockBlobStorage
            .Setup(x => x.ListContainersAsync("sft-"))
            .ReturnsAsync(allContainers);

        _mockActivityService
            .Setup(x => x.GetAccessibleContainersAsync("user-oid", It.IsAny<IReadOnlyList<string>>(), false))
            .ReturnsAsync(accessibleContainers);

        var controller = CreateController("user-oid", groups: new[] { "group-acme-users" });
        var result = await controller.ListContainers();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ListContainers_OrgUserWithoutGroups_SeesNoContainers()
    {
        var allContainers = new List<string> { "sft-acme", "sft-globex" };
        var accessibleContainers = new List<string>();

        _mockBlobStorage
            .Setup(x => x.ListContainersAsync("sft-"))
            .ReturnsAsync(allContainers);

        _mockActivityService
            .Setup(x => x.GetAccessibleContainersAsync("org-user", It.IsAny<IReadOnlyList<string>>(), false))
            .ReturnsAsync(accessibleContainers);

        var controller = CreateController("org-user", "SFT.User");
        var result = await controller.ListContainers();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ListContainers_ServicePrincipal_SeesOnlyTheirContainer()
    {
        var allContainers = new List<string> { "sft-acme", "sft-globex" };
        var accessibleContainers = new List<string> { "sft-acme" };

        _mockBlobStorage
            .Setup(x => x.ListContainersAsync("sft-"))
            .ReturnsAsync(allContainers);

        _mockActivityService
            .Setup(x => x.GetAccessibleContainersAsync("sp-acme", It.IsAny<IReadOnlyList<string>>(), false))
            .ReturnsAsync(accessibleContainers);

        var controller = CreateController("sp-acme");
        var result = await controller.ListContainers();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ListContainers_NoUserIdentity_ReturnsUnauthorized()
    {
        var controller = CreateControllerWithoutIdentity();
        var result = await controller.ListContainers();

        Assert.IsType<UnauthorizedResult>(result);
    }

    private ContainersController CreateController(string userId, string? role = null, string[]? groups = null)
    {
        var claims = new List<Claim> { new("oid", userId) };
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));
        if (groups is not null)
            foreach (var g in groups)
                claims.Add(new Claim("groups", g));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var controller = new ContainersController(
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

    private ContainersController CreateControllerWithoutIdentity()
    {
        var controller = new ContainersController(
            _mockBlobStorage.Object,
            _mockActivityService.Object,
            _configuration,
            _mockLogger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        return controller;
    }
}
