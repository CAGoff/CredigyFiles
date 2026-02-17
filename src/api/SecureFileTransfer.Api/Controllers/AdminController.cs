using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Controllers;

[ApiController]
[Route("v1/admin/third-parties")]
[Authorize(Roles = "SFT.Admin")]
public class AdminController : ControllerBase
{
    private readonly IOnboardingService _onboarding;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IOnboardingService onboarding, ILogger<AdminController> logger)
    {
        _onboarding = onboarding;
        _logger = logger;
    }

    /// <summary>List all registered third parties.</summary>
    [HttpGet]
    public async Task<IActionResult> ListThirdParties([FromQuery] int top = 100)
    {
        var parties = await _onboarding.ListThirdPartiesAsync(top);
        return Ok(new { thirdParties = parties.Select(p => ToResponse(p)) });
    }

    /// <summary>Provision a new third party.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateThirdParty([FromBody] ThirdPartyCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
            return BadRequest(new { error = new { code = "INVALID_INPUT", message = "Company name is required." } });

        if (string.IsNullOrWhiteSpace(request.ContactEmail))
            return BadRequest(new { error = new { code = "INVALID_INPUT", message = "Contact email is required." } });

        var result = await _onboarding.RequestProvisioningAsync(request);
        return Created($"v1/admin/third-parties/{result.Id}", result);
    }

    /// <summary>Get third-party details.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetThirdParty(string id)
    {
        var party = await _onboarding.GetThirdPartyAsync(id);
        if (party is null)
            return NotFound(new { error = new { code = "NOT_FOUND", message = "Third party not found." } });

        return Ok(ToResponse(party));
    }

    /// <summary>Update third-party configuration.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateThirdParty(string id, [FromBody] ThirdPartyCreateRequest request)
    {
        var party = await _onboarding.GetThirdPartyAsync(id);
        if (party is null)
            return NotFound(new { error = new { code = "NOT_FOUND", message = "Third party not found." } });

        party.CompanyName = request.CompanyName;
        party.ContactEmail = request.ContactEmail;
        party.AutomationEnabled = request.EnableAutomation;
        await _onboarding.UpdateThirdPartyAsync(party);

        return Ok(ToResponse(party));
    }

    /// <summary>Deprovision a third party.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteThirdParty(string id)
    {
        var party = await _onboarding.GetThirdPartyAsync(id);
        if (party is null)
            return NotFound(new { error = new { code = "NOT_FOUND", message = "Third party not found." } });

        await _onboarding.RequestDeprovisioningAsync(id);
        return Accepted(new { status = "deprovisioning" });
    }

    private static ThirdPartyResponse ToResponse(ThirdParty p) => new(
        Id: p.RowKey,
        CompanyName: p.CompanyName,
        ContainerName: p.ContainerName,
        AppRegistrationId: p.AppRegistrationId,
        Status: p.Status,
        CreatedAt: p.Timestamp ?? DateTimeOffset.MinValue);
}
