using BitNest.Data;
using BitNest.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors(x =>
            x.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = null;
            options.Limits.KeepAliveTimeout = TimeSpan.FromHours(1);
        });
// Add services to the container.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Services.AddMvc();
        builder.Services.Configure<FormOptions>(o =>
        {
            o.ValueLengthLimit = int.MaxValue;
            o.MultipartBodyLengthLimit = long.MaxValue;
        });
        builder.Services.AddControllers();
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.AddScoped<StorageService>(x =>
            new StorageService(x.GetRequiredService<AppDbContext>(),
                builder.Configuration.GetValue<string>("UploadsPath")));

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
        
        app.UseCors("AllowAll");
        
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}