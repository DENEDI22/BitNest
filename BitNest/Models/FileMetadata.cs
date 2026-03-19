namespace BitNest.Models;

public class FileMetadata
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Extention { get; set; } = null!;
    public long Size { get; set; }
    public string BlobPath { get; set; } = null!;
    public bool IsChunked { get; set; } = false;
    public bool IsUploaded { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public int OwnerUserId { get; set; }

    public List<FileChunk> Chunks { get; set; } = [];
    public List<FileGrant> Grants { get; set; } = [];
    public User OwnerUser { get; set; } = null!;
}
