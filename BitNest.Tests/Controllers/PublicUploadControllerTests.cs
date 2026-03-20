using System.Security.Claims;
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

[Trait("Category", "SharepointUploadSlots")]
public class PublicUploadControllerTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static StorageService CreateStorageService(AppDbContext context)
    {
        var mockLogger = new Mock<ILogger<StorageService>>();
        return new StorageService(context, "/tmp/test-upload", mockLogger.Object);
    }

    private static void SetupControllerContext(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static IFormFile CreateMockFormFile(string fileName = "uploaded.txt", string content = "Hello, World!")
    {
        var mockFile = new Mock<IFormFile>();
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((s, _) => stream.CopyToAsync(s));
        return mockFile.Object;
    }

    [Fact]
    public async Task UploadFile_returns_200_when_slot_is_valid()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateStorageService(context);
        var controller = new PublicShareController(linkService, storageService);
        SetupControllerContext(controller);

        var user = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var (link, rawToken) = await linkService.CreateUploadSlotAsync(
            user.Id, DateTime.UtcNow.AddDays(1), "test slot", 5, "https://localhost");

        var formFile = CreateMockFormFile();
        var result = await controller.UploadFile(rawToken, formFile);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UploadFile_returns_409_when_slot_is_full()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateStorageService(context);
        var controller = new PublicShareController(linkService, storageService);
        SetupControllerContext(controller);

        var user = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Create slot with max 1 file
        var (link, rawToken) = await linkService.CreateUploadSlotAsync(
            user.Id, DateTime.UtcNow.AddDays(1), "test slot", 1, "https://localhost");

        // Fill the slot
        await controller.UploadFile(rawToken, CreateMockFormFile("file1.txt"));

        // Second upload should fail with 409
        var result = await controller.UploadFile(rawToken, CreateMockFormFile("file2.txt"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task UploadFile_returns_404_for_expired_token()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateStorageService(context);
        var controller = new PublicShareController(linkService, storageService);
        SetupControllerContext(controller);

        var user = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Create expired slot
        var (link, rawToken) = await linkService.CreateUploadSlotAsync(
            user.Id, DateTime.UtcNow.AddHours(-1), "expired slot", 5, "https://localhost");

        var result = await controller.UploadFile(rawToken, CreateMockFormFile());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetFileMetadata_returns_upload_link_type_for_upload_slots()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var storageService = CreateStorageService(context);
        var controller = new PublicShareController(linkService, storageService);
        SetupControllerContext(controller);

        var user = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var (link, rawToken) = await linkService.CreateUploadSlotAsync(
            user.Id, DateTime.UtcNow.AddDays(1), "My slot", 10, "https://localhost");

        var result = await controller.GetFileMetadata(rawToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic value = okResult.Value!;
        Assert.Equal("upload", value.linkType.ToString());
        Assert.Equal("My slot", value.description.ToString());
        Assert.Equal(10, (int)value.maxFileCount);
    }

    [Fact]
    public async Task GetLinks_includes_linkType_for_all_links()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var sharepointController = new SharepointController(linkService);

        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", BlobPath = "test" };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        // Create both types of links
        await linkService.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddDays(1), "https://localhost");
        await linkService.CreateUploadSlotAsync(user.Id, DateTime.UtcNow.AddDays(1), "slot", 5, "https://localhost");

        // Setup authenticated controller context
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        }, "mock"));
        sharepointController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };
        sharepointController.HttpContext.Request.Scheme = "https";
        sharepointController.HttpContext.Request.Host = new HostString("localhost");

        var result = await sharepointController.GetLinks();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var links = Assert.IsAssignableFrom<System.Collections.IEnumerable>(okResult.Value);
        var linkList = links.Cast<object>().ToList();
        Assert.Equal(2, linkList.Count);

        // Check that linkType is present on each link
        var downloadLink = linkList.FirstOrDefault(l =>
        {
            dynamic d = l;
            return d.linkType.ToString() == "download";
        });
        var uploadLink = linkList.FirstOrDefault(l =>
        {
            dynamic d = l;
            return d.linkType.ToString() == "upload";
        });

        Assert.NotNull(downloadLink);
        Assert.NotNull(uploadLink);
    }

    [Fact]
    public async Task CreateUploadSlot_returns_201_with_url()
    {
        await using var context = CreateInMemoryContext();
        var linkService = new SharepointLinkService(context);
        var controller = new SharepointController(linkService);

        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        }, "mock"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("localhost");

        var request = new CreateUploadSlotRequest(DateTime.UtcNow.AddDays(7), "My upload slot", 10);
        var result = await controller.CreateUploadSlot(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        dynamic value = createdResult.Value!;
        Assert.Contains("upload.html?token=", value.url.ToString());
    }
}
