using System.Security.Cryptography;

namespace BitNest.Models;

public class ChunkMetadata
{
    public Guid Id { get; set; }
    public string Checksum { get; set; }
    public List<FileMetadata> Files { get; set; } = [];
}