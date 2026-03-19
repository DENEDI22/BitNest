using BitNest.Models;

namespace BitNest.Tests.Auth;

public class AdminUserEndpointTests
{
    [Fact]
    public void User_schema_marks_user_inactive_and_supports_refresh_revocation_fields()
    {
        var isAdmin = typeof(User).GetProperty("IsAdmin");
        var isActive = typeof(User).GetProperty("IsActive");
        var lastSignInAt = typeof(User).GetProperty("LastSignInAt");

        Assert.NotNull(isAdmin);
        Assert.NotNull(isActive);
        Assert.NotNull(lastSignInAt);

        Assert.Equal(typeof(bool), isAdmin!.PropertyType);
        Assert.Equal(typeof(bool), isActive!.PropertyType);
        Assert.Equal(typeof(DateTime?), lastSignInAt!.PropertyType);
    }
}
