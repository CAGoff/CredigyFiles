using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecureFileTransfer.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Azure SDK clients via user-assigned managed identities
        var storageUri = Environment.GetEnvironmentVariable("AppStorageUri")
            ?? "https://placeholder.blob.core.windows.net";

        // Notification identity
        var notifyIdentityClientId = Environment.GetEnvironmentVariable("NotifyIdentityClientId");
        var notifyCredential = string.IsNullOrEmpty(notifyIdentityClientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = notifyIdentityClientId });

        // Provisioning identity
        var provisionIdentityClientId = Environment.GetEnvironmentVariable("ProvisionIdentityClientId");
        var provisionCredential = string.IsNullOrEmpty(provisionIdentityClientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = provisionIdentityClientId });

        // Table client for registry lookups (used by both functions)
        services.AddSingleton(new TableServiceClient(new Uri(storageUri.Replace(".blob.", ".table.")), notifyCredential));

        // Blob client for provisioning (container creation, cert storage)
        services.AddSingleton(new BlobServiceClient(new Uri(storageUri), provisionCredential));

        // Named credentials for identity isolation
        services.AddKeyedSingleton("notify", notifyCredential);
        services.AddKeyedSingleton("provision", provisionCredential);

        // Provisioning services
        services.AddSingleton<IGraphProvisioningService, GraphProvisioningService>();
        services.AddSingleton<IRbacService, RbacService>();
    })
    .Build();

host.Run();
