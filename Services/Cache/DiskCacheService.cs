using System.Collections.Immutable;
using System.ComponentModel;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RosyCrow.Services.Cache;

[Localizable(false)]
public class DiskCacheService : ICacheService
{
    private readonly ILogger<DiskCacheService> _logger;
    private DateTimeOffset _lastPruneTime;

    private const int CacheBucketsRetainCount = 2; // the prior 3 hours' buckets
    private const double CachePruneIntervalMinutes = 30.0;

    public DiskCacheService(ILogger<DiskCacheService> logger)
    {
        _logger = logger;
        _lastPruneTime = DateTimeOffset.MinValue;

        var prunedCount = Prune();
        if (prunedCount > 0)
            _logger.LogInformation(@"{Count} cache buckets pruned upon initialization", prunedCount);
    }

    public async Task<bool> TryRead(Uri uri, Stream destination)
    {
        try
        {
            if (!TryFindCachedByUri(uri, out var path))
                return false;

            _logger.LogDebug(@"Cached resource found at {Path}", path);

            await using var file = File.OpenRead(path);
            await using var brotli = new BrotliStream(file, CompressionMode.Decompress);

            await brotli.CopyToAsync(destination);

            _logger.LogDebug(@"Read {Compressed} bytes from the cache ({Uncompressed} bytes uncompressed)", file.Length,
                destination.Length);

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while loading cached page for {URI}", uri);
            return false;
        }
    }

    public async Task Write(Uri uri, Stream contents)
    {
        try
        {
            var path = GetCurrentPathFromUri(uri);

            _logger.LogDebug(@"Cached page file path is {Path}", path);

            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var file = File.Create(path);
            await using var brotli = new BrotliStream(file, CompressionMode.Compress);

            await contents.CopyToAsync(brotli);

            _logger.LogDebug(@"Stored {Compressed} bytes in the cache ({Uncompressed} bytes uncompressed)", file.Length,
                contents.Length);

            if ((DateTimeOffset.UtcNow - _lastPruneTime).TotalMinutes >= CachePruneIntervalMinutes)
            {
                var prunedCount = Prune();
                if (prunedCount > 0)
                    _logger.LogInformation(@"{Count} cache buckets pruned", prunedCount);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while caching page for {URI}", uri);
        }
    }

    private int Prune()
    {
        _lastPruneTime = DateTimeOffset.UtcNow;

        var buckets = Directory.GetDirectories(GetRootPath());

        if (!buckets.Any())
            return 0;

        var toKeep = buckets
            .Where(p => int.TryParse(Path.GetDirectoryName(p), out _)).OrderDescending()
            .Take(CacheBucketsRetainCount).ToImmutableHashSet();

        var toPrune = buckets.Where(p => !toKeep.Contains(p)).ToList();

        foreach (var bucket in toPrune)
        {
            _logger.LogDebug(@"Pruning cache bucket at {Path}", bucket);
            Directory.Delete(bucket, true);
        }

        return toPrune.Count;
    }

    private static string GetRootPath()
    {
        return FileSystem.CacheDirectory;
    }

    private bool TryFindCachedByUri(Uri uri, out string path)
    {
        // happy path: resource exists in the current hourly bucket
        path = GetCurrentPathFromUri(uri);

        if (File.Exists(path))
            return true;

        var key = ComputeUriCachePath(uri);
        foreach (var bucket in Directory.GetDirectories(GetRootPath()))
        {
            path = Path.Combine(bucket, key);

            if (File.Exists(path))
            {
                // move this file into the current bucket so we find it more quickly next time
                var newPath = GetCurrentPathFromUri(uri);

                var directory = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.Move(path, newPath);

                _logger.LogDebug(@"Moved cache entry from {OldPath} to {NewPath}", path, newPath);

                path = newPath;
                return true;
            }
        }

        _logger.LogDebug(@"No cache entry was found for {URI}", uri);

        // didn't find it
        return false;
    }

    private static string GetCurrentPathFromUri(Uri uri)
    {
        return Path.Combine(GetRootPath(), GetHourlyDirectoryName(), ComputeUriCachePath(uri));
    }

    private static string ComputeCachePath(string bucket, string key)
    {
        return Path.Combine(
            Convert.ToHexString(MD5.HashData(Encoding.Default.GetBytes(bucket)))[..8],
            Convert.ToHexString(MD5.HashData(Encoding.Default.GetBytes(key)))[..12] + ".dat");
    }

    private static string ComputeUriCachePath(Uri uri)
    {
        return ComputeCachePath(uri.Host.ToUpperInvariant(), uri.PathAndQuery);
    }

    private static string GetHourlyDirectoryName()
    {
        var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (time - time % 3600).ToString();
    }
}