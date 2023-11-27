using System.ComponentModel;
using System.Text;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics.Platform;
using Opal;
using Opal.Document.Line;
using Opal.Response;
using Opal.Tofu;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Services.Cache;

namespace RosyCrow.Services.Document;

[Localizable(false)]
internal class DocumentService : IDocumentService
{
    private readonly ICacheService _cache;
    private readonly ILogger<DocumentService> _logger;
    private readonly List<Task> _parallelRenderWorkload;
    private readonly ISettingsDatabase _settingsDatabase;
    private HtmlNode _customCssNode;
    private HtmlNode _themeLinkNode;
    private HtmlNode _fontSizeNode;
    private HtmlDocument _templateDocument;

    public DocumentService(ISettingsDatabase settingsDatabase, ICacheService cache, ILogger<DocumentService> logger)
    {
        _settingsDatabase = settingsDatabase;
        _cache = cache;
        _logger = logger;

        _parallelRenderWorkload = new List<Task>();

        _themeLinkNode = BuildThemeLinkNode();

        if (_settingsDatabase.UseCustomCss)
            _customCssNode = BuildCustomCssNode();

        if (_settingsDatabase.UseCustomFontSize)
            _fontSizeNode = BuildCustomFontSizeNode();

        _settingsDatabase.PropertyChanged += SettingChanged;
    }

    public HtmlDocument CreateEmptyDocument()
    {
        var document = new HtmlDocument();
        document.DocumentNode.CopyFrom(_templateDocument.DocumentNode, true);
        InjectStyleElements(document);
        return document;
    }

    public HtmlDocument LoadFromBuffer(Stream buffer)
    {
        var document = new HtmlDocument();
        document.Load(buffer);

        // remove the old injected stylesheet so that the new one can be used
        document.DocumentNode.Descendants("link").FirstOrDefault(n => n.HasClass("injected-stylesheet"))?.Remove();

        var styleNodes = document.DocumentNode.Descendants("style").ToList();
        foreach (var node in styleNodes)
            node.Remove();

        InjectStyleElements(document);
        return document;
    }

    public async Task LoadResources()
    {
        try
        {
            await using var template = await FileSystem.OpenAppPackageFileAsync("template.html");
            using var reader = new StreamReader(template);
            _templateDocument = new HtmlDocument();
            _templateDocument.Load(template);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load the document template");
            throw;
        }
    }

    public async Task<string> RenderInternalDocument(string name)
    {
        var document = CreateEmptyDocument();
        var body = document.DocumentNode.ChildNodes.FindFirst("main");

        await using (var file = await FileSystem.OpenAppPackageFileAsync($"{name}.html"))
        using (var reader = new StreamReader(file))
        {
            body.AppendChild(HtmlNode.CreateNode(await reader.ReadToEndAsync()));
        }

        return document.DocumentNode.OuterHtml;
    }

    public async Task<RenderedGemtextDocument> RenderGemtextAsHtml(GemtextResponse gemtext)
    {
        var document = CreateEmptyDocument();

        var body = document.DocumentNode.ChildNodes.FindFirst("main");
        string title = null; // this will be set to the first heading we encounter

        HtmlNode preNode = null;
        HtmlNode listNode = null;
        var preText = new StringBuilder();

        foreach (var line in gemtext.AsDocument())
            switch (line)
            {
                case FormattedBeginLine preBegin:
                    var preParent = body.AppendChild(HtmlNode.CreateNode("<figure></figure>"));
                    if (!string.IsNullOrWhiteSpace(preBegin.Text))
                        preParent.AppendChild(HtmlNode.CreateNode($"<figcaption>{preBegin.Text}</figcaption>"));
                    preNode = preParent.AppendChild(HtmlNode.CreateNode("<pre></pre>"));
                    preText.Clear();
                    break;
                case FormattedEndLine when preNode != null:
                    preNode.InnerHtml = HttpUtility.HtmlEncode(preText.ToString());
                    break;
                case FormattedLine formatted:
                    preText.AppendLine(formatted.Text);
                    break;
                case ListLine listLine:
                    listNode ??= HtmlNode.CreateNode("<ul></ul>");
                    listNode.AppendChild(HtmlNode.CreateNode($"<li>{HttpUtility.HtmlEncode(listLine.Text)}</li>"));
                    break;
                default:
                    if (listNode != null)
                    {
                        body.AppendChild(listNode);
                        listNode = null;
                    }

                    if (string.IsNullOrWhiteSpace(title) && line is HeadingLine heading)
                        title = heading.Text;

                    var renderedLine = await RenderGemtextLine(line);
                    if (renderedLine != null)
                        body.AppendChild(renderedLine);

                    break;
            }

        if (listNode != null)
            body.AppendChild(listNode);

        if (_parallelRenderWorkload.Any())
        {
            await Task.WhenAll(_parallelRenderWorkload.ToArray());
            _parallelRenderWorkload.Clear();
        }

        // cache the page prior to injecting the stylesheet
        await using var pageBuffer = new MemoryStream(Encoding.UTF8.GetBytes(document.DocumentNode.OuterHtml));
        await _cache.Write(gemtext.Uri, pageBuffer);


        return new RenderedGemtextDocument { HtmlContents = document.DocumentNode.OuterHtml, Title = title };
    }

    private void SettingChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ISettingsDatabase.Theme):
                _themeLinkNode = BuildThemeLinkNode();
                break;
            case nameof(ISettingsDatabase.CustomCss):
                if (_settingsDatabase.UseCustomCss)
                    _customCssNode = BuildCustomCssNode();
                break;
            case nameof(ISettingsDatabase.CustomFontSizeText):
            case nameof(ISettingsDatabase.CustomFontSizeH1):
            case nameof(ISettingsDatabase.CustomFontSizeH2):
            case nameof(ISettingsDatabase.CustomFontSizeH3):
                if (_settingsDatabase.UseCustomFontSize)
                    _fontSizeNode = BuildCustomFontSizeNode();
                break;
        }
    }

    private void InjectStyleElements(HtmlDocument document)
    {
        var head = document.DocumentNode.ChildNodes.FindFirst("head");
        if (head == null)
            return;

        head.AppendChild(_themeLinkNode);

        if (_settingsDatabase.UseCustomFontSize)
        {
            _fontSizeNode ??= BuildCustomFontSizeNode();
            head.AppendChild(_fontSizeNode);
        }

        if (_settingsDatabase.UseCustomCss)
        {
            _customCssNode ??= BuildCustomCssNode();
            head.AppendChild(_customCssNode);
        }
    }

    private HtmlNode BuildThemeLinkNode()
    {
        return HtmlNode.CreateNode("<link rel=\"stylesheet\" class=\"injected-stylesheet\" " +
                                   $"href=\"Themes/{_settingsDatabase.Theme}.css\" media=\"screen\" />");
    }

    private HtmlNode BuildCustomCssNode()
    {
        return HtmlNode.CreateNode($"<style class=\"custom-css\">{_settingsDatabase.CustomCss}</style>");
    }

    private HtmlNode BuildCustomFontSizeNode()
    {
        var textSize = _settingsDatabase.CustomFontSizeText;
        var h1Size = _settingsDatabase.CustomFontSizeH1;
        var h2Size = _settingsDatabase.CustomFontSizeH2;
        var h3Size = _settingsDatabase.CustomFontSizeH3;
        return HtmlNode.CreateNode("<style class=\"custom-fontsize\">" +
                                   $"body {{ font-size: {textSize}px; }} " +
                                   $"h1 {{ font-size: {h1Size}px; }} " +
                                   $"h2 {{ font-size: {h2Size}px; }} " +
                                   $"h3 {{ font-size: {h3Size}px; }} " +
                                   "</style>");
    }

    private static async Task<MemoryStream> CreateInlinedImagePreview(Stream source, string mimetype)
    {
        var typeHint = mimetype switch
        {
            "image/jpeg" => ImageFormat.Jpeg,
            "image/gif" => ImageFormat.Gif,
            "image/bmp" => ImageFormat.Bmp,
            "image/tiff" => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };

        var image = PlatformImage.FromStream(source, typeHint);
        using var downsized = image.Downsize(256.0f, true);

        var output = new MemoryStream();
        await downsized.SaveAsync(output);

        return output;
    }

    private static string CreateInlineImageDataUrl(MemoryStream data)
    {
        data.Seek(0, SeekOrigin.Begin);
        return $@"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
    }

    private async Task<string> TryLoadCachedImage(Uri uri)
    {
        try
        {
            var image = new MemoryStream();

            if (await _cache.TryRead(uri, image))
            {
                _logger.LogDebug(@"Loaded cached image originally from {URI}", uri);
                return CreateInlineImageDataUrl(image);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while attempting to load a cached image retrieved from {URI}", uri);
        }

        return null;
    }

    private async Task<string> FetchAndCacheInlinedImage(Uri uri)
    {
        try
        {
            for (var i = 0; i < Constants.MaxRequestAttempts; i++)
            {
                try
                {
                    // don't follow redirects that the user isn't aware of
                    var client = new OpalClient(new DummyCertificateDatabase(), RedirectBehavior.Ignore)
                    {
                        AllowIPv6 = _settingsDatabase.AllowIpv6
                    };

                    if (await client.SendRequestAsync(uri.ToString()) is SuccessfulResponse success)
                    {
                        _logger.LogDebug(@"Successfully loaded an image of type {MimeType} to be inlined from {URI}",
                            success.MimeType,
                            uri);

                        var image = await CreateInlinedImagePreview(success.Body, success.MimeType);

                        if (image == null)
                        {
                            _logger.LogWarning(
                                @"Loaded an image to be inlined from {URI} but failed to create the preview",
                                uri);
                            break;
                        }

                        image.Seek(0, SeekOrigin.Begin);
                        await _cache.Write(uri, image);

                        _logger.LogDebug(@"Loaded an inlined image from {URI} after {Attempt} attempt(s)", uri, i + 1);

                        return CreateInlineImageDataUrl(image);
                    }

                    // if the error was only temporary (according to the server), then
                    // we can try again
                }
                catch (Exception e)
                {
                    // don't care
                    _logger.LogDebug(e, "Attempt {Attempt} to fetch and inline an image from {URI} failed", i + 1, uri);
                }

                await Task.Delay(Convert.ToInt32(Math.Pow(2, i) * 100));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while attempting to cache an image from {URI}", uri);
        }

        return null;
    }

    private async Task<HtmlNode> RenderLinkLine(LinkLine line)
    {
        return _settingsDatabase.InlineImages
            ? await RenderInlineImage(line)
            : RenderDefaultLinkLine(line);
    }

    private HtmlNode RenderDefaultLinkLine(LinkLine line)
    {
        var node = HtmlNode.CreateNode(
            $"<p><a href=\"{line.Uri}\">{HttpUtility.HtmlEncode(line.Text ?? line.Uri.ToString())}</a></p>");

        if (_settingsDatabase.AnnotateLinkScheme &&
            !string.IsNullOrWhiteSpace(line.Text) &&
            !line.Uri.Scheme.Equals(Constants.GeminiScheme, StringComparison.OrdinalIgnoreCase))
        {
            node.PrependChild(HtmlNode.CreateNode($"<sup>({HttpUtility.HtmlEncode(line.Uri.Scheme.ToUpperInvariant())})&nbsp;</sup>"));
        }

        return node;
    }

    private async Task<HtmlNode> RenderInlineImage(LinkLine line)
    {
        try
        {
            var fileName = line.Uri.Segments.LastOrDefault()?.Trim('/');

            string mimeType = null;

            if (!string.IsNullOrWhiteSpace(fileName) && MimeTypes.TryGetMimeType(fileName, out mimeType) &&
                mimeType.StartsWith("image"))
            {
                var node = HtmlNode.CreateNode("<p></p>");

                _logger.LogDebug(@"Attempting to render an image preview inline from {URI}", line.Uri);

                if (line.Uri.Scheme == Constants.GeminiScheme)
                {
                    _logger.LogDebug(@"The image URI specifies the gemini protocol");

                    var cached = await TryLoadCachedImage(line.Uri);

                    if (cached != null)
                    {
                        _logger.LogDebug(@"Loading the image preview from the cache");
                        node.AppendChild(RenderInlineImageFigure(line, cached));
                    }
                    else
                    {
                        _logger.LogDebug(
                            @"Queueing the image download to complete after the rest of the page has been rendered.");
                        _parallelRenderWorkload.Add(Task.Run(async () =>
                        {
                            var source = await FetchAndCacheInlinedImage(line.Uri);

                            if (!string.IsNullOrEmpty(source))
                            {
                                _logger.LogDebug(@"Successfully created the image preview; rendering that now");
                                node.AppendChild(RenderInlineImageFigure(line, source));
                            }
                            else
                            {
                                // did not load the image preview; fallback to a simple link
                                _logger.LogDebug(
                                    @"Could not create the image preview; falling-back to a simple gemtext link line");
                                node.AppendChild(HtmlNode.CreateNode(
                                    $"<a href=\"{line.Uri}\">{HttpUtility.HtmlEncode(line.Text ?? line.Uri.ToString())}</a>"));
                            }
                        }));
                    }
                }
                else
                {
                    // http, etc. can be handled by the browser
                    _logger.LogDebug(
                        @"The image URI specifies the HTTP protocol; let the WebView figure out how to render it");
                    node.AppendChild(RenderInlineImageFigure(line, line.Uri.ToString()));
                }

                return node;
            }

            _logger.LogDebug(
                @"The URI {URI} does not appear to point to an image (type: {MimeType}); an anchor tag will be rendered",
                line.Uri,
                mimeType ?? "none");
            return RenderDefaultLinkLine(line);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while rendering an inline image preview from {URI}", line.Uri);
            return null;
        }
    }

    private static HtmlNode RenderInlineImageFigure(LinkLine line, string source)
    {
        // successfully loaded the image preview
        var figure = HtmlNode.CreateNode($"<figure><img src=\"{source}\" /></figure>");

        if (!string.IsNullOrWhiteSpace(line.Text))
        {
            figure.AppendChild(
                HtmlNode.CreateNode($"<figcaption>{HttpUtility.HtmlEncode(line.Text)}</figcaption>"));
        }

        var anchor = HtmlNode.CreateNode($"<a href=\"{line.Uri}\"></a>");
        anchor.AppendChild(figure);

        return anchor;
    }

    private async Task<HtmlNode> RenderGemtextLine(ILine line)
    {
        return line switch
        {
            EmptyLine => HtmlNode.CreateNode("<br>"),
            HeadingLine headingLine => HtmlNode.CreateNode(
                $"<h{headingLine.Level}>{HttpUtility.HtmlEncode(headingLine.Text)}</h{headingLine.Level}>"),
            LinkLine linkLine => await RenderLinkLine(linkLine),
            QuoteLine quoteLine => HtmlNode.CreateNode(
                $"<blockquote><p>{HttpUtility.HtmlEncode(quoteLine.Text)}</p></blockquote>"),
            TextLine textLine => HtmlNode.CreateNode($"<p>{HttpUtility.HtmlEncode(textLine.Text)}</p>"),
            _ => throw new ArgumentOutOfRangeException(nameof(line))
        };
    }
}