using System.ComponentModel.Design;
using Microsoft.EntityFrameworkCore;

namespace BitNest.Models;

public class FileMetadata 
{
    [HelpKeyword]
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Extention { get; set; } = null!;
    public long Size { get; set; }
    public string BlobPath { get; set; } = null!;
    public bool IsChunked { get; set; } = false;
    public bool IsUploaded { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public List<FileChunk> Chunks { get; set; } = [];
}