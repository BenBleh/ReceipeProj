using Nibbles.Models;
using Nibbles.Services;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RecipeAPI.Tests;

public class RecipeSourceTests
{
    private const string Caption = """
        Lemon chicken & rice
        Ingredients:
        Chicken:
        - 2 chicken breasts
        - 1 tbsp olive oil
        Rice:
        - 200g rice
        - 400ml water
        Method:
        1. Coat the chicken with oil.
        2. Cook the rice and serve.
        3. Enjoy!
        #dinner
        """;
    private static string Html(object recipe) => "<script type='application/ld+json'>" + JsonSerializer.Serialize(recipe) + "</script>";
    private static object Metadata => new Dictionary<string, object>
    {
        ["@type"] = "Recipe",
        ["name"] = "Lemon chicken &amp; rice",
        ["recipeIngredient"] = new[] { "2 chicken breasts", "200g rice" },
        ["recipeInstructions"] = new object[]
        {
            new Dictionary<string, object>
            {
                ["@type"] = "HowToSection", ["name"] = "Chicken",
                ["itemListElement"] = new[] { new { text = "Cook the chicken." } }
            },
            new { text = "Cook the rice." }
        }
    };

    [Fact]
    public void WebsiteReadsGraphAndExcludesMethodSectionHeadings()
    {
        var html = Html(new Dictionary<string, object> { ["@graph"] = new[] { Metadata } });
        var draft = RecipeSourceParser.Website(html);
        Assert.Equal("Lemon chicken & rice", draft.Text.Title);
        Assert.Equal(2, draft.Text.Ingredients.Count);
        Assert.Equal(new[] { "Cook the chicken.", "Cook the rice." }, draft.Text.Steps.Select(step => step.Instructions));
        Assert.Equal("", draft.MissingFields);
    }

    [Fact]
    public void WebsiteGroupingRequiresExactIngredientCoverage()
    {
        var body = """
            <div class="wprm-recipe-ingredient-group">
              <h3 class="wprm-recipe-group-name">Chicken</h3>
              <div class="wprm-recipe-ingredient">2 chicken breasts</div>
            </div>
            <div class="wprm-recipe-ingredient-group">
              <h3 class="wprm-recipe-group-name">Rice</h3>
              <div class="wprm-recipe-ingredient">200g rice</div>
            </div>
            """;
        var draft = RecipeSourceParser.Website(Html(Metadata) + body);
        Assert.Equal(new[] { "Chicken", "Rice" }, draft.Text.IngredientGroups.Select(group => group.Name));
        Assert.Equal(draft.Text.IngredientGroups[1].Id, draft.Text.Ingredients[1].GroupId);
        var unmatched = RecipeSourceParser.Website(Html(Metadata) + body.Replace("200g", "300g"));
        Assert.Empty(unmatched.Text.IngredientGroups);
        Assert.Equal("200g rice", unmatched.Text.Ingredients[1].FQIngrediantDescription);
        Assert.Contains(unmatched.Warnings, warning => warning.Contains("Grouping"));
    }

    [Fact]
    public void MultipleOrMalformedRecipesRequirePasteRatherThanGuessing()
    {
        Assert.Throws<RecipeSourceException>(() => RecipeSourceParser.Website(Html(new[] { Metadata, Metadata })));
        Assert.Throws<RecipeSourceException>(() => RecipeSourceParser.Website("<script type='application/ld+json'>{broken</script>"));
    }

    [Fact]
    public void CaptionPreservesGroupsAndOmitsSignoffAndNutrition()
    {
        var draft = RecipeSourceParser.Caption(Caption + "\nServes 4\n400 calories\nPRO 40g CHO 10g FAT 2g");
        Assert.Equal("Lemon chicken & rice", draft.Text.Title);
        Assert.Equal(4, draft.Text.Ingredients.Count);
        Assert.Equal(2, draft.Text.IngredientGroups.Count);
        Assert.Equal(2, draft.Text.Steps.Count);
        Assert.DoesNotContain(draft.Text.Steps, step => step.Instructions!.Contains("Enjoy"));
        Assert.Equal("", draft.MissingFields);
    }

    [Fact]
    public void CollapsedCaptionAndFractionsRemainSourceText()
    {
        var draft = RecipeSourceParser.Caption("Chicken dinner\nIngredients: Sauce: - 1 1/2 tbsp oil - 1/2 cup stock  Rice: - 200g rice Method: 1. Heat the sauce. 2. Cook the rice.");
        Assert.Equal(3, draft.Text.Ingredients.Count);
        Assert.Equal(2, draft.Text.IngredientGroups.Count);
        Assert.Equal("1 1/2 tbsp oil", draft.Text.Ingredients[0].FQIngrediantDescription);
        Assert.Equal(2, draft.Text.Steps.Count);
    }

    [Fact]
    public void MissingMethodIsNotInvented()
    {
        var draft = RecipeSourceParser.Caption("Chicken dinner\nIngredients:\n- 2 chicken breasts\n- 1 tbsp oil");
        Assert.Empty(draft.Text.Steps);
        Assert.Equal("method", draft.MissingFields);
        Assert.Contains(draft.Warnings, warning => warning.StartsWith("Missing method"));
    }

    [Fact]
    public void WebsiteGroupingIgnoresOnlyPresentationParenthesesAndCheckboxes()
    {
        var metadata = new Dictionary<string, object>
        {
            ["@type"] = "Recipe",
            ["name"] = "Chicken",
            ["recipeIngredient"] = new[] { "600 g chicken ((or thighs))", "2 cups milk (, any fat % (Note 1))" }
        };
        var draft = RecipeSourceParser.Website(Html(metadata) + """
            <div class="wprm-recipe-ingredient-group">
              <h3 class="wprm-recipe-group-name">Filling</h3>
              <div class="wprm-recipe-ingredient"> &#9634; 600 g chicken (or thighs)</div>
              <div class="wprm-recipe-ingredient"> &#9634; 2 cups milk , any fat % (Note 1)</div>
            </div>
            """);
        Assert.Single(draft.Text.IngredientGroups);
        Assert.Equal("600 g chicken ((or thighs))", draft.Text.Ingredients[0].FQIngrediantDescription);
    }

    [Fact]
    public void ApplyOnlyReplacesRecipeTextAndKeepsIdentityPhotoNotesAndMetadata()
    {
        var target = new Recipe
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Old title",
            Classification = RecipeClassifications.Mains,
            Source = "My source",
            Notes = "Keep",
            ImagePath = "selected.jpg",
            ImageData = "photo",
            TimeToComplete = "PT20M",
            Ingredients = [new Ingredient { FQIngrediantDescription = "Old ingredient" }],
            Steps = [new Step { Instructions = "Old method", ImageData = "old step photo" }]
        };
        var id = target.Id;
        var draft = RecipeSourceParser.Caption(Caption);
        draft.ApplyTo(target);
        Assert.Equal(id, target.Id);
        Assert.Equal("My source", target.Source);
        Assert.Equal(RecipeClassifications.Mains, target.Classification);
        Assert.Equal("Keep", target.Notes);
        Assert.Equal("selected.jpg", target.ImagePath);
        Assert.Equal("photo", target.ImageData);
        Assert.Equal("PT20M", target.TimeToComplete);
        Assert.All(target.Steps, step => Assert.Null(step.ImageData));
        Assert.Equal(4, target.Ingredients.Count);
        RecipeWritePreparation.ForSave(target);
    }

    [Fact]
    public void PartialReplacementClearsOldMethodAndAddsEditableEmptyStep()
    {
        var target = new Recipe { Title = "Old", Steps = [new Step { Instructions = "Old instructions" }] };
        RecipeSourceParser.Caption("Chicken dinner\nIngredients:\n- 2 chicken breasts").ApplyTo(target);
        Assert.Single(target.Steps);
        Assert.True(string.IsNullOrWhiteSpace(target.Steps[0].Instructions));
        Assert.False(RecipeSourceDraft.HasRecipeText(new Recipe { Ingredients = [new Ingredient()], Steps = [new Step()] }));
    }

    [Fact]
    public void SocialMetadataMustMatchRequestedPost()
    {
        var data = new Dictionary<string, object>
        {
            ["__DEFAULT_SCOPE__"] = new Dictionary<string, object>
            {
                ["webapp.video-detail"] = new { statusCode = 0, itemInfo = new { itemStruct = new
                { id = "12345", desc = Caption, video = new { cover = "https://cdn.example.com/cover.jpg" } } } }
            }
        };
        var html = "<script id='__UNIVERSAL_DATA_FOR_REHYDRATION__'>" + JsonSerializer.Serialize(data) + "</script>";
        Assert.Equal(4, RecipeSourceParser.TikTok(html, "12345").Text.Ingredients.Count);
        Assert.Equal("https://cdn.example.com/cover.jpg", Assert.Single(RecipeSourceParser.TikTok(html, "12345").ImageCandidates));
        Assert.Throws<RecipeSourceException>(() => RecipeSourceParser.TikTok(html, "54321"));
        var json = JsonSerializer.Serialize(new
        {
            data = new
            {
                xdt_api__v1__media__shortcode__web_info = new
                { items = new[] { new { code = "Post123", caption = new { text = Caption },
                    carousel_media = new[] {
                        new { image_versions2 = new { candidates = new[] { new { url = "https://cdn.example.com/first.jpg", width = 1080 } } } },
                        new { image_versions2 = new { candidates = new[] { new { url = "https://cdn.example.com/second.jpg", width = 1080 } } } }
                    } } } }
            }
        });
        Assert.Equal(4, RecipeSourceParser.Instagram(json, "Post123").Text.Ingredients.Count);
        Assert.Equal("https://cdn.example.com/first.jpg", Assert.Single(RecipeSourceParser.Instagram(json, "Post123").ImageCandidates));
        Assert.Throws<RecipeSourceException>(() => RecipeSourceParser.Instagram(json, "Different"));
    }

    [Theory]
    [InlineData("http://example.com/recipe")]
    [InlineData("https://user:password@example.com/recipe")]
    [InlineData("https://example.com:8443/recipe")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://192.168.1.1/")]
    [InlineData("https://[::ffff:10.0.0.1]/")]
    [InlineData("https://nas.local/")]
    [InlineData("https://www.instagram.com/some-profile/")]
    [InlineData("https://www.tiktok.com/")]
    public void UnsupportedUrlsAreNotFetchable(string source) => Assert.False(RecipeSourceService.TrySourceUrl(source, out _));

    [Fact]
    public void RobotsHonorsSpecificAgentLongestRuleAndDelay()
    {
        Assert.Equal(2, RecipeSourceService.RobotsDelay("User-agent: *\nDisallow: /\nUser-agent: NibblesSource\nDisallow: /private\nAllow: /private/recipe\nCrawl-delay: 2", "/private/recipe"));
        Assert.Throws<RecipeSourceException>(() => RecipeSourceService.RobotsDelay("User-agent: *\nDisallow: /*?login=*$", "/recipe?login=true"));
        Assert.Throws<RecipeSourceException>(() => RecipeSourceService.RobotsDelay("User-agent: *\nCrawl-delay: 20", "/"));
    }

    [Fact]
    public async Task WebsiteFetchUsesFreshSourceAfterRobotsAndDoesNotSendCredentials()
    {
        var paths = new List<string>();
        var service = new RecipeSourceService(() => new FakeHandler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            Assert.Null(request.Headers.Authorization);
            Assert.DoesNotContain(request.Headers, header => header.Key.Contains("github", StringComparison.OrdinalIgnoreCase));
            return request.RequestUri.AbsolutePath == "/robots.txt" ? Response(HttpStatusCode.NotFound, "") : Response(HttpStatusCode.OK, Html(Metadata));
        }), () => throw new InvalidOperationException("Website fetch must not read the Apify key."));
        var draft = await service.FetchAsync("https://example.com/new-recipe", CancellationToken.None);
        Assert.Equal(new[] { "/robots.txt", "/new-recipe" }, paths);
        Assert.Equal(2, draft.Text.Ingredients.Count);
    }

    [Fact]
    public async Task PrivateRedirectIsRejectedBeforeFollowingIt()
    {
        var requests = 0;
        var service = new RecipeSourceService(() => new FakeHandler(request =>
        {
            requests++;
            if (request.RequestUri!.AbsolutePath == "/robots.txt") return Response(HttpStatusCode.NotFound, "");
            return new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://127.0.0.1/") } };
        }));
        await Assert.ThrowsAsync<RecipeSourceException>(() => service.FetchAsync("https://example.com/recipe", CancellationToken.None));
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task RestrictedHostsAreStoppedWithoutRetry()
    {
        var requests = 0;
        var service = new RecipeSourceService(() => new FakeHandler(_ => { requests++; return Response(HttpStatusCode.Forbidden, ""); }));
        await Assert.ThrowsAsync<RecipeSourceException>(() => service.FetchAsync("https://example.com/recipe", CancellationToken.None));
        await Assert.ThrowsAsync<RecipeSourceException>(() => service.FetchAsync("https://example.com/another", CancellationToken.None));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task RobotsDisallowPreventsPageRequest()
    {
        var requests = 0;
        var service = new RecipeSourceService(() => new FakeHandler(_ =>
        { requests++; return Response(HttpStatusCode.OK, "User-agent: *\nDisallow: /", "text/plain"); }));
        await Assert.ThrowsAsync<RecipeSourceException>(() => service.FetchAsync("https://example.com/recipe", CancellationToken.None));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task CancellationStopsLookup()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var service = new RecipeSourceService(() => new FakeHandler(_ => throw new InvalidOperationException("Must not send")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.FetchAsync("https://example.com/recipe", cancel.Token));
    }

    [Fact]
    public async Task SocialFetchWithoutPrivateKeyDoesNotContactInstagram()
    {
        var service = new RecipeSourceService(() => new FakeHandler(_ =>
            throw new InvalidOperationException("Missing credentials must not cause any request.")));
        var error = await Assert.ThrowsAsync<RecipeSourceException>(() =>
            service.FetchAsync("https://www.instagram.com/reel/Post123/", CancellationToken.None));
        Assert.Contains("private Apify build input", error.Message);
    }

    [Fact]
    public async Task CompressedResponseCannotExceedDecodedLimit()
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(new string('x', 8_000_001)));
        var bytes = buffer.ToArray();
        var service = new RecipeSourceService(() => new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/robots.txt") return Response(HttpStatusCode.NotFound, "");
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentEncoding.Add("gzip");
            response.Content.Headers.ContentType = new("text/html");
            return response;
        }));
        var error = await Assert.ThrowsAsync<RecipeSourceException>(() => service.FetchAsync("https://example.com/recipe", CancellationToken.None));
        Assert.Contains("size limit", error.Message);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("object")]
    [InlineData("array")]
    [InlineData("reference")]
    public void WebsitePrioritizesItsRecipeImageAndKeepsOpenGraphAsFallback(string shape)
    {
        var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(Metadata))!;
        metadata["image"] = shape switch
        {
            "string" => "/recipe.jpg",
            "object" => new { contentUrl = "/recipe.jpg" },
            "array" => new object[] { "/recipe.jpg", new { url = "/wide.jpg" } },
            _ => new Dictionary<string, object> { ["@id"] = "#photo" }
        };
        var html = Html(new Dictionary<string, object> { ["@graph"] = new object[]
        {
            metadata, new Dictionary<string, object> { ["@type"] = "ImageObject", ["@id"] = "#photo", ["url"] = "/recipe.jpg" }
        } }) + "<meta property='og:image' content='/preview.jpg'><img src='/advert.jpg'>";
        var draft = RecipeSourceParser.Website(html);
        Assert.Equal("/recipe.jpg", draft.ImageCandidates[0]);
        Assert.Equal("/preview.jpg", draft.ImageCandidates[^1]);
        Assert.DoesNotContain("/advert.jpg", draft.ImageCandidates);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("nibbles_chef.png", false)]
    [InlineData("selected.jpg", true)]
    public void PhotoEligibilityPreservesSelectedAndStoredPhotos(string? path, bool expected)
    {
        var recipe = new Recipe { ImagePath = path };
        Assert.Equal(expected, RecipeSourceDraft.HasPhoto(recipe));
        recipe.ImageData = "data:image/jpeg;base64,photo";
        Assert.True(RecipeSourceDraft.HasPhoto(recipe));
    }

    private static readonly byte[] Photo = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jT1sAAAAASUVORK5CYII=");
    private static string PageWithPhoto => Html(Metadata) + "<meta property='og:image' content='/photo.png'>";
    private static HttpResponseMessage PhotoResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Photo) };
        response.Content.Headers.ContentType = new("image/png");
        return response;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ImageIsDownloadedIntoDraftOnlyWhenRequested(bool includeImage)
    {
        var paths = new List<string>();
        var service = new RecipeSourceService(() => new FakeHandler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            Assert.Null(request.Headers.Authorization);
            return request.RequestUri.AbsolutePath switch
            {
                "/robots.txt" => Response(HttpStatusCode.NotFound, ""),
                "/photo.png" => PhotoResponse(),
                _ => Response(HttpStatusCode.OK, PageWithPhoto)
            };
        }));
        var draft = await service.FetchAsync("https://example.com/recipe", CancellationToken.None, includeImage);
        Assert.Equal(2, draft.Text.Ingredients.Count);
        Assert.Equal(includeImage, paths.Contains("/photo.png"));
        if (includeImage) Assert.Equal(Photo, draft.ImageBytes);
        else Assert.Null(draft.ImageBytes);
        Assert.Null(draft.Text.ImagePath);
        Assert.Null(draft.Text.ImageData);
    }

    [Theory]
    [InlineData("private")]
    [InlineData("redirect")]
    [InlineData("format")]
    [InlineData("signature")]
    [InlineData("size")]
    [InlineData("policy")]
    [InlineData("timeout")]
    public async Task UnavailableOrUnsafePhotoDoesNotDiscardTheRecipe(string problem)
    {
        var downloaded = false;
        var service = new RecipeSourceService(() => new FakeHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            Assert.NotEqual("127.0.0.1", request.RequestUri.Host);
            if (path == "/robots.txt") return Response(HttpStatusCode.OK,
                problem == "policy" ? "User-agent: *\nDisallow: /photo.png" : "", "text/plain");
            if (path != "/photo.png") return Response(HttpStatusCode.OK,
                problem == "private" ? PageWithPhoto.Replace("/photo.png", "https://127.0.0.1/photo.png") : PageWithPhoto);
            downloaded = true;
            if (problem == "redirect")
                return new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://127.0.0.1/photo.png") } };
            if (problem == "format") return Response(HttpStatusCode.OK, "<svg/>", "image/svg+xml");
            if (problem == "signature") return Response(HttpStatusCode.OK, "not an image", "image/png");
            if (problem == "timeout") throw new OperationCanceledException();
            var image = PhotoResponse();
            image.Content.Headers.ContentLength = 6_000_001;
            return image;
        }));
        var draft = await service.FetchAsync("https://example.com/recipe", CancellationToken.None);
        Assert.Equal(2, draft.Text.Ingredients.Count);
        Assert.Null(draft.ImageBytes);
        Assert.Contains(draft.Warnings, warning => warning.StartsWith("Photo not imported:"));
        Assert.DoesNotContain(draft.Warnings, warning => warning.Contains("Paste"));
        Assert.Equal(problem is not ("private" or "policy"), downloaded);
    }

    [Fact]
    public async Task BrokenRecipeImageFallsBackToPagePreview()
    {
        var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(Metadata))!;
        metadata["image"] = "/broken.jpg";
        var service = new RecipeSourceService(() => new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/robots.txt" => Response(HttpStatusCode.NotFound, ""),
            "/broken.jpg" => Response(HttpStatusCode.NotFound, ""),
            "/preview.png" => PhotoResponse(),
            _ => Response(HttpStatusCode.OK, Html(metadata) + "<meta property='og:image' content='/preview.png'>")
        }));
        var draft = await service.FetchAsync("https://example.com/recipe", CancellationToken.None);
        Assert.Equal(Photo, draft.ImageBytes);
        Assert.DoesNotContain(draft.Warnings, warning => warning.StartsWith("Photo not imported:"));
    }

    [Fact]
    public async Task CancellingDuringPhotoDownloadCancelsTheWholeLookup()
    {
        using var cancel = new CancellationTokenSource();
        var service = new RecipeSourceService(() => new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/robots.txt") return Response(HttpStatusCode.NotFound, "");
            if (request.RequestUri.AbsolutePath != "/photo.png") return Response(HttpStatusCode.OK, PageWithPhoto);
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.FetchAsync("https://example.com/recipe", cancel.Token));
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body, string mediaType = "text/html") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
