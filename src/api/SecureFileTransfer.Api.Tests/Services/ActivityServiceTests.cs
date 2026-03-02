using Azure;
using Azure.Data.Tables;
using Moq;
using Microsoft.Extensions.Logging;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Services;

public class ActivityServiceTests
{
    private readonly Mock<TableServiceClient> _mockTableServiceClient;
    private readonly Mock<TableClient> _mockTableClient;
    private readonly Mock<ILogger<ActivityService>> _mockLogger;
    private readonly ActivityService _service;

    public ActivityServiceTests()
    {
        _mockTableServiceClient = new Mock<TableServiceClient>();
        _mockTableClient = new Mock<TableClient>();
        _mockLogger = new Mock<ILogger<ActivityService>>();

        _mockTableServiceClient
            .Setup(x => x.GetTableClient(It.IsAny<string>()))
            .Returns(_mockTableClient.Object);

        _service = new ActivityService(_mockTableServiceClient.Object, _mockLogger.Object);
    }

    // --- UserHasContainerAccessAsync ---

    [Fact]
    public async Task UserHasContainerAccessAsync_AdminUser_ReturnsFull()
    {
        var thirdParty = CreateThirdParty("sft-acme", "active", "sp-other");
        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "admin-user-id", Array.Empty<string>(), "sft-acme", isAdmin: true);

        Assert.True(result.HasAccess);
        Assert.True(result.CanDelete);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_UserGroupMatch_ReturnsReadOnly()
    {
        var thirdParty = CreateThirdParty("sft-acme", "active", "sp-other",
            userGroupId: "group-acme-users");
        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "some-user", new[] { "group-acme-users" }, "sft-acme", isAdmin: false);

        Assert.True(result.HasAccess);
        Assert.False(result.CanDelete);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_AdminGroupMatch_ReturnsFull()
    {
        var thirdParty = CreateThirdParty("sft-acme", "active", "sp-other",
            userGroupId: "group-acme-users", adminGroupId: "group-acme-admins");
        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "some-user", new[] { "group-acme-admins" }, "sft-acme", isAdmin: false);

        Assert.True(result.HasAccess);
        Assert.True(result.CanDelete);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_BothGroupsMatch_ReturnsFull()
    {
        var thirdParty = CreateThirdParty("sft-acme", "active", "sp-other",
            userGroupId: "group-acme-users", adminGroupId: "group-acme-admins");
        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "some-user", new[] { "group-acme-users", "group-acme-admins" }, "sft-acme", isAdmin: false);

        Assert.True(result.HasAccess);
        Assert.True(result.CanDelete);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_NoGroupsNoRole_ReturnsNone()
    {
        var thirdParty = CreateThirdParty("sft-acme", "active", "sp-other",
            userGroupId: "group-acme-users");
        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "some-user", Array.Empty<string>(), "sft-acme", isAdmin: false);

        Assert.False(result.HasAccess);
        Assert.False(result.CanDelete);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_ServicePrincipalMatch_ReturnsFull()
    {
        var thirdParty = CreateThirdParty("sft-acme", "active", "sp-matching-id");
        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "sp-matching-id", Array.Empty<string>(), "sft-acme", isAdmin: false);

        Assert.True(result.HasAccess);
        Assert.True(result.CanDelete);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_ServicePrincipalNonMatch_ReturnsNone()
    {
        var thirdParty = CreateThirdParty("sft-acme", "active", "sp-other");
        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "sp-wrong-id", Array.Empty<string>(), "sft-acme", isAdmin: false);

        Assert.False(result.HasAccess);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_InactiveContainer_ReturnsNone()
    {
        var thirdParty = CreateThirdParty("sft-acme", "inactive", "sp-id",
            userGroupId: "group-acme-users");
        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "some-user", new[] { "group-acme-users" }, "sft-acme", isAdmin: true);

        Assert.False(result.HasAccess);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_NoRegistryEntry_ReturnsNone()
    {
        SetupEmptyQueryResult();

        var result = await _service.UserHasContainerAccessAsync(
            "any-user", Array.Empty<string>(), "sft-nonexistent", isAdmin: true);

        Assert.False(result.HasAccess);
    }

    // --- GetAccessibleContainersAsync ---

    [Fact]
    public async Task GetAccessibleContainersAsync_Admin_ReturnsAllActive()
    {
        var parties = new[]
        {
            CreateThirdParty("sft-acme", "active", "sp-1"),
            CreateThirdParty("sft-globex", "active", "sp-2"),
            CreateThirdParty("sft-inactive", "inactive", "sp-3"),
        };

        SetupQueryResults(parties);

        var result = await _service.GetAccessibleContainersAsync(
            "admin-user", Array.Empty<string>(), isAdmin: true);

        Assert.Equal(2, result.Count);
        Assert.Contains("sft-acme", result);
        Assert.Contains("sft-globex", result);
        Assert.DoesNotContain("sft-inactive", result);
    }

    [Fact]
    public async Task GetAccessibleContainersAsync_UserGroup_ReturnsMatching()
    {
        var parties = new[]
        {
            CreateThirdParty("sft-acme", "active", "sp-1", userGroupId: "group-acme-users"),
            CreateThirdParty("sft-globex", "active", "sp-2", userGroupId: "group-globex-users"),
            CreateThirdParty("sft-other", "active", "sp-3", userGroupId: "group-other-users"),
        };

        SetupQueryResults(parties);

        var result = await _service.GetAccessibleContainersAsync(
            "some-user", new[] { "group-acme-users", "group-globex-users" }, isAdmin: false);

        Assert.Equal(2, result.Count);
        Assert.Contains("sft-acme", result);
        Assert.Contains("sft-globex", result);
    }

    [Fact]
    public async Task GetAccessibleContainersAsync_ServicePrincipal_ReturnsMatching()
    {
        var parties = new[]
        {
            CreateThirdParty("sft-acme", "active", "sp-1"),
            CreateThirdParty("sft-globex", "active", "sp-2"),
        };

        SetupQueryResults(parties);

        var result = await _service.GetAccessibleContainersAsync(
            "sp-2", Array.Empty<string>(), isAdmin: false);

        Assert.Single(result);
        Assert.Equal("sft-globex", result[0]);
    }

    [Fact]
    public async Task GetAccessibleContainersAsync_NoMatch_ReturnsEmpty()
    {
        var parties = new[]
        {
            CreateThirdParty("sft-acme", "active", "sp-1", userGroupId: "group-acme-users"),
        };

        SetupQueryResults(parties);

        var result = await _service.GetAccessibleContainersAsync(
            "sp-nonexistent", Array.Empty<string>(), isAdmin: false);

        Assert.Empty(result);
    }

    // --- Helpers ---

    private static ThirdParty CreateThirdParty(
        string containerName, string status, string spId,
        string? userGroupId = null, string? adminGroupId = null)
    {
        return new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = $"tp-{Guid.NewGuid():N}"[..10],
            ContainerName = containerName,
            Status = status,
            ServicePrincipalObjectId = spId,
            UserGroupId = userGroupId,
            AdminGroupId = adminGroupId
        };
    }

    private void SetupQueryResult(ThirdParty entity)
    {
        var page = Page<ThirdParty>.FromValues([entity], null, Mock.Of<Response>());
        var pageable = AsyncPageable<ThirdParty>.FromPages([page]);

        _mockTableClient
            .Setup(x => x.QueryAsync<ThirdParty>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(pageable);
    }

    private void SetupEmptyQueryResult()
    {
        var page = Page<ThirdParty>.FromValues([], null, Mock.Of<Response>());
        var pageable = AsyncPageable<ThirdParty>.FromPages([page]);

        _mockTableClient
            .Setup(x => x.QueryAsync<ThirdParty>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(pageable);
    }

    private void SetupQueryResults(ThirdParty[] entities)
    {
        var page = Page<ThirdParty>.FromValues(entities, null, Mock.Of<Response>());
        var pageable = AsyncPageable<ThirdParty>.FromPages([page]);

        _mockTableClient
            .Setup(x => x.QueryAsync<ThirdParty>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(pageable);
    }
}
