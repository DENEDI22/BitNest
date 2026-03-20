namespace BitNest.Models;

public enum LinkType { Download = 0, Upload = 1 }

public class SharepointLink
{
    public int Id { get; set; }
    public int? FileId { get; set; }
    public int CreatedByUserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public string ShareUrl { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public LinkType LinkType { get; set; } = LinkType.Download;
    public string? Description { get; set; }
    public int? MaxFileCount { get; set; }
    public int UploadCount { get; set; } = 0;

    // Navigation properties
    public FileMetadata? File { get; set; }
    public User CreatedBy { get; set; } = null!;

    // Computed property
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
