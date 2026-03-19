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
        var file = await linkService.ValidateTokenAndGetFileAsync(token);
        if (file == null)
            return NotFound(new { message = "This link is no longer valid" });
        
        return Ok(new
        {
            fileName = file.Name,
            fileSize = file.Size,
            // Note: We'd need to include the link expiry from the SharepointLink entity
            // For now, returning file metadata only
        });
    }

    [HttpGet("{token}/download")]
    public async Task<IActionResult> DownloadFile(string token)
    {
        var file = await linkService.ValidateTokenAndGetFileAsync(token);
        if (file == null)
            return NotFound(new { message = "This link is no longer valid" });
        
        var stream = await storageService.GetDownloadStreamAsync(file.Id);
        return File(stream, "application/octet-stream", file.Name);
    }
}
