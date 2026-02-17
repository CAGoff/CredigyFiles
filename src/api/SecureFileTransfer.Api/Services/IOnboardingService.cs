using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public interface IOnboardingService
{
    Task<ThirdPartyResponse> RequestProvisioningAsync(ThirdPartyCreateRequest request);
    Task<ThirdParty?> GetThirdPartyAsync(string id);
    Task<IReadOnlyList<ThirdParty>> ListThirdPartiesAsync(int take = 100);
    Task UpdateThirdPartyAsync(ThirdParty thirdParty);
    Task RequestDeprovisioningAsync(string id);
}
