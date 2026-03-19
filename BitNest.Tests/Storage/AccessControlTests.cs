using BitNest.Data;
using BitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Tests.Storage;

public class AccessControlTests
{
    [Fact]
    public async Task Access_control_seed_supports_file_setup_for_OWNER_and_GRANTED_users()
    {
        await using var db = CreateDbContext();

        var ownerProperty = typeof(FileMetadata).GetProperty("OwnerUserId");
        var fileGrantType = typeof(AppDbContext).Assembly.GetType("BitNest.Models.FileGrant");

        Assert.NotNull(ownerProperty);
        Assert.NotNull(fileGrantType);

        var owner = new User
        {
            Username = "owner",
            NormalizedUsername = User.NormalizeUsername("owner"),
            PasswordHash = "hash"
        };
        var granted = new User
        {
            Username = "granted",
            NormalizedUsername = User.NormalizeUsername("granted"),
            PasswordHash = "hash"
        };

        db.Users.AddRange(owner, granted);
        await db.SaveChangesAsync();

        var file = new FileMetadata
        {
            Name = "spec",
            Extention = "txt",
            Size = 1,
            BlobPath = "blob/spec.txt",
            IsUploaded = true
        };

        ownerProperty!.SetValue(file, owner.Id);
        db.Files.Add(file);
        await db.SaveChangesAsync();

        var fileIdProperty = fileGrantType!.GetProperty("FileId");
        var grantedUserIdProperty = fileGrantType.GetProperty("GrantedUserId");
        var grantedByUserIdProperty = fileGrantType.GetProperty("GrantedByUserId");

        Assert.NotNull(fileIdProperty);
        Assert.NotNull(grantedUserIdProperty);
        Assert.NotNull(grantedByUserIdProperty);

        var grant = Activator.CreateInstance(fileGrantType)!;
        fileIdProperty!.SetValue(grant, file.Id);
        grantedUserIdProperty!.SetValue(grant, granted.Id);
        grantedByUserIdProperty!.SetValue(grant, owner.Id);

        db.Add(grant);
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Grant_schema_rejects_duplicate_file_and_granted_user_via_unique_constraint()
    {
        using var db = CreateDbContext();

        var fileGrantEntityType = db.Model.GetEntityTypes()
            .SingleOrDefault(x => x.ClrType.FullName == "BitNest.Models.FileGrant");

        Assert.NotNull(fileGrantEntityType);

        var hasUniqueFileGrantIndex = fileGrantEntityType!.GetIndexes()
            .Any(index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { "FileId", "GrantedUserId" }));

        Assert.True(
            hasUniqueFileGrantIndex,
            "Expected a unique index on (FileId, GrantedUserId) for file grants.");
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"access-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
