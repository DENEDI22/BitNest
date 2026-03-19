namespace BitNest.DTOs.Admin;

public class AdminCreateUserRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
