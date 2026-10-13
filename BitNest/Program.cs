using BitNest.Data;
using BitNest.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

internal class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build())
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("Starting web host");
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSerilog();

            builder.Services.AddCors(x =>
                x.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
            builder.WebHost.ConfigureKestrel(options =>
            {
                var listenHost = builder.Configuration["Private:ListenHost"] ?? "127.0.0.1";
                options.Listen(System.Net.Dns.GetHostAddresses(listenHost).First(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork),
                    builder.Configuration.GetValue("Private:Port", 8080));
                options.Limits.MaxRequestBodySize = null;
                options.Limits.KeepAliveTimeout   = TimeSpan.FromHours(1);
            });

            builder.Services.AddMvc();
            builder.Services.Configure<FormOptions>(o =>
            {
                o.ValueLengthLimit         = int.MaxValue;
                o.MultipartBodyLengthLimit = long.MaxValue;
            });
            builder.Services.AddControllers();
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration.GetValue<string>("Auth:Issuer") ?? "bitnest",
                        ValidAudience = builder.Configuration.GetValue<string>("Auth:Audience") ?? "bitnest-client",
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                builder.Configuration.GetValue<string>("Auth:SigningKey")
                                ?? "local-dev-signing-key-change-me-please-123456"
                            )
                        ),
                        ClockSkew = TimeSpan.Zero
                    };
                    // Allow access token via ?token= query param for browser-navigated file downloads
                    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                    {
                        OnMessageReceived = ctx =>
                        {
                            var t = ctx.Request.Query["token"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(t)) ctx.Token = t;
                            return Task.CompletedTask;
                        }
                    };
                });
            builder.Services.AddAuthorization();
            builder.Services.AddOpenApi();

            builder.Services.AddScoped<StorageService>(x =>
                new StorageService(x.GetRequiredService<AppDbContext>(),
                    builder.Configuration.GetValue<string>("UploadsPath")
                    ?? throw new InvalidOperationException("UploadsPath must be configured."),
                    x.GetRequiredService<ILogger<StorageService>>(), x.GetRequiredService<SharingClient>()));
            builder.Services.AddScoped<PasswordHasher>();
            builder.Services.AddScoped<JwtTokenService>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<SharepointLinkService>();
            builder.Services.AddHttpClient<SharingClient>(http =>
            {
                http.BaseAddress = new Uri(builder.Configuration["Sharing:InternalUrl"]
                    ?? throw new InvalidOperationException("Sharing:InternalUrl is required."));
                var key = builder.Configuration["Sharing:ManagementKey"];
                if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
                    throw new InvalidOperationException("Sharing:ManagementKey must contain at least 32 characters.");
                http.DefaultRequestHeaders.Add("X-Sharing-Key", key);
                http.Timeout = TimeSpan.FromHours(1);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            builder.Services.AddScoped<SharingImporter>();
            builder.Services.AddHostedService<SharingImportWorker>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }

            // Seed an initial admin account from environment variables.
            {
                var adminUser = Environment.GetEnvironmentVariable("BITNEST_ADMIN_USER");
                var adminPass = Environment.GetEnvironmentVariable("BITNEST_ADMIN_PASS");
                if (!string.IsNullOrEmpty(adminUser) && !string.IsNullOrEmpty(adminPass))
                {
                    using var seedScope = app.Services.CreateScope();
                    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    if (!await seedDb.Users.AnyAsync())
                    {
                        var authService = seedScope.ServiceProvider.GetRequiredService<AuthService>();
                        var result = await authService.CreateUserAsAdminAsync(adminUser, adminPass, isAdmin: true);
                        if (result.IsSuccess)
                            Log.Information("Admin account '{Username}' seeded from environment variables.", adminUser);
                        else
                            Log.Warning("Failed to seed admin account: {Error}", result.ErrorMessage);
                    }
                }
            }

// Configure the HTTP request pipeline.

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseSerilogRequestLogging();

            app.Use(async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (HttpRequestException) when (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(new { message = "Sharing service is unavailable. Please retry." });
                }
            });

            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
        // EF tooling intentionally aborts startup after obtaining the host services.
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
