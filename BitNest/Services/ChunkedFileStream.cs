using System.Security.Cryptography.X509Certificates;
using BitNest.Models;

namespace BitNest.Services;

public class ChunkedFileStream : Stream
{
    private readonly IEnumerator<FileChunk> chunksEnumerator;
    private readonly string uploadsPath;
    private FileStream currentStream;

    public ChunkedFileStream(IEnumerable<FileChunk> chunks, string uploadsPath)
    {
        this.uploadsPath = uploadsPath;
        chunksEnumerator = chunks.GetEnumerator();
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (currentStream == null)
        {
            if (!chunksEnumerator.MoveNext()) return 0;
            currentStream = File.OpenRead(Path.Combine(uploadsPath,
                Convert.ToBase64String(chunksEnumerator.Current.Chunk.Hash).Replace('/', '-').Replace('+', '-')
                    .TrimEnd('=') + ".chunk"));
        }

        int bytesRead = currentStream.Read(buffer, 0, buffer.Length);
        if (bytesRead == 0)
        {
            currentStream.Dispose();
            currentStream = null;
            return Read(buffer, offset, count);
        }

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (currentStream == null)
        {
            if (!chunksEnumerator.MoveNext()) return 0;
            currentStream = File.OpenRead(Path.Combine(uploadsPath,
                Convert.ToBase64String(chunksEnumerator.Current.Chunk.Hash).Replace('/', '-').Replace('+', '-')
                    .TrimEnd('=') + ".chunk"));
        }

        int bytesRead = await currentStream.ReadAsync(buffer, 0, count, cancellationToken);
        if (bytesRead == 0)
        {
            currentStream.Dispose();
            currentStream = null;
            return await ReadAsync(buffer, offset, count, cancellationToken);
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position { get; set; }

    protected override void Dispose(bool disposing)
    {
        currentStream?.Dispose();
        chunksEnumerator.Dispose();
        base.Dispose(disposing);
    }
}