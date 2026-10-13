using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BitNest.Controllers;
using BitNest.Models;
using BitNest.Services;
using BitNest.Sharing.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Tests.Sharing;

public class SharingWorkflowTests
{
    private sealed class UnavailableSharing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    private sealed class LostSlotResponse(HttpClient destination) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri!.PathAndQuery) { Content = request.Content };
            using var response = await destination.SendAsync(forwarded, ct);
            response.EnsureSuccessStatusCode();
            throw new HttpRequestException("Simulated response loss after public commit.");
        }
    }
    private static User User(string name) => new() { Username = name, NormalizedUsername = name, PasswordHash = "private-secret" };
    private static string Token(LinkResponse link) => new Uri(link.ShareUrl).Query["?token=".Length..];
    private static FormFile File(byte[] bytes) => new(new MemoryStream(bytes), 0, bytes.Length, "formFile", "hello.txt");
    private static MultipartFormDataContent Upload(byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "formFile", "incoming.txt");
        return form;
    }

    [PostgresFact]
    public async Task Lost_provisioning_response_retains_owner_and_retry_does_not_duplicate_slot()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.PrivateDb();
        var owner = User("owner");
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        // An unused private reservation makes private provisioning IDs differ from public link IDs.
        db.UploadSlotOwners.Add(new UploadSlotOwner { OwnerUserId = owner.Id });
        await db.SaveChangesAsync();
        using var http = new HttpClient(new LostSlotResponse(fixture.Internal)) { BaseAddress = new Uri("http://sharing/") };
        var service = new SharepointLinkService(db, fixture.Storage(db), new SharingClient(http));
        var expires = DateTime.UtcNow.AddHours(1);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateUploadSlotAsync(owner.Id, expires, null, 1));
        var mapping = await db.UploadSlotOwners.OrderByDescending(x => x.Id).FirstAsync();
        var listed = Assert.Single(await fixture.Client.GetAsync<List<LinkResponse>>($"internal/links/{owner.Id}"));
        Assert.NotEqual(mapping.Id, listed.Id);
        var replay = await fixture.Client.PostAsync<LinkResponse>("internal/slots",
            new SlotRequest(mapping.Id, owner.Id, owner.Username, expires, null, 1));
        Assert.Equal(listed.Id, replay.Id);
        Assert.Equal(listed.ShareUrl, replay.ShareUrl);
        using var form = Upload([1, 2, 3]);
        (await fixture.Public.PostAsync($"api/share/{Token(listed)}/upload", form)).EnsureSuccessStatusCode();
        var incoming = Assert.Single(await fixture.Client.GetAsync<List<IncomingResponse>>("internal/incoming"));
        Assert.Equal(mapping.Id, incoming.OwnerSlotId);
        await new SharingImporter(db, fixture.Storage(db), fixture.Client).ImportAsync(incoming, default);
        Assert.Equal(owner.Id, (await db.Files.SingleAsync()).OwnerUserId);
    }

    [PostgresFact]
    public async Task Expired_links_wrong_link_types_and_failed_uploads_are_rejected()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.PrivateDb();
        var owner = User("owner");
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var service = new SharepointLinkService(db, fixture.Storage(db), fixture.Client);
        var slot = await service.CreateUploadSlotAsync(owner.Id, DateTime.UtcNow.AddHours(1), null, 1);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Public.GetAsync($"api/share/{Token(slot)}/download")).StatusCode);
        using var oversized = Upload(new byte[1024 * 1024 + 1]);
        await Assert.ThrowsAsync<BadHttpRequestException>(() => fixture.Public.PostAsync($"api/share/{Token(slot)}/upload", oversized));
        await using var publicDb = fixture.PublicDb();
        Assert.Equal(0, (await publicDb.Links.SingleAsync()).UploadCount);
        Assert.Empty(await publicDb.Incoming.ToListAsync());
        await publicDb.Links.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Public.GetAsync($"api/share/{Token(slot)}")).StatusCode);
        using var form = Upload([1]);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Public.PostAsync($"api/share/{Token(slot)}/upload", form)).StatusCode);
        Assert.Empty(await service.GetActiveLinksForUserAsync(owner.Id));
    }

    [PostgresFact]
    public async Task Private_deletion_fails_closed_when_sharing_is_unavailable()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.PrivateDb();
        var owner = User("owner");
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        await fixture.Storage(db).UploadFile(File([1, 2, 3]), "hello.txt", ".txt", owner.Id);
        var file = await db.Files.SingleAsync();
        using var http = new HttpClient(new UnavailableSharing()) { BaseAddress = new Uri("http://unavailable/") };
        var storage = new StorageService(db, fixture.PrivateRoot, Microsoft.Extensions.Logging.Abstractions.NullLogger<StorageService>.Instance,
            new SharingClient(http));
        await Assert.ThrowsAsync<HttpRequestException>(() => storage.SafeDeleteFile(file.Id));
        Assert.False(file.IsDeleted);
        await db.Entry(file).ReloadAsync();
        Assert.False(file.IsDeleted);
    }

    [PostgresFact]
    public async Task Concurrent_importers_create_one_receipt_and_retry_safely()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.PrivateDb();
        var owner = User("owner");
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var service = new SharepointLinkService(db, fixture.Storage(db), fixture.Client);
        var slot = await service.CreateUploadSlotAsync(owner.Id, DateTime.UtcNow.AddHours(1), null, 1);
        using var form = Upload([1, 2, 3]);
        (await fixture.Public.PostAsync($"api/share/{Token(slot)}/upload", form)).EnsureSuccessStatusCode();
        var incoming = Assert.Single(await fixture.Client.GetAsync<List<IncomingResponse>>("internal/incoming"));
        async Task Import()
        {
            await using var context = fixture.PrivateDb();
            try
            {
                await new SharingImporter(context, fixture.Storage(context), fixture.Client).ImportAsync(incoming, default);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                // The losing worker retries using a fresh scope, as the production poller does.
                await using var retry = fixture.PrivateDb();
                await new SharingImporter(retry, fixture.Storage(retry), fixture.Client).ImportAsync(incoming, default);
            }
        }
        await Task.WhenAll(Import(), Import());
        Assert.Single(await db.Files.ToListAsync());
        Assert.Empty(await fixture.Client.GetAsync<List<IncomingResponse>>("internal/incoming"));
    }

    [PostgresFact]
    public async Task Publish_authorizes_privately_copies_bytes_and_delete_permanently_revokes()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.PrivateDb();
        var owner = User("owner");
        var other = User("other");
        db.Users.AddRange(owner, other);
        await db.SaveChangesAsync();
        var storage = fixture.Storage(db);
        var bytes = Encoding.UTF8.GetBytes("private original and independent public copy");
        await storage.UploadFile(File(bytes), "hello.txt", ".txt", owner.Id);
        var file = await db.Files.SingleAsync();
        var service = new SharepointLinkService(db, storage, fixture.Client);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateLinkAsync(file.Id, other.Id, DateTime.UtcNow.AddHours(1)));
        var link = await service.CreateLinkAsync(file.Id, owner.Id, DateTime.UtcNow.AddHours(1));
        Assert.StartsWith("https://public.example/share.html?token=", link.ShareUrl);
        Assert.Empty(await db.SharepointLinks.ToListAsync());
        Assert.Equal(bytes, await fixture.Public.GetByteArrayAsync($"/api/share/{Token(link)}/download"));
        await using (var publicDb = fixture.PublicDb())
        {
            var snapshot = await publicDb.Links.SingleAsync();
            Assert.DoesNotContain(Token(link), snapshot.TokenHash);
            Assert.Equal(file.ContentHash, snapshot.ContentHash);
        }
        var privatePath = Path.Combine(fixture.PrivateRoot, "files", file.ContentHash[..2], file.ContentHash);
        System.IO.File.Move(privatePath, privatePath + ".offline");
        Assert.Equal(bytes, await fixture.Public.GetByteArrayAsync($"/api/share/{Token(link)}/download"));
        System.IO.File.Move(privatePath + ".offline", privatePath);

        db.FileGrants.Add(new FileGrant { FileId = file.Id, GrantedUserId = other.Id, GrantedByUserId = owner.Id });
        await db.SaveChangesAsync();
        var grantedLink = await service.CreateLinkAsync(file.Id, other.Id, DateTime.UtcNow.AddHours(1));
        Assert.Single(await service.GetActiveLinksForUserAsync(other.Id));
        Assert.False(await service.RevokeLinkAsync(link.Id, other.Id));
        await storage.SafeDeleteFile(file.Id);
        Assert.True(file.IsDeleted);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Public.GetAsync($"api/share/{Token(link)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Public.GetAsync($"api/share/{Token(grantedLink)}/download")).StatusCode);
        using var stalePublish = await fixture.Internal.PostAsJsonAsync("internal/links", new PublishRequest(file.Id, owner.Id,
            file.Name, file.ContentHash, file.Size, DateTime.UtcNow.AddHours(1)));
        Assert.Equal(HttpStatusCode.Conflict, stalePublish.StatusCode);
    }

    [PostgresFact]
    public async Task Upload_is_durable_bounded_and_import_is_idempotent_even_after_deletion_and_lost_ack()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.PrivateDb();
        var owner = User("owner");
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var service = new SharepointLinkService(db, fixture.Storage(db), fixture.Client);
        var link = await service.CreateUploadSlotAsync(owner.Id, DateTime.UtcNow.AddHours(1), "send a file", 1);
        var bytes = Encoding.UTF8.GetBytes("durable incoming content");
        using var first = Upload(bytes);
        using var second = Upload(bytes);
        var results = await Task.WhenAll(fixture.Public.PostAsync($"api/share/{Token(link)}/upload", first),
            fixture.Public.PostAsync($"api/share/{Token(link)}/upload", second));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Empty(await db.Files.ToListAsync());
        var incoming = Assert.Single(await fixture.Client.GetAsync<List<IncomingResponse>>("internal/incoming"));
        await service.RevokeLinkAsync(link.Id, owner.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Public.GetAsync($"api/share/{Token(link)}")).StatusCode);
        // Accepted uploads survive slot revocation and private downtime.
        await using (var importDb = fixture.PrivateDb())
            await new SharingImporter(importDb, fixture.Storage(importDb), fixture.Client).ImportAsync(incoming, default);
        var imported = await db.Files.SingleAsync();
        Assert.Equal(owner.Id, imported.OwnerUserId);
        Assert.Equal(incoming.Id, imported.IncomingUploadId);
        Assert.Equal(incoming.ContentHash, imported.ContentHash);
        await using (var downloaded = await fixture.Storage(db).GetDownloadStreamAsync(imported.Id))
        {
            using var output = new MemoryStream();
            await downloaded.CopyToAsync(output);
            Assert.Equal(bytes, output.ToArray());
        }
        Assert.Empty(await fixture.Client.GetAsync<List<IncomingResponse>>("internal/incoming"));
        imported.IsDeleted = true;
        await db.SaveChangesAsync();
        await using (var publicDb = fixture.PublicDb())
            await publicDb.Incoming.ExecuteUpdateAsync(s => s.SetProperty(x => x.ImportedAt, (DateTime?)null));
        await using (var retryDb = fixture.PrivateDb())
            await new SharingImporter(retryDb, fixture.Storage(retryDb), fixture.Client).ImportAsync(incoming, default);
        Assert.Single(await db.Files.ToListAsync());
        Assert.Empty(await fixture.Client.GetAsync<List<IncomingResponse>>("internal/incoming"));
    }

    [PostgresFact]
    public async Task Import_rejects_unknown_owner_and_hash_mismatch_without_acknowledging()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.PrivateDb();
        var owner = User("owner");
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var service = new SharepointLinkService(db, fixture.Storage(db), fixture.Client);
        var slot = await service.CreateUploadSlotAsync(owner.Id, DateTime.UtcNow.AddHours(1), null, 1);
        using var form = Upload([1, 2, 3]);
        (await fixture.Public.PostAsync($"api/share/{Token(slot)}/upload", form)).EnsureSuccessStatusCode();
        var incoming = Assert.Single(await fixture.Client.GetAsync<List<IncomingResponse>>("internal/incoming"));
        var importer = new SharingImporter(db, fixture.Storage(db), fixture.Client);
        await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(incoming with { OwnerSlotId = -1 }, default));
        await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(incoming with { ContentHash = new string('0', 64) }, default));
        Assert.Empty(await db.Files.ToListAsync());
        Assert.Single(await fixture.Client.GetAsync<List<IncomingResponse>>("internal/incoming"));
        owner.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(incoming, default));
    }

    [PostgresFact]
    public async Task Http_boundary_hides_private_routes_and_management_even_with_key_on_public_port()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        fixture.Public.DefaultRequestHeaders.Add("X-Sharing-Key", SharingFixture.Key);
        foreach (var path in new[] { "internal/incoming", "internal/links/1", "auth/me", "admin/users", "Storage/1", "api/sharepoint/links" })
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Public.GetAsync(path)).StatusCode);
        fixture.Internal.DefaultRequestHeaders.Remove("X-Sharing-Key");
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Internal.GetAsync("internal/incoming")).StatusCode);
        fixture.Internal.DefaultRequestHeaders.Add("X-Sharing-Key", "bad-key");
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Internal.GetAsync("internal/incoming")).StatusCode);
    }

    [PostgresFact]
    public async Task Browser_management_contract_validates_requests_and_uses_configured_public_origin()
    {
        await using var fixture = new SharingFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.PrivateDb();
        var owner = User("owner");
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var controller = new SharepointController(new SharepointLinkService(db, fixture.Storage(db), fixture.Client))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString())], "test"))
            } }
        };
        Assert.IsType<BadRequestObjectResult>(await controller.CreateUploadSlot(new(DateTime.UtcNow.AddHours(1), null, 0)));
        Assert.IsType<BadRequestObjectResult>(await controller.CreateLink(new(1, DateTime.UtcNow.AddHours(-1))));
        Assert.IsType<ForbidResult>(await controller.CreateLink(new(999, DateTime.UtcNow.AddHours(1))));
        var result = Assert.IsType<CreatedAtActionResult>(await controller.CreateUploadSlot(new(DateTime.UtcNow.AddHours(1), "hello", 2)));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.StartsWith("https://public.example/upload.html?token=", json.GetProperty("url").GetString());
        Assert.IsType<OkObjectResult>(await controller.GetLinks());
        Assert.IsType<NoContentResult>(await controller.RevokeLink(json.GetProperty("id").GetInt32()));
        Assert.IsType<NotFoundResult>(await controller.RevokeLink(999));
    }
}
