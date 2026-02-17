using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public interface IOnboardingService
{
    Task<ThirdPartyResponse> RequestProvisioningAsync(ThirdPartyCreateRequest request);
    Task<ThirdParty?> GetThirdPartyAsync(string id);
    Task<IReadOnlyList<ThirdParty>> ListThirdPartiesAsync();
    Task UpdateThirdPartyAsync(ThirdParty thirdParty);
    Task RequestDeprovisioningAsync(string id);
}
