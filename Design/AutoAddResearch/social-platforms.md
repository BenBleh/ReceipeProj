# Social-platform access and recipe extraction

**Documentation survey: 13 November 2026. Not an API integration test.**

This appendix separates official supported access from observed public HTML and from vendor claims. Actual corpus probes and caption classifications are in the adjacent JSON/CSV files. No API application, login, paid service, comments retrieval, OCR, transcription or media download was exercised.

## Instagram

**Focused follow-up:** [Instagram caption research](instagram-caption-research.md) provides concrete Business Discovery prerequisites, permission/access uncertainties, the missing username-resolution step, free-tool source inspection and user-assisted OCR options. A subsequent [anonymous Instaloader trial](instagram-instaloader-trial.md) recovered captions for 3/3 sampled corpus sources; the other eleven remain untested.

### Account APIs are not a general arbitrary-post importer

[Meta's platform overview](https://developers.facebook.com/documentation/instagram-platform/overview) describes professional-account integrations, login alternatives, permissions and review. Standard Access supports owned/managed accounts and role-user development/testing; broader user access requires the applicable Advanced Access/App Review/Business Verification. Whether a newly configured role-only app can successfully discover unrelated professional accounts is an unresolved operational gate, not proof that all discovered content must belong to the caller.

Current documentation includes Unified Login alongside Instagram Login and Facebook Login. Instagram Login supports professional accounts without a linked Facebook Page; Facebook Login has Page-linkage requirements. See [Instagram Login](https://developers.facebook.com/documentation/instagram-platform/instagram-api-with-instagram-login).

It would be inaccurate to say Meta can only ever expose the app user's own posts. [Business Discovery](https://developers.facebook.com/documentation/instagram-platform/instagram-api-with-facebook-login/business-discovery) can expose information/media from other professional accounts within its authenticated scope, with exclusions such as age-gated accounts. However, this is account/username-based discovery and field expansion, not a universal public URL-to-caption endpoint for personal or professional posts.

The [Instagram Media reference](https://developers.facebook.com/documentation/instagram-platform/reference/instagram-media) lists fields such as `caption`, `permalink`, `shortcode` and media information, but field availability depends on the login/access configuration. The surveyed field table labels caption availability for Facebook Login; caption coverage across current login alternatives needs explicit verification before choosing an architecture.

Media URLs are not guaranteed: licensed audio, copyright flags and another user's disabled reel downloads can affect availability. The ability to read a caption does not imply the ability or permission to download a video.

### oEmbed is not an extraction loophole

The [Instagram oEmbed guide](https://developers.facebook.com/documentation/instagram-platform/oembed) restricts the endpoint to front-end embedding and prohibits consuming/manipulating/extracting/persisting the content or derivatives for other purposes. Accordingly, **do not use Instagram oEmbed as a recipe-caption extraction backend**.

It also excludes private/inactive/age-restricted/embed-disabled accounts and does not support Stories. [Meta oEmbed Read](https://developers.facebook.com/documentation/development/features-reference/meta-oembed-read) has App Review and Business Verification requirements for live-data access.

[Meta's dated oEmbed update](https://developers.facebook.com/blog/post/2025/04/08/oembed-updates/) documents removal of author/thumbnail fields and a migration date revised to 3 November 2025. Old code examples that assume those fields remain available should not be copied.

**Unresolved:** the exact currently accepted token types. The dated announcement and current guide/reference examples are not consistent enough to justify assuming anonymous access or old app-token rules. The [reference page](https://developers.facebook.com/docs/graph-api/reference/instagram-oembed/) retrieved primarily SDK examples rather than a complete unambiguous parameter contract.

### Implication for the fourteen Nibbles URLs

The generic research crawler was excluded by the observed robots rules; it fetched no Instagram post pages. A separate approved Instaloader trial subsequently recovered three captions, all with ingredient lists and numbered methods. Eleven captions remain untested; professional-account eligibility remains unverified. Neither the original exclusions nor the small positive sample establishes collection-wide import success.

The practical first fallback is explicitly user-supplied permitted text/screenshots or a creator recipe URL. A user-triggered clipping action is not permission to bypass access controls or platform terms, and session-cookie export is not a recommended integration.

## TikTok

### Display API covers the authorizing user's videos

[Display API setup](https://developers.tiktok.com/docs/en/display-api-get-started) requires a developer application, approved products/scopes and user authorization.

The [video query endpoint](https://developers.tiktok.com/docs/en/tiktok-api-v2-video-query) requires bearer authorization and `video.list` scope, and verifies that requested videos belong to the authorized user. Documented fields include title/video description and embed metadata, not a universal transcript or downloadable-media field.

Thus, signing a Nibbles user into TikTok does **not** authorize Display API lookup of every unrelated creator video they paste.

### Embedding is different from text/media extraction

[Embed Videos](https://developers.tiktok.com/docs/en/embed-videos) currently marks the legacy facility for upcoming deprecation and directs new integrations toward [Embed Player](https://developers.tiktok.com/docs/en/embed-player).

The current documented oEmbed example supplies an iframe/provider/dimension/embed response rather than the dedicated title/author/thumbnail fields common in older examples. This is a documentation observation, not a live endpoint guarantee about every returned field.

The player can display description and closed-caption controls, but documented host messages concern playback/state/time/errors. They do not expose a general description-text, transcript-track or media-bytes extraction API. Per-domain player usage limits may also affect playback.

### Research API is not the personal-app route

[Research API eligibility](https://developers.tiktok.com/products/research-api/) requires eligible researchers/institutions, an approved public-interest proposal, non-commercial independence and applicable ethical/security/regional requirements.

The [research codebook](https://developers.tiktok.com/docs/en/research-api-codebook) includes video descriptions and voice-to-text where that feature is available. That is not evidence that Nibbles qualifies or that a transcript exists for each source video.

### Observed public-page captions

Separately from those official APIs, this study read publicly delivered page HTML and checked the requested video ID against `webapp.video-detail.itemInfo.itemStruct.id`. Its caption field was present for 27 saved-recipe references / 25 videos. This was an ordinary unauthenticated GET, not a hidden endpoint or an official API.

The content split matters: nineteen references had ingredient and method text; three had ingredients only; three sparse captions; one referred to comments; one gave ratios/partial oven timings. Eight other short links returned the homepage without a video ID.

Public HTML is a fragile and policy-sensitive dependency. No claim is made that it will behave the same on another network or that robots permission alone establishes permission to extract/store derivative recipes. A development decision should precede production use.

**Subsequent validation:** [three direct TikTok captions were reobtained](source-tool-validation.md) and matched their earlier hashes; a fourth, short URL still redirected to the homepage without a video identity. The sample includes a full recipe, ingredients-only text and a sparse caption. Offline yt-dlp metadata mapping preserved full descriptions, but its native downloader was not run. These are overlapping checks, not three additional recipes added to the 27-reference total.

## YouTube

There is a clearer official path for public title/description:

- [Authentication guide](https://developers.google.com/youtube/data/guides/authentication): public-data requests can use an application API key; private/account operations require OAuth.
- [`videos.list`](https://developers.google.com/youtube/data/reference/rest/v3/videos/list): video-ID lookup with `part=snippet` supplies public title/description. Documented quota cost is one unit, not a monetary price.

Captions have a separate permission boundary:

- [`captions.list`](https://developers.google.com/youtube/data/reference/rest/v3/captions/list) requires OAuth and returns track metadata, not text; documented quota cost is 50 units.
- [`captions.download`](https://developers.google.com/youtube/data/reference/rest/v3/captions/download) requires OAuth and edit permission on the video; documented quota cost is 200 units.
- [Viewer transcript help](https://support.google.com/youtube/answer/15930243?hl=en) describes Show transcript when captions exist. This supports a user-mediated workflow, not an unrestricted official transcript-download API.

The actual crepe video yielded a public description and creator recipe link in this study. The linked recipe was retrieved. No API key, caption download, video/audio download or transcription was used. The current linked recipe includes filling/garnish absent from the saved recipe, so source identity does not eliminate component-selection review.

[YouTube API developer policies](https://developers.google.com/youtube/terms/developer-policies) separately govern credentials, privacy, consent and data use.

## User-mediated inputs and platform integration

Recommended fallback order:

1. Keep the source URL for attribution and identification.
2. Ask for a creator recipe-page link or pasted caption/description/recipe comment.
3. Accept screenshots of ingredient/method cards.
4. Accept audio/video only when the user can supply it with appropriate permission.
5. Present a draft, evidence and missing-field flags before saving.

Do not promise a universal "copy full caption" action or that native sharing includes caption text.

Primary platform references:

- [Android receiving shared data](https://developer.android.com/develop/ui/compose/sharing/receive): text/URLs/media can be sent through intents, but the sending application chooses the payload. This is not a ready-made MAUI share receiver.
- [MAUI clipboard](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/clipboard?view=net-maui-10.0): explicit string Paste is available; it grants no access to platform captions.
- [Apple Share extensions](https://developer.apple.com/library/archive/documentation/General/Conceptual/ExtensibilityPG/Share.html): host-supplied initial text/attachments. This is archived guidance; current MAUI extension packaging and actual payloads need a separate trial.
- [Windows receive-data guidance](https://learn.microsoft.com/en-us/windows/uwp/app-to-app/receive-data): UWP-focused Share contract documentation, not proof of drop-in WinUI/MAUI support.
- [Browser activeTab](https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/activeTab_permission): user-triggered, temporary tab access with origin/frame restrictions. It is an alternative interaction model, not blanket permission for arbitrary automatic collection.

## OCR, transcription and AI

**No generative AI**, **no cloud AI**, and **no machine learning at all** are three different requirements.

| Method | Potential contribution | Missing capability |
|---|---|---|
| Headings/bullets/numbering parser | Structures explicitly provided text without AI | Cannot recover omitted facts or reliably interpret every prose layout. |
| Local screenshot/frame OCR | Reads visible quantities and instructions | Cannot see missing frames or infer quantities never shown. |
| Local or hosted ASR | Reads spoken ingredients/actions | Cannot establish silent visual steps or unspoken ingredient amounts. |
| Text LLM | Converts awkward available text to a draft | May invent or "correct" facts incorrectly; cannot make an inaccessible caption accessible. |
| Multimodal LLM | Combines visible text, audio and action evidence | Still needs permitted media and explicit handling of unresolved quantities/conflicts. |

Representative primary references:

- [ML Kit text recognition](https://developers.google.com/ml-kit/vision/text-recognition/v2) and [ML Kit overview](https://developers.google.com/ml-kit): on-device Android/iOS OCR. MAUI bindings/Windows equivalent were not validated.
- [Tesseract](https://github.com/tesseract-ocr/tesseract): local OCR engine, including a neural recognition mode and image preprocessing considerations.
- [Whisper](https://github.com/openai/whisper): local multilingual speech recognition; not a social-URL downloader. Mobile packaging/performance untested.
- [Hosted speech-to-text documentation](https://developers.openai.com/api/docs/guides/speech-to-text): uploaded media processing, not arbitrary Instagram/TikTok retrieval. File/model limits and deprecations must be checked when implementing.
- [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs): constrained output shape still requires handling refusals/incomplete responses/API errors and independent factual/business validation.
- [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs): local structured JSON/vision options; model/hardware suitability is not established by this survey.
- [Provider data controls](https://developers.openai.com/api/docs/guides/your-data): API training is off by default unless opted in, but retention is endpoint-specific and special controls require eligibility/approval. Do not equate this with automatic zero retention.

Preserve evidence spans/timestamps and original quantities; distinguish extracted from suggested content. If the recipe was never fully stated, ask the user instead of silently constructing a plausible substitute.

## Commercial services

Representative vendor documentation, **not corpus tests or endorsements**:

- [Apify Instagram Scraper](https://apify.com/apify/instagram-scraper): advertises post/reel URL inputs, captions and structured output; explicitly unofficial.
- [Apify TikTok Scraper](https://apify.com/clockworks/tiktok-scraper): advertises video descriptions and optional subtitle/media features.
- [Firecrawl Scrape](https://docs.firecrawl.dev/features/scrape): advertises rendering, page extraction and JSON-schema outputs, with separate target-page status reporting. Some retrieval paths can involve other providers.

These add cost, third-party disclosure, retention, operational dependencies and platform/rights questions. Vendor success/HTTP 200 can describe the vendor job rather than successful retrieval of the target page; inspect target status and required recipe evidence.

No service was given private recipes or credentials, no account was created and no charges were incurred. Binding current prices and retention/legal suitability remain unverified. Do not use a vendor's claim to justify bypassing a source restriction.

## Unresolved before development

Exact Meta token/field availability under the selected login configuration; account/app eligibility; permitted extraction/transformation/storage; actual sender share payloads; modern MAUI receiving integrations; acceptable provider retention and costs; and accuracy/latency on representative permitted captions/screenshots/media.

Attribution and private storage are sensible, but neither settles platform terms or copyright. This appendix is a technical feasibility survey, not legal advice.
