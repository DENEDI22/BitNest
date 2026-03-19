namespace BitNest.DTOs.Storage;

public class AuthorizedFileMetadataDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; }
    public bool IsOwner { get; set; }
}
