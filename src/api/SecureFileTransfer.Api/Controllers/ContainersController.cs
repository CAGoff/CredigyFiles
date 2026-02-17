using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Controllers;

[ApiController]
[Route("v1/[controller]")]
[Authorize]
public class ContainersController : ControllerBase
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IActivityService _activityService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContainersController> _logger;

    public ContainersController(
        IBlobStorageService blobStorage,
        IActivityService activityService,
        IConfiguration configuration,
        ILogger<ContainersController> logger)
    {
        _blobStorage = blobStorage;
        _activityService = activityService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>List containers the caller can access.</summary>
    [HttpGet]
    public async Task<IActionResult> ListContainers()
    {
        var prefix = _configuration["Storage:ContainerPrefix"] ?? "sft-";
        var allContainers = await _blobStorage.ListContainersAsync(prefix);

        var userId = User.FindFirst("oid")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var isAdmin = User.IsInRole("SFT.Admin");
        var isOrgUser = User.IsInRole("SFT.User");

        var accessible = await _activityService.GetAccessibleContainersAsync(userId, isAdmin, isOrgUser);
        var accessibleSet = new HashSet<string>(accessible);
        var containers = allContainers.Where(c => accessibleSet.Contains(c)).ToList();

        return Ok(new { containers });
    }
}
