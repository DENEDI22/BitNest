using BitNest.Models;
using BitNest.Services;
using Microsoft.AspNetCore.Mvc;

namespace BitNest.Controllers;

[Route("[controller]")]
public class StorageController : ControllerBase
{
    private readonly StorageService storageService;
    public StorageController(StorageService storageService)
    {
        this.storageService = storageService;
    }
    
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile formFile)
    {
        var fileName = await storageService.UploadFile(formFile, formFile.FileName, Path.GetExtension(formFile.FileName));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return StatusCode(500, new { message = "Error uploading file" });
        }
        return Ok();
    }

    [HttpGet("download/{fileId}")]
    public async Task<ActionResult> Download(int fileId)
    {
        FileMetadata metadata;
        try
        {
            metadata = await storageService.GetMetadataByIdAsync(fileId);
        }
        catch (Exception e)
        {
            return NotFound();
        }
        var downloadStream = new FileStream(metadata.BlobPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return File(downloadStream, "application/octetStream", metadata.Name);
    }
    
    [HttpGet("{pageNumber}")]
    public async Task<IActionResult> Files(int pageNumber = 1)
    {
        if (pageNumber < 1) return BadRequest();
        return Ok(await storageService.GetFilesAsJson(pageNumber));
    }
}