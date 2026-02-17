namespace SecureFileTransfer.Functions.Services;

public record AppRegistrationResult(
    string ApplicationId,
    string ServicePrincipalObjectId,
    string CertificateThumbprint,
    byte[] PfxBytes);
