namespace RosyCrow.Services.Cache;

public interface ICacheService
{
    Task<bool> TryRead(Uri uri, Stream destination, bool isImage);
    Task Write(Uri uri, Stream contents, bool isImage);
}