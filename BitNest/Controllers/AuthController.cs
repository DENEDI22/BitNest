using System.Security.Claims;
using BitNest.DTOs.Auth;
using BitNest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BitNest.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService authService;

    public AuthController(AuthService authService)
    {
        this.authService = authService;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] AuthCredentialRequestDto request)
    {
        var result = await authService.Signup(request);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthCredentialRequestDto request)
    {
        var result = await authService.Login(request);
        return ToActionResult(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        var result = await authService.Refresh(request);
        return ToActionResult(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request)
    {
        var result = await authService.Logout(request);
        return ToActionResult(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new AuthErrorDto
            {
                Code = "unauthorized",
                Message = "User session is invalid."
            });
        }

        var result = await authService.GetMe(userId);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(AuthService.ServiceResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.StatusCode switch
        {
            400 => BadRequest(result.Error),
            401 => Unauthorized(result.Error),
            409 => Conflict(result.Error),
            _ => StatusCode(result.StatusCode, result.Error)
        };
    }
}
