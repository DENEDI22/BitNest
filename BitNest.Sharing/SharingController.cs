using System.Security.Cryptography;
using System.Text;
using BitNest.Sharing.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Sharing;

[ApiController]
public sealed class SharingController(SharingDbContext db, ObjectStore objects, SharingSettings settings) : ControllerBase
{
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private LinkResponse ToResponse(PublishedLink l) => new(l.Id, l.FileId, l.UserId, l.FileName, l.ShareUrl,
        l.CreatedAt, l.ExpiresAt, l.LinkType, l.Description, l.MaxFileCount, l.UploadCount);

    private async Task<IActionResult> Create(PublishedLink link)
    {
        if (link.ExpiresAt <= DateTime.UtcNow) return BadRequest();
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        link.TokenHash = HashToken(token);
        link.ShareUrl = $"{settings.PublicUrl}/{(link.LinkType == "upload" ? "upload" : "share")}.html?token={token}";
        db.Links.Add(link);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(ToResponse(link));
    }

    [HttpPut("internal/objects/{hash}")]
    public async Task<IActionResult> PutObject(string hash)
    {
        objects.GetPath(hash);
        var stored = await objects.PutAsync(Request.Body, settings.MaxUploadBytes, HttpContext.RequestAborted);
        return stored.Hash == hash ? NoContent() : BadRequest(new { message = "Object hash mismatch." });
    }

    [HttpPost("internal/links")]
    public async Task<IActionResult> Publish(PublishRequest request)
    {
        var path = objects.GetPath(request.ContentHash);
        if (!System.IO.File.Exists(path) || new FileInfo(path).Length != request.FileSize) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Serialize publication with permanent source-file revocation, including first publication.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({request.FileId})");
        if (await db.RevokedFiles.AnyAsync(x => x.FileId == request.FileId)) return Conflict();
        var result = await Create(new PublishedLink { FileId = request.FileId, UserId = request.UserId,
            FileName = request.FileName, ContentHash = request.ContentHash, FileSize = request.FileSize,
            ExpiresAt = request.ExpiresAt });
        await transaction.CommitAsync();
        return result;
    }

    [HttpPost("internal/slots")]
    public async Task<IActionResult> Slot(SlotRequest request)
    {
        if (request.MaxFileCount < 1 || request.OwnerSlotId < 1) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // A separate advisory-lock namespace keeps provisioning retries independent of file revocation.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1, {request.OwnerSlotId})");
        var existing = await db.Links.SingleOrDefaultAsync(x => x.OwnerSlotId == request.OwnerSlotId);
        if (existing != null) return existing.UserId == request.UserId ? Ok(ToResponse(existing)) : Conflict();
        var result = await Create(new PublishedLink { OwnerSlotId = request.OwnerSlotId, UserId = request.UserId, OwnerUsername = request.OwnerUsername,
            ExpiresAt = request.ExpiresAt, Description = request.Description, MaxFileCount = request.MaxFileCount,
            LinkType = "upload" });
        await transaction.CommitAsync();
        return result;
    }

    [HttpGet("internal/links/{userId:int}")]
    public async Task<IActionResult> List(int userId) => Ok((await db.Links.AsNoTracking()
        .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow)
        .OrderByDescending(x => x.CreatedAt).ToListAsync()).Select(ToResponse));

    [HttpDelete("internal/links/{id:int}/{userId:int}")]
    public async Task<IActionResult> Revoke(int id, int userId)
    {
        var count = await db.Links.Where(x => x.Id == id && x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow));
        return count == 0 ? NotFound() : NoContent();
    }

    [HttpDelete("internal/files/{fileId:int}")]
    public async Task<IActionResult> RevokeFile(int fileId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({fileId})");
        if (!await db.RevokedFiles.AnyAsync(x => x.FileId == fileId))
            db.RevokedFiles.Add(new RevokedFile { FileId = fileId });
        await db.Links.Where(x => x.FileId == fileId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    private Task<PublishedLink?> Resolve(string token) => db.Links.AsNoTracking()
        .FirstOrDefaultAsync(x => x.TokenHash == HashToken(token) && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow);

    [HttpGet("api/share/{token}")]
    public async Task<IActionResult> Metadata(string token)
    {
        var link = await Resolve(token);
        if (link == null) return NotFound();
        return link.LinkType == "upload"
            ? Ok(new { link.LinkType, link.OwnerUsername, link.CreatedAt, link.ExpiresAt, link.Description, link.MaxFileCount, link.UploadCount })
            : Ok(new { link.LinkType, link.FileName, link.FileSize, link.ExpiresAt });
    }

    [HttpGet("api/share/{token}/download")]
    public async Task<IActionResult> Download(string token)
    {
        var link = await Resolve(token);
        if (link == null || link.LinkType != "download") return NotFound();
        return PhysicalFile(objects.GetPath(link.ContentHash!), "application/octet-stream", link.FileName, enableRangeProcessing: true);
    }

    [HttpPost("api/share/{token}/upload")]
    public async Task<IActionResult> Upload(string token, [FromForm] IFormFile formFile)
    {
        var link = await Resolve(token);
        if (link == null || link.LinkType != "upload") return NotFound();
        if (link.UploadCount >= link.MaxFileCount) return Conflict(new { message = "This upload slot is full" });
        await using var stream = formFile.OpenReadStream();
        var stored = await objects.PutAsync(stream, settings.MaxUploadBytes, HttpContext.RequestAborted);
        // The count and durable inbox entry commit together. A crash cannot leak a reservation.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var reserved = await db.Links.Where(x => x.Id == link.Id && x.RevokedAt == null
                && x.ExpiresAt > DateTime.UtcNow && x.UploadCount < x.MaxFileCount)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UploadCount, x => x.UploadCount + 1));
        if (reserved == 0) return Conflict(new { message = "This upload slot is full or no longer valid" });
        var incoming = new IncomingUpload { LinkId = link.Id, OwnerSlotId = link.OwnerSlotId!.Value, FileName = Path.GetFileName(formFile.FileName),
            ContentHash = stored.Hash, FileSize = stored.Size };
        db.Incoming.Add(incoming);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { message = "File received", id = incoming.Id });
    }

    [HttpGet("internal/incoming")]
    public async Task<IActionResult> Incoming([FromQuery] int skip = 0) => Ok(await db.Incoming.AsNoTracking()
        .Where(x => x.ImportedAt == null).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(Math.Max(0, skip)).Take(100)
        .Select(x => new IncomingResponse(x.Id, x.OwnerSlotId, x.FileName, x.ContentHash, x.FileSize)).ToListAsync());

    [HttpGet("internal/incoming/{id:guid}/content")]
    public async Task<IActionResult> IncomingContent(Guid id)
    {
        var upload = await db.Incoming.FindAsync(id);
        return upload == null ? NotFound() : PhysicalFile(objects.GetPath(upload.ContentHash), "application/octet-stream");
    }

    [HttpPost("internal/incoming/{id:guid}/ack")]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        var count = await db.Incoming.Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ImportedAt, DateTime.UtcNow));
        return count == 0 ? NotFound() : NoContent();
    }
}
