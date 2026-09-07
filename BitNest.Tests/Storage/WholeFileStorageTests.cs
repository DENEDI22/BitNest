using System.Text;
using BitNest.Data;
using BitNest.Services;
using Blake3;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BitNest.Tests.Storage;

public sealed class WholeFileStorageTests : IDisposable
{
    private readonly string storageRoot = Path.Combine(
        Path.GetTempPath(), "bitnest-whole-file-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Upload_stores_the_unchanged_file_at_its_blake3_hash()
    {
        var bytes = Encoding.UTF8.GetBytes("whole file content");
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.UploadFile(CreateFormFile(bytes, "report.txt"), "report.txt", ".txt", 7);

        var metadata = await context.Files.SingleAsync();
        var expectedHash = Hasher.Hash(bytes).ToString();
        var storedPath = Path.Combine(storageRoot, "files", expectedHash[..2], expectedHash);
        Assert.Equal(expectedHash, metadata.ContentHash);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(storedPath));
        Assert.True(metadata.IsUploaded);
    }

    [Fact]
    public async Task Identical_uploads_create_two_records_but_only_one_stored_file()
    {
        var bytes = Encoding.UTF8.GetBytes("same bytes");
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.UploadFile(CreateFormFile(bytes, "first.txt"), "first.txt", ".txt", 1);
        await service.UploadFile(CreateFormFile(bytes, "second.txt"), "second.txt", ".txt", 2);

        var files = await context.Files.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, files.Count);
        Assert.Equal(files[0].ContentHash, files[1].ContentHash);
        Assert.Single(Directory.GetFiles(Path.Combine(storageRoot, "files"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Download_returns_the_exact_uploaded_bytes()
    {
        var bytes = Enumerable.Range(0, 1_100_003).Select(i => (byte)(i % 251)).ToArray();
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.UploadFile(CreateFormFile(bytes, "large.bin"), "large.bin", ".bin");
        var fileId = await context.Files.Select(x => x.Id).SingleAsync();

        await using var stream = await service.GetDownloadStreamAsync(fileId);
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);

        Assert.Equal(bytes, output.ToArray());
    }

    [Fact]
    public async Task Empty_files_are_stored_and_addressed()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.UploadFile(CreateFormFile([], "empty.txt"), "empty.txt", ".txt");

        var metadata = await context.Files.SingleAsync();
        Assert.Equal(Hasher.Hash([]).ToString(), metadata.ContentHash);
        Assert.Equal(0, metadata.Size);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private StorageService CreateService(AppDbContext context) =>
        new(context, storageRoot, NullLogger<StorageService>.Instance);

    private static IFormFile CreateFormFile(byte[] bytes, string name)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "formFile", name);
    }

    public void Dispose()
    {
        if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, true);
    }
}
