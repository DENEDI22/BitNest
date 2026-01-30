using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using BitNest.Data;
using BitNest.DTOs;
using BitNest.Models;
using Blake3;
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
        this.uploadsPath = Path.Combine(uploadsPath, "files");
        this.chunksPath = Path.Combine(uploadsPath, "chunks");
        if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
        if (!Directory.Exists(this.uploadsPath)) Directory.CreateDirectory(this.uploadsPath);
        if (!Directory.Exists(this.chunksPath)) Directory.CreateDirectory(this.chunksPath);
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
                .Where(x => !x.IsDeleted && x.IsUploaded)
                .Select(x => new FileMetadataDTO { FileName = x.Name, Id = x.Id, Size = x.Size })
                .ToListAsync());
    }

    /// <summary>
    /// returns metadataID if metadata loaded successfully
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    private async Task<FileMetadata> UploadFileMetadata(FileMetadata metadata)
    {
        var md = await ctx.Files.AddAsync(metadata);
        await ctx.SaveChangesAsync();
        return md.Entity;
    }
    
    public async Task<string?> UploadFile(IFormFile formFile, string fileName, string extension)
    {
        var path = Path.Combine(uploadsPath, fileName);
        await using var filestream = new FileStream(path, FileMode.Create);
        try
        {
            var fileMd = await UploadFileMetadata(new FileMetadata
            {
                Name = fileName,
                Extention = extension,
                Size = formFile.Length,
                IsChunked = true,
                IsUploaded = false,
                BlobPath = "DummyPath"
            });
            var chunkSize = 262144; // 256kb
            var buffer = new byte[chunkSize];
            int i;
            long totalRead = 0;
            var totalSize = formFile.Length;
            var readStream = formFile.OpenReadStream();
            int chunkCounter = 0;
            while ((i = await readStream.ReadAsync(buffer)) > 0)
            {
                var hash = Hasher.Hash(buffer).AsSpan().ToArray();
                var chunk = await ctx.Chunks.FirstOrDefaultAsync(x => x.Hash == hash);
                if (chunk != null)
                {
                    ctx.FileChunks.Add(new FileChunk { Chunk = chunk, Order = chunkCounter++, File = fileMd });
                }
                else
                {
                    await using var chunkStream = new FileStream(
                        Path.Combine(chunksPath,
                            Convert.ToBase64String(hash).Replace('/', '-').Replace('+', '-').TrimEnd('=') + ".chunk"),
                        FileMode.Create);
                    await chunkStream.WriteAsync(buffer, 0, i);
                    var chunkMetadata = new ChunkMetadata
                    {
                        Hash = hash
                    };
                    await ctx.Chunks.AddAsync(chunkMetadata);
                    await ctx.FileChunks.AddAsync(new FileChunk
                        { Chunk = chunkMetadata, Order = chunkCounter++, File = fileMd });
                }

                totalRead += i;
                logger.LogInformation("Progress: {progress}%", (totalRead * 100) / totalSize);
            }

            fileMd.IsUploaded = true;
        }
        catch (IOException e)
        {
            logger.LogError("Error writing the file: {message}", e.Message);
        }
        catch (OperationCanceledException e)
        {
            logger.LogError("Connection lost: {message}", e.Message);
        }
        finally
        {
            await ctx.SaveChangesAsync();
        }

        return fileName + extension;
    }

    public async Task<Stream> GetDownloadStreamAsync(int fileId)
    {
        var metadata = await GetMetadataByIdAsync(fileId);
        var fileChunks = await ctx.FileChunks
            .Where(x => x.File.Id == fileId)
            .OrderBy(x => x.Order)
            .Include(x => x.Chunk)
            .ToListAsync();
        var stream = new ChunkedFileStream(fileChunks, chunksPath);
        return stream;
    }
    
    public async Task<FileMetadata> GetMetadataByIdAsync(int fileId)
    {
        return await ctx.Files.FirstAsync(x => x.Id == fileId);
    }

    public async Task SafeDeleteFile(int fileId)
    {
        var metadata = await GetMetadataByIdAsync(fileId);
        metadata.IsDeleted = true;
        await ctx.SaveChangesAsync();
    }
}