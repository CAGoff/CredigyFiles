using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace SecureFileTransfer.Functions.Services;

public class GraphProvisioningService : IGraphProvisioningService
{
    private const string AdminContainer = "sft-admin";

    private readonly GraphServiceClient _graphClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<GraphProvisioningService> _logger;

    public GraphProvisioningService(
        [FromKeyedServices("provision")] DefaultAzureCredential credential,
        BlobServiceClient blobServiceClient,
        ILogger<GraphProvisioningService> logger)
    {
        _graphClient = new GraphServiceClient(credential);
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<AppRegistrationResult> CreateAppRegistrationAsync(
        string companyName, string containerName)
    {
        var displayName = $"sft-3p-{companyName.ToLowerInvariant().Replace(" ", "-")}";

        // Generate self-signed certificate
        var (pfxBytes, thumbprint, keyCredential) = GenerateCertificate(displayName);

        // Create app registration
        var application = await _graphClient.Applications.PostAsync(new Application
        {
            DisplayName = displayName,
            KeyCredentials = [keyCredential]
        });

        var appObjectId = application!.Id!;
        var appId = application.AppId!;

        _logger.LogInformation(
            "App registration created: {DisplayName} (AppId: {AppId})",
            displayName, appId);

        // Create service principal
        var servicePrincipal = await _graphClient.ServicePrincipals.PostAsync(
            new ServicePrincipal { AppId = appId });

        var spObjectId = servicePrincipal!.Id!;

        _logger.LogInformation(
            "Service principal created: {SpId} for app {AppId}",
            spObjectId, appId);

        // Store PFX in admin blob container
        await StorePfxAsync(containerName, pfxBytes);

        return new AppRegistrationResult(appObjectId, spObjectId, thumbprint, pfxBytes);
    }

    public async Task DeleteAppRegistrationAsync(string appRegistrationId)
    {
        await _graphClient.Applications[appRegistrationId].DeleteAsync();
        _logger.LogInformation("App registration deleted: {AppId}", appRegistrationId);
    }

    private static (byte[] PfxBytes, string Thumbprint, KeyCredential Credential) GenerateCertificate(
        string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={subjectName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddYears(1);

        using var cert = request.CreateSelfSigned(notBefore, notAfter);
        var pfxBytes = cert.Export(X509ContentType.Pfx);
        var thumbprint = cert.Thumbprint;

        var keyCredential = new KeyCredential
        {
            DisplayName = $"{subjectName}-cert",
            Type = "AsymmetricX509Cert",
            Usage = "Verify",
            Key = cert.GetRawCertData(),
            StartDateTime = notBefore,
            EndDateTime = notAfter
        };

        return (pfxBytes, thumbprint, keyCredential);
    }

    private async Task StorePfxAsync(string containerName, byte[] pfxBytes)
    {
        var adminContainerClient = _blobServiceClient.GetBlobContainerClient(AdminContainer);
        await adminContainerClient.CreateIfNotExistsAsync();

        var blobPath = $"certs/sft-3p-{containerName}.pfx";
        var blobClient = adminContainerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(new BinaryData(pfxBytes), overwrite: true);

        _logger.LogInformation("PFX stored at {Container}/{Path}", AdminContainer, blobPath);
    }
}
