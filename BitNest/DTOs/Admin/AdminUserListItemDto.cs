namespace BitNest.DTOs.Admin;

public class AdminUserListItemDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSignInAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
