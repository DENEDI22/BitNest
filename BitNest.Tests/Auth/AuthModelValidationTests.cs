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

        Assert.Equal("alice.user", user.NormalizedUsername);
        Assert.Matches("^[a-z0-9._-]+$", user.NormalizedUsername);
    }

    [Fact]
    public void Password_must_be_at_least_8_characters()
    {
        var hasher = new PasswordHasher();

        Assert.Throws<ArgumentException>(() => hasher.Hash("short"));

        var hash = hasher.Hash("long-enough");
        Assert.NotNull(hash);
    }

    [Fact]
    public void Refresh_session_is_inactive_when_revoked_or_expired()
    {
        var now = DateTime.UtcNow;

        var revoked = new RefreshSession
        {
            ExpiresAt = now.AddMinutes(5),
            RevokedAt = now
        };

        var expired = new RefreshSession
        {
            ExpiresAt = now.AddMinutes(-1)
        };

        Assert.False(revoked.IsActive);
        Assert.False(expired.IsActive);
    }
}
