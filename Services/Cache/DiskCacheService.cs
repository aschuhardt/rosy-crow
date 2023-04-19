using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace RosyCrow.Services.Cache;

public class DiskCacheService : ICacheService
{
    private const string PageCacheDirectory = "cached-pages";

    public DiskCacheService()
    {
        var path = Path.Combine(FileSystem.CacheDirectory, PageCacheDirectory);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public async Task<string> TryGetCached(Uri uri, string query)
    {
        var path = GetPathFromUri(uri, query);

        if (!File.Exists(path))
            return null;

        await using var file = File.OpenRead(path);
        await using var brotli = new BrotliStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(brotli);

        return await reader.ReadToEndAsync();
    }

    public async Task WriteCached(Uri uri, string query, string contents)
    {
        var path = GetPathFromUri(uri, query);

        await using var file = File.Create(path);
        await using var brotli = new BrotliStream(file, CompressionMode.Compress);
        await using var writer = new StreamWriter(brotli);

        await writer.WriteAsync(contents);
    }

    private static string GetPathFromUri(Uri uri, string query)
    {
        var identifier = new StringBuilder(uri.ToString().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query))
            identifier.Append(query);
        return Path.Combine(FileSystem.CacheDirectory, PageCacheDirectory,
            Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(uri.ToString().ToUpperInvariant()))));
    }
}