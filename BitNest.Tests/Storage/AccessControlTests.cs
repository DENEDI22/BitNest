using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BitNest.Controllers;
using BitNest.Data;
using BitNest.DTOs;
using BitNest.Models;
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

namespace BitNest.Tests.Storage;

public class AccessControlTests
{
    [Fact]
    public async Task File_list_includes_owner_and_granted_files_only()
    {
        await using var host = await StorageHost.StartAsync();

        var owner = await host.SignupAsync("owner-user", "password123");
        var grantedUser = await host.SignupAsync("granted-user", "password123");
        var unauthorizedUser = await host.SignupAsync("unauthorized-user", "password123");

        // Owner uploads a file
        var fileId = await host.UploadFileAsync(owner, "test-file.txt");

        // Owner grants access to grantedUser
        await host.GrantAccessAsync(owner, fileId, grantedUser.UserId);

        // Unauthorized user lists files
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", unauthorizedUser.AccessToken);

        var unauthorizedListResponse = await host.Client.GetAsync("/Storage/1");
        Assert.Equal(HttpStatusCode.OK, unauthorizedListResponse.StatusCode);
        var unauthorizedFiles = await unauthorizedListResponse.Content.ReadFromJsonAsync<List<FileMetadataDTO>>();
        Assert.Empty(unauthorizedFiles);

        // Granted user lists files
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", grantedUser.AccessToken);

        var grantedListResponse = await host.Client.GetAsync("/Storage/1");
        Assert.Equal(HttpStatusCode.OK, grantedListResponse.StatusCode);
        var grantedFiles = await grantedListResponse.Content.ReadFromJsonAsync<List<FileMetadataDTO>>();
        Assert.Single(grantedFiles);
        Assert.Equal(fileId, grantedFiles[0].Id);

        // Owner lists their own file
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", owner.AccessToken);

        var ownerListResponse = await host.Client.GetAsync("/Storage/1");
        Assert.Equal(HttpStatusCode.OK, ownerListResponse.StatusCode);
        var ownerFiles = await ownerListResponse.Content.ReadFromJsonAsync<List<FileMetadataDTO>>();
        Assert.Single(ownerFiles);
        Assert.Equal(fileId, ownerFiles[0].Id);
    }

    [Fact]
    public async Task Unauthorized_file_download_returns_404()
    {
        await using var host = await StorageHost.StartAsync();

        var owner = await host.SignupAsync("owner-user", "password123");
        var unauthorizedUser = await host.SignupAsync("unauthorized-user", "password123");

        var fileId = await host.UploadFileAsync(owner, "test-file.txt");

        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", unauthorizedUser.AccessToken);

        var response = await host.Client.GetAsync($"/Storage/download/{fileId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthorized_file_delete_returns_404()
    {
        await using var host = await StorageHost.StartAsync();

        var owner = await host.SignupAsync("owner-user", "password123");
        var unauthorizedUser = await host.SignupAsync("unauthorized-user", "password123");

        var fileId = await host.UploadFileAsync(owner, "test-file.txt");

        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", unauthorizedUser.AccessToken);

        var response = await host.Client.DeleteAsync($"/Storage/{fileId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class StorageHost : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly string uploadsRoot;
        private readonly AppDbContext dbContext;

        private StorageHost(WebApplication app, string uploadsRoot, AppDbContext dbContext)
        {
            this.app = app;
            this.uploadsRoot = uploadsRoot;
            this.dbContext = dbContext;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<StorageHost> StartAsync()
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

            var databaseName = $"storage-tests-{Guid.NewGuid()}";
            var uploadsRoot = Path.Combine(Path.GetTempPath(), "bitnest-storage-tests", Guid.NewGuid().ToString("N"));

            AppDbContext? dbContextInstance = null;

            builder.Services.AddControllers().AddApplicationPart(typeof(StorageController).Assembly);
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
            });
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

            // Get the db context instance
            await using var scope = app.Services.CreateAsyncScope();
            dbContextInstance = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return new StorageHost(app, uploadsRoot, dbContextInstance!);
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

        public async Task<int> UploadFileAsync((int UserId, string AccessToken, string RefreshToken) user, string fileName)
        {
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", user.AccessToken);

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "test content");

            try
            {
                using var content = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(tempFile);
                content.Add(new StreamContent(fileStream), "formFile", fileName);

                var response = await Client.PostAsync("/Storage", content);
                response.EnsureSuccessStatusCode();

                // Retrieve the uploaded file ID from the database
                var fileId = await WithDbAsync(async db =>
                {
                    var file = await db.Files.FirstAsync(x => x.Name == fileName);
                    return file.Id;
                });

                return fileId;
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        public async Task GrantAccessAsync(
            (int UserId, string AccessToken, string RefreshToken) owner,
            int fileId,
            int grantedUserId)
        {
            await WithDbAsync(async db =>
            {
                var grant = new FileGrant
                {
                    FileId = fileId,
                    GrantedUserId = grantedUserId,
                    GrantedByUserId = owner.UserId
                };
                db.FileGrants.Add(grant);
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
