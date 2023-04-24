namespace RosyCrow.Services.Cache;

public interface ICacheService
{
    Task<string> LoadString(Uri uri, string query);
    Task StoreString(Uri uri, string query, string contents);
    Task StoreResource(string bucket, string key, Stream contents);
    bool ResourceExists(string bucket, string key);
    Task<string> LoadResource(string bucket, string key, Stream destination);
    string GetResourcePath(string bucket, string key);
}