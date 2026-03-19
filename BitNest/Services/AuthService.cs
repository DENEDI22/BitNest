using System.Text.RegularExpressions;
using BitNest.Data;
using BitNest.DTOs.Auth;
using BitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Services;

public class AuthService
{
    private static readonly Regex UsernameRegex = new("^[a-z0-9._-]+$", RegexOptions.Compiled);
    private static readonly TimeSpan StandardRefreshLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan RememberRefreshLifetime = TimeSpan.FromDays(30);

    private readonly AppDbContext db;
    private readonly PasswordHasher passwordHasher;
    private readonly JwtTokenService jwtTokenService;

    public AuthService(AppDbContext db, PasswordHasher passwordHasher, JwtTokenService jwtTokenService)
    {
        this.db = db;
        this.passwordHasher = passwordHasher;
        this.jwtTokenService = jwtTokenService;
    }

    public async Task<ServiceResult<AuthTokensDto>> Signup(AuthCredentialRequestDto request)
    {
        var usernameValidation = ValidateUsername(request.Username);
        if (usernameValidation is not null)
        {
            return ServiceResult<AuthTokensDto>.Fail(400, "invalid_username", usernameValidation);
        }

        var normalizedUsername = User.NormalizeUsername(request.Username);
        if (await db.Users.AnyAsync(x => x.NormalizedUsername == normalizedUsername))
        {
            return ServiceResult<AuthTokensDto>.Fail(409, "username_taken", "Username is already taken.");
        }

        string passwordHash;
        try
        {
            passwordHash = passwordHasher.Hash(request.Password);
        }
        catch (ArgumentException)
        {
            return ServiceResult<AuthTokensDto>.Fail(400, "invalid_password", "Password must be at least 8 characters long.");
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var tokens = await CreateTokensForUser(user, request.RememberMe);
        return ServiceResult<AuthTokensDto>.Ok(tokens);
    }

    public async Task<ServiceResult<AuthTokensDto>> Login(AuthCredentialRequestDto request)
    {
        var normalizedUsername = User.NormalizeUsername(request.Username);
        var user = await db.Users.FirstOrDefaultAsync(x => x.NormalizedUsername == normalizedUsername);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return ServiceResult<AuthTokensDto>.Fail(401, "invalid_credentials", "Invalid username or password.");
        }

        if (!user.IsActive)
        {
            return ServiceResult<AuthTokensDto>.Fail(401, "user_disabled", "This account is disabled.");
        }

        user.LastSignInAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var tokens = await CreateTokensForUser(user, request.RememberMe);
        return ServiceResult<AuthTokensDto>.Ok(tokens);
    }

    public async Task<ServiceResult<AuthTokensDto>> Refresh(RefreshRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult<AuthTokensDto>.Fail(401, "invalid_refresh", "Refresh token is invalid.");
        }

        var tokenHash = jwtTokenService.HashRefreshSecret(request.RefreshToken);
        var session = await db.RefreshSessions
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (session is null || !session.IsActive)
        {
            return ServiceResult<AuthTokensDto>.Fail(401, "invalid_refresh", "Refresh token is invalid.");
        }

        if (!session.User.IsActive)
        {
            return ServiceResult<AuthTokensDto>.Fail(401, "user_disabled", "This account is disabled.");
        }

        session.RevokedAt = DateTime.UtcNow;
        var tokens = await CreateTokensForUser(session.User, session.RememberMe);

        var newHash = jwtTokenService.HashRefreshSecret(tokens.RefreshToken);
        var replacement = await db.RefreshSessions
            .OrderByDescending(x => x.Id)
            .FirstAsync(x => x.UserId == session.UserId && x.TokenHash == newHash);
        session.ReplacedBySessionId = replacement.Id;

        await db.SaveChangesAsync();

        return ServiceResult<AuthTokensDto>.Ok(tokens);
    }

    public async Task<ServiceResult<object>> Logout(LogoutRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult<object>.Fail(401, "invalid_refresh", "Refresh token is invalid.");
        }

        var tokenHash = jwtTokenService.HashRefreshSecret(request.RefreshToken);
        var session = await db.RefreshSessions.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        if (session is null || !session.IsActive)
        {
            return ServiceResult<object>.Fail(401, "invalid_refresh", "Refresh token is invalid.");
        }

        session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ServiceResult<object>.Ok(new { success = true });
    }

    public async Task<ServiceResult<MeResponseDto>> GetMe(int userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ServiceResult<MeResponseDto>.Fail(401, "unauthorized", "User session is invalid.");
        }

        return ServiceResult<MeResponseDto>.Ok(new MeResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            IsAdmin = user.IsAdmin,
            IsActive = user.IsActive
        });
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await db.Users.OrderBy(x => x.CreatedAt).ToListAsync();
    }

    public async Task<ServiceResult<User>> CreateUserAsAdminAsync(string username, string password, bool isAdmin)
    {
        var usernameValidation = ValidateUsername(username);
        if (usernameValidation is not null)
        {
            return ServiceResult<User>.Fail(400, "invalid_username", usernameValidation);
        }

        var normalizedUsername = User.NormalizeUsername(username);
        if (await db.Users.AnyAsync(x => x.NormalizedUsername == normalizedUsername))
        {
            return ServiceResult<User>.Fail(409, "username_taken", "Username is already taken.");
        }

        string passwordHash;
        try
        {
            passwordHash = passwordHasher.Hash(password);
        }
        catch (ArgumentException)
        {
            return ServiceResult<User>.Fail(400, "invalid_password", "Password must be at least 8 characters long.");
        }

        var user = new User
        {
            Username = username.Trim(),
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash,
            IsAdmin = isAdmin,
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return ServiceResult<User>.Ok(user);
    }

    public async Task<ServiceResult<User>> DisableUserAsync(int userId)
    {
        var user = await db.Users.Include(x => x.RefreshSessions).FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ServiceResult<User>.Fail(404, "user_not_found", "User not found.");
        }

        user.IsActive = false;

        // Revoke all active refresh sessions for this user
        foreach (var session in user.RefreshSessions.Where(s => s.IsActive))
        {
            session.RevokedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return ServiceResult<User>.Ok(user);
    }

    private async Task<AuthTokensDto> CreateTokensForUser(User user, bool rememberMe)
    {
        var refreshToken = jwtTokenService.GenerateRefreshSecret();
        var refreshTokenHash = jwtTokenService.HashRefreshSecret(refreshToken);
        var accessToken = jwtTokenService.CreateAccessToken(user);

        db.RefreshSessions.Add(new RefreshSession
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.Add(rememberMe ? RememberRefreshLifetime : StandardRefreshLifetime),
            RememberMe = rememberMe
        });
        await db.SaveChangesAsync();

        return new AuthTokensDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    private static string? ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username is required.";
        }

        var normalized = User.NormalizeUsername(username);
        if (!UsernameRegex.IsMatch(normalized))
        {
            return "Username must contain only lowercase letters, numbers, '.', '_' or '-'.";
        }

        return null;
    }

    public sealed class ServiceResult<T>
    {
        public bool IsSuccess { get; private init; }
        public int StatusCode { get; private init; }
        public T? Value { get; private init; }
        public T? Data => Value;
        public AuthErrorDto? Error { get; private init; }
        public string? ErrorCode => Error?.Code;
        public string? ErrorMessage => Error?.Message;

        public static ServiceResult<T> Ok(T value) => new()
        {
            IsSuccess = true,
            StatusCode = 200,
            Value = value
        };

        public static ServiceResult<T> Fail(int statusCode, string code, string message) => new()
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Error = new AuthErrorDto
            {
                Code = code,
                Message = message
            }
        };
    }
}
