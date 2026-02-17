using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Services;

public class OnboardingServiceTests
{
    private readonly Mock<TableServiceClient> _mockTableServiceClient;
    private readonly Mock<TableClient> _mockTableClient;
    private readonly Mock<QueueServiceClient> _mockQueueServiceClient;
    private readonly Mock<QueueClient> _mockQueueClient;
    private readonly Mock<ILogger<OnboardingService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly OnboardingService _service;

    public OnboardingServiceTests()
    {
        _mockTableServiceClient = new Mock<TableServiceClient>();
        _mockTableClient = new Mock<TableClient>();
        _mockQueueServiceClient = new Mock<QueueServiceClient>();
        _mockQueueClient = new Mock<QueueClient>();
        _mockLogger = new Mock<ILogger<OnboardingService>>();

        var configData = new Dictionary<string, string?>
        {
            ["Storage:ContainerPrefix"] = "sft-",
            ["Storage:ProvisioningQueueName"] = "sft-provisioning",
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _mockTableServiceClient
            .Setup(x => x.GetTableClient(It.IsAny<string>()))
            .Returns(_mockTableClient.Object);

        _mockQueueServiceClient
            .Setup(x => x.GetQueueClient(It.IsAny<string>()))
            .Returns(_mockQueueClient.Object);

        _service = new OnboardingService(
            _mockTableServiceClient.Object,
            _mockQueueServiceClient.Object,
            _configuration,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RequestProvisioningAsync_ReturnsResponseWithProvisioningStatus()
    {
        var request = new ThirdPartyCreateRequest("Acme Corp", "admin@acme.com", true);

        var result = await _service.RequestProvisioningAsync(request);

        Assert.Equal("Acme Corp", result.CompanyName);
        Assert.Equal("provisioning", result.Status);
        Assert.StartsWith("tp-", result.Id);
        Assert.StartsWith("sft-", result.ContainerName);
        Assert.Null(result.AppRegistrationId);
    }

    [Fact]
    public async Task RequestProvisioningAsync_SanitizesCompanyName()
    {
        var request = new ThirdPartyCreateRequest("Acme Corp!", "admin@acme.com", false);

        var result = await _service.RequestProvisioningAsync(request);

        // Should be lowercased, non-alphanumeric replaced with hyphens, trimmed
        Assert.Equal("sft-acme-corp", result.ContainerName);
    }

    [Fact]
    public async Task RequestProvisioningAsync_WritesToTableAndQueue()
    {
        var request = new ThirdPartyCreateRequest("Globex", "g@globex.com", true);

        await _service.RequestProvisioningAsync(request);

        _mockTableClient.Verify(x => x.AddEntityAsync(
            It.Is<ThirdParty>(tp => tp.CompanyName == "Globex" && tp.Status == "provisioning"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockQueueClient.Verify(x => x.SendMessageAsync(
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetThirdPartyAsync_Exists_ReturnsEntity()
    {
        var entity = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            CompanyName = "Acme",
            Status = "active"
        };

        _mockTableClient
            .Setup(x => x.GetEntityAsync<ThirdParty>("ThirdParty", "tp-001",
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(entity, Mock.Of<Response>()));

        var result = await _service.GetThirdPartyAsync("tp-001");

        Assert.NotNull(result);
        Assert.Equal("Acme", result.CompanyName);
    }

    [Fact]
    public async Task GetThirdPartyAsync_NotFound_ReturnsNull()
    {
        _mockTableClient
            .Setup(x => x.GetEntityAsync<ThirdParty>("ThirdParty", "tp-missing",
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        var result = await _service.GetThirdPartyAsync("tp-missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task ListThirdPartiesAsync_ReturnsAllEntities()
    {
        var parties = new[]
        {
            new ThirdParty { PartitionKey = "ThirdParty", RowKey = "tp-001", CompanyName = "Acme", Status = "active" },
            new ThirdParty { PartitionKey = "ThirdParty", RowKey = "tp-002", CompanyName = "Globex", Status = "provisioning" },
        };

        var page = Page<ThirdParty>.FromValues(parties, null, Mock.Of<Response>());
        var pageable = AsyncPageable<ThirdParty>.FromPages([page]);

        _mockTableClient
            .Setup(x => x.QueryAsync<ThirdParty>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(pageable);

        var result = await _service.ListThirdPartiesAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task RequestDeprovisioningAsync_UpdatesStatusAndQueues()
    {
        var entity = new ThirdParty
        {
            PartitionKey = "ThirdParty",
            RowKey = "tp-001",
            CompanyName = "Acme",
            ContainerName = "sft-acme",
            Status = "active"
        };

        _mockTableClient
            .Setup(x => x.GetEntityAsync<ThirdParty>("ThirdParty", "tp-001",
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(entity, Mock.Of<Response>()));

        await _service.RequestDeprovisioningAsync("tp-001");

        _mockTableClient.Verify(x => x.UpsertEntityAsync(
            It.Is<ThirdParty>(tp => tp.Status == "deprovisioning"),
            TableUpdateMode.Replace,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockQueueClient.Verify(x => x.SendMessageAsync(
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RequestDeprovisioningAsync_NotFound_DoesNothing()
    {
        _mockTableClient
            .Setup(x => x.GetEntityAsync<ThirdParty>("ThirdParty", "tp-missing",
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        await _service.RequestDeprovisioningAsync("tp-missing");

        _mockQueueClient.Verify(x => x.SendMessageAsync(
            It.IsAny<string>()), Times.Never);
    }
}
