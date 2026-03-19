using BitNest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BitNest.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/share")]
public class PublicShareController : ControllerBase
{
    private readonly SharepointLinkService linkService;
    private readonly StorageService storageService;

    public PublicShareController(SharepointLinkService linkService, StorageService storageService)
    {
        this.linkService = linkService;
        this.storageService = storageService;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> GetFileMetadata(string token)
    {
        var result = await linkService.ValidateTokenAndGetFileAsync(token);
        if (result == null)
            return NotFound(new { message = "This link is no longer valid" });
        
        return Ok(new
        {
            fileName = result.Value.File.Name,
            fileSize = result.Value.File.Size,
            expiresAt = result.Value.ExpiresAt
        });
    }

    [HttpGet("{token}/download")]
    public async Task<IActionResult> DownloadFile(string token)
    {
        var result = await linkService.ValidateTokenAndGetFileAsync(token);
        if (result == null)
            return NotFound(new { message = "This link is no longer valid" });
        
        var stream = await storageService.GetDownloadStreamAsync(result.Value.File.Id);
        return File(stream, "application/octet-stream", result.Value.File.Name);
    }
}
