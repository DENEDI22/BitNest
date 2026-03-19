using System.Security.Claims;
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
        
        try
        {
            var (link, rawToken) = await linkService.CreateLinkAsync(request.FileId, userId, request.ExpiresAt);
            var url = $"{Request.Scheme}://{Request.Host}/api/share/{rawToken}";
            
            return CreatedAtAction(nameof(GetLinks), new
            {
                id = link.Id,
                token = rawToken,
                url = url,
                expiresAt = link.ExpiresAt
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
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
            fileName = l.File.Name,
            createdAt = l.CreatedAt,
            expiresAt = l.ExpiresAt,
            url = $"{Request.Scheme}://{Request.Host}/api/share/[hidden]" // Token not exposed in list
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
