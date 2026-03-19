namespace BitNest.Models;

public class SharepointLink
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public int CreatedByUserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public FileMetadata File { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    
    // Computed property
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
