using Nibbles.Services;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RecipeAPI.Tests;

public class ApifyRecipeSourceTests
{
    private const string Caption = "Chicken dinner\nIngredients:\n- 200g chicken\n- 1 tbsp oil\nMethod:\n1. Heat the oil.\n2. Cook the chicken.";
    private const string Token = "apify_api_test_only_not_a_real_key";
    private static readonly byte[] Photo = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jT1sAAAAASUVORK5CYII=");

    [Theory]
    [InlineData("reel", "apify~instagram-reel-scraper")]
    [InlineData("post", "apify~instagram-post-scraper")]
    [InlineData("tiktok", "clockworks~tiktok-video-scraper")]
    [InlineData("short", "clockworks~tiktok-video-scraper")]
    public async Task SocialPlatformsShareStoredCoverDeliveryWithoutContactingSource(string kind, string actor)
    {
        var fixture = new Scenario(kind);
        var draft = await fixture.Service.FetchAsync(fixture.Source + "?tracking=private", CancellationToken.None);
        Assert.Equal("Chicken dinner", draft.Text.Title);
        Assert.Equal(2, draft.Text.Ingredients.Count);
        Assert.Equal(2, draft.Text.Steps.Count);
        Assert.Equal(Photo, draft.ImageBytes);
        Assert.Empty(draft.ImageCandidates);
        Assert.Null(draft.Text.ImagePath);
        Assert.Null(draft.Text.ImageData);
        Assert.DoesNotContain(draft.Warnings, warning => warning.StartsWith("Photo not imported:"));
        Assert.Equal(2, fixture.Inputs.Count);
        Assert.Contains(actor, fixture.Inputs[0].Url.AbsolutePath);
        var metadata = fixture.Inputs[0].Body;
        var submitted = metadata.GetProperty(kind is "reel" or "post" ? "username" : "postURLs")[0].GetString();
        Assert.DoesNotContain("tracking", submitted);
        if (kind == "reel")
        {
            Assert.False(metadata.GetProperty("includeTranscript").GetBoolean());
            Assert.False(metadata.GetProperty("includeDownloadedVideo").GetBoolean());
        }
        else if (kind is "tiktok" or "short")
        {
            Assert.False(metadata.GetProperty("scrapeRelatedVideos").GetBoolean());
            Assert.False(metadata.GetProperty("shouldDownloadVideos").GetBoolean());
            Assert.False(metadata.GetProperty("shouldDownloadCovers").GetBoolean());
            Assert.Equal("NEVER_DOWNLOAD_SUBTITLES", metadata.GetProperty("downloadSubtitlesOptions").GetString());
        }
        var images = fixture.Inputs[1].Body;
        Assert.Equal(0, images.GetProperty("maxRetries").GetInt32());
        Assert.Equal(1, images.GetProperty("concurrency").GetInt32());
        Assert.Single(images.GetProperty("urls").EnumerateArray());
        Assert.Equal(fixture.Cover, images.GetProperty("urls")[0].GetProperty("url").GetString());
        Assert.Equal("file", images.GetProperty("treatHtmlAs").GetString());
        Assert.Contains("maxTotalChargeUsd=0.03", fixture.Inputs[0].Url.Query);
        Assert.Contains("maxTotalChargeUsd=0.05", fixture.Inputs[1].Url.Query);
        Assert.All(fixture.Inputs, input => Assert.Contains("restartOnError=false", input.Url.Query));
        Assert.All(fixture.Inputs, input => Assert.Contains("forcePermissionLevel=LIMITED_PERMISSIONS", input.Url.Query));
    }

    [Fact]
    public async Task ExistingPhotoSkipsAllImageWorkAndItsCreditCheck()
    {
        var fixture = new Scenario("tiktok");
        var draft = await fixture.Service.FetchAsync(fixture.Source, CancellationToken.None, includeImage: false);
        Assert.Null(draft.ImageBytes);
        Assert.Single(fixture.Inputs);
        Assert.Equal(1, fixture.AccountChecks);
        Assert.DoesNotContain(fixture.Paths, path => path.Contains("/records/"));
    }

    [Theory]
    [InlineData("reel", "shortCode", "Different")]
    [InlineData("reel", "url", "https://www.instagram.com/p/Different/")]
    [InlineData("reel", "url", "https://example.com/p/Post123/")]
    [InlineData("reel", "productType", "unrelated")]
    [InlineData("tiktok", "id", "999999")]
    [InlineData("tiktok", "webVideoUrl", "https://www.tiktok.com/@cook/video/999999")]
    [InlineData("short", "submittedVideoUrl", "https://vm.tiktok.com/Different/")]
    public async Task MismatchedPostCannotProduceRecipeOrImage(string kind, string key, string value)
    {
        var fixture = new Scenario(kind);
        fixture.Metadata[key] = value;
        await Assert.ThrowsAsync<RecipeSourceException>(() =>
            fixture.Service.FetchAsync(fixture.Source, CancellationToken.None));
        Assert.Single(fixture.Inputs);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("paying")]
    [InlineData("exhausted")]
    [InlineData("missing")]
    public async Task UnverifiedFreeAllowanceNeverStartsAnActor(string problem)
    {
        var fixture = new Scenario("reel") { CreditProblem = problem };
        var error = await Assert.ThrowsAsync<RecipeSourceException>(() =>
            fixture.Service.FetchAsync(fixture.Source, CancellationToken.None));
        Assert.Empty(fixture.Inputs);
        Assert.DoesNotContain(Token, error.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ApiRefusalsStopSubsequentRequestsWithoutDirectFallback(HttpStatusCode status)
    {
        var fixture = new Scenario("tiktok") { ApiFailure = status };
        for (var attempt = 0; attempt < 2; attempt++)
            await Assert.ThrowsAsync<RecipeSourceException>(() =>
                fixture.Service.FetchAsync(fixture.Source, CancellationToken.None));
        Assert.Single(fixture.Paths);
        Assert.Empty(fixture.Inputs);
    }

    [Theory]
    [InlineData("cover")]
    [InlineData("source")]
    [InlineData("hash")]
    [InlineData("attempts")]
    [InlineData("status")]
    [InlineData("signature")]
    [InlineData("size")]
    [InlineData("redirect")]
    public async Task UnavailableOrUntrustedCoverKeepsCaptionAndReportsWarning(string problem)
    {
        var fixture = new Scenario("reel") { ImageProblem = problem };
        if (problem == "cover") fixture.Metadata["displayUrl"] = "https://127.0.0.1/private.png";
        if (problem == "source") fixture.Manifest["source_url"] = "https://scontent.cdninstagram.com/other.png";
        if (problem == "hash") fixture.Manifest["sha256"] = new string('0', 64);
        if (problem == "attempts") fixture.Manifest["attempts"] = 2;
        if (problem == "status") fixture.Manifest["status"] = "FAILED";
        var draft = await fixture.Service.FetchAsync(fixture.Source, CancellationToken.None);
        Assert.Equal(2, draft.Text.Ingredients.Count);
        Assert.Null(draft.ImageBytes);
        Assert.Contains(draft.Warnings, warning => warning.StartsWith("Photo not imported:"));
        Assert.Equal(problem == "cover" ? 1 : 2, fixture.Inputs.Count);
        Assert.True(fixture.Paths.Count(path => path.Contains("/records/")) <= 1);
    }

    [Fact]
    public async Task CancellationAbortsKnownMetadataRunAndNeverStartsImageRun()
    {
        using var cancel = new CancellationTokenSource();
        var fixture = new Scenario("tiktok") { CancelOnPoll = cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.FetchAsync(fixture.Source, cancel.Token));
        Assert.Single(fixture.Inputs);
        Assert.Single(fixture.Paths, path => path.EndsWith("/abort", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedRunIsNotRetriedOrUsedAsMetadata()
    {
        var fixture = new Scenario("reel") { MetadataStatus = "FAILED" };
        await Assert.ThrowsAsync<RecipeSourceException>(() =>
            fixture.Service.FetchAsync(fixture.Source, CancellationToken.None));
        Assert.Single(fixture.Inputs);
        Assert.DoesNotContain(fixture.Paths, path => path.StartsWith("/v2/datasets/"));
    }

    [Fact]
    public async Task CancelledStoredImageCannotReturnAnAppliedDraft()
    {
        using var cancel = new CancellationTokenSource();
        var fixture = new Scenario("reel") { CancelOnImage = cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.FetchAsync(fixture.Source, cancel.Token));
        Assert.Equal(2, fixture.Inputs.Count);
    }

    [Fact]
    public async Task CreditUsedUpAfterMetadataKeepsRecipeWithoutStartingImageRun()
    {
        var fixture = new Scenario("reel") { CreditProblem = "image" };
        var draft = await fixture.Service.FetchAsync(fixture.Source, CancellationToken.None);
        Assert.Single(fixture.Inputs);
        Assert.Null(draft.ImageBytes);
        Assert.Equal(2, draft.Text.Steps.Count);
        Assert.Contains(draft.Warnings, warning => warning.Contains("not enough Apify free credit"));
    }

    [Fact]
    public async Task AmbiguousActorSubmissionIsNotAutomaticallyRepeated()
    {
        var fixture = new Scenario("tiktok") { SubmissionFailure = true };
        var error = await Assert.ThrowsAsync<RecipeSourceException>(() =>
            fixture.Service.FetchAsync(fixture.Source, CancellationToken.None));
        Assert.Single(fixture.Inputs);
        Assert.Contains("already started may use free credit", error.Message);
        Assert.DoesNotContain(fixture.Paths, path => path.Contains("/actor-runs/"));
    }

    [Fact]
    public async Task MultipleMetadataResultsCannotBeSilentlySelected()
    {
        var fixture = new Scenario("reel") { DuplicatePost = true };
        await Assert.ThrowsAsync<RecipeSourceException>(() =>
            fixture.Service.FetchAsync(fixture.Source, CancellationToken.None));
        Assert.Single(fixture.Inputs);
    }

    private sealed class Scenario
    {
        private const string MetadataRun = "11111111111111111";
        private const string ImageRun = "22222222222222222";
        private const string MetadataDataset = "33333333333333333";
        private const string ImageDataset = "44444444444444444";
        private const string ImageStore = "55555555555555555";
        public string Source { get; }
        public string Cover { get; }
        public RecipeSourceService Service { get; }
        public Dictionary<string, object?> Metadata { get; }
        public Dictionary<string, object?> Manifest { get; }
        public List<(Uri Url, JsonElement Body)> Inputs { get; } = [];
        public List<string> Paths { get; } = [];
        public int AccountChecks { get; private set; }
        public string? CreditProblem { get; init; }
        public string? ImageProblem { get; init; }
        public HttpStatusCode? ApiFailure { get; init; }
        public string MetadataStatus { get; init; } = "SUCCEEDED";
        public bool SubmissionFailure { get; init; }
        public bool DuplicatePost { get; init; }
        public CancellationTokenSource? CancelOnPoll { get; init; }
        public CancellationTokenSource? CancelOnImage { get; init; }

        public Scenario(string kind)
        {
            var instagram = kind is "reel" or "post";
            var id = instagram ? "Post123" : "7374209506705788168";
            Source = kind switch
            {
                "reel" => "https://www.instagram.com/reel/Post123/",
                "post" => "https://www.instagram.com/p/Post123/",
                "short" => "https://vm.tiktok.com/AbC123/",
                _ => "https://www.tiktok.com/@cook/video/" + id
            };
            Cover = instagram ? "https://scontent.cdninstagram.com/cover.jpg?signature=test"
                : "https://p16-common-sign.tiktokcdn.com/cover.jpg?signature=test";
            Metadata = instagram ? new()
            {
                ["shortCode"] = id, ["url"] = "https://www.instagram.com/p/Post123/",
                ["caption"] = Caption, ["type"] = kind == "reel" ? "Video" : "Image",
                ["productType"] = "clips", ["displayUrl"] = Cover
            } : new()
            {
                ["id"] = id, ["webVideoUrl"] = "https://www.tiktok.com/@cook/video/" + id,
                ["submittedVideoUrl"] = Source, ["text"] = Caption,
                ["videoMeta"] = new { coverUrl = Cover }, ["isSlideshow"] = false
            };
            Manifest = new()
            {
                ["status"] = "SUCCESS", ["http_status"] = 200, ["attempts"] = 1,
                ["source_url"] = Cover, ["file_name"] = id + ".jpg",
                ["kvs_key"] = "cover.png", ["file_store_id"] = ImageStore,
                ["sha256"] = Convert.ToHexString(SHA256.HashData(Photo)).ToLowerInvariant(),
                ["bytes"] = Photo.Length, ["storage_url"] = "https://untrusted.example/never-follow"
            };
            Service = new(() => new Handler(Respond), () => Token);
        }

        private async Task<HttpResponseMessage> Respond(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!;
            var path = url.AbsolutePath;
            Paths.Add(path);
            Assert.Equal("https", url.Scheme);
            Assert.Equal("api.apify.com", url.Host);
            Assert.DoesNotContain(Token, url.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(Token, request.Headers.Authorization?.Parameter);
            Assert.Null(request.Headers.Referrer);
            if (ApiFailure is { } failure) return Json(new { error = "Never display this body: " + Token }, failure);
            if (path == "/v2/users/me")
            {
                AccountChecks++;
                return Json(new { data = new { isPaying = CreditProblem == "paying", plan = new
                {
                    id = CreditProblem == "paid" ? "BRONZE" : "FREE", monthlyUsageCreditsUsd = 5m
                } } });
            }
            if (path == "/v2/users/me/limits")
                return CreditProblem == "missing" ? Json(new { data = new { } }) :
                    Json(new { data = new { limits = new { maxMonthlyUsageUsd = 5m },
                        current = new { monthlyUsageUsd = CreditProblem == "exhausted" ||
                            (CreditProblem == "image" && AccountChecks > 1) ? 4.99m : 0m } } });
            if (path.StartsWith("/v2/actors/", StringComparison.Ordinal))
            {
                var input = await request.Content!.ReadAsStringAsync(ct);
                Assert.DoesNotContain(Token, input);
                using var document = JsonDocument.Parse(input);
                Inputs.Add((url, document.RootElement.Clone()));
                if (SubmissionFailure) throw new HttpRequestException("Uncertain submission.");
                return Json(new { data = Run(path.Contains("parallel-bulk-file-downloader"), "RUNNING") });
            }
            if (path.EndsWith("/abort", StringComparison.Ordinal))
                return Json(new { data = new { status = "ABORTED" } });
            if (path == "/v2/actor-runs/" + MetadataRun)
            {
                if (CancelOnPoll is { } cancel)
                {
                    cancel.Cancel();
                    throw new OperationCanceledException(cancel.Token);
                }
                return Json(new { data = Run(false, MetadataStatus) });
            }
            if (path == "/v2/actor-runs/" + ImageRun)
                return Json(new { data = Run(true, "SUCCEEDED") });
            if (path == "/v2/datasets/" + MetadataDataset + "/items")
                return Json(DuplicatePost ? new[] { Metadata, Metadata } : new[] { Metadata });
            if (path == "/v2/datasets/" + ImageDataset + "/items")
                return Json(new[] { Manifest });
            if (path == "/v2/key-value-stores/" + ImageStore + "/records/cover.png")
            {
                if (CancelOnImage is { } cancel)
                {
                    cancel.Cancel();
                    throw new OperationCanceledException(cancel.Token);
                }
                if (ImageProblem == "redirect")
                    return new(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://untrusted.example/never-follow") } };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new ByteArrayContent(ImageProblem == "signature" ? Encoding.UTF8.GetBytes("not an image") : Photo) };
                response.Content.Headers.ContentType = new("image/png");
                if (ImageProblem == "size") response.Content.Headers.ContentLength = 6_000_001;
                return response;
            }
            throw new InvalidOperationException("Unexpected API path: " + path);
        }

        private static object Run(bool image, string status) => new
        {
            id = image ? ImageRun : MetadataRun, status,
            defaultDatasetId = image ? ImageDataset : MetadataDataset,
            defaultKeyValueStoreId = ImageStore
        };
        private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return respond(request, cancellationToken);
            }
        }
    }
}
