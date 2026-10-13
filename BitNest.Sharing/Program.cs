using System.Security.Cryptography;
using System.Text;
using BitNest.Sharing;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// Bearer tokens are in browser route paths; do not put them in routine access logs.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
var publicPort = builder.Configuration.GetValue("Sharing:PublicPort", 8080);
var internalPort = builder.Configuration.GetValue("Sharing:InternalPort", 8081);
if (publicPort == internalPort) throw new InvalidOperationException("Sharing listeners must be separate.");
var key = builder.Configuration["Sharing:ManagementKey"];
if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
    throw new InvalidOperationException("Sharing:ManagementKey must contain at least 32 characters.");
var publicUrl = builder.Configuration["Sharing:PublicUrl"];
if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
    throw new InvalidOperationException("Sharing:PublicUrl must be an absolute HTTP(S) URL.");
var maxBytes = builder.Configuration.GetValue<long>("Sharing:MaxUploadBytes", 1024L * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(publicPort);
    var internalHost = builder.Configuration["Sharing:InternalHost"] ?? "127.0.0.1";
    o.Listen(System.Net.Dns.GetHostAddresses(internalHost).First(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork), internalPort);
    o.Limits.MaxRequestBodySize = maxBytes + 1024 * 1024;
});
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxBytes);
builder.Services.AddDbContext<SharingDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Sharing")));
builder.Services.AddSingleton(new ObjectStore(builder.Configuration["Sharing:ObjectsPath"]
    ?? throw new InvalidOperationException("Sharing:ObjectsPath is required.")));
builder.Services.AddSingleton(new SharingSettings(publicUrl!.TrimEnd('/'), maxBytes));
builder.Services.AddControllers();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<SharingDbContext>().Database.MigrateAsync();
app.Use(async (context, next) =>
{
    if (!ManagementBoundary.IsAllowed(context, publicPort, internalPort, key))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next(context);
});
app.MapControllers();
await app.RunAsync();

namespace BitNest.Sharing
{
    public record SharingSettings(string PublicUrl, long MaxUploadBytes);

    public static class ManagementBoundary
    {
        public static bool IsAllowed(HttpContext context, int publicPort, int internalPort, string key)
        {
            var management = context.Request.Path.StartsWithSegments("/internal");
            if (!management)
                return context.Connection.LocalPort == publicPort && context.Request.Path.StartsWithSegments("/api/share");
            if (context.Connection.LocalPort != internalPort) return false;
            var supplied = context.Request.Headers["X-Sharing-Key"].ToString();
            return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
        }
    }
}
