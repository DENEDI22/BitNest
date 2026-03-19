using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
using Microsoft.IdentityModel.Tokens;

namespace BitNest.Tests.Auth;

public class AuthEndpointTests
{
    [Fact]
    public async Task Login_returns_401_with_stable_error_shape_for_invalid_credentials()
    {
        await using var host = await AuthTestHost.StartAsync();

        var response = await host.Client.PostAsJsonAsync("/auth/login", new
        {
            username = "missing-user",
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("code", out _));
        Assert.True(body.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task Refresh_rotates_and_invalidates_previous_token()
    {
        await using var host = await AuthTestHost.StartAsync();

        var signup = await host.SignupAsync("rotator", "password123");

        var refreshResponse = await host.Client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = signup.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refresh = await refreshResponse.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.NotNull(refresh);
        Assert.NotEqual(signup.RefreshToken, refresh.RefreshToken);

        var oldTokenResponse = await host.Client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = signup.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_session()
    {
        await using var host = await AuthTestHost.StartAsync();

        var signup = await host.SignupAsync("logout-user", "password123");

        var logoutResponse = await host.Client.PostAsJsonAsync("/auth/logout", new
        {
            refreshToken = signup.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var refreshResponse = await host.Client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = signup.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Me_requires_valid_access_token()
    {
        await using var host = await AuthTestHost.StartAsync();

        var unauthenticatedResponse = await host.Client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);

        var signup = await host.SignupAsync("reader", "password123");
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", signup.AccessToken);

        var authenticatedResponse = await host.Client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
    }

    private sealed class AuthTestHost : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly WebApplication app;

        private AuthTestHost(WebApplication app)
        {
            this.app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<AuthTestHost> StartAsync()
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
            var databaseName = $"auth-tests-{Guid.NewGuid()}";
            builder.Services.AddControllers().AddApplicationPart(typeof(StorageController).Assembly);
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
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
                            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("local-dev-signing-key-change-me-please-123456")),
                        ClockSkew = TimeSpan.Zero
                    };
                });
            builder.Services.AddAuthorization();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();

            return new AuthTestHost(app);
        }

        public async Task<AuthTokens> SignupAsync(string username, string password)
        {
            var response = await Client.PostAsJsonAsync("/auth/signup", new
            {
                username,
                password,
                rememberMe = false
            });

            response.EnsureSuccessStatusCode();
            return await ReadTokensAsync(response);
        }

        private static async Task<AuthTokens> ReadTokensAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadFromJsonAsync<AuthTokens>(JsonOptions);
            if (body is null || string.IsNullOrWhiteSpace(body.AccessToken) || string.IsNullOrWhiteSpace(body.RefreshToken))
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Invalid token response body: {content}");
            }

            return body;
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
            Client.Dispose();
        }
    }

    private sealed class AuthTokens
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
