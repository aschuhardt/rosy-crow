using HtmlAgilityPack;
using Opal.Response;
using RosyCrow.Models;

namespace RosyCrow.Services.Document;

public interface IDocumentService
{
    HtmlDocument CreateEmptyDocument();
    HtmlDocument LoadFromBuffer(Stream buffer);
    Task LoadResources();
    Task<RenderedGemtextDocument> RenderGemtextAsHtml(GemtextResponse gemtext);
    Task<string> RenderInternalDocument(string name);
}