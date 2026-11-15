# Anonymous Instagram caption trial

**13 November 2026 - research only. Positive result: captions returned for all three attempted source URLs, without an Instagram login or business account.**

The user explicitly approved a small local trial of free tooling, separate from Nibbles or NAS development.

## What actually worked

Instaloader **4.16** returned the requested captions from three existing `rfiles` source links. All three captions contained an identifiable recipe title, an explicit ingredient list and numbered cooking directions. No video processing, paid service or separate AI extraction model was used.

| Saved recipe | URL form | Caption characters | Source / saved ingredient lines | Source / saved numbered steps | Assessment |
|---|---|---:|---:|---:|---|
| Creamy chicken | `/reel/` | 1,435 | 12 / 11 | 8 / 8 | Recipe text is available. Source includes a salt/pepper ingredient row absent from the saved list; saved wording and final serving instruction were edited. |
| Creamy tomato burrata pasta | `/p/` | 1,095 | 10 / 10 | 8 / 7 | All ten ordered ingredient strings match after case/whitespace normalization. The eighth source step is a closing enjoyment line, omitted from the saved method; other punctuation differs. |
| Crispy chicken bao buns | `/p/` | 1,154 | 11 / 10 | 7 / 7 | Recipe text is available, but the saved recipe changes chicken thighs to breast and omits the coleslaw ingredient. Source also has an optional garnish line outside the dashed list. |

These are **three successful caption retrievals**, not three completed app imports or an estimate of overall Instagram reliability. The other **eleven** source URLs were not attempted. The sample was selected deterministically: the first reel and first two `/p/` references in the existing inventory, not links preselected for known accessibility.

No creator username was supplied or discovered as a prerequisite. The user's personal Instagram account was not accessed.

### Why the saved recipes still differ

The burrata example is especially encouraging for deterministic ingredient extraction. Conversely, no URL-only importer can infer that the user wants chicken breast instead of the creator's thighs. Import the source faithfully into a reviewable draft, rather than trying to recreate personal edits without evidence.

The captions also reveal parser requirements:

- The burrata caption uses Unicode line separators (`U+2028`), not just ordinary newlines. A parser must handle these.
- The bao caption starts with promotional text; its first line is not the recipe title.
- Bao ingredient rows include price annotations, which must not become food quantities.
- Optional garnishes must not disappear simply because they are outside a dashed list.
- A numbered closing remark need not be a cooking step, but removing it should not remove useful serving instructions elsewhere.
- Preserve fractions, heaped teaspoons, stock preparation and ingredient alternatives.

Counts were verified offline from the returned text and compared with the pinned private recipe snapshot. This was a manual content assessment plus simple line/count checks, **not a benchmark of an automated recipe parser or AI model**.

## Method and evidence

Installed the official PyPI package `instaloader==4.16` in an isolated virtual environment under the session's private files directory. No global installation, application dependency change, NAS deployment or account configuration was performed.

The [trial script](trial_instagram_captions.py) invokes the library's normal anonymous metadata path with:

- No login, imported browser cookies, saved user session or official Meta token.
- No pictures, videos, thumbnails, comments, profiles or geotags downloaded as separate resources.
- One connection attempt; no retries or account/proxy switching.
- A maximum of twelve HTTP requests, restricted to HTTPS `www.instagram.com`.
- No redirects followed; the entire trial stops on an HTTP error, redirect, network failure or extractor error.
- Temporary anonymous client state only. The library may receive anonymous cookies in memory; no personal-account cookies were supplied or persisted.

The successful run made **four HTTP requests**, all status **200**: one anonymous bootstrap page and three metadata requests. Per-source retrieval time was approximately **2.0-3.8 seconds** on this machine/network; the whole run took approximately fifteen seconds including deliberate pauses. This is not a phone/NAS latency benchmark.

This is **not the official Instagram Graph API**. Instaloader uses an unsupported internal interface and its normal default client headers. No query identifiers, token values, request bodies, response headers containing cookies or workaround instructions are included in these artifacts.

The approach differs from the original robots-gated generic HTML crawler. Its fourteen exclusions remain unchanged in the original evidence. The new, explicitly approved tool trial is recorded separately; it does not mean robots rules changed, that the earlier crawler fetched those pages, or that deployment/retention rights have been established.

### Files

| Artifact | Contents |
|---|---|
| [instagram-instaloader-trial.json](instagram-instaloader-trial.json) | Actual execution timestamps, exact installed dependency versions, request statuses/body hashes, requested shortcodes and caption lengths/hashes. |
| [trial_instagram_captions.py](trial_instagram_captions.py) | Bounded, caption-only research harness. Requires the inspected `instaloader==4.16`; not wired into the app. |
| Private session cache | Three caption text files, named by recipe UUID. Verified against the recorded SHA-256 values; not copied into the research folder. |
| [source-inventory.csv](source-inventory.csv) | Original sample identities and saved counts, pinned to the existing repository snapshot. |

The JSON's `recipe_completeness: not_assessed` records the retrieval stage as executed. The subsequent content review is the table above; the original execution record was not rewritten to imply an automated completeness check.

### Reproduction

Run the standalone script with the isolated environment's Python and supply:

```powershell
& "<trial-venv>\Scripts\python.exe" `
  "C:\Dev\ReceipeProj\Design\AutoAddResearch\trial_instagram_captions.py" `
  --inventory "C:\Dev\ReceipeProj\Design\AutoAddResearch\source-inventory.csv" `
  --output "<new-local-results-file.json>" `
  --private-caption-dir "<new-private-caption-directory>"
```

Replace placeholders with new local paths; existing evidence is intentionally not overwritten. Dependencies used in this run are recorded in the JSON. Do not repeatedly rerun against a login/rate-limit barrier or treat this example as platform authorization.

Version matters: the [4.16 release notes](https://github.com/instaloader/instaloader/releases/tag/v4.16) include anonymous profile retrieval fixes and request-handling changes. Those notes do not prove these three post lookups will remain available.

## Recommendation after the trial

**The user's preferred direction now has actual supporting evidence:**

`Instagram URL -> bounded anonymous caption attempt -> recipe draft`

On failure or insufficient recipe content:

`explicit caption paste / expanded-caption screenshots / creator recipe-page link -> recipe draft`

No business account is required for the demonstrated anonymous path. A personal account also does not grant extra official API access, but it does not need to be involved here.

For a future implementation:

1. Keep the unofficial extractor replaceable and server-side if practical, rather than embedding its Python dependencies and changing interfaces into the mobile app.
2. Check both retrieval and content sufficiency. A returned caption can still be promotional or ingredients-only.
3. Stop promptly at access/rate-limit barriers; do not cycle through tools/accounts to get around them. Use a user-assisted fallback instead.
4. Distinguish temporary network trouble, login/access restriction, unavailable content and missing recipe fields in the UI.
5. Cache permitted results and avoid repeated source calls for the same draft.
6. Preserve attribution and review before saving through the existing Nibbles write path.

Since Instaloader succeeded, no second downloader was installed or invoked. There is no evidence yet that chaining several tools improves coverage enough to justify the extra maintenance.

**Remaining gates:** source/platform permissions and retention terms, reliability across more posts and networks, failure UX, caption-to-draft accuracy, and NAS/container performance. Successful anonymous retrieval is technical evidence, not permission to deploy automated collection or store derivatives. See the [earlier access/terms investigation](instagram-caption-research.md).

No app importer, fallback UI, OCR integration, NAS service or recipe write was implemented.
