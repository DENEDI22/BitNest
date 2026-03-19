namespace BitNest.DTOs.Auth;

public class AuthCredentialRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
