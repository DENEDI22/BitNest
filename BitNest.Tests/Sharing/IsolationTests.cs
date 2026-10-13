using BitNest.Controllers;
using BitNest.Data;
using BitNest.Sharing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Tests.Sharing;

public class IsolationTests
{
    private const string Key = "a-management-secret-with-at-least-32-characters";

    [Theory]
    [InlineData(8080, "/internal/links/1", Key, false)]
    [InlineData(8080, "/internal/links/1", "", false)]
    [InlineData(8081, "/internal/links/1", "", false)]
    [InlineData(8081, "/internal/links/1", "incorrect", false)]
    [InlineData(8081, "/internal/links/1", Key, true)]
    [InlineData(8080, "/api/share/token", "", true)]
    [InlineData(8081, "/api/share/token", Key, false)]
    [InlineData(8080, "/auth/login", "", false)]
    [InlineData(8080, "/Storage/1", "", false)]
    [InlineData(8080, "/api/sharepoint/links", Key, false)]
    public void Listener_and_secret_are_both_required(int port, string path, string key, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.LocalPort = port;
        context.Request.Path = path;
        context.Request.Host = new HostString("localhost", 8081);
        context.Request.Headers["X-Forwarded-Port"] = "8081";
        context.Request.Headers["X-Sharing-Key"] = key;
        Assert.Equal(expected, ManagementBoundary.IsAllowed(context, 8080, 8081, Key));
    }

    [Fact]
    public void Public_model_and_assembly_have_no_private_dependencies()
    {
        using var db = new SharingDbContext(new DbContextOptionsBuilder<SharingDbContext>()
            .UseNpgsql("Host=unused;Database=unused").Options);
        Assert.Equal(new[] { "IncomingUpload", "PublishedLink", "RevokedFile" },
            db.Model.GetEntityTypes().Select(x => x.ClrType.Name).Order().ToArray());
        Assert.DoesNotContain(typeof(SharingDbContext).Assembly.GetReferencedAssemblies(), x => x.Name == "BitNest");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), x => x.ClrType.Assembly == typeof(AppDbContext).Assembly);
    }

    [Fact]
    public void Private_assembly_no_longer_discovers_public_controllers()
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(new AssemblyPart(typeof(StorageController).Assembly));
        manager.FeatureProviders.Add(new ControllerFeatureProvider());
        var feature = new ControllerFeature();
        manager.PopulateFeature(feature);
        Assert.DoesNotContain(feature.Controllers, x => x.Name.Contains("PublicShare"));
        Assert.Contains(feature.Controllers, x => x.Name == nameof(SharepointController));
    }
}
