using System.Security.Claims;
using BitNest.Models;
using BitNest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BitNest.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SharepointController : ControllerBase
{
    private readonly SharepointLinkService linkService;

    public SharepointController(SharepointLinkService linkService)
    {
        this.linkService = linkService;
    }

    [HttpPost("links")]
    public async Task<IActionResult> CreateLink([FromBody] CreateLinkRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (request.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new { message = "Expiry date must be in the future." });

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var (link, rawToken) = await linkService.CreateLinkAsync(request.FileId, userId, request.ExpiresAt, baseUrl);

            return CreatedAtAction(nameof(GetLinks), null, new
            {
                id = link.Id,
                url = link.ShareUrl,
                expiresAt = link.ExpiresAt
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("slots")]
    public async Task<IActionResult> CreateUploadSlot([FromBody] CreateUploadSlotRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (request.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new { message = "Expiry date must be in the future." });

        if (request.MaxFileCount < 1)
            return BadRequest(new { message = "Max file count must be at least 1." });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (link, rawToken) = await linkService.CreateUploadSlotAsync(
            userId, request.ExpiresAt, request.Description, request.MaxFileCount, baseUrl);

        return CreatedAtAction(nameof(GetLinks), null, new
        {
            id = link.Id,
            url = link.ShareUrl,
            expiresAt = link.ExpiresAt
        });
    }

    [HttpGet("links")]
    public async Task<IActionResult> GetLinks()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var links = await linkService.GetActiveLinksForUserAsync(userId);

        return Ok(links.Select(l => new
        {
            id = l.Id,
            fileId = l.FileId,
            fileName = l.File?.Name,
            shareUrl = l.ShareUrl,
            createdAt = l.CreatedAt,
            expiresAt = l.ExpiresAt,
            linkType = l.LinkType == LinkType.Download ? "download" : "upload",
            description = l.Description,
            maxFileCount = l.MaxFileCount,
            uploadCount = l.UploadCount
        }));
    }

    [HttpDelete("links/{id}")]
    public async Task<IActionResult> RevokeLink(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var revoked = await linkService.RevokeLinkAsync(id, userId);

        return revoked ? NoContent() : NotFound();
    }
}

public record CreateLinkRequest(int FileId, DateTime ExpiresAt);
public record CreateUploadSlotRequest(DateTime ExpiresAt, string? Description, int MaxFileCount);
