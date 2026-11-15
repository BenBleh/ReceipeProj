# Instagram recipes: caption-first import options

**Research only - 13 November 2026. No app, NAS, account or recipe changes.**

**Subsequent local trial:** [Instaloader returned captions for 3/3 attempted source URLs](instagram-instaloader-trial.md), anonymously. All three contained ingredient lists and numbered methods. Eleven other references remain untested; the original documentation-only conclusions below are qualified by that separate runtime evidence.

## Bottom line

**Instagram can be covered without paying for an AI API, but dependable "any Instagram URL -> complete recipe" was not established.** The difficult part is obtaining the written caption, not turning available text into a recipe.

The strongest low-cost design is:

1. Attempt caption lookup through a selected, replaceable adapter, subject to source-use permission; official account APIs are an optional route, not a prerequisite.
2. Otherwise accept the caption as pasted text, or screenshots of the expanded description.
3. Extract screenshot text on the phone, then split title, ingredient groups and method into an editable draft.
4. Use optional local AI only when ordinary text parsing is inadequate. Do not process the video when the description already contains the recipe.

There is a more useful official API possibility than "only your own posts": **Business Discovery can expose captions from other public Business/Creator accounts without each creator connecting to Nibbles.** However, it requires your own eligible account/app configuration and the target's **username**, not just the post URL. Personal accounts, access-level behavior, approval for recipe storage and some current login-documentation discrepancies remain important limits.

**Updated recommendation:** given the user's preference not to create a business account and the positive local trial, investigate a bounded anonymous free-tool attempt first, with explicit paste/screenshot fallback. Keep the extractor replaceable and optional; do not make a paid service, professional account or large local model mandatory.

## What we know about the actual collection

The existing pinned corpus contains **14 Instagram references: two `/reel/` addresses and twelve `/p/` addresses**. A `/p/` address does not establish whether the content is a photograph, carousel or video.

All fourteen retain their original **`robots_disallowed_not_crawled`** classifications in [per-recipe-feasibility.csv](per-recipe-feasibility.csv). That generic crawler did not fetch their pages. A subsequently authorized, separate Instaloader experiment returned captions for three references; it did not alter the original evidence.

An additional offline check found no creator `@handle` or separate recipe-page URL in their saved `Source`/`Notes` fields. The source permalinks themselves do not identify the creator username.

Current evidence boundaries:

- Three captions were recovered and reviewed; the remaining eleven captions remain unknown.
- Creator professional-account eligibility remains unverified for all fourteen. It was not required for the anonymous tool trial.
- No official API lookup, share-payload capture or screenshot OCR was tested.
- The 3/3 sample result is not an overall Instagram availability or successful-import percentage.

This is **not fourteen failed imports**, nor evidence that fourteen recipes need video transcription. The existing per-recipe rows preserve the original inventory/crawler evidence; [separate execution records](instagram-instaloader-trial.json) establish the three actual caption retrievals.

## Route comparison

| Route | What it could provide | Cost position | Main limitation / evidence |
|---|---|---|---|
| Meta Business Discovery | Attached captions for eligible other professional accounts | No per-call charge specified in reviewed documentation; setup/maintenance required | Documented fields, not tested. Requires caller setup and target username; not a universal URL resolver. |
| Ordinary public post HTML | Caption if delivered in an allowed public response | No extraction-service fee | Not attempted for these posts because of observed robots exclusions. Layout, login and access restrictions make this unsuitable as a guaranteed dependency. |
| Instaloader | Full caption field; caption text files; single-shortcode input | MIT-licensed software; self-hosting/maintenance costs | Subsequently returned captions for 3/3 sampled sources anonymously. Still uses unsupported interfaces; broader reliability is unknown. |
| yt-dlp / gallery-dl | Media metadata, potentially accompanying text | Free software; deployment/licensing/maintenance still matter | Documentation/source survey only. Downloader support is not proof of dependable caption-only extraction. |
| Explicit caption paste | Text the user supplies, plus the source URL | No API or model fee | Some manual work; do not assume Instagram's mobile UI always offers full-caption copying. |
| Expanded-caption screenshots + OCR | Visible description text, ingredient cards or method cards | Local OCR can avoid per-import fees | User captures all relevant sections; OCR errors and incomplete captures need review. |
| User-triggered browser clipper | Selected/visible caption text and current URL | No extraction-service fee; extension development | Browser-specific, layout-sensitive and subject to platform terms. Does not grant permission to access hidden content. |
| Creator's own recipe page | Often structured title, ingredients and steps | Usually no model required | Need the explicit destination; do not guess a matching recipe or assume it equals the saved variant. |
| Hosted extractor with free credits | Vendor-advertised caption/post metadata | Potentially zero cash at small volume, not perpetually free | External processing, changing quotas, permission/terms questions; no service tested or private URLs submitted. |
| Instagram oEmbed | An embedded front-end view | Pricing is not the deciding issue | **Unsuitable:** Meta explicitly prohibits extraction/persistence for other purposes. |

### 1. Official Meta route: a limited but genuine possibility

The [Business Discovery reference][meta-discovery-reference] says it can obtain data about other Instagram Business/Creator users. The [IG Media reference][meta-media] labels `caption`, `permalink` and `shortcode` public fields. The target creator does not individually OAuth into Nibbles for this mechanism.

For the least ambiguous documented path, **Facebook Login**, the caller needs:

- An Instagram Business/Creator account connected to a Facebook Page.
- A Meta developer app and appropriate Page/account roles.
- A Facebook User access token and endpoint-specific permissions.

The [getting-started guide][meta-get-started] covers caller account-ID discovery. The endpoint reference requires `instagram_basic`, `instagram_manage_insights` and `pages_read_engagement`; permission dependencies include `pages_show_list`. Page roles granted through Business Manager can additionally require `ads_read` or `ads_management`. The introductory tutorial's shorter permission list should not be mistaken for the complete Business Discovery requirements. See the [permission reference][meta-permissions].

**Proposed lookup, not an exercised integration:**

1. Obtain the target creator username independently.
2. Query that professional account's media through Business Discovery, requesting caption, shortcode and permalink inside the field expansion.
3. Page through the media and match the supplied shortcode/permalink locally.
4. Use the matched caption as source text, with missing-field checks.

The endpoint supports cursor-based nested-media pagination, but does not document a general shortcode/permalink filter. An old post may require several calls; ordering and exhaustive historical coverage must not be assumed. The [guide][meta-discovery-guide] also warns that directly reading a discovered media ID can fail for insufficient permissions: obtain permitted caption fields **inside the discovery expansion**, not by treating returned IDs as unrestricted access.

**What "full caption" means:** the API field is the attached caption, not a preview snippet. However, the reference does not promise byte-for-byte original text, documents an `@`-symbol restriction in some access contexts, and excludes carousel children from the caption field. Read the parent caption. Comments, subtitles and text inside images are separate sources. A complete caption can still lack the recipe method.

#### Personal pilot versus a distributed app

[Access-level documentation][meta-access] allows Standard Access for role-only apps. This provides a basis for a personal development pilot using the user's own eligible account, but **does not establish that a newly configured Standard Access app will successfully read unrelated creators' captions**. Broader ownership/review statements and feature limitations make that an operational gate to test, not a guarantee.

Ordinary non-role users introduce Advanced Access, App Review, Business Verification and Live-mode requirements. The permission's review guidance emphasizes business insights. A personal recipe organizer does not obviously match that review narrative. **Approval and permitted retention of caption-derived recipes remain unresolved; do not disguise the app's purpose.**

No subscription/per-caption price was specified in the reviewed API documents. This is not a guaranteed pricing statement or unlimited access: [rate limits][meta-rate-limits] and pagination still apply.

#### Current login documentation is not fully aligned

The Business Discovery guide names Facebook Login and Unified Login. The endpoint reference names Facebook Login and its user-token requirements; the shared media reference labels caption availability Facebook Login only. Unified Login uses different server-side authentication and requires a business portfolio; ordinary Instagram Login is a separate integration.

Therefore, **do not assume "no Facebook Page required" automatically gives this same other-creators caption route**. Facebook Login is currently the best-documented candidate for this particular pilot, while Unified Login caption parity needs explicit confirmation. See [overview][meta-overview] and [Unified authentication][meta-unified].

#### Routes that do not solve the problem

[oEmbed][meta-oembed] accepts post URLs but explicitly prohibits consuming, manipulating, extracting or persisting its content/metadata for anything other than a front-end view. Do not use it to obtain a recipe or to resolve a username for another extraction route. [Meta oEmbed Read][meta-oembed-read] also has review/verification requirements.

The [2025 migration announcement][meta-oembed-update] removes author/thumbnail fields; old snippets are not evidence of current username lookup. Exact accepted token types could not be established consistently from the retrieved guide/reference examples, which does not change the extraction prohibition.

[Basic Display was retired on 4 December 2024][meta-basic-display]. It is not a current free personal-account workaround.

### 2. Free extraction tools: useful capability, unproven reliability

**Instaloader is the most directly relevant open-source caption candidate.** Its [CLI documentation][instaloader-cli] supports a single post shortcode and caption `.txt` output; media download can be disabled. Its [troubleshooting documentation][instaloader-troubleshooting] also records rate limits and access/login failures.

Source inspection at indexed commit `7acd1b6a75d9964b6e7bf91c0769e172e2a64359` found:

- [`Post.caption`, lines 591-612][instaloader-caption], returns caption text.
- [`pcaption`, lines 614-632][instaloader-pcaption], is intentionally shortened for filenames. It is **not** the recipe caption. `accessibility_caption` is also a different field.
- [Metadata retrieval, lines 410-428][instaloader-metadata], depends on an internal GraphQL mechanism, not the supported Business Discovery contract.

Its [MIT license][instaloader-license] permits software use subject to its conditions; it does not grant permission to collect Instagram content. The indexed commit is not asserted to be today's upstream HEAD. A later [local trial of installed version 4.16](instagram-instaloader-trial.md) recovered three captions using the normal anonymous metadata path, without media downloads or imported account sessions.

**yt-dlp** supports Instagram URL patterns, but inspected [extractor control flow][ytdlp-instagram] includes login/rate-limit failures and video-format failures. That makes it less attractive as a universal caption-only importer for mixed post types. Its [README][ytdlp-readme] also distinguishes source/package licensing from bundled executable licensing.

**gallery-dl** is another downloader candidate; its [repository][gallerydl] now points active development to Codeberg. [Configuration documentation][gallerydl-config] contains Instagram metadata options, but these are not evidence that arbitrary URLs currently yield complete captions anonymously.

**Assessment:** Instaloader now has positive runtime evidence on three corpus sources; yt-dlp and gallery-dl were not executed. This is not proof of dependable collection-wide coverage. Login/cookie export, manual hidden-endpoint workarounds, proxy rotation and CAPTCHA bypass are not recommended fallback architecture.

### 3. User-assisted caption capture: the strongest coverage fallback

The user already sees the recipe in Instagram. Supplying that visible text can avoid the platform lookup problem without downloading or understanding the video.

**Paste workflow:** keep the source URL, explicitly paste the complete description where copying is available, show the raw input, then build a draft. [MAUI's clipboard API][maui-clipboard] supports string text; clipboard access should be explicit, not background monitoring. It does not retrieve Instagram captions itself.

**Screenshot workflow:** expand the description, capture the ingredient and method sections in order, and share/select the screenshots. A long caption may require several captures. Crop unrelated usernames/comments where possible, preserve ordering and remove overlapping duplicate lines with review.

Native sharing is useful plumbing, not a caption API:

- [Android receiving documentation][android-share] describes text through `EXTRA_TEXT` and images through `EXTRA_STREAM`. The sender determines what is shared. A URL share does not imply the description is included; even the source URL accompanying a screenshot is optional.
- On iOS, [`NSExtensionItem`][apple-extension] has optional content text and attachments. A receiving share extension still only gets what the sender provides.

Actual Instagram Android/iOS payloads were **not observed**. An eventual prototype must distinguish link-only shares from caption text and images, rather than silently parsing a URL as a recipe.

For a browser clipper, [temporary `activeTab` access][mdn-activetab] can support explicit user selection/capture without blanket browsing access. This is a desktop/browser-oriented option, not proof that the Instagram phone app exposes caption text. Browser permissions do not override site terms or access controls.

#### Local OCR choices

| Option | Suitable role | Evidence and caveats |
|---|---|---|
| Google ML Kit Text Recognition | Android screenshot OCR; iOS also supported | [Overview][mlkit-overview] says offered at no cost. [Android documentation][mlkit-android] offers bundled or downloaded models; bundled Latin model is approximately 4 MB per architecture. Minimum Android API 23. |
| Apple Vision | Native iOS screenshot OCR | [Apple documentation][apple-vision] says processing occurs on-device; provides text candidates/confidence. No cloud model required. |
| Tesseract | Optional NAS/local screenshot OCR | [Apache-2.0 OCR engine][tesseract]. Potentially useful for central processing; not benchmarked on this NAS. |

ML Kit's [terms][mlkit-terms] say input and recognition results are processed on-device and not sent to Google servers, but model updates and performance/utilization metrics can use the network. Do not describe this as "no network ever."

**App compatibility detail:** Nibbles currently supports Android API 21 (`Nibbles\Nibbles.csproj`, line 37), while current ML Kit requires API 23. A future implementation must preserve existing compatibility with a guarded OCR feature/fallback or explicitly agree a support change; it must not silently raise the minimum Android version.

No OCR engine, MAUI binding, device integration or recognition accuracy was tested. Fractions, decimal quantities, degree symbols, ingredient groups and overlapping screenshots are acceptance-test priorities.

### 4. Hosted free-credit services: economically possible, not a selected solution

[Apify's pricing][apify-pricing] currently advertises a $0 plan with $5 included usage per month and no card required. Unused credits do not roll over; exhausted free-plan usage is blocked until the next cycle. These are advertised dollar amounts, not an AUD quotation.

Its [Instagram Post Scraper][apify-instagram] advertises specific-post URL input and caption/shortcode/post-type output, starting from $1 per 1,000 posts. **That starting rate is not a guaranteed free-plan import count**: actor events and platform/storage/proxy costs can differ. Vendor success/coverage claims are not measurements on our collection.

This could be considered only after permission, terms, privacy, retention and actual pricing are checked. It is not self-hosted, not guaranteed indefinitely free, and not a way to bypass platform restrictions. No account was created, service invoked, private URLs submitted or money spent.

## Turning a caption into the same recipe

Once source text is available, a small non-AI parser can recognize headings, ingredient lines, numbered steps and paragraphs. Recipe titles are not always explicit: a proposed title must remain editable. Preserve quantities, units, optional ingredients and group headings rather than performing implicit conversions.

Optional AI can help separate mixed prose, suggest a title or split long directions. It must not supply quantities, temperatures, timings or omitted steps from general cooking knowledge. Missing information remains a visible warning.

Keep the original URL and enough user-approved source evidence to review the draft, subject to the source's retention terms. A caption, selected creator comment and screenshot should have distinct provenance; never silently combine another post's recipe or assume a food photo contains the written recipe.

The target remains the existing Nibbles editor/save path: free-text `FQIngrediantDescription`, ingredient groups and ordered method. Do not add a separate backend writer to `rfiles`. Exact reproduction of an already personalized saved recipe is not generally inferable from a source URL.

## Role of the NAS and existing domain

Assuming the user's "DS925" is the DS925+, the current **4 GB RAM** is a reason to keep the first version lightweight, not to buy an upgrade before testing.

- Caption lookup, deterministic text parsing and phone-side OCR do **not** require a local generative model.
- The NAS could host an authenticated, low-concurrency draft service behind the existing HTTPS reverse proxy. Hardware suitability and latency were not benchmarked.
- If an official Meta integration is pursued, handle its secrets/tokens server-side and follow its lifecycle/access requirements. Do not upload Instagram browser sessions or the private GitHub token.
- An optional small CPU model would receive only the necessary caption/OCR text. Keep Ollama private; expose the authenticated importer, not the model port.
- Prefer phone-side OCR for screenshots to reduce NAS work and unnecessary image transfer. Raw screenshots should not be retained by default.

The domain/reverse proxy solves connectivity, **not** Instagram permissions, caption availability or model performance. There is no reason to begin with video download, transcription or a vision-language model when the description contains the recipe.

## Bounded validation before development

| Question | Current answer | Evidence still needed |
|---|---|---|
| Can other creators' captions be read through an official mechanism? | Documented for eligible professional accounts through Business Discovery | Authorized app/token test and permitted-use confirmation |
| Can arbitrary source URLs alone be resolved officially? | Not established; username requirement is a material gap | Supported resolution route, or explicitly accept user-provided username |
| Do our fourteen sources have recipe captions? | Three recovered captions have ingredient lists and numbered methods; eleven untested | Broader permitted retrieval/capture and completeness assessment |
| Does sharing from Instagram include the caption? | Not established | Observe real Android/iOS payloads; do not assume |
| Can screenshots become accurate drafts cheaply? | Credible local OCR components exist | Actual captures, OCR accuracy and draft-fidelity measurements |
| Is NAS AI necessary or fast enough? | Not necessary for clean captions; performance unknown | Compare deterministic parsing with a small local model on the same permitted text |
| Is the official route viable beyond a personal pilot? | Unresolved review, access and purpose gates | Meta confirmation/review for the actual recipe-import purpose |

A useful future proof-of-concept would start with a small, deliberately varied set: complete caption, ingredients-only caption, carousel, recipe in a creator comment, and a creator website link. Record acquisition method, user effort, missing fields, quantity/unit preservation and edits required. Do not report a successful import merely because a caption string was returned.

**No app/NAS implementation is authorized by this report.** A separate standalone research script was approved and exercised. No production importer, official account setup, on-device OCR, model benchmark or complete fourteen-source coverage is claimed.

## Sources

Meta sources were checked in a dedicated documentation investigation; tool capabilities were checked against public documentation and the identified source snapshots. Documentation claims, static code findings and vendor advertising are not runtime observations.

[meta-discovery-reference]: https://developers.facebook.com/documentation/instagram-platform/instagram-graph-api/reference/ig-user/business_discovery
[meta-discovery-guide]: https://developers.facebook.com/documentation/instagram-platform/instagram-api-with-facebook-login/business-discovery
[meta-media]: https://developers.facebook.com/documentation/instagram-platform/reference/instagram-media
[meta-get-started]: https://developers.facebook.com/documentation/instagram-platform/instagram-api-with-facebook-login/get-started
[meta-permissions]: https://developers.facebook.com/documentation/development/permissions
[meta-access]: https://developers.facebook.com/documentation/development/graph-api/overview/access-levels
[meta-overview]: https://developers.facebook.com/documentation/instagram-platform/overview
[meta-unified]: https://developers.facebook.com/documentation/instagram-platform/instagram-api-with-unified-login/authentication
[meta-rate-limits]: https://developers.facebook.com/docs/graph-api/overview/rate-limiting/
[meta-oembed]: https://developers.facebook.com/documentation/instagram-platform/oembed
[meta-oembed-read]: https://developers.facebook.com/documentation/development/features-reference/meta-oembed-read
[meta-oembed-update]: https://developers.facebook.com/blog/post/2025/04/08/oembed-updates/
[meta-basic-display]: https://developers.facebook.com/blog/post/2024/09/04/update-on-instagram-basic-display-api/
[instaloader-cli]: https://instaloader.github.io/cli-options.html
[instaloader-troubleshooting]: https://instaloader.github.io/troubleshooting.html
[instaloader-caption]: https://github.com/instaloader/instaloader/blob/7acd1b6a75d9964b6e7bf91c0769e172e2a64359/instaloader/structures.py#L591-L612
[instaloader-pcaption]: https://github.com/instaloader/instaloader/blob/7acd1b6a75d9964b6e7bf91c0769e172e2a64359/instaloader/structures.py#L614-L632
[instaloader-metadata]: https://github.com/instaloader/instaloader/blob/7acd1b6a75d9964b6e7bf91c0769e172e2a64359/instaloader/structures.py#L410-L428
[instaloader-license]: https://github.com/instaloader/instaloader/blob/7acd1b6a75d9964b6e7bf91c0769e172e2a64359/LICENSE
[ytdlp-instagram]: https://github.com/yt-dlp/yt-dlp/blob/412e7f0a4b0588fe884a02b344ac66a06538475b/yt_dlp/extractor/instagram.py#L436-L535
[ytdlp-readme]: https://github.com/yt-dlp/yt-dlp
[gallerydl]: https://github.com/mikf/gallery-dl
[gallerydl-config]: https://gdl-org.github.io/docs/configuration.html#extractorinstagram
[maui-clipboard]: https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/clipboard?view=net-maui-10.0
[android-share]: https://developer.android.com/develop/ui/compose/sharing/receive
[apple-extension]: https://developer.apple.com/documentation/foundation/nsextensionitem
[mdn-activetab]: https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/activeTab_permission
[mlkit-overview]: https://developers.google.com/ml-kit/guides
[mlkit-android]: https://developers.google.com/ml-kit/vision/text-recognition/v2/android
[mlkit-terms]: https://developers.google.com/ml-kit/terms
[apple-vision]: https://developer.apple.com/documentation/vision/recognizing-text-in-images
[tesseract]: https://github.com/tesseract-ocr/tesseract
[apify-pricing]: https://apify.com/pricing
[apify-instagram]: https://apify.com/apify/instagram-post-scraper

Access/use must also comply with [Meta's Automated Data Collection Terms](https://www.facebook.com/legal/automated_data_collection_terms) and [Platform Terms](https://developers.facebook.com/terms/). An available field, free software license or public post is not blanket permission for automated collection or permanent reuse. This is a technical/policy-risk assessment, not legal advice.
