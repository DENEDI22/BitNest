using Blake3;

namespace BitNest.Sharing;

public sealed class ObjectStore
{
    private readonly string root;
    public ObjectStore(string root)
    {
        this.root = Path.GetFullPath(root);
        Directory.CreateDirectory(this.root);
    }

    public string GetPath(string hash)
    {
        if (hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidDataException("Invalid object hash.");
        return Path.Combine(root, hash[..2], hash);
    }

    public async Task<(string Hash, long Size)> PutAsync(Stream input, long limit, CancellationToken ct)
    {
        var temporary = Path.Combine(root, $"{Guid.NewGuid():N}.tmp");
        try
        {
            using var hasher = Hasher.New();
            long size = 0;
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             1024 * 1024, FileOptions.Asynchronous))
            {
                var buffer = new byte[1024 * 1024];
                int count;
                while ((count = await input.ReadAsync(buffer, ct)) > 0)
                {
                    size += count;
                    if (size > limit) throw new BadHttpRequestException("File is too large.", 413);
                    hasher.Update(buffer.AsSpan(0, count));
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                }
                await output.FlushAsync(ct);
                output.Flush(flushToDisk: true);
            }
            var hash = hasher.Finalize().ToString();
            var path = GetPath(hash);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Atomic replacement also repairs a damaged existing copy with verified bytes.
            File.Move(temporary, path, overwrite: true);
            return (hash, size);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
