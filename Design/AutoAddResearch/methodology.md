# Evidence, scope and reproducibility

## Scope and boundaries

Research was performed on 13 November 2026 against the read-only pinned `rfiles` commit `0703832e953e41966957650d07b076a67a7bd97d`. The source application was Nibbles 1.6.0; its code was inspected only to understand destination fields and validation.

The user requested research, not application development. Files here are research reports, generated inventories/evidence and standalone collection/analysis scripts. They are not wired into the app, its builds, CI or production storage. No recipe, application, package, installer or editable design was changed. No GitHub write, commit or push was performed.

The initial [focused Instagram follow-up](instagram-caption-research.md) added official-documentation findings, static inspection of indexed open-source tool snapshots and user-assisted sharing/OCR options. It separately checked the existing fourteen Instagram records offline for creator handles or website links in Source/Notes; none were found. That documentation-only phase exercised no extractor.

The user subsequently approved a small local free-tool trial. [Separate Instaloader evidence](instagram-instaloader-trial.md) records three anonymous caption retrievals with version 4.16 in an isolated session-local environment. It used the library's normal anonymous metadata path/default client headers, not the original robots-gated HTML crawler. Four HTTP requests returned 200; no login, imported browser cookies/session, media download, redirect, retry or hosted service was used. Response bodies were hashed but not retained; three caption text files are in the private session cache and their hashes were verified. Original crawler classifications remain unchanged; current combined coverage is explained in the README. No official API, share payload or OCR engine was exercised.

The requested [website/TikTok validation](source-tool-validation.md) then exercised recipe-scrapers 15.13.0 on eleven hash-verified website fixtures and three fresh, robots-checked public pages. It rechecked three direct TikTok URLs and one unresolved short URL with the original bounded HTTP helper. TikTok status/identity and caption hashes were verified; yt-dlp 2026.10.26 was used only for offline flat metadata mapping with its network access disabled. Its native downloader, challenge handling, formats and subtitles were not exercised. Follow-up HTML is private; sanitized outcomes are in a new JSON record rather than overwriting original probes.

## Collection

1. Read the branch reference, commit/tree and archive using the existing local token file, provided to the inventory script through an environment variable. The token was not printed, copied into this folder or used for public-source requests.
2. Pin all recipe reads to the returned commit. Verify every examined JSON file's Git blob SHA against the tree. Retain the full private snapshot only in the separate local session cache.
3. Inspect all 72 recipe records. Extract HTTP(S) source addresses, classify host/platform and record saved ingredient/step/group counts. There are 66 nonempty source fields, 64 usable source URLs and eight recipes without a usable URL.
4. Remove social tracking/sharing queries from exported inventory URLs. Keep stable video/post identity rather than transient sharing identifiers. Do not silently reinterpret the two non-URL labels as web addresses.

There are 64 distinct stored URL strings, reducing to 63 after tracking cleanup. URL strings are not the same thing as source-content identities: two TikTok video IDs each serve two saved recipe variants, one pair evident after query cleanup and another after short-link resolution. The remaining unresolved short links cannot safely be deduplicated.

## Public-source probes

The initial probe used Python standard-library HTTPS requests and the explicit `NibblesSourceResearch` user agent. No browser impersonation, accounts, cookies, private endpoints, session export, proxy rotation, CAPTCHA solving or access-control bypass was used.

For each origin, fetch its robots file first. If unavailable, conservatively skip crawling; if disallowed, record that without fetching the post page. Each allowed page request has a 30-second timeout, an 8 MB response bound and a one-second pause. A 401/403/429 stops further page probes on that origin. The configured one-second spacing is a study bound, not a general-purpose production crawler policy; a future implementation must handle applicable rate limits and crawl-delay explicitly.

The initial probe deliberately did not automatically follow HTTP redirects. A separate bounded pass:

- Recorded eight TikTok homepage fallbacks without requesting the homepage or guessing a video.
- Extracted numeric video identities from seven other TikTok redirects, removed sharing parameters, checked the destination origin's robots policy and requested each identifiable public destination once.
- Separately checked the YouTube destination before requesting its watch page.

No redirect chain or restricted-host workaround was attempted. The source's reported HTTP 403 at Simple Home Edit remained a blocker; the other two recipe pages on that origin were not requested.

During that initial crawler pass, all fourteen Instagram source URLs were considered against the observed exclusion rules. **They did not receive fourteen failed page requests**: they received policy-excluded classifications, with no post content fetched in that pass. The same distinction applies to same-host stops and unresolved redirects. The later anonymous tool experiment is separate evidence described above.

## What was extracted and compared

### Websites

Parse public `application/ld+json` blocks without executing page JavaScript. Locate Recipe objects, including nested graphs, and flatten the observed `HowToStep`/`HowToSection` structures into ordered text for counting/comparison.

Record source title/ingredient/step counts, image metadata availability, recipe-card markers, response SHA-256 and basic equality/line-overlap diagnostics. All eleven successful initial website pages had a single Recipe candidate. Group/notes adapters were considered but not implemented.

Normalization for website equality:

1. Remove HTML tags from text and decode entities.
2. Collapse whitespace.
3. Ignore case.
4. Compare the entire ordered ingredient list and ordered step list, not just counts.

No synonym, punctuation, unit, fraction or semantic normalization is implied. For example, `1 cup` versus `237 ml` remains unequal, and parentheses removal can cause a harmless inequality. Conversely, equal counts do not establish equal content. Factual and editorial examples were examined separately for the report.

### TikTok

Read only the delivered `__UNIVERSAL_DATA_FOR_REHYDRATION__` object's `__DEFAULT_SCOPE__.webapp.video-detail.itemInfo.itemStruct`, not arbitrary `desc` fields from recommendation objects.

Require post status zero and equality between requested and returned numeric video IDs. Record caption length/hash, manual content classification, literal-presence diagnostics and advertised subtitle-track counts. No subtitle/media URLs or full captions are exported to this folder.

The classification `ingredients_and_method_text` means a reviewer observed ingredient information and cooking directions in the caption. **It does not mean all quantities are unambiguous, every cooking action is stated, an extractor matched the saved recipe, or a draft passed human review.**

Literal-presence diagnostics check normalized saved ingredient/step text as substrings in the caption. They are deliberately limited, may be affected by wording or boundaries, and must not be described as semantic precision/recall.

### YouTube and its linked website

Read the watch page's delivered player metadata for title, description and advertised caption-track count. Follow the description's explicit creator recipe link using HTTPS after checking the destination policy. Retrieve and count the linked Recipe fields.

This is a real linked-source probe but not an exact import: the current linked Nutella-crepe recipe is broader than the saved plain-crepe recipe. No caption text, video or audio was downloaded.

## Reading the evidence files

- `source-probes.json`: initial URL/policy/HTTP observations. `robots_disallowed_not_crawled` and `host_access_stop_not_crawled` are **not** page response codes. Initial `public_response_no_recipe_jsonld` for TikTok is superseded by caption assessment, not evidence that no recipe text exists.
- `redirect-probes.json`: the separately checked identifiable destinations. Original transient sharing parameters are deliberately not retained.
- `youtube-linked-probe.json`: explicit creator-link provenance and linked-page outcome.
- `extraction-assessment.json`: final observed-coverage counts, website comparison counts, caption evidence hashes/classifications and confirmed duplicate-content groups.
- `per-recipe-feasibility.csv`: final user-facing matrix. Saved counts describe the current app data; source counts are filled only where structured fields were actually observed. Blank social source counts are not zero-ingredient/zero-step claims.

Image availability means an image/cover URL was advertised in metadata. No images were fetched, validated, licensed, resized or imported.

## Reproducibility scripts

Python 3 standard library only; no dependencies were installed.

| Script | Network effects | Output |
|---|---|---|
| `inventory_sources.py` | Authenticated **read-only GitHub** requests; no repository writes | Current pinned inventory and separate private snapshot |
| `probe_sources.py` | Conservative unauthenticated public robots/page requests | Initial per-source evidence and separate cached HTML |
| `probe_redirects.py` | Separate identifiable public-target checks only | Follow-up evidence; removes transient sharing queries from initial records |
| `assess_evidence.py` | **None** | Offline matrix and statistics from retained evidence; contains the study's manual caption classifications |

Scripts expect an explicitly supplied `--private-cache` directory, outside the research deliverable folder. The inventory additionally expects `NIBBLES_GITHUB_TOKEN_FILE` to name an existing private local file. Do not put that file or its contents into the research folder, command output, source control or an external service.

An example invocation shape is:

```powershell
python .\inventory_sources.py --private-cache C:\PrivateResearchCache
python .\probe_sources.py --private-cache C:\PrivateResearchCache
python .\probe_redirects.py --private-cache C:\PrivateResearchCache
python .\assess_evidence.py --private-cache C:\PrivateResearchCache
```

These commands are **not a test suite** and should not be run automatically. The inventory fetches the latest head and overwrites generated inventory files; a future run can change both corpus and network results. Use a separate output copy/cache for a new study. Manual caption classifications are specific to the captured video IDs and must be reviewed or extended if the corpus changes.

The YouTube creator-link follow-through was a separately bounded interactive standard-library probe; its result, URL, response hash and timestamp are persisted in `youtube-linked-probe.json`, with the page cached privately. The matrix's YouTube interpretation is study-specific, not a general extractor implementation.

## Private evidence retention

Full recipe data, the pinned ZIP and downloaded HTML were retained locally under this session's `files\autoadd-research-20261113` cache, not in the application repository's research folder. These contain private/user data and copyrighted source material; do not publish or feed them to a new third-party service without a separate permission/privacy decision.

The reports themselves still contain the user's collection titles, IDs and public source URLs. **They should be treated as private research material**, not automatically committed/pushed or published just because the URLs are public.

## Limits

- A one-time successful public GET does not prove availability on every platform, country, network or future date.
- Robots permission, public accessibility, embedding support and an unofficial service's capability are distinct from authorization to reuse/retain content. Terms/rights need review.
- No Instagram caption was retrieved, no logged-in browser was used and no API application/access review was attempted.
- No external AI/extraction service processed the private collection. No model accuracy, cost or latency result is claimed.
- No OCR, ASR, social comment, video/audio/image download or media-analysis trial was performed.
- Existing edited recipes are not equivalent to source-transcription truth; exact equality and source-faithful correctness must be evaluated separately.
- No importer app code or UI exists as a result of this work, so no importer Android/iOS/Windows runtime compatibility is claimed.
