using System.Text.Json;
using BitNest.Data;
using BitNest.DTOs;
using BitNest.Models;
using Blake3;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Services;

public class StorageService
{
    private readonly ILogger<StorageService> logger;
    private readonly string                  filesPath;
    private readonly string                  temporaryPath;
    private readonly AppDbContext            ctx;

    public StorageService(AppDbContext ctx, string uploadsPath, ILogger<StorageService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadsPath);
        filesPath = Path.Combine(uploadsPath, "files");
        temporaryPath = Path.Combine(uploadsPath, "temporary");
        Directory.CreateDirectory(filesPath);
        Directory.CreateDirectory(temporaryPath);
        this.ctx    = ctx;
        this.logger = logger;
    }

    
    
    public async Task<string> GetFilesAsJson(int pageNumber)
    {
        var fileMetadataDtos = await ctx.Files
            .OrderBy(x => x.Id)
            .Skip((pageNumber - 1) * 50)
            .Take(50)
            .Where(x => !x.IsDeleted && x.IsUploaded)
            .Select(x => new FileMetadataDTO { FileName = x.Name, Id = x.Id, Size = x.Size })
            .ToListAsync();
        return JsonSerializer.Serialize(
            fileMetadataDtos);
    }

    public async Task<string> UploadFile(
        IFormFile formFile,
        string fileName,
        string extension,
        int ownerUserId = 0,
        CancellationToken cancellationToken = default)
    {
        var temporaryFile = Path.Combine(temporaryPath, $"{Guid.NewGuid():N}.upload");
        try
        {
            using var hasher = Hasher.New();
            await using (var input = formFile.OpenReadStream())
            await using (var output = new FileStream(
                             temporaryFile,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[1024 * 1024];
                int bytesRead;
                long totalRead = 0;
                while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    hasher.Update(buffer.AsSpan(0, bytesRead));
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;
                }

                if (totalRead != formFile.Length)
                    throw new InvalidDataException($"Expected {formFile.Length} bytes but received {totalRead}.");

                await output.FlushAsync(cancellationToken);
            }

            var contentHash = hasher.Finalize().ToString();
            var finalDirectory = Path.Combine(filesPath, contentHash[..2]);
            var finalPath = Path.Combine(finalDirectory, contentHash);
            Directory.CreateDirectory(finalDirectory);

            if (File.Exists(finalPath))
            {
                if (!await StoredObjectMatchesAsync(finalPath, contentHash, formFile.Length, cancellationToken))
                    throw new InvalidDataException($"Stored object {contentHash} failed integrity verification.");
                File.Delete(temporaryFile);
            }
            else
            {
                try
                {
                    File.Move(temporaryFile, finalPath);
                }
                catch (IOException) when (File.Exists(finalPath))
                {
                    if (!await StoredObjectMatchesAsync(finalPath, contentHash, formFile.Length, cancellationToken))
                        throw new InvalidDataException($"Stored object {contentHash} failed integrity verification.");
                    File.Delete(temporaryFile);
                }
            }

            ctx.Files.Add(new FileMetadata
            {
                Name = fileName,
                Extention = extension,
                Size = formFile.Length,
                ContentHash = contentHash,
                IsUploaded = true,
                OwnerUserId = ownerUserId
            });
            await ctx.SaveChangesAsync(cancellationToken);

            return fileName;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to store file {FileName}", fileName);
            throw;
        }
        finally
        {
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
        }
    }

    public async Task<Stream> GetDownloadStreamAsync(int fileId)
    {
        var metadata = await GetMetadataByIdAsync(fileId);
        var path = GetObjectPath(metadata.ContentHash);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    public async Task<FileMetadata> GetMetadataByIdAsync(int fileId)
    {
        return await ctx.Files.FirstAsync(x => x.Id == fileId && !x.IsDeleted && x.IsUploaded);
    }

    public async Task SafeDeleteFile(int fileId)
    {
        var metadata = await GetMetadataByIdAsync(fileId);
        metadata.IsDeleted = true;
        await ctx.SaveChangesAsync();
    }

    public async Task<string> GetFilesAsJsonAsync(int pageNumber, int currentUserId)
    {
        var fileMetadataDtos = await ctx.Files
            .Where(x => !x.IsDeleted && x.IsUploaded)
            .Where(x => x.OwnerUserId == currentUserId || x.Grants.Any(g => g.GrantedUserId == currentUserId))
            .OrderBy(x => x.Id)
            .Skip((pageNumber - 1) * 50)
            .Take(50)
            .Select(x => new FileMetadataDTO
            {
                FileName = x.Name,
                Id = x.Id,
                Size = x.Size
            })
            .ToListAsync();
        return JsonSerializer.Serialize(fileMetadataDtos);
    }

    public async Task<bool> CanAccessFileAsync(int fileId, int currentUserId)
    {
        var file = await ctx.Files
            .Include(x => x.Grants)
            .FirstOrDefaultAsync(x => x.Id == fileId && !x.IsDeleted && x.IsUploaded);

        if (file == null)
            return false;

        // Owner can always access
        if (file.OwnerUserId == currentUserId)
            return true;

        // Check if user has a grant
        return file.Grants.Any(g => g.GrantedUserId == currentUserId);
    }

    private string GetObjectPath(string contentHash)
    {
        if (contentHash.Length != 64 || contentHash.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("The stored content hash is invalid.");

        return Path.Combine(filesPath, contentHash[..2], contentHash);
    }

    private static async Task<bool> StoredObjectMatchesAsync(
        string path,
        string expectedHash,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length != expectedSize) return false;

        using var hasher = Hasher.New();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[1024 * 1024];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            hasher.Update(buffer.AsSpan(0, bytesRead));

        return string.Equals(hasher.Finalize().ToString(), expectedHash, StringComparison.Ordinal);
    }
}
