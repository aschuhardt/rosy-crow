namespace RosyCrow.Services.Cache;

public interface ICacheService
{
    Task<bool> TryRead(Uri uri, Stream destination);
    Task Write(Uri uri, Stream contents);
}