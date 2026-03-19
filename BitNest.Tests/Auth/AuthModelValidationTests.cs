using BitNest.Models;
using BitNest.Services;

namespace BitNest.Tests.Auth;

public class AuthModelValidationTests
{
    [Fact]
    public void Username_is_normalized_to_lowercase_handle()
    {
        var user = new User
        {
            Username = "Alice.User",
            NormalizedUsername = User.NormalizeUsername("Alice.User")
        };

        Assert.Equal("Alice.User", user.Username);
        Assert.Equal("alice.user", user.NormalizedUsername);
        Assert.Matches("^[a-z0-9._-]+$", user.NormalizedUsername);
    }

    [Fact]
    public void Password_must_be_at_least_8_characters()
    {
        var hasher = new PasswordHasher();

        Assert.Throws<ArgumentException>(() => hasher.Hash("short"));

        var hash = hasher.Hash("long-enough-password");
        Assert.NotEqual("long-enough-password", hash);
        Assert.True(hasher.Verify("long-enough-password", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Refresh_session_is_inactive_when_revoked_or_expired()
    {
        var now = DateTime.UtcNow;

        var revoked = new RefreshSession
        {
            TokenHash = "token-hash",
            ExpiresAt = now.AddMinutes(5),
            RevokedAt = now,
            RememberMe = true
        };

        var expired = new RefreshSession
        {
            TokenHash = "token-hash-2",
            ExpiresAt = now.AddMinutes(-1)
        };

        Assert.False(revoked.IsActive);
        Assert.False(expired.IsActive);
        Assert.True(revoked.RememberMe);
    }
}
