using BitNest.DTOs.Admin;
using BitNest.Models;
using BitNest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BitNest.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminUsersController : ControllerBase
{
    private readonly AuthService authService;
    private readonly ILogger<AdminUsersController> logger;

    public AdminUsersController(AuthService authService, ILogger<AdminUsersController> logger)
    {
        this.authService = authService;
        this.logger = logger;
    }

    private bool IsAdmin()
    {
        var adminClaim = User.FindFirst("admin");
        return adminClaim?.Value == "true";
    }

    [HttpGet("/admin/users")]
    public async Task<IActionResult> ListUsers()
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var users = await authService.GetAllUsersAsync();
        var dtos = users.Select(u => new AdminUserListItemDto
        {
            Id = u.Id,
            Username = u.Username,
            IsAdmin = u.IsAdmin,
            IsActive = u.IsActive,
            LastSignInAt = u.LastSignInAt,
            CreatedAt = u.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("/admin/users")]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequestDto request)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var result = await authService.CreateUserAsAdminAsync(request.Username, request.Password, request.IsAdmin);
        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, new { code = result.ErrorCode, message = result.ErrorMessage });
        }

        var user = result.Data;
        var dto = new AdminUserListItemDto
        {
            Id = user.Id,
            Username = user.Username,
            IsAdmin = user.IsAdmin,
            IsActive = user.IsActive,
            LastSignInAt = user.LastSignInAt,
            CreatedAt = user.CreatedAt
        };

        return CreatedAtAction(nameof(ListUsers), dto);
    }

    [HttpPost("/admin/users/{userId}/disable")]
    public async Task<IActionResult> DisableUser(int userId)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var result = await authService.DisableUserAsync(userId);
        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, new { code = result.ErrorCode, message = result.ErrorMessage });
        }

        var user = result.Data;
        var dto = new AdminDisableUserResponseDto
        {
            UserId = user.Id,
            Username = user.Username,
            IsActive = user.IsActive
        };

        return Ok(dto);
    }
}
