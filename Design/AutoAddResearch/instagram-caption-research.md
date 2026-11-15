# Instagram recipes: caption-first import options

**Initial research: 13 November 2026; later authorized service trials below.
No native app, NAS, installer or recipe changes.**

**Subsequent local trial:** [Instaloader returned captions for 3/3 attempted source URLs](instagram-instaloader-trial.md), anonymously. All three contained ingredient lists and numbered methods. Eleven other references remain untested; the original documentation-only conclusions below are qualified by that separate runtime evidence.

## Free thumbnail services: follow-up, 15 November 2026

### Authenticated Apify TikTok follow-up, 16 November 2026 (Australia)

**Result: 2/2 TikTok recipe posts returned the correct stored cover images.**
The user requested this follow-up to assess one shared image-fetching approach
for Instagram and TikTok. Both returned exact video-ID matches, usable captions
and actual JPEG files retrieved through Apify storage, not merely CDN URLs.
The images were decoded, manifest-hash checked and visually compared with the
matched captions.

| TikTok source | Caption length | Original JPEG | Original dimensions | Visual result |
|---|---:|---:|---|---|
| `@jasminseats/video/7374209506705788168` | 2,103 characters | 806,428 bytes | 2160 x 3840 | Chicken boscaiola pasta; the cover title and dish match the caption. |
| `@food_bylucy/video/7538351446840904967` | 1,488 characters | 186,926 bytes | 1040 x 1386 | Gut friendly chicken, rice and broth; the cover title and dish match the caption. |

Metadata used [TikTok Video Scraper](https://apify.com/clockworks/tiktok-video-scraper),
build `0.0.511`, run `u69Vn4soWiuI5tZpK`, with only the two explicit post
URLs. Related videos, video/cover/slideshow downloads and subtitles/transcripts
were disabled. Each returned `id` matched the submitted post, as did the ID in
`webVideoUrl`; the selected image field was `videoMeta.coverUrl`. This
video-only actor fit the $0.03 metadata cap; the broader TikTok Scraper
advertised a $0.50 minimum cap and was not run.

The image stage used **the same downloader and settings as Instagram**:
`automa-flow/parallel-bulk-file-downloader`, build `0.1.27`, run
`Z2feZ61fbxtw2aU3o`, two matched cover URLs, file mode, concurrency one,
`maxRetries: 0`, 8 MiB/file and 15-second transfer limits, with a $0.05 run cap.
Both manifest rows reported `SUCCESS`, HTTP 200 and exactly one attempt.
Authenticated Apify file retrieval produced bytes matching the manifest
SHA-256 values. After the same JPEG normalization, both hashes also exactly
matched the previously verified direct-source TikTok covers in the historical
evidence; they were not substituted page images or avatars.

**Refreshed run charges:** metadata $0.00600 (two result events, no video or
transcript events), images $0.00805: **$0.01405 total**, within the announced
$0.08 bound and existing Free credit. The account remained `FREE`,
`isPaying: false`, with approximately **$4.95949 remaining**. Initial completion
responses again had stale event counts; refreshed records supplied these
amounts. No plans, payment settings or account limits changed.

**Integration conclusion:** one optional Apify-backed "Get image" flow is now
supported by these samples for both platforms. Internally, Instagram uses
`shortCode`/`displayUrl`, TikTok uses `id`/`videoMeta.coverUrl`, and both can
share the bounded stored-image download, validation and Save-only local-photo
persistence path. This is a shared provider/workflow, not one universal
metadata actor or a guarantee for every post. TikTok's actor also documents
`shouldDownloadCovers`, but that built-in download mode was not tested; the
shared zero-retry downloader was used instead.

Keys and signed URLs remain private. Provider collection internals and
source/creator retention permissions are not established by this trial.
Seven-day Apify storage still requires copying chosen photos into local recipe
storage on Save. No native app code, installers, commits or pushes changed.
Sanitized evidence is in `apify_tiktok_authenticated_trial` in
[`Prototype/social-image-validation.json`](Prototype/social-image-validation.json).

### Authenticated Apify Free trial: verified Reel covers

**Result: 2/2 exact recipe Reels returned usable covers, downloaded from Apify
storage, decoded and visually checked.** This supersedes the earlier
documentation-only Apify recommendation and failed Peekalink trial as the
most promising integration route. It is not yet integrated into Nibbles.

The user supplied an Apify token privately and authorized testing within the
account's recurring Free allowance. The account was confirmed as `FREE`,
`isPaying: false`, with a $5 monthly allowance and seven-day storage retention.
No plan, payment setting, subscription or account limit was changed.
The token was used only in Authorization headers to `api.apify.com`;
signed media URLs, full captions and credentials remain outside the repository.

The bounded two-stage trial used documented REST endpoints:

1. [Instagram Reel Scraper](https://apify.com/apify/instagram-reel-scraper),
   build `0.0.651`, run `ETdAQIjLmxIvbSe69`: two explicit Reel URLs, no
   transcripts, shares-count add-on or downloaded videos. Both returned one
   exact `shortCode` match, `type: Video`, `productType: clips` and `displayUrl`.
   The metadata run had a $0.03 total-charge cap.
2. [Bulk File Downloader](https://apify.com/automa-flow/parallel-bulk-file-downloader),
   build `0.1.27`, run `xq1jtORYlD8QSLY0j`: only those two matched cover URLs,
   custom shortcode filenames, file mode (no page discovery), concurrency one,
   `maxRetries: 0`, an 8 MiB/file bound and 15-second transfer deadlines.
   The image run had a $0.05 total-charge cap. The output manifest reported
   HTTP 200 and exactly one attempt for each file.
3. Retrieved the stored images through authenticated Apify API requests,
   checked the manifest's original-file SHA-256 against actual bytes, decoded
   both as JPEG and visually compared their titles/subjects with the matched
   recipe captions. Nibbles would retrieve stored bytes, not depend on a
   temporary Instagram CDN URL.

| Reel shortcode | Caption length | Original JPEG | Dimensions | Visual result |
|---|---:|---:|---|---|
| `C5BjXxMvlUq` | 1,363 characters | 184,603 bytes | 720 x 1280 | Creamy pesto chicken tortellini; dish and cover title match the caption. |
| `C1r36c1vLmQ` | 1,435 characters | 186,588 bytes | 720 x 1280 | Creamy chicken with potatoes and broccoli; dish and cover title match the caption. |

The originally proposed `lukaskrivka/images-download-upload` actor rejected
`imageCheckMaxRetries: 0` with HTTP 400 **before a run started**. Its schema
requires at least one retry, and inspected source retries refusals other than
404. It was not rerun with relaxed settings. The alternative downloader
documents zero extra attempts and its manifest confirms one attempt per file.
No new direct Instagram metadata lookup or local CDN image request was made.
These results do not establish the upstream metadata actor's internal
collection behavior or grant source/creator permission.

**Measured run charges, refreshed after completion:** $0.00620 for metadata
(one start plus two Reel events), $0.00805 for stored images (one start, two
file events and two storage-size events): **$0.01425 total**, within existing
free credit and both announced caps. The account then reported approximately
**$4.97378 remaining**. Account-level usage also includes other requests/storage
and should not be equated exactly with these two run charges. Initial
run-completion responses underreported event counts; refreshed run records
reconciled them. Pricing and allowances may change.

For integration, retain optional explicit provider use, a privately supplied
per-user key rather than a shared installer secret, bounded charges and no
automatic paid overages. Copy the chosen image into local recipe storage on
Save; Apify's seven-day retention is not permanent photo storage. Preserve
manual photo selection and existing recipe edits when fetching fails.
Source-use and creator-image retention rights remain separate unresolved
questions; successful delivery is not permission. Two successful samples are
not a reliability guarantee for all public, private, deleted or restricted Reels.

Sanitized per-file hashes, attempt counts and billing evidence:
[`Prototype/social-image-validation.json`](Prototype/social-image-validation.json),
`apify_authenticated_trial`. Full responses and proof images are retained
privately outside the repository. No production code, installers, commits or
pushes were made during this trial.

### Reel-specific follow-up, 16 November 2026 (Australia; 15 November UTC)

**Historical documentation stage; the subsequent authenticated Apify trial
is recorded above.** No new authenticated lookups, accounts, charges, app changes
or installers occurred during this stage. The user reports that TrendSee displayed Reel thumbnails,
whereas their Microlink trial found page images but not the wanted cover.
Treat that Microlink result as user-reported evidence, not an independently
reproduced test or proof of failure on every Instagram URL.

The user's Peekalink distinction is valid: its documentation names
`INSTAGRAM_POST` and demonstrates a `/p/` link, not explicit `/reel/` coverage.
It mentions video media, so the docs do not prove that Reels are excluded
either. The prior trial therefore establishes failed sample outcomes, not a
diagnosis of unsupported Reels. Do not retry by changing `/reel/` to `/p/`:
the URL spelling does not establish whether the underlying media is a video.

The next candidates must document **Reel-specific thumbnail fields**, not just
an arbitrary page `image`, and must pass both metadata and actual-file checks.

| Candidate | Verified documentation / free position | Remaining issue |
|---|---|---|
| [Apify Instagram Reel Scraper](https://apify.com/apify/instagram-reel-scraper) | Explicitly accepts individual Reel URLs and returns `shortCode`, caption and `displayUrl` cover. [Free plan](https://apify.com/pricing) renews $5 usage credit monthly, requires no card and blocks when exhausted. The actor README quotes $2.60/1,000 results on Free, excluding the start event; the headline "from $1" is not the Free rate. | Initially documentation-only; the later authorized two-stage trial above successfully stored and verified both recipe covers. |
| [SocialKit Instagram Stats](https://docs.socialkit.dev/api-reference/instagram-stats-api) | Explicit `/reel/` example with matching `shortcode`, `contentType: reel`, caption and `thumbnail`. [Free pricing](https://www.socialkit.dev/pricing) includes **20 credits/month**, all APIs, no card. Successful single stats requests cost one credit, including when views are unavailable. | Roughly 20 metadata lookups/month if no other credit use; not 20 guaranteed saved covers. The documented thumbnail is on Instagram's CDN, not SocialKit storage. No account/key or fresh Reel trial available here. |
| [SocialKit Instagram Download](https://docs.socialkit.dev/api-reference/instagram-download-api) | Explicit Reel support, a temporary S3-hosted **video** download and a separate thumbnail field. Up to 480p costs one credit per started minute; higher quality costs four per started minute. | The thumbnail example still uses Meta's CDN. The S3 URL is for the MP4, not a hosted cover. Downloading video and extracting a local frame is a different, heavier feature, and the frame may differ from the creator's chosen cover. Not tested. |
| [CodeNest Reel Thumbnail Downloader on Apify](https://apify.com/codenest/instagram-reels-thumbnail-downloader) | Explicit cover extraction at several sizes, including width/height fields. May consume the same platform Free credit. | Advertises direct Instagram CDN links, not stored images. Headline from $6/1,000 plus platform usage, with no established measured Free-plan total. Not preferable to the first-party Reel actor on current evidence; not run. |
| [SociaVault](https://sociavault.com/blog/instagram-reels-api) | Explicit Reel cover data and single-Reel Post Info lookup. | [Pricing](https://sociavault.com/pricing) is **50 one-time trial credits**, then purchased packs. No recurring zero-cost allowance established; not a fit for ongoing free use. Not run. |
| [Instabatch thumbnail tool](https://www.instabatch.net/instagram-thumbnail-downloader) | Advertises free Reel cover extraction without downloading video. | Page describes Meta-CDN images; no supported external API/free automation contract found in the reviewed pages. Its [pricing](https://www.instabatch.net/pricing) separately sells downloader/extraction products. Free-page claims do not establish free API access. No form submission tested. |
| [cobalt](https://github.com/imputnet/cobalt/blob/main/docs/api.md) | Open-source media downloader; the public instance's informational GET lists Instagram support. | Hosted API documentation explicitly requires owner permission for other projects or self-hosting. It is not a free public backend for Nibbles, and self-hosting conflicts with the deployment preference. No extraction POST or challenge attempt made. |

**At this historical stage, a hosted-image route existed in Apify's documented
toolchain but was not yet validated for these recipes.** Its
[Dataset Image Downloader & Uploader](https://apify.com/lukaskrivka/images-download-upload)
can consume the Reel actor's dataset, read `displayUrl`, and write image files
to an Apify key-value store rather than requiring Nibbles to fetch the original
CDN link. The actor is pay-per-platform-usage, so the download, storage and
metadata steps all share the $5 allowance; do not promise a fixed number of
free saved covers. This requires neither our own server nor an AWS account when
using Apify storage. It is not evidence that the files will download, that the
upstream permits collection, or that creator-photo retention rights are granted.
Do not configure proxy/identity rotation or retries to evade a refusal.

That two-stage route is the **most relevant next bounded free-tier trial**:
one supplied public recipe Reel, one matched cover, then actual stored JPEG
retrieval/decoding and visual identity confirmation. Stop on access restrictions
or credit exhaustion, cap the run's charges within existing free credit, and
report each stage separately. Account/token authorization is needed before
running it; no signup or run was performed during this follow-up.

**TrendSee remains positive evidence that cover discovery is possible**, from
both the earlier captured browser session and the user's latest test. The gap
is supported app integration and permitted file acquisition, not an assertion
that Instagram has no thumbnails. The tool still advertises the creator's
actual cover and no-signup use; reviewed material has not established a public
integration API. Reusing the earlier capture avoided redundant extraction
requests. Contacting its operator for supported free integration is preferable
to silently depending on its private web endpoint.

**SocialKit is a second, smaller recurring-free candidate**, not a proven fix.
Its public zero-credit `/status` endpoint responded, but `instagram.stats` was
`unknown` with missing freshness information; that is neither a success nor an
outage diagnosis. Its terms do not explicitly settle permanent third-party
recipe-image retention. Do not ask the user to create another account on the
strength of a generic `image` field alone.

**Research only. No account creation, paid API runs, app changes or installer
changes.** The user explicitly expanded the research to hosted third-party
services, provided they can be used without paying. A free webpage, an ongoing
free API quota and a time-limited trial are different products.

**Authenticated trial update:** Peekalink's free plan was the strongest
documented candidate, but the subsequent three-request trial below did not
establish a usable Instagram cover. It is not currently recommended for
integration. TrendSee's provided webpage was successfully exercised, but its
original Instagram image URL still fails the app-style automated-access policy
check.

### Options and their current limits

These are vendor-published prices/limits checked on this date, not guaranteed
future allowances or measured Instagram success rates.

| Service | Free position | Instagram/image fit | Evidence and remaining gap |
|---|---|---|---|
| [TrendSee thumbnail tool](https://tools.trendsee.io/instagram-thumbnail-downloader) | Advertises free, no signup. No public API quota established. | Accepts a Reel permalink and returns its cover. | **Web UI tested successfully on one matched Reel.** Its observed `/api/download` web backend is not evidence of a documented, supported third-party API. Returned image remains on Instagram's CDN. |
| [ApyHub video thumbnail generator](https://apyhub.com/apyhub/service/generate-thumbnail-from-video) | [Starter](https://apyhub.com/pricing): 3,000 atoms/month; this utility advertises 550 atoms/operation, approximately five operations if no other usage. Requires an API key. | Extracts a frame from a video file or remote video-file URL. | The user's Public APIs link is a directory entry for this service, not an Instagram resolver. No documented support for resolving an Instagram Reel webpage to a video file was established. It does not remove the media-obtainment problem. Not invoked. |
| [Peekalink](https://www.peekalink.io/pricing) | $0, 50 requests/hour; free plan says no extra requests allowed. Account/API key required. | [Instagram Post support is BETA](https://docs.peekalink.io/supported-link-types/instagram-post.md); documented caption/media fields and preview images hosted on `cdn.peekalink.io`. | **Authenticated trial did not produce a verified usable cover:** two Reels returned generic `PAGE` results; the inspected image was a blank screenshot. A `/p/` post returned HTTP 400. The earlier published CDN example download is not evidence that fresh Instagram extraction works. |
| [Microlink](https://microlink.io/pricing) | Ongoing $0 plan advertises 25 requests/day, no card, quota pause rather than overages. | General URL metadata/media extraction; an `image` response is documented. | Subsequent user trial found page images, not the wanted Reel thumbnails. Not independently reproduced; deprioritized for this use rather than claiming universal failure. Do not select proxy/unblocking workarounds for restricted sources. |
| [LinkPreview](https://www.linkpreview.net/) | $0 for personal use, 60 requests/hour, API key required. | General link preview image API. | No current Instagram-specific guarantee established. Its FAQ explicitly returns a robots restriction error for excluded sources, so it may preserve the same obstacle rather than solve it. Not invoked. |
| [Apify Instagram Post Scraper](https://apify.com/apify/instagram-post-scraper) | [Free account](https://apify.com/pricing): $5 recurring monthly usage credit, no card; free access blocks when credit is exhausted. Actor advertised from $1/1,000 posts, not a guaranteed free-import count. | Documented specific-post URL input and image URLs. | Conditional zero-cash option, not an unlimited free API. Needs account/token, actual event-cost measurement, image-download and permitted-use checks; a raw Instagram CDN URL alone may not solve the policy problem. Not invoked. |
| [OpenGraph.io](https://www.opengraph.io/pricing) | Lists a testing-oriented Free allowance of 100 requests; renewal period was not established. | General preview API, not demonstrated Instagram cover retrieval. | Insufficient evidence to recommend an ongoing free Instagram integration. Not invoked. |
| [InstaFix](https://github.com/Wikidepia/InstaFix) | MIT software/public embed service; self-hosting has operating costs. | Instagram embed repair rather than a promised thumbnail API. | Repository was archived on 2 April 2026. Self-hosting conflicts with the app-only preference; the public instance has no established stable API/retention contract for this use. Not invoked. |

### What actually happened with TrendSee

An isolated, empty-profile Chrome session submitted the public test permalink
`https://www.instagram.com/reel/C5BjXxMvlUq/` through the tool's normal form.
Its `POST /api/download` returned **200**. The page rendered a thumbnail and
download link; the browser's image request also returned **200**. The returned
image's media identity matched the submitted shortcode. This is positive runtime
evidence, not just the tool's marketing claim.

REA browser Evidence:
`ev_6ca829f12563ffb84595021c0cea7dbfe7b5401701a9f9a1440f97d64aeb94b4`.
The owned browser was terminated during cleanup. The full capture is retained
privately in session artifacts; signed image URLs are not copied into this report.

However, the cover link targets `scontent-lga3-1.cdninstagram.com`, not a
TrendSee-hosted image. A separate bounded prototype check received **200 for
that CDN's robots.txt**, then found an explicit exclusion for the image URL.
It did **not** request the image after that exclusion. This is different from
TikTok's unavailable-policy HTTP 403 case: the TikTok fix does not make this
Instagram policy permit automated image acquisition.

No supported external API/free automation contract was established from the
reviewed TrendSee pages. The tool footer's Privacy/Terms links were `#`
placeholders, so "No data is stored" cannot be treated as a verified retention
guarantee. Its webpage can be considered a user-operated manual helper;
silently depending on the internal backend is not yet a justified production
integration.

### What was verified for Peekalink

The public Instagram example image linked from Peekalink's own homepage passed
robots checking, returned **200 image/jpeg** from `cdn.peekalink.io`, and decoded:
48,528 source bytes, normalized to a 768 x 960 JPEG. This establishes that this
published CDN delivery path works with the existing image safeguards. It is
**not** a fresh API extraction test, a recipe example, or proof of Reel coverage.

Its [Terms](https://docs.peekalink.io/legal/terms.md) describe fetching/processing/
caching link previews, require lawful use and respect for others' rights, and do
not guarantee availability. Its [Privacy Policy](https://docs.peekalink.io/legal/privacy.md)
says submitted-link metadata/media and usage logs are retained for service
delivery, including infrastructure at Hetzner/Cloudflare. Do not describe this
as an entirely local or no-retention lookup. Long-term storage of a returned
preview as a recipe photo still needs confirmation; a vendor's access does not
itself grant rights to the creator's image.

### Earlier recommendation for the app-only, zero-cost requirement

The successful Apify trial above now supersedes this earlier provider
recommendation; the privacy, key handling and fallback requirements still apply.

The originally recommended **Peekalink Free** trial has now been performed with
the user's service key. Its results do not justify wiring it into the app.
Clarify current Instagram/Reel support with the vendor before further
integration work; HTTP 200 and a JPEG URL are insufficient acceptance criteria.
Permission to retain the photo rather than only display a transient preview
also remains unresolved.

If that succeeds, the MAUI app can call the documented REST API directly; no
Python runtime, NAS service or backend we operate is required. Keep provider
use optional and explicit: the public source URL is sent to that provider, but
private recipe edits, notes and GitHub credentials must not be sent. A service
key should be supplied/stored privately per user rather than embedded as a
shared credential in distributed installers. Preserve manual photo selection
when the free allowance, service or source is unavailable. Do not automatically
rotate through services to get around a source restriction or trigger paid use.

### Authenticated Peekalink Free trial

The user supplied a key outside the repository. It was sent only as an
Authorization header to the documented `POST https://api.peekalink.io/`,
with `{"link": "<public Instagram permalink>"}`. No key was printed or copied
into the app, report or repository. API redirects/retries were disabled, and
the image/text checks used separate sessions without the API credential.

Exactly **three API requests** were made:

| Public source | API result | Actual outcome |
|---|---|---|
| `https://www.instagram.com/reel/C5BjXxMvlUq/` | HTTP 200, `ok: true`, `type: PAGE`, returned permalink matched | No `instagramPost` object or verified cover. The returned image was not inspected for this first response; do not claim it was tested and found blank. |
| `https://www.instagram.com/reel/C1r36c1vLmQ/` | HTTP 200, `ok: true`, `type: PAGE`, returned permalink matched | The image was the page's screenshot, not a verified post cover. Its JPEG downloaded successfully from the vendor CDN, but every decoded pixel was white. The vendor-hosted extracted-text artifact was also empty. |
| `https://www.instagram.com/p/C08wu-CpLgm/` | HTTP 400 | No usable preview. No retry or alternate endpoint was attempted; the error's underlying cause was not established. |

The second image was 3,024 bytes from the vendor, decoded to 1280 x 720,
and normalized to a 15,027-byte JPEG with SHA-256
`b3ea039586ffed3ef4bc3cd549216daacee5b702e93cd9cff7beeaab1fc3beb7`.
Visual inspection and RGB extrema `((255,255),(255,255),(255,255))` confirmed
that it was completely white. Its URL equalled the API's
`page.screenshot.large.url`. Both the CDN policy and image request returned
HTTP 200, so this is not the earlier Instagram-CDN policy exclusion: the
provider delivered an unusable fallback image.

**Result: zero confirmed usable covers in this bounded sample.** This does not
establish that every Instagram link fails, that the free plan excludes
Instagram, or that a paid upgrade would fix it. Beta availability, source
retrieval and account-specific behavior remain possible explanations, not
diagnosed causes. No plan, account or payment settings were changed.

Sanitized results are included in
[the thumbnail evidence JSON](Prototype/social-image-validation.json).
The trial harness, limited response evidence and blank proof image are private
session artifacts, not application dependencies or public credentials.

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
