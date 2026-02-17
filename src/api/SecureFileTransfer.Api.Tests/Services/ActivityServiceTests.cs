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

    [Fact]
    public async Task UserHasContainerAccessAsync_AdminUser_ReturnsTrue_WhenContainerActive()
    {
        var thirdParty = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            ContainerName = "sft-acme",
            Status = "active",
            ServicePrincipalObjectId = "sp-other"
        };

        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "admin-user-id", "sft-acme", isAdmin: true, isOrgUser: false);

        Assert.True(result);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_OrgUser_ReturnsTrue_WhenContainerActive()
    {
        var thirdParty = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            ContainerName = "sft-acme",
            Status = "active",
            ServicePrincipalObjectId = "sp-other"
        };

        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "org-user-id", "sft-acme", isAdmin: false, isOrgUser: true);

        Assert.True(result);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_ThirdPartyMatching_ReturnsTrue()
    {
        var thirdParty = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            ContainerName = "sft-acme",
            Status = "active",
            ServicePrincipalObjectId = "sp-matching-id"
        };

        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "sp-matching-id", "sft-acme", isAdmin: false, isOrgUser: false);

        Assert.True(result);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_ThirdPartyNonMatching_ReturnsFalse()
    {
        var thirdParty = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            ContainerName = "sft-acme",
            Status = "active",
            ServicePrincipalObjectId = "sp-other"
        };

        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "sp-wrong-id", "sft-acme", isAdmin: false, isOrgUser: false);

        Assert.False(result);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_InactiveContainer_ReturnsFalse()
    {
        var thirdParty = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            ContainerName = "sft-acme",
            Status = "inactive",
            ServicePrincipalObjectId = "sp-id"
        };

        SetupQueryResult(thirdParty);

        var result = await _service.UserHasContainerAccessAsync(
            "sp-id", "sft-acme", isAdmin: true, isOrgUser: false);

        Assert.False(result);
    }

    [Fact]
    public async Task UserHasContainerAccessAsync_NoRegistryEntry_ReturnsFalse()
    {
        SetupEmptyQueryResult();

        var result = await _service.UserHasContainerAccessAsync(
            "any-user", "sft-nonexistent", isAdmin: true, isOrgUser: false);

        Assert.False(result);
    }

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
            "admin-user", isAdmin: true, isOrgUser: false);

        Assert.Equal(2, result.Count);
        Assert.Contains("sft-acme", result);
        Assert.Contains("sft-globex", result);
        Assert.DoesNotContain("sft-inactive", result);
    }

    [Fact]
    public async Task GetAccessibleContainersAsync_ThirdParty_ReturnsOnlyMatching()
    {
        var parties = new[]
        {
            CreateThirdParty("sft-acme", "active", "sp-1"),
            CreateThirdParty("sft-globex", "active", "sp-2"),
            CreateThirdParty("sft-other", "active", "sp-3"),
        };

        SetupQueryResults(parties);

        var result = await _service.GetAccessibleContainersAsync(
            "sp-2", isAdmin: false, isOrgUser: false);

        Assert.Single(result);
        Assert.Equal("sft-globex", result[0]);
    }

    [Fact]
    public async Task GetAccessibleContainersAsync_ThirdParty_NoMatch_ReturnsEmpty()
    {
        var parties = new[]
        {
            CreateThirdParty("sft-acme", "active", "sp-1"),
        };

        SetupQueryResults(parties);

        var result = await _service.GetAccessibleContainersAsync(
            "sp-nonexistent", isAdmin: false, isOrgUser: false);

        Assert.Empty(result);
    }

    private static ThirdParty CreateThirdParty(string containerName, string status, string spId)
    {
        return new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = $"tp-{Guid.NewGuid():N}"[..10],
            ContainerName = containerName,
            Status = status,
            ServicePrincipalObjectId = spId
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
