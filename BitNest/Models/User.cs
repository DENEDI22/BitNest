namespace BitNest.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string NormalizedUsername { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSignInAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RefreshSession> RefreshSessions { get; set; } = [];
    public List<FileMetadata> OwnedFiles { get; set; } = [];
    public List<FileGrant> GrantedFiles { get; set; } = [];
    public List<FileGrant> IssuedFileGrants { get; set; } = [];

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}
