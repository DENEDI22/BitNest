using System.Security.Cryptography;
using Blake3;

namespace BitNest.Models;

public class ChunkMetadata
{
    public byte[] Hash { get; set; }
    public List<FileChunk> Files { get; set; } = [];
}