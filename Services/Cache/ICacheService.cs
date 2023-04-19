namespace RosyCrow.Services.Cache;

public interface ICacheService
{
    Task<string> TryGetCached(Uri uri, string query);
    Task WriteCached(Uri uri, string query, string contents);
}