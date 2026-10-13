namespace BitNest.Sharing.Contracts;

public record PublishRequest(int FileId, int UserId, string FileName, string ContentHash, long FileSize, DateTime ExpiresAt);
public record SlotRequest(int OwnerSlotId, int UserId, string OwnerUsername, DateTime ExpiresAt, string? Description, int MaxFileCount);
public record LinkResponse(int Id, int? FileId, int UserId, string? FileName, string ShareUrl,
    DateTime CreatedAt, DateTime ExpiresAt, string LinkType, string? Description, int? MaxFileCount, int UploadCount);
public record IncomingResponse(Guid Id, int OwnerSlotId, string FileName, string ContentHash, long FileSize);
