using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Serilog;

namespace SeuManga.Desktop.Networking;

public sealed class MangaDexBrowserTransport
{
    private readonly object _initializationLock = new();
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly Dictionary<string, TaskCompletionSource<BrowserResponse>> _pendingRequests = new(StringComparer.Ordinal);
    private WebView2? _browser;
    private Task? _initializationTask;
    private bool _coreConfigured;

    public void Attach(WebView2 browser)
    {
        ArgumentNullException.ThrowIfNull(browser);
        _browser = browser;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get)
            throw new NotSupportedException("O transporte da MangaDex aceita somente consultas GET.");

        var uri = request.RequestUri ?? throw new InvalidOperationException("A solicitação não contém uma URL.");
        if (uri.Scheme != Uri.UriSchemeHttps || !IsAllowedHost(uri.Host))
            throw new InvalidOperationException($"O host '{uri.Host}' não é permitido pelo transporte da MangaDex.");

        var browser = _browser ?? throw new InvalidOperationException("O navegador de rede da MangaDex não foi conectado à janela principal.");
        await _requestLock.WaitAsync(cancellationToken);
        BrowserResponse result;
        try
        {
            result = await OnBrowserThreadAsync(browser, async () =>
            {
                await EnsureInitializedAsync(browser);
                var navigationUri = uri.Host.EndsWith(".mangadex.network", StringComparison.OrdinalIgnoreCase)
                    ? new Uri("https://api.mangadex.org/manga?limit=1")
                    : uri;
                await EnsureOriginAsync(browser, navigationUri);
                cancellationToken.ThrowIfCancellationRequested();
                var requestId = Guid.NewGuid().ToString("N");
                var completion = new TaskCompletionSource<BrowserResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_initializationLock)
                    _pendingRequests.Add(requestId, completion);
                using var registration = cancellationToken.Register(() => _ = AbortRequestAsync(browser, requestId, cancellationToken));
                try
                {
                    await browser.CoreWebView2.ExecuteScriptAsync(CreateFetchScript(uri, requestId));
                    return await completion.Task.WaitAsync(cancellationToken);
                }
                finally
                {
                    lock (_initializationLock)
                        _pendingRequests.Remove(requestId);
                }
            });
        }
        finally
        {
            _requestLock.Release();
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
            throw new HttpRequestException($"O WebView2 não conseguiu consultar a MangaDex: {result.Error}");

        var contentBytes = result.IsBase64
            ? Convert.FromBase64String(result.Body)
            : Encoding.UTF8.GetBytes(result.Body);
        var response = new HttpResponseMessage((HttpStatusCode)result.Status)
        {
            Content = new ByteArrayContent(contentBytes),
            RequestMessage = request
        };
        if (!string.IsNullOrWhiteSpace(result.ContentType))
            response.Content.Headers.TryAddWithoutValidation("Content-Type", result.ContentType);
        return response;
    }

    private async Task EnsureInitializedAsync(WebView2 browser)
    {
        Task initialization;
        lock (_initializationLock)
            initialization = _initializationTask ??= InitializeCoreAsync(browser);
        try
        {
            await initialization;
        }
        catch
        {
            lock (_initializationLock)
                if (ReferenceEquals(_initializationTask, initialization))
                    _initializationTask = null;
            throw;
        }
    }

    private async Task InitializeCoreAsync(WebView2 browser)
    {
        await browser.EnsureCoreWebView2Async();
        var core = browser.CoreWebView2;
        if (!_coreConfigured)
        {
            core.Settings.IsScriptEnabled = true;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.NavigationStarting += (_, args) =>
            {
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var navigationUri) ||
                    navigationUri.Scheme != Uri.UriSchemeHttps ||
                    !IsAllowedHost(navigationUri.Host) && !navigationUri.Host.Equals("mangadex.org", StringComparison.OrdinalIgnoreCase))
                    args.Cancel = true;
            };
            core.WebMessageReceived += OnWebMessageReceived;
            _coreConfigured = true;
        }
    }

    private static async Task EnsureOriginAsync(WebView2 browser, Uri uri)
    {
        var core = browser.CoreWebView2;
        if (Uri.TryCreate(core.Source, UriKind.Absolute, out var current) &&
            current.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase))
            return;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
                completion.TrySetException(new HttpRequestException($"O WebView2 não conseguiu abrir a MangaDex (erro {args.WebErrorStatus})."));
            else if (Uri.TryCreate(core.Source, UriKind.Absolute, out var source) &&
                     source.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase))
                completion.TrySetResult();
            else if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var challenge) ||
                     !challenge.Host.Equals("mangadex.org", StringComparison.OrdinalIgnoreCase) ||
                     !challenge.AbsolutePath.Equals("/edge/challenge", StringComparison.OrdinalIgnoreCase))
                completion.TrySetException(new HttpRequestException("A MangaDex redirecionou o WebView2 para uma página inesperada."));
        }

        core.NavigationCompleted += NavigationCompleted;
        try
        {
            var navigationRequest = core.Environment.CreateWebResourceRequest(
                uri.AbsoluteUri, "GET", null, "Referer: https://mangadex.org/\r\n");
            core.NavigateWithWebResourceRequest(navigationRequest);
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
        }
        finally
        {
            core.NavigationCompleted -= NavigationCompleted;
        }
    }

    private static Task<T> OnBrowserThreadAsync<T>(WebView2 browser, Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (browser.Dispatcher.CheckAccess())
            return operation();
        return browser.Dispatcher.InvokeAsync(operation).Task.Unwrap().WaitAsync(cancellationToken);
    }

    private static string CreateFetchScript(Uri uri, string requestId)
    {
        var serializedUri = JsonSerializer.Serialize(uri.AbsoluteUri);
        var serializedRequestId = JsonSerializer.Serialize(requestId);
        return $$"""
            (async () => {
                const requestId = {{serializedRequestId}};
                const controller = new AbortController();
                window.__seumangaRequests ??= Object.create(null);
                window.__seumangaRequests[requestId] = controller;
                const timer = setTimeout(() => controller.abort(), 25000);
                try {
                    const response = await fetch({{serializedUri}}, {
                        method: "GET",
                        credentials: "same-origin",
                        cache: "no-store",
                        referrer: "https://mangadex.org/",
                        referrerPolicy: "no-referrer-when-downgrade",
                        signal: controller.signal
                    });
                    const contentType = response.headers.get("content-type") || "";
                    const maxBytes = 20 * 1024 * 1024;
                    const declaredLength = Number(response.headers.get("content-length") || 0);
                    if (declaredLength > maxBytes) {
                        throw new Error("A resposta excede o limite de tamanho permitido.");
                    }
                    const reader = response.body?.getReader();
                    const chunks = [];
                    let totalBytes = 0;
                    if (reader) {
                        while (true) {
                            const { value, done } = await reader.read();
                            if (done) break;
                            totalBytes += value.length;
                            if (totalBytes > maxBytes) {
                                await reader.cancel();
                                throw new Error("A resposta excede o limite de tamanho permitido.");
                            }
                            chunks.push(value);
                        }
                    }
                    const bytes = new Uint8Array(totalBytes);
                    let offset = 0;
                    for (const chunk of chunks) {
                        bytes.set(chunk, offset);
                        offset += chunk.length;
                    }
                    const isText = /^(text\/|application\/(json|xml|javascript|problem\+json))/i.test(contentType);
                    let body;
                    if (isText) {
                        body = new TextDecoder().decode(bytes);
                    } else {
                        let binary = "";
                        for (let index = 0; index < bytes.length; index += 0x8000) {
                            binary += String.fromCharCode(...bytes.subarray(index, index + 0x8000));
                        }
                        body = btoa(binary);
                    }
                    window.chrome.webview.postMessage(JSON.stringify({
                        requestId,
                        status: response.status,
                        contentType,
                        isBase64: !isText,
                        body
                    }));
                } catch (error) {
                    window.chrome.webview.postMessage(JSON.stringify({
                        requestId,
                        error: String(error)
                    }));
                } finally {
                    clearTimeout(timer);
                    delete window.__seumangaRequests[requestId];
                }
            })()
            """;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var message = JsonSerializer.Deserialize<BrowserResponse>(args.TryGetWebMessageAsString(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (message is null || string.IsNullOrWhiteSpace(message.RequestId))
                return;
            TaskCompletionSource<BrowserResponse>? completion;
            lock (_initializationLock)
                _pendingRequests.TryGetValue(message.RequestId, out completion);
            completion?.TrySetResult(message);
        }
        catch (JsonException ex)
        {
            Log.Debug(ex, "Ignored an invalid message from the MangaDex WebView2.");
        }
    }

    private async Task AbortRequestAsync(WebView2 browser, string requestId, CancellationToken cancellationToken)
    {
        lock (_initializationLock)
        {
            if (_pendingRequests.Remove(requestId, out var completion))
                completion.TrySetCanceled(cancellationToken);
        }
        try
        {
            await OnBrowserThreadAsync(browser,
                () => browser.CoreWebView2.ExecuteScriptAsync($"window.__seumangaRequests?.[{JsonSerializer.Serialize(requestId)}]?.abort()"),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not cancel a MangaDex WebView2 request.");
        }
    }

    private static bool IsAllowedHost(string host) =>
        host.Equals("api.mangadex.org", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("uploads.mangadex.org", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".mangadex.network", StringComparison.OrdinalIgnoreCase);

    private sealed record BrowserResponse(string RequestId, int Status, string? ContentType, bool IsBase64, string Body, string? Error);
}

public sealed class MangaDexBrowserHttpMessageHandler(MangaDexBrowserTransport transport) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        transport.SendAsync(request, cancellationToken);
}
