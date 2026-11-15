using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Nibbles.Services;

public sealed class RecipeSourceService
{
    private const string Agent = "NibblesSource";
    private const int BodyLimit = 8_000_000;
    private const int ImageLimit = 6_000_000;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly HashSet<string> TikTokHosts = new(StringComparer.OrdinalIgnoreCase)
        { "tiktok.com", "www.tiktok.com", "m.tiktok.com", "vm.tiktok.com", "vt.tiktok.com" };
    private readonly ConcurrentDictionary<string, string> stoppedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (DateTimeOffset Time, string? Policy)> policies = new();
    private readonly Func<HttpMessageHandler> handlerFactory;
    private readonly ApifyRecipeSourceService social;

    public RecipeSourceService() : this(CreateHandler, ApifyRecipeSourceService.ReadPrivateToken) { }
    public RecipeSourceService(Func<HttpMessageHandler> handlerFactory) : this(handlerFactory, () => null) { }
    public RecipeSourceService(Func<HttpMessageHandler> handlerFactory, Func<string?> apifyToken)
    {
        this.handlerFactory = handlerFactory;
        social = new(handlerFactory, apifyToken);
    }

    public static bool IsSocialSource(string? value) =>
        TrySourceUrl(value, out var uri) && (IsInstagram(uri) || TikTokHosts.Contains(uri.IdnHost));

    public static bool TrySourceUrl(string? value, out Uri uri)
    {
        if (!TryPublicUrl(value, out uri)) return false;
        return (!IsInstagram(uri) || InstagramCode(uri) is not null) &&
            (!TikTokHosts.Contains(uri.IdnHost) || IsTikTokUrl(uri));
    }

    internal static bool TryPublicUrl(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps || parsed.Port != 443 || parsed.UserInfo.Length > 0 ||
            parsed.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))
            return false;
        var host = parsed.IdnHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(host.Trim('[', ']'), out var address) && !IsPublicAddress(address)))
            return false;
        uri = new UriBuilder(parsed) { Fragment = "" }.Uri;
        return true;
    }

    public async Task<RecipeSourceDraft> FetchAsync(string source, CancellationToken cancellationToken, bool includeImage = true)
    {
        if (!TrySourceUrl(source, out var url))
            throw new RecipeSourceException("Use a public HTTPS recipe or Instagram/TikTok post URL, without credentials or a custom port.");
        if (IsInstagram(url) || TikTokHosts.Contains(url.IdnHost))
            return await social.FetchAsync(url, cancellationToken, includeImage);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        using var handler = handlerFactory();
        using var client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Agent + "/1.0");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.8");
        var requests = 0;
        var nextRequest = DateTimeOffset.MinValue;
        async Task<(HttpStatusCode Status, string Body, Uri? Redirect, byte[]? Image)> Send(Uri target, bool image = false)
        {
            if (!TryPublicUrl(target.AbsoluteUri, out _))
                throw new RecipeSourceException("A redirect or source request points to an unsupported address.");
            var host = target.IdnHost.TrimEnd('.').Replace("www.", "", StringComparison.OrdinalIgnoreCase);
            if (stoppedHosts.TryGetValue(host, out var reason)) throw new RecipeSourceException(reason);
            if (++requests > 10) throw new RecipeSourceException("The lookup reached its request limit. Paste the recipe text instead.");
            var wait = nextRequest - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, deadline.Token);
            nextRequest = DateTimeOffset.UtcNow.AddSeconds(1);
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            if (image) request.Headers.Accept.ParseAdd("image/jpeg,image/png,image/webp;q=0.8");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                var reasonText = image
                    ? $"The photo host restricted automated access (HTTP {(int)response.StatusCode}). Photo requests to this host are stopped until the app restarts."
                    : $"This source restricted automated access (HTTP {(int)response.StatusCode}). Further fetches to this host are stopped until the app restarts. Paste its text instead.";
                stoppedHosts[host] = reasonText;
                throw new RecipeSourceException(reasonText);
            }
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (response.Headers.Location is null)
                    throw new RecipeSourceException("The source redirect has no destination. Paste its text instead.");
                return (response.StatusCode, "", new Uri(target, response.Headers.Location), null);
            }
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                throw new RecipeSourceException($"The source returned HTTP {(int)response.StatusCode}. Paste its text instead.");
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (image)
            {
                if (!response.IsSuccessStatusCode || mediaType is not ("image/jpeg" or "image/png" or "image/webp"))
                    throw new RecipeSourceException("The source photo is unavailable or uses an unsupported format.");
                var bytes = await ReadResponseBytesAsync(response, ImageLimit, deadline.Token);
                if (!IsSupportedImage(bytes, mediaType))
                    throw new RecipeSourceException("The source photo did not contain a supported image.");
                return (response.StatusCode, "", null, bytes);
            }
            var limit = target.AbsolutePath == "/robots.txt" ? 1_000_000 : BodyLimit;
            var body = await ReadBodyAsync(response, limit, deadline.Token);
            if (response.IsSuccessStatusCode && target.AbsolutePath != "/robots.txt" &&
                mediaType is not ("text/html" or "application/xhtml+xml"))
                throw new RecipeSourceException("The source returned an unsupported response. Paste the recipe text instead.");
            return (response.StatusCode, body, null, null);
        }
        async Task Robots(Uri target)
        {
            var origin = target.GetLeftPart(UriPartial.Authority);
            if (!policies.TryGetValue(origin, out var cached) || DateTimeOffset.UtcNow - cached.Time > TimeSpan.FromHours(1))
            {
                var robotsUrl = new Uri(origin + "/robots.txt");
                (HttpStatusCode Status, string Body, Uri? Redirect, byte[]? Image) result = (HttpStatusCode.NotFound, "", null, null);
                for (var redirect = 0; redirect < 3; redirect++)
                {
                    result = await Send(robotsUrl);
                    if (result.Redirect is null) break;
                    robotsUrl = result.Redirect;
                    if (redirect == 2) throw new RecipeSourceException("The robots policy redirected too many times.");
                }
                if (result.Status != HttpStatusCode.OK && result.Status != HttpStatusCode.NotFound)
                    throw new RecipeSourceException("The site's automated-access policy is unavailable. Paste its text instead.");
                if (result.Status == HttpStatusCode.OK &&
                    Regex.IsMatch(result.Body[..Math.Min(1000, result.Body.Length)], @"<(?:!doctype|html)\b", RegexOptions.IgnoreCase, RegexTimeout))
                    throw new RecipeSourceException("The site's automated-access policy could not be read. Paste its text instead.");
                cached = (DateTimeOffset.UtcNow, result.Status == HttpStatusCode.NotFound ? null : result.Body);
                policies[origin] = cached;
            }
            if (cached.Policy is not null)
            {
                var delay = RobotsDelay(cached.Policy, target.PathAndQuery);
                nextRequest = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, delay));
            }
        }
        async Task<RecipeSourceDraft> WithImage(RecipeSourceDraft draft, Uri page)
        {
            if (!includeImage) return draft;
            var reason = "No source photo was available.";
            foreach (var candidate in draft.ImageCandidates.Distinct(StringComparer.Ordinal).Take(3))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!Uri.TryCreate(page, candidate, out var imageUrl) || !TryPublicUrl(imageUrl.AbsoluteUri, out imageUrl))
                        throw new RecipeSourceException("The source photo points to an unsupported address.");
                    for (var redirect = 0; redirect < 3; redirect++)
                    {
                        try { await Robots(imageUrl); }
                        catch (RecipeSourceException exception)
                        {
                            throw new RecipeSourceException("The photo host's automated-access policy could not be satisfied.", exception);
                        }
                        var response = await Send(imageUrl, image: true);
                        if (response.Redirect is { } target)
                        {
                            if (!TryPublicUrl(target.AbsoluteUri, out imageUrl))
                                throw new RecipeSourceException("The source photo redirected to an unsupported address.");
                            continue;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        return draft with { ImageBytes = response.Image };
                    }
                    reason = "The source photo redirected too many times.";
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    reason = "The source photo timed out.";
                    break;
                }
                catch (Exception exception) when (exception is RecipeSourceException or HttpRequestException or IOException or SocketException)
                {
                    reason = exception.Message;
                    System.Diagnostics.Debug.WriteLine($"Source image unavailable: {exception.GetType().Name}");
                }
            }
            return draft with { Warnings = draft.Warnings.Append($"Photo not imported: {reason} You can choose a photo manually.").ToList() };
        }
        try
        {
            for (var redirect = 0; redirect < 4; redirect++)
            {
                await Robots(url);
                var response = await Send(url);
                if (response.Redirect is { } target)
                {
                    if (!TrySourceUrl(target.AbsoluteUri, out target))
                        throw new RecipeSourceException("The source redirected to an unsupported address.");
                    if (IsInstagram(target) || TikTokHosts.Contains(target.IdnHost))
                        throw new RecipeSourceException("This page redirects to a social post. Use its direct post URL.");
                    url = target;
                    continue;
                }
                if (response.Status != HttpStatusCode.OK)
                    throw new RecipeSourceException($"The source returned HTTP {(int)response.Status}. Paste its text instead.");
                var draft = RecipeSourceParser.Website(response.Body);
                return await WithImage(draft, url);
            }
            throw new RecipeSourceException("The source redirected too many times. Paste the recipe text instead.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RecipeSourceException("The lookup timed out. Your edits are unchanged; paste the text or try later.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or SocketException)
        {
            throw new RecipeSourceException("The source could not be reached. Your edits are unchanged; paste its text or try later.", exception);
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new RecipeSourceException("The source text could not be parsed within the lookup limit. Paste a shorter recipe description.", exception);
        }
    }

    internal static bool IsInstagram(Uri uri) =>
        uri.IdnHost.Equals("instagram.com", StringComparison.OrdinalIgnoreCase) ||
        uri.IdnHost.Equals("www.instagram.com", StringComparison.OrdinalIgnoreCase);
    internal static string? InstagramCode(Uri uri)
    {
        var match = Regex.Match(uri.AbsolutePath, @"^/(?:p|reel)/([A-Za-z0-9_-]+)/?$", RegexOptions.None, RegexTimeout);
        return match.Success ? match.Groups[1].Value : null;
    }
    internal static string? TikTokVideoId(Uri uri)
    {
        var match = Regex.Match(uri.AbsolutePath, @"^/(?:@[^/]*/)?video/(\d+)/?$", RegexOptions.None, RegexTimeout);
        return match.Success ? match.Groups[1].Value : null;
    }
    private static bool IsTikTokUrl(Uri uri) => TikTokVideoId(uri) is not null ||
        ((uri.IdnHost is "vm.tiktok.com" or "vt.tiktok.com") &&
            Regex.IsMatch(uri.AbsolutePath, @"^/[A-Za-z0-9]+/?$", RegexOptions.None, RegexTimeout)) ||
        Regex.IsMatch(uri.AbsolutePath, @"^/t/[A-Za-z0-9]+/?$", RegexOptions.None, RegexTimeout);

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xe0) == 0x20 && !(bytes[0] == 0x20 && bytes[1] == 0x01 &&
                (bytes[2] < 2 || (bytes[2] == 0x0d && bytes[3] == 0xb8))) &&
                !(bytes[0] == 0x20 && bytes[1] == 0x02) &&
                !(bytes[0] == 0x3f && bytes[1] == 0xff && (bytes[2] & 0xf0) == 0);
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        return bytes[0] != 0 && bytes[0] != 10 && bytes[0] != 127 && bytes[0] < 224 &&
            !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
            !(bytes[0] == 169 && bytes[1] == 254) &&
            !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
            !(bytes[0] == 192 && (bytes[1] == 168 || bytes[1] == 0 ||
                (bytes[1] == 2) || (bytes[1] == 88 && bytes[2] == 99))) &&
            !(bytes[0] == 198 && (bytes[1] is 18 or 19 || (bytes[1] == 51 && bytes[2] == 100))) &&
            !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
    }

    private static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = true,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            if (context.DnsEndPoint.Port != 443 || addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
                throw new RecipeSourceException("Source fetch cannot contact local, private or reserved network addresses.");
            var address = addresses.OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).First();
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, 443), token);
                // HttpClient negotiates TLS with the original URL hostname over this pinned socket.
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, int limit, CancellationToken token)
    {
        var bytes = await ReadResponseBytesAsync(response, limit, token);
        var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
        try { return (string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset)).GetString(bytes); }
        catch (ArgumentException exception) { throw new RecipeSourceException("The source text encoding is unsupported.", exception); }
    }

    internal static bool IsSupportedImage(byte[] bytes, string? mediaType) => mediaType switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    internal static async Task<byte[]> ReadResponseBytesAsync(HttpResponseMessage response, int limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit)
            throw new RecipeSourceException("The source response exceeded the size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = await ReadBoundedAsync(stream, limit, token);
        var encodings = response.Content.Headers.ContentEncoding.ToList();
        if (encodings.Count > 1) throw new RecipeSourceException("The source returned unsupported compressed data.");
        if (encodings.Count == 1 && encodings[0] != "identity")
        {
            using var input = new MemoryStream(bytes);
            using Stream decoder = encodings[0].ToLowerInvariant() switch
            {
                "gzip" => new GZipStream(input, CompressionMode.Decompress),
                "deflate" => new ZLibStream(input, CompressionMode.Decompress),
                _ => throw new RecipeSourceException("The source returned an unsupported content encoding.")
            };
            bytes = await ReadBoundedAsync(decoder, limit, token);
        }
        return bytes;
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[32_768];
        int count;
        while ((count = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, limit + 1 - (int)buffer.Length)), token)) > 0)
        {
            buffer.Write(chunk, 0, count);
            if (buffer.Length > limit) throw new RecipeSourceException("The source response exceeded the size limit.");
        }
        return buffer.ToArray();
    }

    public static double RobotsDelay(string policy, string path)
    {
        var groups = new List<(List<string> Agents, List<(bool Allow, string Path)> Rules, double Delay)>();
        var agents = new List<string>();
        var rules = new List<(bool Allow, string Path)>();
        var delay = 0d;
        var seenRule = false;
        void Flush()
        {
            if (agents.Count > 0) groups.Add((agents, rules, delay));
            agents = []; rules = []; delay = 0; seenRule = false;
        }
        foreach (var raw in policy.Split('\n'))
        {
            var line = raw.Split('#', 2)[0].Trim();
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            if (key == "user-agent")
            {
                if (seenRule) Flush();
                agents.Add(value);
            }
            else if (agents.Count > 0)
            {
                seenRule = true;
                if (key is "allow" or "disallow" && value.Length > 0) rules.Add((key == "allow", value));
                if (key == "crawl-delay" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                    delay = Math.Max(delay, seconds);
                if (key == "request-rate")
                {
                    var parts = value.Split('/');
                    if (parts.Length == 2 && double.TryParse(parts[0], CultureInfo.InvariantCulture, out var count) &&
                        double.TryParse(parts[1], CultureInfo.InvariantCulture, out var interval) && count > 0)
                        delay = Math.Max(delay, interval / count);
                }
            }
        }
        Flush();
        var scored = groups.Select(group => (Group: group, Score: group.Agents.Select(agent =>
            agent == "*" ? 0 : Agent.Contains(agent, StringComparison.OrdinalIgnoreCase) ? agent.Length : -1).Max())).ToList();
        var best = scored.Count == 0 ? -1 : scored.Max(group => group.Score);
        var applicable = scored.Where(group => best >= 0 && group.Score == best).Select(group => group.Group).ToList();
        var matching = applicable.SelectMany(group => group.Rules).Where(rule =>
        {
            var pattern = "^" + Regex.Escape(rule.Path.TrimEnd('$')).Replace("\\*", ".*") + (rule.Path.EndsWith('$') ? "$" : "");
            return Regex.IsMatch(path, pattern, RegexOptions.None, RegexTimeout);
        }).OrderByDescending(rule => rule.Path.Replace("*", "").TrimEnd('$').Length).ThenByDescending(rule => rule.Allow).ToList();
        if (matching.Count > 0 && !matching[0].Allow)
            throw new RecipeSourceException("The site's automated-access policy excludes this URL. Paste the recipe text instead.");
        var wait = applicable.Select(group => group.Delay).DefaultIfEmpty(0).Max();
        if (!double.IsFinite(wait) || wait > 10)
            throw new RecipeSourceException("The site requires a longer crawl delay. Paste the recipe text instead.");
        return wait;
    }
}
