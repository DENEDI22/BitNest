using System.Security.Claims;
using System.Text.Json;
using BitNest.Controllers;
using BitNest.Data;
using BitNest.Models;
using BitNest.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Tests.Controllers;

[Trait("Category", "SharepointLinks")]
public class SharepointControllerTests
{
    private const string TestHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SetupControllerContext(ControllerBase controller, int userId)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "mock"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("localhost");
    }

    [Fact]
    public async Task CreateLink_returns_201_with_url_for_authenticated_user()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        var controller = new SharepointController(service);

        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        SetupControllerContext(controller, user.Id);

        var request = new CreateLinkRequest(file.Id, DateTime.UtcNow.AddHours(1));
        var result = await controller.CreateLink(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var value = JsonSerializer.SerializeToElement(createdResult.Value);
        Assert.Equal(Assert.Single(context.SharepointLinks).Id, value.GetProperty("id").GetInt32());
        Assert.Contains("share.html?token=", value.GetProperty("url").GetString());
    }

    [Fact]
    public async Task CreateLink_returns_403_if_user_doesnt_own_file()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        var controller = new SharepointController(service);

        var owner = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        var unauthorized = new User { Username = "unauthorized", NormalizedUsername = "unauthorized", PasswordHash = "hash" };
        context.Users.AddRange(owner, unauthorized);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = owner.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        SetupControllerContext(controller, unauthorized.Id);

        var request = new CreateLinkRequest(file.Id, DateTime.UtcNow.AddHours(1));
        var result = await controller.CreateLink(request);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetLinks_returns_200_with_array_of_active_links()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        var controller = new SharepointController(service);

        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        // Create a link
        await service.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(1));

        SetupControllerContext(controller, user.Id);

        var result = await controller.GetLinks();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var links = Assert.IsAssignableFrom<System.Collections.IEnumerable>(okResult.Value);
        Assert.Single(links);
    }

    [Fact]
    public async Task RevokeLink_returns_204_if_user_owns_link()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        var controller = new SharepointController(service);

        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        var (link, _) = await service.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(1));

        SetupControllerContext(controller, user.Id);

        var result = await controller.RevokeLink(link.Id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RevokeLink_returns_404_if_link_doesnt_exist_or_user_doesnt_own()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        var controller = new SharepointController(service);

        var owner = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        var other = new User { Username = "other", NormalizedUsername = "other", PasswordHash = "hash" };
        context.Users.AddRange(owner, other);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = owner.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        var (link, _) = await service.CreateLinkAsync(file.Id, owner.Id, DateTime.UtcNow.AddHours(1));

        SetupControllerContext(controller, other.Id);

        var result = await controller.RevokeLink(link.Id);

        Assert.IsType<NotFoundResult>(result);
    }
}
