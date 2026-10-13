using Microsoft.EntityFrameworkCore;

namespace BitNest.Sharing;

// Only publication snapshots and an incoming queue live here. No private navigation properties.
public sealed class SharingDbContext(DbContextOptions<SharingDbContext> options) : DbContext(options)
{
    public DbSet<PublishedLink> Links => Set<PublishedLink>();
    public DbSet<IncomingUpload> Incoming => Set<IncomingUpload>();
    public DbSet<RevokedFile> RevokedFiles => Set<RevokedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PublishedLink>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<PublishedLink>().HasIndex(x => x.FileId);
        modelBuilder.Entity<PublishedLink>().HasIndex(x => x.OwnerSlotId).IsUnique();
        modelBuilder.Entity<IncomingUpload>().HasIndex(x => new { x.ImportedAt, x.CreatedAt });
        modelBuilder.Entity<RevokedFile>().HasKey(x => x.FileId);
        modelBuilder.Entity<RevokedFile>().Property(x => x.FileId).ValueGeneratedNever();
    }
}

public sealed class PublishedLink
{
    public int Id { get; set; }
    public int? FileId { get; set; }
    public int? OwnerSlotId { get; set; }
    public int UserId { get; set; }
    public string OwnerUsername { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string ShareUrl { get; set; } = "";
    public string? FileName { get; set; }
    public string? ContentHash { get; set; }
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string LinkType { get; set; } = "download";
    public string? Description { get; set; }
    public int? MaxFileCount { get; set; }
    public int UploadCount { get; set; }
}

public sealed class IncomingUpload
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int LinkId { get; set; }
    public int OwnerSlotId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ImportedAt { get; set; }
}

public sealed class RevokedFile
{
    public int FileId { get; set; }
}
