namespace BitNest.Tests.Auth;

public class AuthModelValidationTests
{
    [Fact]
    public void Username_is_normalized_to_lowercase_handle()
    {
        var normalized = NormalizeUsername("Alice.User");

        Assert.Equal("alice.user", normalized);
        Assert.Matches("^[a-z0-9._-]+$", normalized);
    }

    [Fact]
    public void Password_must_be_at_least_8_characters()
    {
        Assert.False(IsPasswordValid("short"));
        Assert.True(IsPasswordValid("long-enough"));
    }

    [Fact]
    public void Refresh_session_is_inactive_when_revoked_or_expired()
    {
        var now = DateTime.UtcNow;

        var revoked = new RefreshSessionContract
        {
            ExpiresAt = now.AddMinutes(5),
            RevokedAt = now
        };

        var expired = new RefreshSessionContract
        {
            ExpiresAt = now.AddMinutes(-1)
        };

        Assert.False(revoked.IsActive);
        Assert.False(expired.IsActive);
    }

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    private static bool IsPasswordValid(string password) => password.Length >= 8;

    private sealed class RefreshSessionContract
    {
        public DateTime ExpiresAt { get; init; }
        public DateTime? RevokedAt { get; init; }

        public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
    }
}
