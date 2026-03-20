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
    public DbSet<FileGrant> FileGrants { get; set; }
    public DbSet<SharepointLink> SharepointLinks { get; set; }

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

        modelBuilder.Entity<FileMetadata>()
            .HasOne(x => x.OwnerUser)
            .WithMany(x => x.OwnedFiles)
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FileGrant>()
            .HasOne(x => x.File)
            .WithMany(x => x.Grants)
            .HasForeignKey(x => x.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FileGrant>()
            .HasOne(x => x.GrantedUser)
            .WithMany(x => x.GrantedFiles)
            .HasForeignKey(x => x.GrantedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FileGrant>()
            .HasOne(x => x.GrantedByUser)
            .WithMany(x => x.IssuedFileGrants)
            .HasForeignKey(x => x.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FileGrant>()
            .HasIndex(x => new { x.FileId, x.GrantedUserId })
            .IsUnique();

        // SharepointLink configuration
        modelBuilder.Entity<SharepointLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => new { e.CreatedByUserId, e.RevokedAt, e.ExpiresAt });
            
            entity.HasOne(e => e.File)
                .WithMany()
                .HasForeignKey(e => e.FileId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);  // File deleted → links deleted

            entity.Property(e => e.LinkType).HasDefaultValue(LinkType.Download);
            entity.Property(e => e.UploadCount).HasDefaultValue(0);
            
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);  // Prevent user deletion if links exist
        });

        base.OnModelCreating(modelBuilder);
    }
}
