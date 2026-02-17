namespace SecureFileTransfer.Functions.Services;

public interface IGraphProvisioningService
{
    Task<AppRegistrationResult> CreateAppRegistrationAsync(string companyName, string containerName);
    Task DeleteAppRegistrationAsync(string appRegistrationId);
}
