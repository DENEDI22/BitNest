using BitNest.Data;
using BitNest.Sharing.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Services;

public sealed class SharingImporter(AppDbContext db, StorageService storage, SharingClient client)
{
    public async Task ImportAsync(IncomingResponse incoming, CancellationToken ct)
    {
        if (!await db.Files.AnyAsync(x => x.IncomingUploadId == incoming.Id, ct))
        {
            var owner = await db.UploadSlotOwners.SingleOrDefaultAsync(x => x.Id == incoming.OwnerSlotId, ct)
                ?? throw new InvalidDataException("Incoming upload belongs to an unknown slot.");
            if (!await db.Users.AnyAsync(x => x.Id == owner.OwnerUserId && x.IsActive, ct))
                throw new InvalidDataException("Incoming upload owner is disabled or missing.");
            await using var stream = await client.OpenIncomingAsync(incoming.Id, ct);
            var name = Path.GetFileName(incoming.FileName);
            var file = new IncomingFormFile(stream, incoming.FileSize, name);
            // Metadata and the unique receipt are a single DB insert. Retry after a lost ACK is harmless.
            await storage.UploadFile(file, name, Path.GetExtension(name), owner.OwnerUserId, ct,
                incoming.Id, incoming.ContentHash);
        }
        await client.AcknowledgeAsync(incoming.Id, ct);
    }

    private sealed class IncomingFormFile(Stream stream, long length, string fileName) : IFormFile
    {
        public string ContentType => "application/octet-stream";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length => length;
        public string Name => "formFile";
        public string FileName => fileName;
        public Stream OpenReadStream() => stream;
        public void CopyTo(Stream target) => stream.CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => stream.CopyToAsync(target, cancellationToken);
    }
}

public sealed class SharingImportWorker(IServiceScopeFactory scopes, ILogger<SharingImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<SharingClient>();
                var failed = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    var incoming = await client.GetAsync<List<IncomingResponse>>($"internal/incoming?skip={failed}", stoppingToken);
                    foreach (var upload in incoming)
                    {
                        try
                        {
                            using var importScope = scopes.CreateScope();
                            await importScope.ServiceProvider.GetRequiredService<SharingImporter>().ImportAsync(upload, stoppingToken);
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            failed++;
                            logger.LogError(ex, "Could not import sharing upload {UploadId}; it remains queued", upload.Id);
                        }
                    }
                    // Skip retained failures so a poisoned batch cannot starve subsequent uploads.
                    if (incoming.Count < 100) break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Sharing inbox unavailable; import will retry");
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
