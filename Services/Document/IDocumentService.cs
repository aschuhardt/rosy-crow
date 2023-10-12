using HtmlAgilityPack;

namespace RosyCrow.Services.Document;

public interface IDocumentService
{
    HtmlDocument CreateEmptyDocument();
    HtmlDocument LoadFromBuffer(Stream buffer);
    Task LoadResources();
}