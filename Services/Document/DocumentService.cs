using HtmlAgilityPack;
using RosyCrow.Interfaces;

namespace RosyCrow.Services.Document;

internal class DocumentService : IDocumentService
{
    private readonly ISettingsDatabase _settingsDatabase;
    private HtmlNode _templateNode;

    public DocumentService(ISettingsDatabase settingsDatabase)
    {
        _settingsDatabase = settingsDatabase;
    }

    public HtmlDocument CreateEmptyDocument()
    {
        var document = new HtmlDocument();
        document.DocumentNode.CopyFrom(_templateNode, true);
        InjectStylesheet(document);
        return document;
    }

    private void InjectStylesheet(HtmlDocument document)
    {
        document.DocumentNode.ChildNodes.FindFirst("head")
            .AppendChild(HtmlNode.CreateNode(
                $"<link rel=\"stylesheet\" class=\"injected-stylesheet\" href=\"Themes/{_settingsDatabase.Theme}.css\" media=\"screen\" />"));
    }

    public HtmlDocument LoadFromBuffer(Stream buffer)
    {
        var document = new HtmlDocument();
        document.Load(buffer);

        // remove the old injected stylesheet so that the new one can be used
        document.DocumentNode.Descendants("link").FirstOrDefault(n => n.HasClass("injected-stylesheet"))?.Remove();

        InjectStylesheet(document);
        return document;
    }

    public async Task LoadResources()
    {
        await using var template = await FileSystem.OpenAppPackageFileAsync("template.html");
        using var reader = new StreamReader(template);
        _templateNode = new HtmlDocument().DocumentNode;
        _templateNode.AppendChild(HtmlNode.CreateNode(await reader.ReadToEndAsync()));
    }
}