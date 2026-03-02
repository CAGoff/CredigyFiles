using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SecureFileTransfer.Api.Controllers;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Controllers;

public class AdminControllerTests
{
    private readonly Mock<IOnboardingService> _mockOnboarding;
    private readonly Mock<ILogger<AdminController>> _mockLogger;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _mockOnboarding = new Mock<IOnboardingService>();
        _mockLogger = new Mock<ILogger<AdminController>>();
        _controller = new AdminController(_mockOnboarding.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task UpdateThirdPartyStatus_ActiveParty_Returns202()
    {
        var party = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            CompanyName = "Acme",
            Status = "active"
        };

        _mockOnboarding
            .Setup(x => x.GetThirdPartyAsync("tp-001"))
            .ReturnsAsync(party);

        var result = await _controller.UpdateThirdPartyStatus("tp-001", new ThirdPartyStatusRequest("inactive"));

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(accepted.Value);

        _mockOnboarding.Verify(x => x.RequestDeactivationAsync("tp-001"), Times.Once);
    }

    [Fact]
    public async Task UpdateThirdPartyStatus_NotFound_Returns404()
    {
        _mockOnboarding
            .Setup(x => x.GetThirdPartyAsync("tp-missing"))
            .ReturnsAsync((ThirdParty?)null);

        var result = await _controller.UpdateThirdPartyStatus("tp-missing", new ThirdPartyStatusRequest("inactive"));

        Assert.IsType<NotFoundObjectResult>(result);
        _mockOnboarding.Verify(x => x.RequestDeactivationAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateThirdPartyStatus_EmptyStatus_Returns400()
    {
        var result = await _controller.UpdateThirdPartyStatus("tp-001", new ThirdPartyStatusRequest(""));

        Assert.IsType<BadRequestObjectResult>(result);
        _mockOnboarding.Verify(x => x.GetThirdPartyAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateThirdPartyStatus_UnsupportedStatus_Returns400()
    {
        var result = await _controller.UpdateThirdPartyStatus("tp-001", new ThirdPartyStatusRequest("active"));

        Assert.IsType<BadRequestObjectResult>(result);
        _mockOnboarding.Verify(x => x.GetThirdPartyAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateThirdPartyStatus_NonActiveParty_Returns409()
    {
        var party = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            CompanyName = "Acme",
            Status = "provisioning"
        };

        _mockOnboarding
            .Setup(x => x.GetThirdPartyAsync("tp-001"))
            .ReturnsAsync(party);

        var result = await _controller.UpdateThirdPartyStatus("tp-001", new ThirdPartyStatusRequest("inactive"));

        Assert.IsType<ConflictObjectResult>(result);
        _mockOnboarding.Verify(x => x.RequestDeactivationAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateThirdPartyStatus_InactiveParty_Returns409()
    {
        var party = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            CompanyName = "Acme",
            Status = "inactive"
        };

        _mockOnboarding
            .Setup(x => x.GetThirdPartyAsync("tp-001"))
            .ReturnsAsync(party);

        var result = await _controller.UpdateThirdPartyStatus("tp-001", new ThirdPartyStatusRequest("inactive"));

        Assert.IsType<ConflictObjectResult>(result);
        _mockOnboarding.Verify(x => x.RequestDeactivationAsync(It.IsAny<string>()), Times.Never);
    }

    // --- UpdateThirdParty (PUT) with group IDs ---

    [Fact]
    public async Task UpdateThirdParty_SetsGroupIds()
    {
        var party = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            CompanyName = "Acme",
            ContainerName = "sft-acme",
            Status = "active"
        };

        _mockOnboarding
            .Setup(x => x.GetThirdPartyAsync("tp-001"))
            .ReturnsAsync(party);

        var request = new ThirdPartyCreateRequest("Acme Updated", "admin@acme.com", true,
            UserGroupId: "group-acme-users", AdminGroupId: "group-acme-admins");

        var result = await _controller.UpdateThirdParty("tp-001", request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        _mockOnboarding.Verify(x => x.UpdateThirdPartyAsync(
            It.Is<ThirdParty>(tp =>
                tp.UserGroupId == "group-acme-users" &&
                tp.AdminGroupId == "group-acme-admins")),
            Times.Once);
    }

    [Fact]
    public async Task UpdateThirdParty_NullGroupIds_ClearsGroupIds()
    {
        var party = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            CompanyName = "Acme",
            ContainerName = "sft-acme",
            Status = "active",
            UserGroupId = "old-group",
            AdminGroupId = "old-admin-group"
        };

        _mockOnboarding
            .Setup(x => x.GetThirdPartyAsync("tp-001"))
            .ReturnsAsync(party);

        var request = new ThirdPartyCreateRequest("Acme Updated", "admin@acme.com", true);

        var result = await _controller.UpdateThirdParty("tp-001", request);

        Assert.IsType<OkObjectResult>(result);

        _mockOnboarding.Verify(x => x.UpdateThirdPartyAsync(
            It.Is<ThirdParty>(tp =>
                tp.UserGroupId == null &&
                tp.AdminGroupId == null)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateThirdParty_NotFound_Returns404()
    {
        _mockOnboarding
            .Setup(x => x.GetThirdPartyAsync("tp-missing"))
            .ReturnsAsync((ThirdParty?)null);

        var request = new ThirdPartyCreateRequest("Acme", "admin@acme.com", true);
        var result = await _controller.UpdateThirdParty("tp-missing", request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- GetThirdParty with group IDs ---

    [Fact]
    public async Task GetThirdParty_ReturnsGroupIdsInResponse()
    {
        var party = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            CompanyName = "Acme",
            ContainerName = "sft-acme",
            Status = "active",
            UserGroupId = "group-acme-users",
            AdminGroupId = "group-acme-admins"
        };

        _mockOnboarding
            .Setup(x => x.GetThirdPartyAsync("tp-001"))
            .ReturnsAsync(party);

        var result = await _controller.GetThirdParty("tp-001");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ThirdPartyResponse>(okResult.Value);
        Assert.Equal("group-acme-users", response.UserGroupId);
        Assert.Equal("group-acme-admins", response.AdminGroupId);
    }

    // --- CreateThirdParty with group IDs ---

    [Fact]
    public async Task CreateThirdParty_PassesGroupIdsToService()
    {
        var expectedResponse = new ThirdPartyResponse(
            "tp-001", "Acme", "sft-acme", null, "provisioning", DateTimeOffset.UtcNow,
            "group-acme-users", "group-acme-admins");

        _mockOnboarding
            .Setup(x => x.RequestProvisioningAsync(It.IsAny<ThirdPartyCreateRequest>()))
            .ReturnsAsync(expectedResponse);

        var request = new ThirdPartyCreateRequest("Acme", "admin@acme.com", true,
            UserGroupId: "group-acme-users", AdminGroupId: "group-acme-admins");

        var result = await _controller.CreateThirdParty(request);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.NotNull(created.Value);

        _mockOnboarding.Verify(x => x.RequestProvisioningAsync(
            It.Is<ThirdPartyCreateRequest>(r =>
                r.UserGroupId == "group-acme-users" &&
                r.AdminGroupId == "group-acme-admins")),
            Times.Once);
    }
}
