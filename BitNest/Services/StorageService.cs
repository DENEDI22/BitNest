using System.Text.Json;
using System.Text.Json.Serialization;
using BitNest.Data;
using BitNest.DTOs;
using BitNest.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Services;

public class StorageService
{
    private readonly ILogger<StorageService> logger;
    private readonly string uploadsPath;
    private readonly string chunksPath;
    private readonly AppDbContext ctx;

    public StorageService(AppDbContext ctx, string uploadsPath, ILogger<StorageService> logger)
    {
        this.uploadsPath = uploadsPath + "files";
        this.chunksPath = uploadsPath + "chunks";
        if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
        this.ctx = ctx;
        this.logger = logger;
    }

    public async Task<string> GetFilesAsJson(int pageNumber)
    {
        return JsonSerializer.Serialize(
            await ctx.Files
                .OrderBy(x => x.Id)
                .Skip((pageNumber - 1) * 50)
                .Take(50)
                .Select(x => new FileMetadataDTO { FileName = x.Name, Id = x.Id, Size = x.Size })
                .ToListAsync());
    }

    public async Task SplitFileInChunks(string filePath, int chunkSize = 4096)
    {
        await using var fileStream = new FileStream(filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            chunkSize,
            FileOptions.Asynchronous);
        byte[] buffer = new byte[chunkSize];
        int i = 0;
        while ((i = await fileStream.ReadAsync(buffer)) > 0)
        {
            await using var chunkStream = new FileStream(Path.Combine(chunksPath, Guid.NewGuid().ToString() + ".chunk"),
                FileMode.Create);
            await chunkStream.WriteAsync(buffer, 0, i);
        }
    }

    public async Task<string?> UploadFile(IFormFile formFile, string fileName, string extension)
    {
        var path = Path.Combine(uploadsPath, fileName);
        await using var filestream = new FileStream(path, FileMode.Create);
        try
        {
            await formFile.CopyToAsync(filestream).ConfigureAwait(false);
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
            await ctx.SaveChangesAsync();
        }
        catch (Exception e)
        {
            logger.LogError("Error saving file metadata: {message}", e.Message);
            return null;
        }

        return fileName + extension;
    }

    public async Task<FileStream> GetDownloadStreamAsync(int fileId)
    {
        return File.OpenRead((await ctx.Files.FirstAsync(x => x.Id == fileId)).BlobPath);
    }

    public async Task<FileMetadata> GetMetadataByIdAsync(int fileId)
    {
        return await ctx.Files.FirstAsync(x => x.Id == fileId);
    }
}