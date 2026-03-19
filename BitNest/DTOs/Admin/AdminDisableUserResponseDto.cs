namespace BitNest.DTOs.Admin;

public class AdminDisableUserResponseDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
