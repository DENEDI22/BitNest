using BitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<FileMetadata> Files { get; set; }
    public DbSet<ChunkMetadata> Chunks { get; set; }
    public DbSet<FileChunk> FileChunks { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshSession> RefreshSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileMetadata>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<ChunkMetadata>()
            .HasKey(x => x.Hash);
        modelBuilder.Entity<FileChunk>()
            .HasKey(x => new { x.Order, x.FileId });

        modelBuilder.Entity<User>()
            .HasIndex(x => x.NormalizedUsername)
            .IsUnique();

        modelBuilder.Entity<RefreshSession>()
            .HasOne(x => x.User)
            .WithMany(x => x.RefreshSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshSession>()
            .HasIndex(x => new { x.UserId, x.ExpiresAt });

        modelBuilder.Entity<RefreshSession>()
            .HasIndex(x => x.TokenHash)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
