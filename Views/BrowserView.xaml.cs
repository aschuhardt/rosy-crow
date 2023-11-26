using System.ComponentModel;
using Android.Views;
using Android.Webkit;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Opal;
using Opal.Authentication.Certificate;
using Opal.CallbackArgs;
using Opal.Response;
using Opal.Tofu;
using RosyCrow.Controls.Tabs;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Platforms.Android;
using RosyCrow.Resources.Localization;
using RosyCrow.Services.Cache;
using RosyCrow.Services.Document;
using RosyCrow.Services.Identity;
using Tab = RosyCrow.Models.Tab;
using WebView = Android.Webkit.WebView;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class BrowserView : ContentView
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ICacheService _cache;
    private readonly IDocumentService _documentService;
    private readonly IOpalClient _geminiClient;
    private readonly IIdentityService _identityService;
    private readonly ILogger<BrowserView> _logger;
    private readonly ISettingsDatabase _settingsDatabase;

    private string _findNextQuery;
    private bool _isLoading;
    private IPrintService _printService;
    private bool _resetFindNext;
    private Tab _tab;

    public BrowserView()
        : this(MauiProgram.Services.GetRequiredService<IOpalClient>(),
            MauiProgram.Services.GetRequiredService<ISettingsDatabase>(),
            MauiProgram.Services.GetRequiredService<IBrowsingDatabase>(),
            MauiProgram.Services.GetRequiredService<IIdentityService>(),
            MauiProgram.Services.GetRequiredService<ICacheService>(),
            MauiProgram.Services.GetRequiredService<ILogger<BrowserView>>(),
            MauiProgram.Services.GetRequiredService<IDocumentService>())
    {
    }

    public BrowserView(IOpalClient geminiClient, ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase,
        IIdentityService identityService, ICacheService cache, ILogger<BrowserView> logger, IDocumentService documentService)
    {
        InitializeComponent();

        _geminiClient = geminiClient;
        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _identityService = identityService;
        _cache = cache;
        _logger = logger;
        _documentService = documentService;

        _geminiClient.GetActiveClientCertificateCallback = GetActiveCertificateCallback;
        _geminiClient.RemoteCertificateInvalidCallback = RemoteCertificateInvalidCallback;
        _geminiClient.RemoteCertificateUnrecognizedCallback = RemoteCertificateUnrecognizedCallback;
    }

    public bool HasFindNextQuery
    {
        get => !string.IsNullOrEmpty(FindNextQuery);
    }

    public string FindNextQuery
    {
        get => _findNextQuery;
        private set
        {
            if (value == _findNextQuery)
                return;

            _findNextQuery = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFindNextQuery));
        }
    }

    private async Task RemoteCertificateUnrecognizedCallback(RemoteCertificateUnrecognizedArgs arg)
    {
        if (_tab.ParentPage == null)
        {
            _logger.LogWarning(@"Unable to prompt the user to verify an unrecognized certificate because no ParentPage was set");
            return;
        }

        arg.AcceptAndTrust = await _tab.ParentPage.DisplayAlertOnMainThread(
            Text.BrowserView_RemoteCertificateUnrecognizedCallback_New_Certificate,
            string.Format(
                Text
                    .BrowserView_RemoteCertificateUnrecognizedCallback_Accept_the_host_s_new_certificate_and_continue___Its_fingerprint_is__0__,
                arg.Fingerprint),
            Text.BrowserView_RemoteCertificateUnrecognizedCallback_Yes,
            Text.BrowserView_RemoteCertificateUnrecognizedCallback_No);

        if (arg.AcceptAndTrust)
            _browsingDatabase.AcceptHostCertificate(arg.Host);
    }

    private async Task RemoteCertificateInvalidCallback(RemoteCertificateInvalidArgs arg)
    {
        if (_tab.ParentPage == null)
            return;

        var message = arg.Reason switch
        {
            InvalidCertificateReason.NameMismatch => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_name_on_the_server_s_certificate_is_incorrect_,
            InvalidCertificateReason.TrustedMismatch => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_has_changed_,
            InvalidCertificateReason.Expired => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_has_expired_,
            InvalidCertificateReason.NotYet => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_is_not_valid_yet_,
            InvalidCertificateReason.Other => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_is_invalid_,
            InvalidCertificateReason.MissingInformation => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_is_missing_required_information_,
            _ => throw new ArgumentOutOfRangeException(nameof(arg))
        };

        await _tab.ParentPage.DisplayAlertOnMainThread(Text.BrowserView_RemoteCertificateInvalidCallback_Certificate_Problem,
            message,
            Text.BrowserView_RemoteCertificateInvalidCallback_Cancel);
    }

    private async Task<IClientCertificate> GetActiveCertificateCallback()
    {
        if (_identityService.ShouldReloadActiveCertificate)
            return new ClientCertificate(await _identityService.LoadActiveCertificate());

        if (_identityService.ActiveCertificate != null)
            return new ClientCertificate(_identityService.ActiveCertificate);

        return null;
    }

    private event EventHandler FindNext;
    private event EventHandler ClearMatches;

    public void Print()
    {
        try
        {
            _printService.Print(_tab.Title);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while attempting to print the current page");
        }
    }

    public void GoBack()
    {
        try
        {
            // first pop the current page, then peek to get the prior
            if (_tab.RecentHistory.TryPop(out var current))
            {
                if (_tab.RecentHistory.TryPeek(out var prev))
                    _tab.Location = prev;
                else
                {
                    // there was no previous entry; re-push the current one in order to revert the stack to its initial state
                    _tab.RecentHistory.Push(current);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while navigating backward");
        }
    }

    public void ClearFindResults()
    {
        FindNextQuery = null;
        OnClearFindNext();
    }

    public void FindTextInPage(string query)
    {
        // if the query is different from the last time, then start the search over from the top of the page
        if (query != FindNextQuery)
            _resetFindNext = true;

        FindNextQuery = query;

        OnFindNext();
        _resetFindNext = false;
    }

    [Localizable(false)]
    private string RenderCachedHtml(Stream buffer)
    {
        var document = _documentService.LoadFromBuffer(buffer);
        var childNodes = document.DocumentNode.ChildNodes;
        _tab.Title = (childNodes.FindFirst("h1") ?? childNodes.FindFirst("h2") ?? childNodes.FindFirst("h3"))?.InnerText;
        return document.DocumentNode.OuterHtml;
    }

    private static bool IsRetryAppropriate(StatusCode status)
    {
        // only retry requests that could potentially return a different result

        return status switch
        {
            StatusCode.TemporaryFailure => true,
            StatusCode.ServerUnavailable => true,
            StatusCode.CgiError => true,
            StatusCode.ProxyError => true,
            StatusCode.SlowDown => true,
            _ => false
        };
    }

    public async Task Upload(TitanPayload payload)
    {
        if (_isLoading)
            return;

        _isLoading = true;

        if (!_tab.IsRefreshing)
            _tab.IsRefreshing = true;

        try
        {
            for (var attempts = 0; attempts < Constants.MaxRequestAttempts; attempts++)
            {
                if (!string.IsNullOrWhiteSpace(_tab.Input))
                {
                    _logger.LogInformation(@"User provided input ""{Input}""", _tab.Input);
                    _tab.Location = new UriBuilder(_tab.Location) { Query = _tab.Input }.Uri;
                }

                _geminiClient.AllowIPv6 = _settingsDatabase.AllowIpv6;

                var response = await _geminiClient.UploadAsync(_tab.Location,
                    payload.Size,
                    payload.Token,
                    payload.MimeType,
                    payload.Contents);

                if (await HandleTitanResponse(response, attempts) == ResponseAction.Finished)
                {
                    _logger.LogInformation(@"Upload finished after {Attempts} attempt(s)", attempts + 1);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while navigating to {URI}", _tab.Location);
        }

        _tab.IsRefreshing = false;
        _isLoading = false;
    }

    public async Task LoadPage(bool triggeredByRefresh = false, bool useCache = false)
    {
        if (_isLoading)
            return;

        _tab.CanShowHostCertificate = false;

        if (_tab.Location == null || _tab.Location.Scheme == Constants.InternalScheme)
        {
            await LoadInternalPage(_tab.Location?.Host ?? @"default");
            if (_tab.Location != null)
                _tab.RenderUrl = $@"{_tab.Location.Host}{_tab.Location.PathAndQuery}";
            _tab.IsRefreshing = false;
            _tab.CanShowHostCertificate = false;
            _isLoading = false;
            return;
        }

        if (_tab.Location.Scheme == Constants.TitanScheme)
        {
            if (MainThread.IsMainThread)
                await Navigation.PushModalPageAsync<TitanUploadPage>(page => page.Browser = this);
            else
                await MainThread.InvokeOnMainThreadAsync(() => Navigation.PushModalPageAsync<TitanUploadPage>(page => page.Browser = this));

            return;
        }

        _isLoading = true;

        if (!_tab.IsRefreshing)
            _tab.IsRefreshing = true;

        _tab.CanPrint = false;

        if (HasFindNextQuery)
            ClearFindResults();

        _logger.LogInformation(@"Navigating to {URI}", _tab.Location);

        try
        {
            for (var attempts = 0; attempts < Constants.MaxRequestAttempts; attempts++)
            {
                if (useCache && !triggeredByRefresh)
                {
                    var cached = new MemoryStream();

                    if (await _cache.TryRead(_tab.Location, cached))
                    {
                        _logger.LogInformation(@"Loading a cached copy of the page");

                        cached.Seek(0, SeekOrigin.Begin);
                        _tab.Html = RenderCachedHtml(cached);
                        _tab.CanShowHostCertificate = true;
                        _tab.RenderUrl = $@"{_tab.Location.Host}{_tab.Location.PathAndQuery}";
                        StoreVisitedLocation(_tab.Location, false);
                        _tab.CanPrint = _printService != null;
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(_tab.Input))
                {
                    _logger.LogInformation(@"User provided input ""{Input}""", _tab.Input);
                    _tab.Location = new UriBuilder(_tab.Location) { Query = _tab.Input }.Uri;
                }

                _geminiClient.AllowIPv6 = _settingsDatabase.AllowIpv6;

                var response = await _geminiClient.SendRequestAsync(_tab.Location);

                _tab.RenderUrl = $@"{response.Uri.Host}{response.Uri.PathAndQuery}";
                _logger.LogInformation(@"Response was {Response}", response);

                if (await HandleGeminiResponse(response, attempts) == ResponseAction.Finished)
                {
                    _logger.LogInformation(@"Request finished after {Attempts} attempt(s)", attempts + 1);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while navigating to {URI}", _tab.Location);
        }

        _tab.IsRefreshing = false;
        _tab.Input = null;
        _isLoading = false;
    }

    private async Task<ResponseAction> HandleTitanResponse(IGeminiResponse response, int attempt)
    {
        switch (response)
        {
            case InputRequiredResponse inputRequired:
            {
                if (_tab.ParentPage == null)
                {
                    _logger.LogWarning(@"Unable to prompt the user for input because no ParentPage was set");
                    return ResponseAction.Finished;
                }

                _tab.Input = await _tab.ParentPage.DisplayPromptOnMainThread(Text.BrowserView_LoadPage_Input_Required,
                    inputRequired.Message);

                if (string.IsNullOrEmpty(_tab.Input))
                    return ResponseAction.Finished; // if no user-input was provided, then we cannot continue

                return ResponseAction.Retry;
            }
            case ErrorResponse error:
            {
                if (!error.CanRetry)
                    return ResponseAction.Finished;

                _logger.LogInformation(@"{Attempts} attempt(s) remaining", Constants.MaxRequestAttempts - attempt);

                if (Constants.MaxRequestAttempts - attempt <= 1 || !IsRetryAppropriate(error.Status))
                {
                    _logger.LogInformation(@"No further attempts will be made");
                    if (_tab.ParentPage != null)
                        await _tab.ParentPage.DisplayAlertOnMainThread(Text.BrowserView_LoadPage_Error,
                            error.Message,
                            Text.BrowserView_LoadPage_OK);
                    return ResponseAction.Finished;
                }

                await Task.Delay(Convert.ToInt32(Math.Pow(2, attempt) * 100));

                return ResponseAction.Retry;
            }
            case SuccessfulResponse success:
            {
                await HandleSuccessfulResponse(success);
                return ResponseAction.Finished;
            }
            default:
                return ResponseAction.Retry;
        }
    }

    private async Task<ResponseAction> HandleGeminiResponse(IGeminiResponse response, int attempt)
    {
        switch (response)
        {
            case InputRequiredResponse inputRequired:
            {
                if (_tab.ParentPage == null)
                {
                    _logger.LogWarning(@"Unable to prompt the user for input because no ParentPage was set");
                    return ResponseAction.Finished;
                }

                _tab.Input = await _tab.ParentPage.DisplayPromptOnMainThread(Text.BrowserView_LoadPage_Input_Required,
                    inputRequired.Message);
                ;

                if (string.IsNullOrEmpty(_tab.Input))
                {
                    _settingsDatabase.LastVisitedUrl = response.Uri.ToString();
                    return ResponseAction.Finished; // if no user-input was provided, then we cannot continue
                }

                return ResponseAction.Retry;
            }
            case ErrorResponse error:
            {
                if (!error.CanRetry)
                {
                    // Opal has indicated that this request should not be re-sent; bail early
                    //
                    // Currently this only happens in the case of invalid or rejected remote
                    // certificates, where re-sending the request would not make sense
                    return ResponseAction.Finished;
                }

                _logger.LogInformation(@"{Attempts} attempt(s) remaining", Constants.MaxRequestAttempts - attempt);

                if (Constants.MaxRequestAttempts - attempt <= 1 || !IsRetryAppropriate(error.Status))
                {
                    _logger.LogInformation(@"No further attempts will be made");

                    if (_tab.ParentPage == null)
                    {
                        return ResponseAction.Finished;
                    }

                    if (_tab.ParentPage != null)
                    {
                        await _tab.ParentPage.DisplayAlertOnMainThread(Text.BrowserView_LoadPage_Error,
                            error.Message,
                            Text.BrowserView_LoadPage_OK);
                    }

                    return ResponseAction.Finished;
                }

                await Task.Delay(Convert.ToInt32(Math.Pow(2, attempt) * 100));

                return ResponseAction.Retry;
            }
            case SuccessfulResponse success:
            {
                await HandleSuccessfulResponse(success);
                return ResponseAction.Finished;
            }
            default:
                return ResponseAction.Retry;
        }
    }

    private static string GetDefaultFileNameByMimeType(string mimeType)
    {
        var extension = MimeTypes.GetMimeTypeExtensions(mimeType).FirstOrDefault();
        return extension != null
            ? $@"file_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{extension}"
            : $@"file_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private async Task HandleSuccessfulResponse(SuccessfulResponse response)
    {
        _tab.Location = response.Uri;

        if (response is GemtextResponse gemtext)
        {
            _logger.LogInformation(@"Response is a gemtext document");

            var result = await _documentService.RenderGemtextAsHtml(gemtext);
            _tab.Html = result.HtmlContents;
            _tab.Title = result.Title;
            _tab.CanShowHostCertificate = true;
            _tab.CanPrint = _printService != null;
            _tab.Url = _tab.Location.ToString();
            _tab.Label = CreateTabLabel();
            _browsingDatabase.Update(_tab);
            StoreVisitedLocation(_tab.Location, false);
        }
        else
        {
            _logger.LogInformation(
                @"Response is not a gemtext document, so it will be opened externally");

            StoreVisitedLocation(_tab.Location, true);

            // not gemtext; save as a file
            var fileName = response.Uri.Segments.LastOrDefault() ??
                           GetDefaultFileNameByMimeType(response.MimeType);
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);

            await using (var outputFile = File.Create(path))
            {
                await response.Body.CopyToAsync(outputFile);
            }

            _logger.LogInformation(@"Opening file {Path}", path);

            await Launcher.Default.OpenAsync(
                new OpenFileRequest(fileName, new ReadOnlyFile(path, response.MimeType)));
        }
    }

    private void StoreVisitedLocation(Uri uri, bool isExternal)
    {
        try
        {
            if (!isExternal)
            {
                _settingsDatabase.LastVisitedUrl = uri.ToString();

                if (!_tab.RecentHistory.TryPeek(out var prev) || !prev.Equals(uri))
                    _tab.RecentHistory.Push(uri);
            }

            if (_settingsDatabase.SaveVisited)
            {
                _browsingDatabase.AddVisitedPage(new Visited
                {
                    Url = uri.ToString(), Timestamp = DateTime.Now,
                    Title = _tab.Title ?? uri.Segments.LastOrDefault() ?? uri.Host
                });
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while storing a visited page {URI}", uri);
        }
    }

    private async Task LoadInternalPage(string name = "default")
    {
        try
        {
            _logger.LogInformation(@"Loading internal page {Name}", name);
            _tab.RenderUrl = $@"{Constants.InternalScheme}://{name}";
            _tab.Html = await _documentService.RenderInternalDocument(name);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while loading internal page {Name}", name);
        }
    }

    private async void PageWebView_OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        // don't ever let the default navigation behavior happen
        e.Cancel = true;

        var uri = e.Url.ToUri();
        if (!uri.IsAbsoluteUri || uri.Scheme is Constants.GeminiScheme or Constants.TitanScheme or Constants.InternalScheme)
            _tab.Location = uri;
        else if (!await Launcher.Default.TryOpenAsync(uri))
        {
            await _tab.ParentPage.DisplayAlertOnMainThread(
                Text.BrowserView_PageWebView_OnNavigating_Cannot_Open_URL, 
                string.Format(Text.BrowserView_PageWebView_OnNavigating_No_app_is_configured_to_open__0__links_, uri.Scheme),
                Text.BrowserView_PageWebView_OnNavigating_OK);
        }
    }

    protected virtual void OnFindNext()
    {
        FindNext?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnClearFindNext()
    {
        ClearMatches?.Invoke(this, EventArgs.Empty);
    }

    private string CreateTabLabel()
    {
        if (_browsingDatabase.TryGetCapsule(_tab.Location.Host, out var capsule) && !string.IsNullOrEmpty(capsule.Icon))
        {
            _logger.LogInformation(@"Capsule {Host} has a stored icon: {Icon}", _tab.Location.Host, capsule.Icon);
            return capsule.Icon;
        }

        return _tab.DefaultLabel;
    }

#if ANDROID
    private void BuildContextMenu(IMenu menu, WebView view)
    {
        var hitTest = view.GetHitTestResult();

        if (hitTest.Type is HitTestResult.AnchorType or HitTestResult.SrcAnchorType or HitTestResult.SrcImageAnchorType &&
            !string.IsNullOrWhiteSpace(hitTest.Extra))
        {
            menu.Add(Text.BrowserView_BuildContextMenu_Copy_URL)?.SetOnMenuItemClickListener(
                new ActionMenuClickHandler<string>(hitTest.Extra,
                    async uri => await Clipboard.Default.SetTextAsync(uri)));
            menu.Add(Text.BrowserView_BuildContextMenu_Share_URL)?.SetOnMenuItemClickListener(
                new ActionMenuClickHandler<string>(hitTest.Extra,
                    async uri => await Share.Default.RequestAsync(new ShareTextRequest(uri))));

            if (_settingsDatabase.TabsEnabled)
            {
                menu.Add(Text.BrowserView_BuildContextMenu_Open_in_New_Tab)?.SetOnMenuItemClickListener(
                    new ActionMenuClickHandler<string>(hitTest.Extra,
                        uri => OnOpeningUrlInNewTab(
                            new UrlEventArgs(uri.ToGeminiUri()))));
            }
        }
    }
#endif

    private void PageWebView_OnHandlerChanged(object sender, EventArgs e)
    {
#if ANDROID
        if ((sender as Microsoft.Maui.Controls.WebView)?.Handler is not WebViewHandler webViewHandler)
            return;

        webViewHandler.PlatformView.ContextMenuCreated +=
            (o, args) => BuildContextMenu(args.Menu, o as WebView);

        _printService = new AndroidPrintService(webViewHandler.PlatformView);

        ClearMatches += (_, _) => webViewHandler.PlatformView.ClearMatches();

        FindNext += (_, _) =>
        {
            if (_resetFindNext) // new query
                webViewHandler.PlatformView.FindAllAsync(FindNextQuery);
            else // existing query; continue forward
                webViewHandler.PlatformView.FindNext(true);
        };

        webViewHandler.PlatformView.SetFindListener(new CallbackFindListener(count =>
        {
            if (!_resetFindNext || _tab.ParentPage == null)
                return;

            if (count == 0)
            {
                _tab.ParentPage.ShowToast(Text.BrowserView_FindNext_No_instances_found, ToastDuration.Short);
                FindNextQuery = null;
            }
            else
            {
                _tab.ParentPage.ShowToast(string.Format(Text.BrowserView_FindNext_Found__0__instances, count),
                    ToastDuration.Short);
            }
        }));
#endif
    }

    private void RefreshView_OnHandlerChanged(object sender, EventArgs e)
    {
#if ANDROID
        var refreshViewHandler = (sender as RefreshView)?.Handler as RefreshViewHandler;

        // having to access the window this way is weird and bad, hopefully GetVisualElementWindow() will work one day
        if (Application.Current != null && Application.Current.Windows[0] is { } window)
            refreshViewHandler?.PlatformView.SetProgressViewOffset(false, 0, (int)window.Height / 4);
#endif
    }

    protected virtual void OnOpeningUrlInNewTab(UrlEventArgs e)
    {
        _tab.OnOpeningUrlInNewTab(e);
    }

    private void BrowserView_OnBindingContextChanged(object sender, EventArgs e)
    {
        if (BindingContext is not Tab tab)
            throw new InvalidOperationException();

        _tab = tab;
        tab.Refresh = new Command(async () => await LoadPage(true));
        tab.FindNext = new Command(query => FindTextInPage((string)query));
        tab.Print = new Command(Print, () => _tab.CanPrint);
        tab.GoBack = new Command(GoBack, () => _tab.RecentHistory.TryPeek(out _));
        tab.ClearFind = new Command(ClearFindResults, () => HasFindNextQuery);
        tab.Load = new Command(async () => await LoadPage(false, true), () => !_isLoading);
        tab.Location = tab.Url.ToGeminiUri();
    }

    private enum ResponseAction
    {
        Retry,
        Finished
    }
}