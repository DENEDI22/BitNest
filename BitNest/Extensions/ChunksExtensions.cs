using BitNest.Models;

namespace BitNest.Extensions;

public static class ChunksExtensions
{
    public static string GetChunkPath(this FileChunk chunk, string uploadsPath) => Path.Combine(uploadsPath,
        Convert.ToBase64String(chunk.Chunk.Hash).Replace('/', '-').Replace('+', '-')
            .TrimEnd('=') + ".chunk");

    public static string GetChunkName(this ChunkMetadata chunk) =>
        Convert.ToBase64String(chunk.Hash).Replace('/', '-').Replace('+', '-').TrimEnd('=');
}