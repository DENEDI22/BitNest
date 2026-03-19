using System.Security.Cryptography;
using System.Text;
using BitNest.Data;
using BitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Services;

public class SharepointLinkService
{
    private readonly AppDbContext context;

    public SharepointLinkService(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<(SharepointLink link, string rawToken)> CreateLinkAsync(int fileId, int userId, DateTime expiresAt)
    {
        // Verify user owns file or has grant access
        var hasAccess = await context.Files
            .AnyAsync(f => f.Id == fileId && (f.OwnerUserId == userId || 
                context.FileGrants.Any(g => g.FileId == fileId && g.GrantedUserId == userId)));
        
        if (!hasAccess)
            throw new UnauthorizedAccessException("User does not have access to this file");
        
        // Generate token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashToken(rawToken);
        
        var link = new SharepointLink
        {
            FileId = fileId,
            CreatedByUserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
        
        context.SharepointLinks.Add(link);
        await context.SaveChangesAsync();
        
        return (link, rawToken);
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
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;
        
        var link = await context.SharepointLinks
            .Include(l => l.File)
            .FirstOrDefaultAsync(l => l.TokenHash == tokenHash);
        
        if (link == null || link.RevokedAt != null || link.ExpiresAt <= now)
            return null;
        
        return link.File;
    }

    private string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
