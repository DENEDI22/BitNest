using System.Net;
using BitNest.Data;
using BitNest.Models;
using BitNest.Sharing.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Services;

public sealed class SharingClient(HttpClient http)
{
    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<T>(path, ct) ?? throw new InvalidDataException("Empty sharing response.");

    public async Task<T> PostAsync<T>(string path, object body)
    {
        using var response = await http.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidDataException("Empty sharing response.");
    }

    public async Task PublishObjectAsync(string hash, Stream stream)
    {
        using var content = new StreamContent(stream);
        using var response = await http.PutAsync($"internal/objects/{hash}", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> DeleteAsync(string path)
    {
        using var response = await http.DeleteAsync(path);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    public Task<Stream> OpenIncomingAsync(Guid id, CancellationToken ct) =>
        http.GetStreamAsync($"internal/incoming/{id}/content", ct);

    public async Task AcknowledgeAsync(Guid id, CancellationToken ct)
    {
        using var response = await http.PostAsync($"internal/incoming/{id}/ack", null, ct);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class SharepointLinkService(AppDbContext context, StorageService storage, SharingClient client)
{
    public async Task<LinkResponse> CreateLinkAsync(int fileId, int userId, DateTime expiresAt)
    {
        if (!await storage.CanAccessFileAsync(fileId, userId)) throw new UnauthorizedAccessException();
        var file = await storage.GetMetadataByIdAsync(fileId);
        await using var stream = await storage.GetDownloadStreamAsync(fileId);
        await client.PublishObjectAsync(file.ContentHash, stream);
        var result = await client.PostAsync<LinkResponse>("internal/links",
            new PublishRequest(fileId, userId, file.Name, file.ContentHash, file.Size, expiresAt));
        return result;
    }

    public async Task<LinkResponse> CreateUploadSlotAsync(
        int userId, DateTime expiresAt, string? description, int maxFileCount)
    {
        var user = await context.Users.SingleAsync(x => x.Id == userId && x.IsActive);
        // Commit ownership before publishing. Even a lost public response leaves a valid import destination.
        var owner = new UploadSlotOwner { OwnerUserId = userId };
        context.UploadSlotOwners.Add(owner);
        await context.SaveChangesAsync();
        return await client.PostAsync<LinkResponse>("internal/slots",
            new SlotRequest(owner.Id, userId, user.Username, expiresAt, description, maxFileCount));
    }

    public Task<List<LinkResponse>> GetActiveLinksForUserAsync(int userId) =>
        client.GetAsync<List<LinkResponse>>($"internal/links/{userId}");

    public Task<bool> RevokeLinkAsync(int linkId, int userId) => client.DeleteAsync($"internal/links/{linkId}/{userId}");
}
