using System.Text.Json;
using BitNest.Controllers;
using BitNest.Data;
using BitNest.Models;
using BitNest.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BitNest.Tests.Controllers;

[Trait("Category", "SharepointLinks")]
public class PublicShareControllerTests : IDisposable
{
    private readonly string uploadsRoot = Path.Combine(Path.GetTempPath(), $"bitnest-public-share-tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(uploadsRoot))
            Directory.Delete(uploadsRoot, recursive: true);
    }

    private const string TestHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private StorageService CreateMockStorageService(AppDbContext context)
    {
        var mockLogger = new Mock<ILogger<StorageService>>();

        return new StorageService(context, uploadsRoot, mockLogger.Object);
    }

    [Fact]
    public async Task GetFileMetadata_returns_200_with_file_info_for_valid_download_token()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateMockStorageService(context);
        var controller = new PublicShareController(linkService, storageService);

        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 1234, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash, IsUploaded = true };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        var (link, rawToken) = await linkService.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(1));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetFileMetadata(rawToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = JsonSerializer.SerializeToElement(okResult.Value);
        Assert.Equal("download", value.GetProperty("linkType").GetString());
        Assert.Equal("test.txt", value.GetProperty("fileName").GetString());
        Assert.Equal(1234L, value.GetProperty("fileSize").GetInt64());
    }

    [Fact]
    public async Task GetFileMetadata_returns_404_for_expired_token()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateMockStorageService(context);
        var controller = new PublicShareController(linkService, storageService);

        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash, IsUploaded = true };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        // Create expired link
        var (link, rawToken) = await linkService.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(-1));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetFileMetadata(rawToken);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var value = JsonSerializer.SerializeToElement(notFoundResult.Value);
        Assert.Contains("no longer valid", value.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetFileMetadata_returns_404_for_revoked_token()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateMockStorageService(context);
        var controller = new PublicShareController(linkService, storageService);

        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash, IsUploaded = true };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        var (link, rawToken) = await linkService.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(1));
        await linkService.RevokeLinkAsync(link.Id, user.Id);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetFileMetadata(rawToken);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var value = JsonSerializer.SerializeToElement(notFoundResult.Value);
        Assert.Contains("no longer valid", value.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetFileMetadata_returns_404_for_invalid_token()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateMockStorageService(context);
        var controller = new PublicShareController(linkService, storageService);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetFileMetadata("invalid-token");

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var value = JsonSerializer.SerializeToElement(notFoundResult.Value);
        Assert.Contains("no longer valid", value.GetProperty("message").GetString());
    }

    [Fact]
    public async Task DownloadFile_returns_404_for_invalid_token()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateMockStorageService(context);
        var controller = new PublicShareController(linkService, storageService);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.DownloadFile("invalid-token");

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var value = JsonSerializer.SerializeToElement(notFoundResult.Value);
        Assert.Contains("no longer valid", value.GetProperty("message").GetString());
    }
}
