namespace BitNest.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string NormalizedUsername { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RefreshSession> RefreshSessions { get; set; } = [];

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}
