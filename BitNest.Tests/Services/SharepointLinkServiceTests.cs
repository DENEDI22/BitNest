using BitNest.Data;
using BitNest.Models;
using BitNest.Services;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Tests.Services;

[Trait("Category", "SharepointLinks")]
public class SharepointLinkServiceTests
{
    private const string TestHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateLinkAsync_generates_unique_64byte_token_and_hashes_it()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        // Create user and file
        var user = new User { Username = "test-user", NormalizedUsername = "test-user", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash, IsUploaded = true };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        var expiresAt = DateTime.UtcNow.AddHours(1);
        
        // Act
        var (link, rawToken) = await service.CreateLinkAsync(file.Id, user.Id, expiresAt);
        
        // Assert
        Assert.NotNull(rawToken);
        Assert.NotEmpty(rawToken);
        Assert.NotEqual(rawToken, link.TokenHash); // Token is hashed
        Assert.Equal(file.Id, link.FileId);
        Assert.Equal(user.Id, link.CreatedByUserId);
        Assert.Equal(expiresAt, link.ExpiresAt, TimeSpan.FromSeconds(1));
        Assert.Null(link.RevokedAt);
    }

    [Fact]
    public async Task CreateLinkAsync_ensures_user_owns_or_has_grant_access()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        var owner = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        var unauthorized = new User { Username = "unauthorized", NormalizedUsername = "unauthorized", PasswordHash = "hash" };
        context.Users.AddRange(owner, unauthorized);
        
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = owner.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        // Unauthorized user tries to create link
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await service.CreateLinkAsync(file.Id, unauthorized.Id, DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public async Task CreateLinkAsync_allows_granted_user_to_create_link()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        var owner = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        var granted = new User { Username = "granted", NormalizedUsername = "granted", PasswordHash = "hash" };
        context.Users.AddRange(owner, granted);
        
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = owner.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        var grant = new FileGrant { FileId = file.Id, GrantedUserId = granted.Id, GrantedByUserId = owner.Id };
        context.FileGrants.Add(grant);
        await context.SaveChangesAsync();
        
        // Granted user can create link
        var (link, rawToken) = await service.CreateLinkAsync(file.Id, granted.Id, DateTime.UtcNow.AddHours(1));
        
        Assert.NotNull(link);
        Assert.Equal(granted.Id, link.CreatedByUserId);
    }

    [Fact]
    public async Task GetActiveLinksForUserAsync_returns_only_active_non_expired_links()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        var now = DateTime.UtcNow;
        
        // Active link
        var activeLink = new SharepointLink
        {
            FileId = file.Id,
            CreatedByUserId = user.Id,
            TokenHash = "active-hash",
            ShareUrl = "https://localhost/share.html?token=active-token",
            ExpiresAt = now.AddHours(1),
            File = file
        };
        
        // Expired link
        var expiredLink = new SharepointLink
        {
            FileId = file.Id,
            CreatedByUserId = user.Id,
            TokenHash = "expired-hash",
            ShareUrl = "https://localhost/share.html?token=expired-token",
            ExpiresAt = now.AddHours(-1),
            File = file
        };
        
        // Revoked link
        var revokedLink = new SharepointLink
        {
            FileId = file.Id,
            CreatedByUserId = user.Id,
            TokenHash = "revoked-hash",
            ShareUrl = "https://localhost/share.html?token=revoked-token",
            ExpiresAt = now.AddHours(1),
            RevokedAt = now.AddMinutes(-5),
            File = file
        };
        
        context.SharepointLinks.AddRange(activeLink, expiredLink, revokedLink);
        await context.SaveChangesAsync();
        
        // Act
        var activeLinks = await service.GetActiveLinksForUserAsync(user.Id);
        
        // Assert
        Assert.Single(activeLinks);
        Assert.Equal("active-hash", activeLinks[0].TokenHash);
    }

    [Fact]
    public async Task RevokeLinkAsync_sets_RevokedAt_only_if_user_owns_link()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        var owner = new User { Username = "owner", NormalizedUsername = "owner", PasswordHash = "hash" };
        var other = new User { Username = "other", NormalizedUsername = "other", PasswordHash = "hash" };
        context.Users.AddRange(owner, other);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = owner.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        var link = new SharepointLink
        {
            FileId = file.Id,
            CreatedByUserId = owner.Id,
            TokenHash = "link-hash",
            ShareUrl = "https://localhost/share.html?token=link-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        context.SharepointLinks.Add(link);
        await context.SaveChangesAsync();
        
        // Other user tries to revoke
        var result1 = await service.RevokeLinkAsync(link.Id, other.Id);
        Assert.False(result1);
        
        // Refresh link
        await context.Entry(link).ReloadAsync();
        Assert.Null(link.RevokedAt);
        
        // Owner revokes
        var result2 = await service.RevokeLinkAsync(link.Id, owner.Id);
        Assert.True(result2);
        
        // Refresh link
        await context.Entry(link).ReloadAsync();
        Assert.NotNull(link.RevokedAt);
    }

    [Fact]
    public async Task ValidateTokenAndGetFileAsync_returns_file_if_token_valid()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash, IsUploaded = true };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        // Create link
        var (link, rawToken) = await service.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(1));
        
        // Validate token
        var retrievedFile = await service.ValidateTokenAndGetFileAsync(rawToken);
        
        Assert.NotNull(retrievedFile);
        Assert.Equal(file.Id, retrievedFile.Id);
        Assert.Equal(file.Name, retrievedFile.Name);
    }

    [Fact]
    public async Task ValidateTokenAndGetFileAsync_returns_null_if_token_expired()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        // Create link that's already expired
        var (link, rawToken) = await service.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(-1));
        
        // Validate token
        var retrievedFile = await service.ValidateTokenAndGetFileAsync(rawToken);
        
        Assert.Null(retrievedFile);
    }

    [Fact]
    public async Task ValidateTokenAndGetFileAsync_returns_null_if_token_revoked()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        // Create and then revoke link
        var (link, rawToken) = await service.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(1));
        await service.RevokeLinkAsync(link.Id, user.Id);
        
        // Validate revoked token
        var retrievedFile = await service.ValidateTokenAndGetFileAsync(rawToken);
        
        Assert.Null(retrievedFile);
    }

    [Fact]
    public async Task ValidateTokenAndGetFileAsync_returns_null_if_token_invalid()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        // Validate non-existent token
        var retrievedFile = await service.ValidateTokenAndGetFileAsync("nonexistent-token");
        
        Assert.Null(retrievedFile);
    }

    [Fact]
    public async Task Token_generation_produces_unique_tokens_across_1000_iterations()
    {
        await using var context = CreateInMemoryContext();
        var service = new SharepointLinkService(context);
        
        var user = new User { Username = "test", NormalizedUsername = "test", PasswordHash = "hash" };
        context.Users.Add(user);
        var file = new FileMetadata { Name = "test.txt", Size = 100, OwnerUserId = user.Id, Extention = ".txt", ContentHash = TestHash };
        context.Files.Add(file);
        await context.SaveChangesAsync();
        
        var tokens = new HashSet<string>();
        
        for (int i = 0; i < 1000; i++)
        {
            var (link, rawToken) = await service.CreateLinkAsync(file.Id, user.Id, DateTime.UtcNow.AddHours(1));
            Assert.True(tokens.Add(rawToken), $"Collision detected at iteration {i}");
            Assert.True(tokens.Add(link.TokenHash), $"Hash collision detected at iteration {i}");
        }
    }
}
