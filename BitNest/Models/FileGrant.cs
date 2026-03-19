namespace BitNest.Models;

public class FileGrant
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public int GrantedUserId { get; set; }
    public int GrantedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public FileMetadata File { get; set; } = null!;
    public User GrantedUser { get; set; } = null!;
    public User GrantedByUser { get; set; } = null!;
}
