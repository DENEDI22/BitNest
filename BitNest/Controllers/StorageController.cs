using System.Security.Claims;
using BitNest.Models;
using BitNest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BitNest.Controllers;

[Route("[controller]")]
[Authorize]
public class StorageController : ControllerBase
{
    private readonly StorageService storageService;

    public StorageController(StorageService storageService)
    {
        this.storageService = storageService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    [RequestSizeLimit(long.MaxValue)]
    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] IFormFile formFile)
    {
        var currentUserId = GetCurrentUserId();
        var fileName =
            await storageService.UploadFile(formFile, formFile.FileName, Path.GetExtension(formFile.FileName),
                currentUserId, HttpContext.RequestAborted);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return StatusCode(500, new { message = "Error uploading file" });
        }

        return Ok();
    }

    [HttpGet("download/{fileId}")]
    public async Task<ActionResult> Download(int fileId)
    {
        var currentUserId = GetCurrentUserId();
        
        FileMetadata metadata;
        try
        {
            metadata = await storageService.GetMetadataByIdAsync(fileId);
        }
        catch (Exception)
        {
            return NotFound();
        }

        // Check authorization
        if (!await storageService.CanAccessFileAsync(fileId, currentUserId))
        {
            return NotFound();
        }

        var downloadStream = await storageService.GetDownloadStreamAsync(fileId);
        return File(downloadStream, "application/octet-stream", metadata.Name, enableRangeProcessing: true);
    }

    [HttpGet("{pageNumber}")]
    public async Task<IActionResult> Files(int pageNumber = 1)
    {
        if (pageNumber < 1) return BadRequest();
        var currentUserId = GetCurrentUserId();
        return Ok(await storageService.GetFilesAsJsonAsync(pageNumber, currentUserId));
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(int fileId)
    {
        var currentUserId = GetCurrentUserId();
        
        FileMetadata metadata;
        try
        {
            metadata = await storageService.GetMetadataByIdAsync(fileId);
        }
        catch (Exception)
        {
            return NotFound();
        }

        // Check authorization
        if (!await storageService.CanAccessFileAsync(fileId, currentUserId))
        {
            return NotFound();
        }
        
        await storageService.SafeDeleteFile(fileId);
        return NoContent();
    }
    
    
}
