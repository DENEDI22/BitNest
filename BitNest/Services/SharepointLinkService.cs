using System.Security.Cryptography;
using System.Text;
using BitNest.Data;
using BitNest.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Services;

public class UploadSlotValidationResult
{
    public bool IsValid { get; private set; }
    public bool IsSlotFull { get; private set; }
    public SharepointLink? Link { get; private set; }

    public static UploadSlotValidationResult InvalidOrExpired => new() { IsValid = false, IsSlotFull = false };
    public static UploadSlotValidationResult SlotFull => new() { IsValid = false, IsSlotFull = true };
    public static UploadSlotValidationResult Valid(SharepointLink link) => new() { IsValid = true, Link = link };
}

public class SharepointLinkService
{
    private readonly AppDbContext context;

    public SharepointLinkService(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<(SharepointLink link, string rawToken)> CreateLinkAsync(int fileId, int userId, DateTime expiresAt, string baseUrl = "")
    {
        // Verify user owns file or has grant access
        var hasAccess = await context.Files
            .AnyAsync(f => f.Id == fileId && (f.OwnerUserId == userId ||
                context.FileGrants.Any(g => g.FileId == fileId && g.GrantedUserId == userId)));

        if (!hasAccess)
            throw new UnauthorizedAccessException("User does not have access to this file");

        // Generate token — Base64Url encoding avoids +/=/ chars that corrupt URL routing
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashToken(rawToken);
        var shareUrl = $"{baseUrl}/share.html?token={rawToken}";

        var link = new SharepointLink
        {
            FileId = fileId,
            CreatedByUserId = userId,
            TokenHash = tokenHash,
            ShareUrl = shareUrl,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            LinkType = LinkType.Download
        };

        context.SharepointLinks.Add(link);
        await context.SaveChangesAsync();

        return (link, rawToken);
    }

    public async Task<(SharepointLink link, string rawToken)> CreateUploadSlotAsync(
        int userId, DateTime expiresAt, string? description, int maxFileCount, string baseUrl)
    {
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashToken(rawToken);
        var shareUrl = $"{baseUrl}/upload.html?token={rawToken}";

        var link = new SharepointLink
        {
            FileId = null,
            CreatedByUserId = userId,
            TokenHash = tokenHash,
            ShareUrl = shareUrl,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            LinkType = LinkType.Upload,
            Description = description,
            MaxFileCount = maxFileCount,
            UploadCount = 0
        };

        context.SharepointLinks.Add(link);
        await context.SaveChangesAsync();
        return (link, rawToken);
    }

    public async Task<UploadSlotValidationResult> ValidateAndReserveUploadSlotAsync(string token)
    {
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;

        var link = await context.SharepointLinks
            .Include(l => l.CreatedBy)
            .FirstOrDefaultAsync(l => l.TokenHash == tokenHash);

        if (link == null || link.LinkType != LinkType.Upload || link.RevokedAt != null || link.ExpiresAt <= now)
            return UploadSlotValidationResult.InvalidOrExpired;

        if (link.MaxFileCount.HasValue && link.UploadCount >= link.MaxFileCount.Value)
            return UploadSlotValidationResult.SlotFull;

        try
        {
            var updated = await context.SharepointLinks
                .Where(l => l.Id == link.Id
                         && l.LinkType == LinkType.Upload
                         && l.RevokedAt == null
                         && l.ExpiresAt > now
                         && (l.MaxFileCount == null || l.UploadCount < l.MaxFileCount))
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.UploadCount, l => l.UploadCount + 1));

            if (updated == 0)
            {
                await context.Entry(link).ReloadAsync();
                return link.LinkType != LinkType.Upload || link.RevokedAt != null || link.ExpiresAt <= now
                    ? UploadSlotValidationResult.InvalidOrExpired
                    : UploadSlotValidationResult.SlotFull;
            }
        }
        catch (InvalidOperationException)
        {
            // InMemory provider fallback — safe because production uses Postgres
            await context.Entry(link).ReloadAsync();
            if (link.MaxFileCount.HasValue && link.UploadCount >= link.MaxFileCount.Value)
                return UploadSlotValidationResult.SlotFull;
            link.UploadCount++;
            await context.SaveChangesAsync();
        }

        return UploadSlotValidationResult.Valid(link);
    }

    public async Task ReleaseUploadSlotAsync(int linkId)
    {
        try
        {
            await context.SharepointLinks
                .Where(link => link.Id == linkId && link.UploadCount > 0)
                .ExecuteUpdateAsync(setters => setters.SetProperty(link => link.UploadCount, link => link.UploadCount - 1));
        }
        catch (InvalidOperationException)
        {
            var link = await context.SharepointLinks.FindAsync(linkId);
            if (link is { UploadCount: > 0 })
            {
                link.UploadCount--;
                await context.SaveChangesAsync();
            }
        }
    }

    public async Task<SharepointLink?> ValidateTokenAndGetLinkAsync(string token)
    {
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;

        var link = await context.SharepointLinks
            .Include(l => l.File)
            .Include(l => l.CreatedBy)
            .FirstOrDefaultAsync(l => l.TokenHash == tokenHash);

        if (link == null || link.RevokedAt != null || link.ExpiresAt <= now
                         || link.LinkType == LinkType.Download
                         && (link.File == null || link.File.IsDeleted || !link.File.IsUploaded))
            return null;

        return link;
    }

    public async Task<List<SharepointLink>> GetActiveLinksForUserAsync(int userId)
    {
        var now = DateTime.UtcNow;
        return await context.SharepointLinks
            .Include(l => l.File)
            .Where(l => l.CreatedByUserId == userId && l.RevokedAt == null && l.ExpiresAt > now)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RevokeLinkAsync(int linkId, int userId)
    {
        var link = await context.SharepointLinks.FindAsync(linkId);
        if (link == null || link.CreatedByUserId != userId)
            return false;

        link.RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<FileMetadata?> ValidateTokenAndGetFileAsync(string token)
    {
        var link = await ValidateTokenAndGetLinkAsync(token);
        if (link == null || link.LinkType != LinkType.Download)
            return null;

        return link.File;
    }

    private string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
