using BitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<FileMetadata> Files { get; set; }
    public DbSet<ChunkMetadata> Chunks { get; set; }
    public DbSet<FileChunk> FileChunks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileMetadata>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<ChunkMetadata>()
            .HasKey(x => x.Hash);
        modelBuilder.Entity<FileChunk>()
            .HasKey(x => new { x.Order, x.FileId });
        base.OnModelCreating(modelBuilder);
    }
}