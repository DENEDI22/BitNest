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

    [HttpGet]
    public IActionResult Files()
    {
        return Ok();
    }
}