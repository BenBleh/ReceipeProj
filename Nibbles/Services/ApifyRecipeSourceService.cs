using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nibbles.Services;

public sealed class ApifyRecipeSourceService(Func<HttpMessageHandler> handlerFactory, Func<string?> tokenProvider)
{
    private const decimal MetadataCap = 0.03m;
    private const decimal ImageCap = 0.05m;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? stoppedReason;

    internal static string? ReadPrivateToken()
    {
        using var stream = typeof(ApifyRecipeSourceService).Assembly.GetManifestResourceStream("Nibbles.ApifyToken");
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        var matches = Regex.Matches(reader.ReadToEnd(), @"apify_api_[A-Za-z0-9_-]+",
            RegexOptions.CultureInvariant, RegexTimeout).Select(match => match.Value).Distinct().ToList();
        return matches.Count == 1 ? matches[0] :
            throw new RecipeSourceException("The private Apify build input is invalid. Rebuild with one Apify API token.");
    }

    public async Task<RecipeSourceDraft> FetchAsync(Uri source, CancellationToken cancellationToken, bool includeImage)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await FetchCoreAsync(source, cancellationToken, includeImage); }
        finally { gate.Release(); }
    }

    private async Task<RecipeSourceDraft> FetchCoreAsync(Uri source, CancellationToken cancellationToken, bool includeImage)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = tokenProvider();
        if (token is null || !Regex.IsMatch(token, @"^apify_api_[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant, RegexTimeout))
            throw new RecipeSourceException("Social fetching needs the private Apify build input. Rebuild with NIBBLES_APIFY_TOKEN_FILE, or paste the caption instead.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(4));
        using var handler = handlerFactory();
        using var client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        var cleanSource = new UriBuilder(source) { Query = "", Fragment = "" }.Uri;
        var instagram = RecipeSourceService.IsInstagram(source);
        var expectedId = instagram ? RecipeSourceService.InstagramCode(source) : RecipeSourceService.TikTokVideoId(source);
        try
        {
            await CheckCreditAsync(MetadataCap + (includeImage ? ImageCap : 0));
            var reel = instagram && source.AbsolutePath.StartsWith("/reel/", StringComparison.Ordinal);
            var actor = instagram ? reel ? "apify~instagram-reel-scraper" : "apify~instagram-post-scraper"
                : "clockworks~tiktok-video-scraper";
            var input = instagram ? new Dictionary<string, object>
            {
                ["username"] = new[] { cleanSource.AbsoluteUri }, ["resultsLimit"] = 1
            } : new Dictionary<string, object>
            {
                ["postURLs"] = new[] { cleanSource.AbsoluteUri }, ["scrapeRelatedVideos"] = false,
                ["resultsPerPage"] = 1, ["shouldDownloadVideos"] = false, ["shouldDownloadCovers"] = false,
                ["shouldDownloadSlideshowImages"] = false, ["downloadSubtitlesOptions"] = "NEVER_DOWNLOAD_SUBTITLES"
            };
            if (reel)
            {
                input["includeSharesCount"] = false;
                input["includeTranscript"] = false;
                input["includeDownloadedVideo"] = false;
            }
            else if (instagram)
            {
                input["includeMusicData"] = false;
                input["dataDetailLevel"] = "basicData";
            }
            var run = await RunAsync(actor, instagram ? reel ? "0.0.651" : "0.0.679" : "0.0.511",
                input, MetadataCap, 120);
            var rows = await JsonAsync(HttpMethod.Get, "/datasets/" + Identifier(run, "defaultDatasetId") +
                "/items?format=json&limit=2&clean=true", null, deadline.Token);
            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != 1)
                throw new RecipeSourceException("Apify did not return one unambiguous social post. Paste its caption instead.");
            var row = rows[0];
            var id = Text(row, instagram ? "shortCode" : "id");
            var canonical = Text(row, instagram ? "url" : "webVideoUrl");
            if (!RecipeSourceService.TrySourceUrl(canonical, out var canonicalUrl) ||
                RecipeSourceService.IsInstagram(canonicalUrl) != instagram ||
                !RecipeSourceService.IsSocialSource(canonical) ||
                id.Length == 0 || (expectedId is not null && id != expectedId) ||
                (instagram ? RecipeSourceService.InstagramCode(canonicalUrl) : RecipeSourceService.TikTokVideoId(canonicalUrl)) != id ||
                (expectedId is null && Text(row, "submittedVideoUrl") != cleanSource.AbsoluteUri) ||
                (reel && (Text(row, "type") != "Video" || Text(row, "productType") != "clips")))
                throw new RecipeSourceException("Apify returned a different or unavailable post. Paste the requested post's caption instead.");
            var draft = RecipeSourceParser.Caption(Text(row, instagram ? "caption" : "text"));
            cancellationToken.ThrowIfCancellationRequested();
            if (!includeImage) return draft;
            var cover = instagram ? Text(row, "displayUrl") : Text(Property(row, "videoMeta"), "coverUrl");
            try
            {
                if (!RecipeSourceService.TryPublicUrl(cover, out var coverUrl) || !IsCoverHost(coverUrl.IdnHost, instagram))
                    throw new RecipeSourceException("No supported cover was returned for the matched post.");
                await CheckCreditAsync(ImageCap);
                var imageRun = await RunAsync("automa-flow~parallel-bulk-file-downloader", "0.1.27",
                    new
                    {
                        urls = new[] { new { url = cover, fileName = id + ".jpg" } },
                        urlsText = "", treatHtmlAs = "file", concurrency = 1,
                        maxFileSizeMb = 5, timeoutSeconds = 15, maxRetries = 0, zip = false
                    }, ImageCap, 90);
                var manifest = await JsonAsync(HttpMethod.Get, "/datasets/" + Identifier(imageRun, "defaultDatasetId") +
                    "/items?format=json&limit=2&clean=true", null, deadline.Token);
                if (manifest.ValueKind != JsonValueKind.Array || manifest.GetArrayLength() != 1)
                    throw new RecipeSourceException("Apify did not return one stored cover.");
                var file = manifest[0];
                var store = Identifier(imageRun, "defaultKeyValueStoreId");
                if (Text(file, "status") != "SUCCESS" || Number(file, "http_status") != 200 ||
                    Number(file, "attempts") != 1 || Text(file, "source_url") != cover ||
                    Text(file, "file_name") != id + ".jpg" || Text(file, "kvs_key").Length == 0 ||
                    (Text(file, "file_store_id").Length > 0 && Text(file, "file_store_id") != store))
                    throw new RecipeSourceException("The cover download failed or could not be matched to this post. No automatic retry was made.");
                using var response = await SendAsync(HttpMethod.Get, "/key-value-stores/" + store +
                    "/records/" + Uri.EscapeDataString(Text(file, "kvs_key")), null, deadline.Token);
                var bytes = await RecipeSourceService.ReadResponseBytesAsync(response, 6_000_000, deadline.Token);
                if (!RecipeSourceService.IsSupportedImage(bytes, response.Content.Headers.ContentType?.MediaType) ||
                    !Convert.ToHexString(SHA256.HashData(bytes)).Equals(Text(file, "sha256"), StringComparison.OrdinalIgnoreCase) ||
                    Number(file, "bytes") != bytes.Length)
                    throw new RecipeSourceException("The stored cover failed its image or integrity check.");
                cancellationToken.ThrowIfCancellationRequested();
                return draft with { ImageBytes = bytes };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return PhotoUnavailable(draft, "The Apify photo lookup timed out.");
            }
            catch (Exception exception) when (exception is RecipeSourceException or HttpRequestException or IOException or JsonException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                System.Diagnostics.Debug.WriteLine($"Apify cover unavailable: {exception.GetType().Name}");
                return PhotoUnavailable(draft, exception is RecipeSourceException ? exception.Message : "Apify's stored cover could not be read.");
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RecipeSourceException("The Apify lookup timed out. Your edits are unchanged; paste the caption or try later.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            throw new RecipeSourceException("Apify could not return a readable post. Your edits are unchanged; paste the caption instead. Any work already started may use free credit.", exception);
        }

        async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
        {
            if (stoppedReason is not null) throw new RecipeSourceException(stoppedReason);
            using var request = new HttpRequestMessage(method, new Uri("https://api.apify.com/v2" + path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.UserAgent.ParseAdd("NibblesSource/1.0");
            request.Headers.AcceptEncoding.ParseAdd("identity");
            if (body is not null) request.Content = JsonContent.Create(body);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                var message = status switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Apify refused access. Check the private API token and rebuild.",
                    HttpStatusCode.TooManyRequests => "Apify's request limit was reached. No automatic retry was made.",
                    HttpStatusCode.PaymentRequired => "Apify's free credit is unavailable. No paid fallback was used.",
                    _ => $"Apify returned HTTP {(int)status}. No automatic retry or redirect was made."
                };
                if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                    stoppedReason = message + " Further Apify requests are stopped until the app restarts.";
                throw new RecipeSourceException(stoppedReason ?? message);
            }
            return response;
        }

        async Task<JsonElement> JsonAsync(HttpMethod method, string path, object? body, CancellationToken ct)
        {
            using var response = await SendAsync(method, path, body, ct);
            if (response.Content.Headers.ContentType?.MediaType != "application/json")
                throw new RecipeSourceException("Apify returned an unsupported API response.");
            var bytes = await RecipeSourceService.ReadResponseBytesAsync(response, 4_000_000, ct);
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.Clone();
        }

        async Task CheckCreditAsync(decimal required)
        {
            var user = Property(await JsonAsync(HttpMethod.Get, "/users/me", null, deadline.Token), "data");
            var plan = Property(user, "plan");
            if (Text(plan, "id") != "FREE" || Property(user, "isPaying").ValueKind != JsonValueKind.False)
                throw new RecipeSourceException("Social fetching is limited to the non-paying Apify Free plan. No paid lookup was started.");
            var limits = Property(await JsonAsync(HttpMethod.Get, "/users/me/limits", null, deadline.Token), "data");
            var remaining = Math.Min(Decimal(plan, "monthlyUsageCreditsUsd"), Decimal(Property(limits, "limits"), "maxMonthlyUsageUsd"))
                - Decimal(Property(limits, "current"), "monthlyUsageUsd");
            if (remaining < required)
                throw new RecipeSourceException("There is not enough Apify free credit for this bounded lookup. Paste the caption or choose a photo manually.");
        }

        async Task<JsonElement> RunAsync(string actor, string build, object input, decimal cap, int timeout)
        {
            string? runId = null;
            var terminal = false;
            try
            {
                // Never retry a run submission: an interrupted response may already have incurred charges.
                var run = Property(await JsonAsync(HttpMethod.Post, "/actors/" + actor + "/runs?build=" + build +
                    "&timeout=" + timeout + "&maxTotalChargeUsd=" + cap.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    "&restartOnError=false&forcePermissionLevel=LIMITED_PERMISSIONS", input, deadline.Token), "data");
                runId = Identifier(run, "id");
                for (var poll = 0; poll < 10; poll++)
                {
                    var status = Text(run, "status");
                    terminal = status is "SUCCEEDED" or "FAILED" or "ABORTED" or "TIMED-OUT";
                    if (terminal)
                    {
                        if (status != "SUCCEEDED")
                            throw new RecipeSourceException("The Apify lookup did not succeed. No automatic retry was made; paste the caption or choose a photo manually.");
                        return run;
                    }
                    if (poll == 9) break;
                    run = Property(await JsonAsync(HttpMethod.Get, "/actor-runs/" + runId + "?waitForFinish=20",
                        null, deadline.Token), "data");
                    if (Identifier(run, "id") != runId)
                        throw new RecipeSourceException("Apify returned a different run while waiting for the lookup.");
                }
                throw new RecipeSourceException("The Apify lookup did not finish within its polling limit.");
            }
            finally
            {
                if (runId is not null && !terminal && stoppedReason is null)
                {
                    using var abort = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        using var response = await SendAsync(HttpMethod.Post, "/actor-runs/" + runId + "/abort", null, abort.Token);
                    }
                    catch (Exception exception) when (exception is RecipeSourceException or HttpRequestException or IOException or OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"Apify run abort was not confirmed; run timeout and charge cap still apply: {exception.GetType().Name}");
                    }
                }
            }
        }
    }

    private static RecipeSourceDraft PhotoUnavailable(RecipeSourceDraft draft, string reason) =>
        draft with { Warnings = draft.Warnings.Append($"Photo not imported: {reason} You can choose a photo manually.").ToList() };

    private static bool IsCoverHost(string host, bool instagram) =>
        (instagram ? new[] { "cdninstagram.com", "fbcdn.net" } : new[] { "tiktokcdn.com", "tiktokcdn-us.com", "tiktokcdn-eu.com" })
        .Any(suffix => host.Equals(suffix, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase));

    private static JsonElement Property(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) ? value : default;
    private static string Text(JsonElement node, string key) => Property(node, key) is { ValueKind: JsonValueKind.String } value ? value.GetString()! : "";
    private static int? Number(JsonElement node, string key) =>
        Property(node, key) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number) ? number : null;
    private static decimal Decimal(JsonElement node, string key) =>
        Property(node, key) is { ValueKind: JsonValueKind.Number } value && value.TryGetDecimal(out var number) && number >= 0 ? number :
        throw new RecipeSourceException("Apify's free-credit allowance could not be verified. No paid lookup was started.");
    private static string Identifier(JsonElement node, string key)
    {
        var value = Text(node, key);
        return Regex.IsMatch(value, @"^[A-Za-z0-9]{17}$", RegexOptions.CultureInvariant, RegexTimeout) ? value :
            throw new RecipeSourceException("Apify returned an invalid run or storage reference.");
    }
}
