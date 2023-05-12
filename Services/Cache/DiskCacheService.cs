using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RosyCrow.Services.Cache;

public class DiskCacheService : ICacheService
{
    private const string PageCacheDirectory = "pages";
    private const string ResourceCacheDirectory = "resources";

    private readonly ILogger<DiskCacheService> _logger;

    public DiskCacheService(ILogger<DiskCacheService> logger)
    {
        _logger = logger;
    }

    public async Task<string> LoadString(Uri uri, string query)
    {
        try
        {
            var path = GetPathFromUri(uri, query);

            _logger.LogDebug("Cached page file path is {Path}", path);

            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(path))
            {
                _logger.LogInformation("Tried to load cached page for {URI} but the file does not exist", uri);
                return null;
            }

            await using var file = File.OpenRead(path);
            await using var brotli = new BrotliStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(brotli);

            _logger.LogInformation("Loading cached page for {URI}", uri);
            _logger.LogDebug("Cached page is {Size} bytes (compressed)", file.Length);

            return await reader.ReadToEndAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while loading cached page for {URI}", uri);
            return null;
        }
    }

    public async Task StoreString(Uri uri, string query, string contents)
    {
        try
        {
            var path = GetPathFromUri(uri, query);

            _logger.LogDebug("Cached page file path is {Path}", path);

            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var file = File.Create(path);
            await using var brotli = new BrotliStream(file, CompressionMode.Compress);
            await using var writer = new StreamWriter(brotli);

            _logger.LogInformation("Caching page for {URI}", uri);
            _logger.LogDebug("Page is {Length} characters in length", contents.Length);

            await writer.WriteAsync(contents);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while caching page for {URI}", uri);
        }
    }

    public async Task StoreResource(string bucket, string key, Stream contents)
    {
        try
        {
            var path = GetPathFromKey(bucket, key);

            _logger.LogDebug("Cached image file path is {Path}", path);

            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var file = File.Create(path);

            _logger.LogInformation("Caching an image of size {Size}", contents.Length);

            contents.Seek(0, SeekOrigin.Begin);
            await contents.CopyToAsync(file);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while caching image of size {Size}", contents.Length);
        }
    }

    public bool ResourceExists(string bucket, string key)
    {
        var path = GetPathFromKey(bucket, key);
        var exists = File.Exists(path);

        if (exists)
            _logger.LogDebug("Cached image exists at {Path}", path);
        else
            _logger.LogDebug("No cached image exists at {Path}", path);

        return exists;
    }

    public async Task LoadResource(string bucket, string key, Stream destination)
    {
        var path = GetPathFromKey(bucket, key);

        try
        {
            if (!File.Exists(path))
            {
                _logger.LogInformation("Tried to load a cached image from {Path} but the file does not exist", path);
                return;
            }

            await using var file = File.OpenRead(path);
            await file.CopyToAsync(destination);

            _logger.LogInformation("Loaded a cached image of size {Size} from {Path}", destination.Length, path);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while loading cached image from {Path}", path);
        }
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