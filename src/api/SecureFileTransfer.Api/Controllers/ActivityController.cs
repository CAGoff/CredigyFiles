using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFileTransfer.Api.Middleware;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Controllers;

[ApiController]
[Route("v1")]
[Authorize]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(IActivityService activityService, ILogger<ActivityController> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>Get activity log for a specific container.</summary>
    [HttpGet("containers/{containerName}/activity")]
    public async Task<IActionResult> GetContainerActivity(string containerName)
    {
        if (!await HttpContext.HasContainerAccessAsync(_activityService, containerName, _logger))
            return Forbid();

        var records = await _activityService.GetActivityAsync(containerName);
        return Ok(new { container = containerName, activity = records });
    }

    /// <summary>Get activity log across all containers (admin only).</summary>
    [HttpGet("activity")]
    [Authorize(Roles = "SFT.Admin")]
    public async Task<IActionResult> GetAllActivity()
    {
        var records = await _activityService.GetAllActivityAsync();
        return Ok(new { activity = records });
    }
}
