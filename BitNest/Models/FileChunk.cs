namespace BitNest.Models;

public class FileChunk
{
    public int Order { get; set; }
    public ChunkMetadata Chunk { get; set; }
    
    public int FileId { get; set; }
    public FileMetadata File { get; set; }
}