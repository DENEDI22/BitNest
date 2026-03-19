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
    public static void Main(string[] args)
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
                options.Limits.MaxRequestBodySize = null;
                options.Limits.KeepAliveTimeout   = TimeSpan.FromHours(1);
            });

            builder.Services.AddMvc();
            builder.Services.Configure<FormOptions>(o =>
            {
                o.ValueLengthLimit         = int.MaxValue;
                o.MultipartBodyLengthLimit = long.MaxValue;
            });
            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
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
                });
            builder.Services.AddAuthorization();
            builder.Services.AddOpenApi();

            builder.Services.AddScoped<StorageService>(x =>
                new StorageService(x.GetRequiredService<AppDbContext>(),
                    builder.Configuration.GetValue<string>("UploadsPath"),
                    x.GetRequiredService<ILogger<StorageService>>()));
            builder.Services.AddScoped<PasswordHasher>();
            builder.Services.AddScoped<JwtTokenService>();
            builder.Services.AddScoped<AuthService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }


// Configure the HTTP request pipeline.

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseSerilogRequestLogging();

            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapReverseProxy();
            
            app.MapControllers();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
