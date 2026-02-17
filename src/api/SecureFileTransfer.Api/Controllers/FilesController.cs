using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SecureFileTransfer.Api.Middleware;
using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Controllers;

[ApiController]
[Route("v1/containers/{containerName}/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IActivityService _activityService;
    private readonly FileValidationOptions _fileOptions;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IBlobStorageService blobStorage,
        IActivityService activityService,
        IConfiguration configuration,
        ILogger<FilesController> logger)
    {
        _blobStorage = blobStorage;
        _activityService = activityService;
        _fileOptions = new FileValidationOptions();
        configuration.GetSection(FileValidationOptions.Section).Bind(_fileOptions);
        _logger = logger;
    }

    /// <summary>List files (Hot tier only) in a container directory.</summary>
    [HttpGet]
    public async Task<IActionResult> ListFiles(string containerName, [FromQuery] string dir)
    {
        if (!FileValidationService.IsValidDirectory(dir))
            return BadRequest(new { error = new { code = "INVALID_DIRECTORY", message = "dir must be 'inbound' or 'outbound'." } });

        if (!await HttpContext.HasContainerAccessAsync(_activityService, containerName, _logger))
            return Forbid();

        var files = await _blobStorage.ListFilesAsync(containerName, dir);
        return Ok(new FileListResponse(containerName, dir, files));
    }

    /// <summary>Download a file.</summary>
    [HttpGet("{fileName}")]
    public async Task<IActionResult> DownloadFile(string containerName, string fileName, [FromQuery] string dir)
    {
        if (!FileValidationService.IsValidDirectory(dir))
            return BadRequest(new { error = new { code = "INVALID_DIRECTORY", message = "dir must be 'inbound' or 'outbound'." } });

        if (!await HttpContext.HasContainerAccessAsync(_activityService, containerName, _logger))
            return Forbid();

        var sanitized = FileValidationService.SanitizeFileName(fileName);
        if (sanitized is null)
            return BadRequest(new { error = new { code = "INVALID_FILENAME", message = "Invalid file name." } });

        var stream = await _blobStorage.DownloadFileAsync(containerName, dir, sanitized);

        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? "";
        var userId = HttpContext.User.FindFirst("preferred_username")?.Value ?? "unknown";
        await _activityService.LogActivityAsync(containerName, "Download", sanitized, dir, userId, 0, correlationId);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, "application/octet-stream", sanitized);
    }

    /// <summary>Upload a file.</summary>
    [HttpPost]
    [RequestSizeLimit(52_428_800)] // 50MB
    public async Task<IActionResult> UploadFile(string containerName, [FromQuery] string dir, IFormFile file)
    {
        if (!FileValidationService.IsValidDirectory(dir))
            return BadRequest(new { error = new { code = "INVALID_DIRECTORY", message = "dir must be 'inbound' or 'outbound'." } });

        if (!await HttpContext.HasContainerAccessAsync(_activityService, containerName, _logger))
            return Forbid();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = new { code = "EMPTY_FILE", message = "No file provided." } });

        if (file.Length > _fileOptions.MaxSizeBytes)
            return StatusCode(413, new { error = new { code = "FILE_TOO_LARGE", message = $"File exceeds {_fileOptions.MaxSizeBytes / 1_048_576}MB limit." } });

        var sanitized = FileValidationService.SanitizeFileName(file.FileName);
        if (sanitized is null)
            return BadRequest(new { error = new { code = "INVALID_FILENAME", message = "Invalid file name." } });

        if (!FileValidationService.HasAllowedExtension(sanitized, _fileOptions))
            return BadRequest(new { error = new { code = "INVALID_FILE_TYPE", message = "File type not allowed." } });

        await using var stream = file.OpenReadStream();

        if (!await FileValidationService.ValidateMagicBytesAsync(stream, sanitized))
            return BadRequest(new { error = new { code = "CONTENT_MISMATCH", message = "File content does not match the declared file type." } });

        var userId = HttpContext.User.FindFirst("preferred_username")?.Value ?? "unknown";

        try
        {
            var result = await _blobStorage.UploadFileAsync(containerName, dir, sanitized, stream, userId);

            var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? "";
            await _activityService.LogActivityAsync(containerName, "Upload", sanitized, dir, userId, file.Length, correlationId);

            return Created($"v1/containers/{containerName}/files/{sanitized}?dir={dir}", result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new { error = new { code = "FILE_EXISTS", message = ex.Message } });
        }
    }

    /// <summary>Delete a file.</summary>
    [HttpDelete("{fileName}")]
    public async Task<IActionResult> DeleteFile(string containerName, string fileName, [FromQuery] string dir)
    {
        if (!FileValidationService.IsValidDirectory(dir))
            return BadRequest(new { error = new { code = "INVALID_DIRECTORY", message = "dir must be 'inbound' or 'outbound'." } });

        if (!await HttpContext.HasContainerAccessAsync(_activityService, containerName, _logger))
            return Forbid();

        var sanitized = FileValidationService.SanitizeFileName(fileName);
        if (sanitized is null)
            return BadRequest(new { error = new { code = "INVALID_FILENAME", message = "Invalid file name." } });

        await _blobStorage.DeleteFileAsync(containerName, dir, sanitized);

        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? "";
        var userId = HttpContext.User.FindFirst("preferred_username")?.Value ?? "unknown";
        await _activityService.LogActivityAsync(containerName, "Delete", sanitized, dir, userId, 0, correlationId);

        return NoContent();
    }
}
