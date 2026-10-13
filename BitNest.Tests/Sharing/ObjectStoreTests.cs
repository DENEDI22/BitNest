using BitNest.Sharing;
using Microsoft.AspNetCore.Http;

namespace BitNest.Tests.Sharing;

public sealed class ObjectStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "sharing-objects-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Objects_are_whole_file_blake3_and_failed_uploads_leave_no_temporary_file()
    {
        var objects = new ObjectStore(root);
        var stored = await objects.PutAsync(new MemoryStream([1, 2, 3]), 3, default);
        Assert.Equal(3, stored.Size);
        Assert.Equal(Blake3.Hasher.Hash(new byte[] { 1, 2, 3 }).ToString(), stored.Hash);
        Assert.Equal(new byte[] { 1, 2, 3 }, await System.IO.File.ReadAllBytesAsync(objects.GetPath(stored.Hash)));
        var error = await Assert.ThrowsAsync<BadHttpRequestException>(() => objects.PutAsync(new MemoryStream([1, 2, 3, 4]), 3, default));
        Assert.Equal(413, error.StatusCode);
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        Assert.Throws<InvalidDataException>(() => objects.GetPath("../../private"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
