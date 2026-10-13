using BitNest.Data;
using BitNest.Services;
using BitNest.Sharing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace BitNest.Tests.Sharing;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BITNEST_TEST_POSTGRES")))
            Skip = "Set BITNEST_TEST_POSTGRES to a disposable PostgreSQL server connection string.";
    }
}

public sealed class SharingFixture : IAsyncDisposable
{
    public const string Key = "test-management-secret-at-least-32-characters";
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "bitnest-split-tests", Guid.NewGuid().ToString("N"));
    public string PublicRoot => Path.Combine(Root, "public");
    public string PrivateRoot => Path.Combine(Root, "private");
    private readonly string adminConnection = Environment.GetEnvironmentVariable("BITNEST_TEST_POSTGRES")!;
    private readonly string publicDatabase = "sharing_test_" + Guid.NewGuid().ToString("N");
    private readonly string privateDatabase = "private_test_" + Guid.NewGuid().ToString("N");
    private TestServer server = null!;
    public HttpClient Public { get; private set; } = null!;
    public HttpClient Internal { get; private set; } = null!;
    public SharingClient Client => new(Internal);

    private string Connection(string database) => new NpgsqlConnectionStringBuilder(adminConnection) { Database = database }.ConnectionString;
    public SharingDbContext PublicDb() => new(new DbContextOptionsBuilder<SharingDbContext>().UseNpgsql(Connection(publicDatabase)).Options);
    public AppDbContext PrivateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection(privateDatabase)).Options);
    public StorageService Storage(AppDbContext db) => new(db, PrivateRoot, NullLogger<StorageService>.Instance, Client);

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        foreach (var database in new[] { publicDatabase, privateDatabase })
            await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection).ExecuteNonQueryAsync();
        await using (var db = PublicDb()) await db.Database.MigrateAsync();
        await using (var db = PrivateDb()) await db.Database.MigrateAsync();
        server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            services.AddDbContext<SharingDbContext>(o => o.UseNpgsql(Connection(publicDatabase)));
            services.AddSingleton(new ObjectStore(PublicRoot));
            services.AddSingleton(new SharingSettings("https://public.example", 1024 * 1024));
            services.AddControllers().ConfigureApplicationPartManager(parts =>
            {
                parts.ApplicationParts.Clear();
                parts.ApplicationParts.Add(new AssemblyPart(typeof(SharingController).Assembly));
            });
        }).Configure(app =>
        {
            app.Use(async (context, next) =>
            {
                context.Connection.LocalPort = context.Request.Headers.ContainsKey("Test-Internal-Port") ? 8081 : 8080;
                if (!ManagementBoundary.IsAllowed(context, 8080, 8081, Key))
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                await next(context);
            });
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));
        Public = server.CreateClient();
        Internal = server.CreateClient();
        Internal.DefaultRequestHeaders.Add("Test-Internal-Port", "true");
        Internal.DefaultRequestHeaders.Add("X-Sharing-Key", Key);
    }

    public async ValueTask DisposeAsync()
    {
        Public?.Dispose();
        Internal?.Dispose();
        server?.Dispose();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        foreach (var database in new[] { publicDatabase, privateDatabase })
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
    }
}
