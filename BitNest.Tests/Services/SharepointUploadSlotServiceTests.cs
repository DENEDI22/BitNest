using BitNest.Data;
using BitNest.Models;
using BitNest.Services;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Tests.Services;

[Trait("Category", "SharepointUploadSlots")]
public class SharepointUploadSlotServiceTests
{
    private const string TestHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static (User user, AppDbContext context, SharepointLinkService service) CreateUserAndService()
    {
        var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        var user = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        context.Users.Add(user);
        context.SaveChanges();
        return (user, context, service);
    }

    [Fact]
    public async Task CreateUploadSlotAsync_creates_link_with_correct_properties()
    {
        var (user, context, service) = CreateUserAndService();

        var expiresAt = DateTime.UtcNow.AddDays(7);
        var (link, rawToken) = await service.CreateUploadSlotAsync(
            user.Id, expiresAt, "My upload slot", 5, "https://localhost");

        Assert.NotNull(rawToken);
        Assert.NotEmpty(rawToken);
        Assert.Equal(LinkType.Upload, link.LinkType);
        Assert.Null(link.FileId);
        Assert.Equal("My upload slot", link.Description);
        Assert.Equal(5, link.MaxFileCount);
        Assert.Equal(0, link.UploadCount);
        Assert.Equal(user.Id, link.CreatedByUserId);
        Assert.Contains("upload.html?token=", link.ShareUrl);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task ValidateAndReserveUploadSlotAsync_returns_valid_and_increments_upload_count()
    {
        var (user, context, service) = CreateUserAndService();

        var (link, rawToken) = await service.CreateUploadSlotAsync(
            user.Id, DateTime.UtcNow.AddDays(1), "slot", 3, "https://localhost");

        var result = await service.ValidateAndReserveUploadSlotAsync(rawToken);

        Assert.True(result.IsValid);
        Assert.False(result.IsSlotFull);
        Assert.NotNull(result.Link);

        // Verify UploadCount was incremented
        await context.Entry(link).ReloadAsync();
        Assert.Equal(1, link.UploadCount);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task ValidateAndReserveUploadSlotAsync_returns_SlotFull_when_at_capacity()
    {
        var (user, context, service) = CreateUserAndService();

        var (link, rawToken) = await service.CreateUploadSlotAsync(
            user.Id, DateTime.UtcNow.AddDays(1), "slot", 2, "https://localhost");

        // Fill the slot
        await service.ValidateAndReserveUploadSlotAsync(rawToken);
        await service.ValidateAndReserveUploadSlotAsync(rawToken);

        // Now it should be full
        var result = await service.ValidateAndReserveUploadSlotAsync(rawToken);

        Assert.False(result.IsValid);
        Assert.True(result.IsSlotFull);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task ValidateAndReserveUploadSlotAsync_returns_InvalidOrExpired_for_expired_token()
    {
        var (user, context, service) = CreateUserAndService();

        var (link, rawToken) = await service.CreateUploadSlotAsync(
            user.Id, DateTime.UtcNow.AddHours(-1), "slot", 5, "https://localhost");

        var result = await service.ValidateAndReserveUploadSlotAsync(rawToken);

        Assert.False(result.IsValid);
        Assert.False(result.IsSlotFull);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task ValidateAndReserveUploadSlotAsync_returns_InvalidOrExpired_for_revoked_token()
    {
        var (user, context, service) = CreateUserAndService();

        var (link, rawToken) = await service.CreateUploadSlotAsync(
            user.Id, DateTime.UtcNow.AddDays(1), "slot", 5, "https://localhost");

        // Revoke the link
        link.RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var result = await service.ValidateAndReserveUploadSlotAsync(rawToken);

        Assert.False(result.IsValid);
        Assert.False(result.IsSlotFull);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task ValidateAndReserveUploadSlotAsync_returns_InvalidOrExpired_for_download_type_link()
    {
        var (user, context, service) = CreateUserAndService();

        // Create a file for the download link
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        // Create a download link
        var (link, rawToken) = await service.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddDays(1), "https://localhost");

        var result = await service.ValidateAndReserveUploadSlotAsync(rawToken);

        Assert.False(result.IsValid);
        Assert.False(result.IsSlotFull);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveLinksForUserAsync_returns_both_download_and_upload_links()
    {
        var (user, context, service) = CreateUserAndService();

        // Create a file for the download link
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();

        // Create both types of links
        await service.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddDays(1), "https://localhost");
        await service.CreateUploadSlotAsync(user.Id, DateTime.UtcNow.AddDays(1), "upload slot", 5, "https://localhost");

        var links = await service.GetActiveLinksForUserAsync(user.Id);

        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.LinkType == LinkType.Download);
        Assert.Contains(links, l => l.LinkType == LinkType.Upload);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task ValidateAndReserveUploadSlotAsync_returns_InvalidOrExpired_for_invalid_token()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);

        var result = await service.ValidateAndReserveUploadSlotAsync("nonexistent-token");

        Assert.False(result.IsValid);
        Assert.False(result.IsSlotFull);
    }
}
