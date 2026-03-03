using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Controllers;

[ApiController]
[Route("v1/branding")]
public class BrandingController : ControllerBase
{
    private readonly IBrandingService _brandingService;
    private readonly ILogger<BrandingController> _logger;

    public BrandingController(IBrandingService brandingService, ILogger<BrandingController> logger)
    {
        _brandingService = brandingService;
        _logger = logger;
    }

    /// <summary>Get branding settings. Public — needed before authentication on login page.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetBranding()
    {
        var branding = await _brandingService.GetBrandingAsync();
        return Ok(branding);
    }

    /// <summary>Update app name and primary color.</summary>
    [HttpPut]
    [Authorize(Roles = "SFT.Admin")]
    public async Task<IActionResult> UpdateBranding([FromBody] BrandingUpdateRequest request)
    {
        try
        {
            var branding = await _brandingService.UpdateBrandingAsync(request);
            return Ok(branding);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
        }
    }

    /// <summary>Upload company logo.</summary>
    [HttpPost("logo")]
    [Authorize(Roles = "SFT.Admin")]
    [RequestSizeLimit(5_242_880)] // 5MB
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = new { code = "EMPTY_FILE", message = "No file provided." } });

        try
        {
            await using var stream = file.OpenReadStream();
            var branding = await _brandingService.UploadLogoAsync(stream, file.FileName);
            return Ok(branding);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_FILE_TYPE", message = ex.Message } });
        }
    }

    /// <summary>Upload favicon.</summary>
    [HttpPost("favicon")]
    [Authorize(Roles = "SFT.Admin")]
    [RequestSizeLimit(1_048_576)] // 1MB
    public async Task<IActionResult> UploadFavicon(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = new { code = "EMPTY_FILE", message = "No file provided." } });

        try
        {
            await using var stream = file.OpenReadStream();
            var branding = await _brandingService.UploadFaviconAsync(stream, file.FileName);
            return Ok(branding);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_FILE_TYPE", message = ex.Message } });
        }
    }
}
