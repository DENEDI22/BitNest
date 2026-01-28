using BitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<FileMetadata> Files { get; set; }
    public DbSet<ChunkMetadata> Chunks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileMetadata>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<ChunkMetadata>()
            .HasKey(nameof(ChunkMetadata.Id));
        modelBuilder.Entity<ChunkMetadata>()
            .HasIndex(x => x.Checksum)
            .IsUnique();
        modelBuilder.Entity<FileMetadata>()
            .HasMany(x => x.Chunks)
            .WithMany(x => x.Files);
    }
}