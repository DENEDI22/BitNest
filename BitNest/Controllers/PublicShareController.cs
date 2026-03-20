using BitNest.Models;
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
        var link = await linkService.ValidateTokenAndGetLinkAsync(token);
        if (link == null)
            return NotFound(new { message = "This link is no longer valid" });

        if (link.LinkType == LinkType.Upload)
        {
            return Ok(new
            {
                linkType = "upload",
                ownerUsername = link.CreatedBy.Username,
                createdAt = link.CreatedAt,
                expiresAt = link.ExpiresAt,
                description = link.Description,
                maxFileCount = link.MaxFileCount,
                uploadCount = link.UploadCount
            });
        }

        return Ok(new
        {
            linkType = "download",
            fileName = link.File!.Name,
            fileSize = link.File!.Size,
            expiresAt = link.ExpiresAt
        });
    }

    [HttpPost("{token}/upload")]
    public async Task<IActionResult> UploadFile(string token, [FromForm] IFormFile formFile)
    {
        var result = await linkService.ValidateAndReserveUploadSlotAsync(token);

        if (!result.IsValid && !result.IsSlotFull)
            return NotFound(new { message = "This link is no longer valid" });

        if (result.IsSlotFull)
            return Conflict(new { message = "This upload slot is full" });

        await storageService.UploadFile(formFile, formFile.FileName,
            Path.GetExtension(formFile.FileName), result.Link!.CreatedByUserId);

        return Ok(new { message = "File received" });
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
