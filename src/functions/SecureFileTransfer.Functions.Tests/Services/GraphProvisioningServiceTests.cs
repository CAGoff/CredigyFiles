using System.Security.Cryptography.X509Certificates;
using SecureFileTransfer.Functions.Services;

namespace SecureFileTransfer.Functions.Tests.Services;

public class GraphProvisioningServiceTests
{
    [Fact]
    public void AppRegistrationResult_StoresAllFields()
    {
        var result = new AppRegistrationResult(
            ApplicationId: "app-123",
            ServicePrincipalObjectId: "sp-456",
            CertificateThumbprint: "AABBCCDD",
            PfxBytes: new byte[] { 1, 2, 3 });

        Assert.Equal("app-123", result.ApplicationId);
        Assert.Equal("sp-456", result.ServicePrincipalObjectId);
        Assert.Equal("AABBCCDD", result.CertificateThumbprint);
        Assert.Equal(3, result.PfxBytes.Length);
    }

    [Fact]
    public void GenerateSelfSignedCert_ProducesValidPfx()
    {
        // Simulate the cert generation logic from GraphProvisioningService
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=sft-3p-test-company",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddYears(1);

        using var cert = request.CreateSelfSigned(notBefore, notAfter);
        var pfxBytes = cert.Export(X509ContentType.Pfx);
        var thumbprint = cert.Thumbprint;

        Assert.NotEmpty(pfxBytes);
        Assert.NotEmpty(thumbprint);
        Assert.Equal(40, thumbprint.Length); // SHA1 thumbprint = 40 hex chars

        // Verify PFX can be loaded back
        using var loadedCert = X509CertificateLoader.LoadPkcs12(pfxBytes, null);
        Assert.Equal(thumbprint, loadedCert.Thumbprint);
        Assert.Contains("sft-3p-test-company", loadedCert.Subject);
    }

    [Fact]
    public void GenerateSelfSignedCert_HasCorrectValidity()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=sft-3p-validity-test",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddYears(1);

        using var cert = request.CreateSelfSigned(notBefore, notAfter);

        Assert.True(cert.NotBefore <= DateTime.UtcNow);
        Assert.True(cert.NotAfter > DateTime.UtcNow.AddMonths(11));
    }
}
