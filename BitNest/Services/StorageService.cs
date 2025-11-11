using BitNest.Data;
using BitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Services;

public class StorageService
{
    private readonly ILogger<StorageService> logger;
    private readonly string uploadsPath;
    private readonly AppDbContext ctx;

    public StorageService(string storagePath, AppDbContext ctx, string uploadsPath)
    {
        this.uploadsPath = uploadsPath;
        this.ctx = ctx;
    }

    public async Task<List<string>> GetFileNames()
    {
        return ctx.Files.Select(x => x.Name).ToList();
    }

    public async Task<string?> UploadFile(IFormFile formFile, string fileName, string extension)
    {
        var path = Path.Combine(uploadsPath, fileName + extension);
        var filestream = new FileStream(path, FileMode.Create);
        try
        {
            await formFile.CopyToAsync(filestream);
        }
        catch (IOException e)
        {
            logger.LogError("Error writing the file: {message}", e.Message);
            return null;
        }
        catch (OperationCanceledException e)
        {
            logger.LogError("Connection lost: {message}", e.Message);
            return null;
        }

        try
        {
            await ctx.Files.AddAsync(new FileMetadata
            {
                Name = fileName,
                Extention = extension,
                Size = formFile.Length,
                BlobPath = path
            });
        }
        catch (Exception e)
        {
            logger.LogError("Error saving file metadata: {message}", e.Message);
            return null;
        }

        return fileName + extension;
    }
}