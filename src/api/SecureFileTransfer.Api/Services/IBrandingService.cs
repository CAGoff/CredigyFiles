using SecureFileTransfer.Api.Models;

namespace SecureFileTransfer.Api.Services;

public interface IBrandingService
{
    Task<BrandingResponse> GetBrandingAsync();
    Task<BrandingResponse> UpdateBrandingAsync(BrandingUpdateRequest request);
    Task<BrandingResponse> UploadLogoAsync(Stream stream, string fileName);
    Task<BrandingResponse> UploadFaviconAsync(Stream stream, string fileName);
}
