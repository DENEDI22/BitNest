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

    [HttpGet]
    public async Task<IActionResult> Files()
    {
        return Ok(await storageService.GetFileNames());
    }
}