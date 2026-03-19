using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BitNest.Controllers;
using BitNest.Data;
using BitNest.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BitNest.Tests.Auth;

public class AdminUserEndpointTests
{
    [Fact]
    public async Task Non_admin_calls_to_admin_endpoints_return_403()
    {
        await using var host = await AdminHost.StartAsync();

        var member = await host.SignupAsync("member-user", "password123");
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", member.AccessToken);

        var response = await host.Client.GetAsync("/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_create_disable_and_invalidate_target_refresh_session()
    {
        await using var host = await AdminHost.StartAsync();

        var admin = await host.SignupAsync("admin-user", "password123");
        var target = await host.SignupAsync("target-user", "password123");
        await host.SetAdminAsync("admin-user");

        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var listResponse = await host.Client.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var createResponse = await host.Client.PostAsJsonAsync("/admin/users", new
        {
            username = "created-by-admin",
            password = "password123"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var disableResponse = await host.Client.PostAsync($"/admin/users/{target.UserId}/disable", content: null);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        host.Client.DefaultRequestHeaders.Authorization = null;

        var loginAfterDisable = await host.Client.PostAsJsonAsync("/auth/login", new
        {
            username = "target-user",
            password = "password123"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfterDisable.StatusCode);

        var refreshAfterDisable = await host.Client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = target.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterDisable.StatusCode);
    }

    private sealed class AdminHost : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly string uploadsRoot;

        private AdminHost(WebApplication app, string uploadsRoot)
        {
            this.app = app;
            this.uploadsRoot = uploadsRoot;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<AdminHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Issuer"] = "bitnest",
                ["Auth:Audience"] = "bitnest-client",
                ["Auth:AccessTokenMinutes"] = "15",
                ["Auth:SigningKey"] = "local-dev-signing-key-change-me-please-123456"
            });

            builder.WebHost.UseTestServer();

            var databaseName = $"admin-tests-{Guid.NewGuid()}";
            var uploadsRoot = Path.Combine(Path.GetTempPath(), "bitnest-admin-tests", Guid.NewGuid().ToString("N"));

            builder.Services.AddControllers().AddApplicationPart(typeof(StorageController).Assembly);
            builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
            builder.Services.AddScoped<StorageService>(x =>
                new StorageService(
                    x.GetRequiredService<AppDbContext>(),
                    uploadsRoot,
                    x.GetRequiredService<ILogger<StorageService>>()));
            builder.Services.AddScoped<PasswordHasher>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<JwtTokenService>();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "bitnest",
                        ValidAudience = "bitnest-client",
                        IssuerSigningKey =
                            new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("local-dev-signing-key-change-me-please-123456")),
                        ClockSkew = TimeSpan.Zero
                    };
                });
            builder.Services.AddAuthorization();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();

            return new AdminHost(app, uploadsRoot);
        }

        public async Task<(int UserId, string AccessToken, string RefreshToken)> SignupAsync(string username, string password)
        {
            var response = await Client.PostAsJsonAsync("/auth/signup", new
            {
                username,
                password,
                rememberMe = true
            });

            response.EnsureSuccessStatusCode();
            var tokens = await response.Content.ReadFromJsonAsync<AuthTokens>();
            Assert.NotNull(tokens);

            var userId = await WithDbAsync(async db =>
            {
                var user = await db.Users.FirstAsync(x => x.NormalizedUsername == username);
                return user.Id;
            });

            return (userId, tokens!.AccessToken, tokens.RefreshToken);
        }

        public async Task SetAdminAsync(string username)
        {
            await WithDbAsync(async db =>
            {
                var normalized = username.Trim().ToLowerInvariant();
                var user = await db.Users.FirstAsync(x => x.NormalizedUsername == normalized);
                user.IsAdmin = true;
                await db.SaveChangesAsync();
                return true;
            });
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
            Client.Dispose();
            if (Directory.Exists(uploadsRoot))
            {
                Directory.Delete(uploadsRoot, true);
            }
        }

        private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await action(db);
        }

        private sealed class AuthTokens
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
        }
    }
}
