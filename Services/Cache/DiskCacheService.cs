using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace RosyCrow.Services.Cache;

public class DiskCacheService : ICacheService
{
    private const string PageCacheDirectory = "pages";
    private const string ResourceCacheDirectory = "resources";

    public async Task<string> LoadString(Uri uri, string query)
    {
        try
        {
            var path = GetPathFromUri(uri, query);

            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(path))
                return null;

            await using var file = File.OpenRead(path);
            await using var brotli = new BrotliStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(brotli);

            return await reader.ReadToEndAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task StoreString(Uri uri, string query, string contents)
    {
        var path = GetPathFromUri(uri, query);

        var directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var file = File.Create(path);
        await using var brotli = new BrotliStream(file, CompressionMode.Compress);
        await using var writer = new StreamWriter(brotli);

        await writer.WriteAsync(contents);
    }

    public async Task StoreResource(string bucket, string key, Stream contents)
    {
        var path = GetPathFromKey(bucket, key);

        var directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var file = File.Create(path);

        contents.Seek(0, SeekOrigin.Begin);
        await contents.CopyToAsync(file);
    }

    public bool ResourceExists(string bucket, string key)
    {
        return File.Exists(GetPathFromKey(bucket, key));
    }

    public async Task<string> LoadResource(string bucket, string key, Stream destination)
    {
        var path = GetPathFromKey(bucket, key);

        if (!File.Exists(path))
            return null;

        await using var file = File.OpenRead(path);
        await file.CopyToAsync(destination);

        return path;
    }

    private static string GetPathFromKey(string bucket, string key)
    {
        return Path.Combine(FileSystem.CacheDirectory, ResourceCacheDirectory,
            Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(bucket))),
            $"{Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(key)))}.data");
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