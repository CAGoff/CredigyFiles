using Azure.Communication.Email;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SecureFileTransfer.Functions;

/// <summary>
/// Triggered by Event Grid when a blob is created in the app storage account.
/// Looks up the container in the third-party registry and sends a minimal
/// notification email via Azure Communication Services.
///
/// Uses user-assigned managed identity: id-sft-func-notify
/// </summary>
public class NotificationTrigger
{
    private readonly TableServiceClient _tableClient;
    private readonly ILogger<NotificationTrigger> _logger;

    public NotificationTrigger(
        TableServiceClient tableClient,
        ILogger<NotificationTrigger> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
    }

    [Function(nameof(NotificationTrigger))]
    public async Task Run(
        [EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation("Blob event received: {Subject}", eventGridEvent.Subject);

        // Extract container name from the blob URL subject
        // Subject format: /blobServices/default/containers/{container}/blobs/{path}
        var subject = eventGridEvent.Subject ?? "";
        var containerStart = subject.IndexOf("/containers/") + "/containers/".Length;
        var containerEnd = subject.IndexOf("/blobs/");
        if (containerStart < 0 || containerEnd < 0)
        {
            _logger.LogWarning("Could not parse container name from subject: {Subject}", subject);
            return;
        }
        var containerName = subject[containerStart..containerEnd];

        // Look up third party in registry
        var registryTable = _tableClient.GetTableClient("SftRegistry");
        string? contactEmail = null;

        await foreach (var entity in registryTable.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq 'ThirdParty' and ContainerName eq '{ODataSanitizer.EscapeStringValue(containerName)}'"))
        {
            contactEmail = entity.GetString("ContactEmail");
            break;
        }

        if (string.IsNullOrEmpty(contactEmail))
        {
            _logger.LogWarning("No contact email found for container {Container}", containerName);
            return;
        }

        // Send minimal notification email — all settings are required
        var acsConnectionString = Environment.GetEnvironmentVariable("AcsConnectionString");
        var senderAddress = Environment.GetEnvironmentVariable("AcsSenderAddress");
        var portalUrl = Environment.GetEnvironmentVariable("PortalUrl");

        if (string.IsNullOrEmpty(acsConnectionString))
        {
            _logger.LogWarning("AcsConnectionString not configured. Skipping email notification.");
            return;
        }
        if (string.IsNullOrEmpty(senderAddress))
        {
            _logger.LogWarning("AcsSenderAddress not configured. Skipping email notification.");
            return;
        }
        if (string.IsNullOrEmpty(portalUrl))
        {
            _logger.LogWarning("PortalUrl not configured. Skipping email notification.");
            return;
        }

        var emailClient = new EmailClient(acsConnectionString);
        var emailMessage = new EmailMessage(
            senderAddress: senderAddress,
            content: new EmailContent("Secure File Transfer - New File Available")
            {
                PlainText = $"A new file is available for you in the Secure File Transfer portal.\n\nPlease log in to review: {portalUrl}\n\nThis is an automated notification. Do not reply to this email."
            },
            recipients: new EmailRecipients([new EmailAddress(contactEmail)]));

        await emailClient.SendAsync(Azure.WaitUntil.Started, emailMessage);
        _logger.LogInformation("Notification email sent to {Email} for container {Container}", contactEmail, containerName);
    }
}

/// <summary>
/// Minimal Event Grid event model for the isolated worker.
/// </summary>
public class EventGridEvent
{
    public string? Id { get; set; }
    public string? Subject { get; set; }
    public string? EventType { get; set; }
    public DateTimeOffset EventTime { get; set; }
    public object? Data { get; set; }
}
