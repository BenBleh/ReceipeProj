# TikTok and website extraction: actual validation

**13 November 2026 - research only. No Nibbles, recipe, NAS or account changes.**

## Result

**Free, non-generative tooling is a credible first stage for both source types, with important completeness checks.**

| Validation | Actual result | What this establishes |
|---|---|---|
| Website parser on retained pages | Required fields extracted from **11/11** hash-verified pages | `recipe-scrapers` can extract title, ingredients and methods from every website page already obtained successfully. Not 11/14 fresh URL successes. |
| Fresh website requests + parsing | **3/3** selected public pages returned usable recipe fields | Current URL-to-fields path works across Tasty Recipes, WP Recipe Maker and a different publisher format. |
| Retained TikTok text + metadata mapping | Full descriptions preserved for **27 references / 25 videos** | Existing requested-post captions can be mapped without using the shortened video title or processing media. |
| Fresh direct TikTok URLs | **3/3** returned the correct captions, identical to the earlier captures | Retrieval works now on these links, but only one of this deliberately varied sample contains both ingredients and method. |
| Fresh TikTok short link | **1** redirected to the homepage without a video ID | Treat as unresolved; do not import homepage/recommendation text as the requested recipe. |

These are different measurements: fetching content, extracting source fields, and producing the user's desired complete recipe. None should be reported as a single general import-success percentage.

## 1. Website parser validation

Installed **`recipe-scrapers==15.13.0`** in the existing isolated trial environment. Its `scrape_html(..., online=False, supported_only=False)` API parsed supplied HTML; our bounded HTTP reader, not the package, controlled live requests.

The generic-schema fallback matters: several of these domains used `SchemaScraper` rather than a dedicated site adapter. A domain does not necessarily need a bespoke integration if it publishes usable Recipe metadata.

### All retained, allowed website pages

The eleven pages were checked against their original response hashes before parsing. No new network requests were needed for this part.

| Recipe | Extracted ingredient lines | Extracted step entries | Notable finding |
|---|---:|---:|---|
| Blueberry & Lemon Buttermilk Pancakes | 13 | 3 | Source fields match JSON-LD. The saved ingredient groups are editorial, not headings in the source ingredient card. |
| Chicken Alfredo Lasagne | 15 | 9 | Source fields match JSON-LD; ingredient grouping metadata also available. |
| Chicken meatballs in a creamy paprika sauce | 14 | 8 | Source fields match JSON-LD; meatball/sauce groups recovered. |
| Chicken Pot Pie | 18 | **20** | Seventeen cooking actions plus **three section headings** in the library's instruction list. Do not present every returned entry as a cooking step. |
| Chicken rissoles | 11 | 6 | Ingredient punctuation differs from JSON-LD; inspected quantities/alternatives are retained. |
| Chicken Soup | 15 | 5 | Source fields match JSON-LD. |
| Creamy Mushroom Gnocchi | 12 | 7 | Source fields match JSON-LD. |
| Curried sausages | 12 | 9 | Source fields match JSON-LD. |
| Easy No-Knead Focaccia | 8 | 7 | Source fields match JSON-LD, but the ingredient list itself omits water that appears in the method. |
| Garlic Parmesan Chicken and Pasta (One-Pan) | 14 | 7 | Ingredient punctuation differs from JSON-LD; alternatives are retained. |
| Polenta Chips | 7 | 7 | Source fields match JSON-LD. |

All eleven supplied image metadata, but no image was downloaded. Grouping output covered the same ingredient multiset as the parser's flat list in all eleven; five recipes supplied named groups.

### Field fidelity, not just nonempty output

After case/whitespace/HTML-entity normalization, parser output matched the independently read Recipe JSON-LD for:

- **Title: 11/11**
- **Entire ordered ingredient list: 8/11**
- **Entire ordered method list: 10/11**
- **All three: 8/11**

The three ingredient-list differences were inspected offline. They remove redundant/doubled parentheses or normalize note/alternative punctuation; no quantity changes were found in those differing lines. That is not a reason to silently alter source text by default, but it explains why exact-string equality understates practical fidelity.

The pot-pie method difference is more important: the library includes section headings as separate entries. Its remaining seventeen cooking actions match the independent source sequence. This is a presentation/structure distinction, not three missing or invented cooking actions.

**Recommended extraction order:** Recipe JSON-LD for the base ingredient/action lists, with DOM/site adapters for component headings, notes and extra source detail. Preserve instruction sections separately; do not blindly treat `instructions_list()` output as an already-correct Nibbles step array.

### Source fidelity is not the same as recreating saved edits

The existing collection contains conversions, shortened instructions, extra water/butter rows, ingredient substitutions and custom groups. Exact saved-field equality remains a poor sole acceptance criterion.

Two useful examples:

- The focaccia card has eight ingredients, while the saved recipe has ten. Water is explicitly mentioned in the source method rather than its ingredient list. Copying that list faithfully still needs a cross-field completeness review.
- The pancakes' saved dry/wet ingredient grouping is not present as those headings in the source card. A faithful source importer should not pretend those exact personal group names were obtained from the URL.

No automatic semantic enrichment or missing-ingredient detection was implemented in this research.

### Fresh website checks

Three pages were requested again after fresh robots checks:

| Site / recipe | Result |
|---|---|
| CarnalDish / Blueberry & Lemon Buttermilk Pancakes | HTTP 200; title, 13 ingredients, 3 steps |
| Chef Jon Watts / Chicken Alfredo Lasagne | HTTP 200; title, 15 ingredients, 9 steps |
| Bon Appetit / Easy No-Knead Focaccia | HTTP 200; title, 8 ingredients, 7 steps |

For all three, extracted title/ingredient/method fields matched both the newly served Recipe JSON-LD and the prior captured fields under the stated normalization. Dynamic HTML changed; the recipe fields did not.

The three Simple Home Edit references were **not retried**. The earlier HTTP 403 remains a retrieval limitation, with the other two pages excluded by the same-host stop. A recipe parser cannot make an unavailable response accessible.

## 2. TikTok validation

### Fresh retrieval sample

Used ordinary unauthenticated HTTPS, a descriptive research user agent, current robots checks, a response-size bound and no automatic redirect following. Read only the exact requested video's data, requiring status zero and matching numeric video IDs.

The sample deliberately includes different known content conditions rather than only promising recipes:

| Saved recipe | Retrieval | Content consequence |
|---|---|---|
| Chicken boscaiola | HTTP 200; matching 2,096-character caption | Ingredient components and twelve numbered actions are available; saved method condenses them to five steps. A source mushroom quantity lacks a unit, so do not invent one. |
| Creamy Chicken and bacon pasta soup | HTTP 200; matching 787-character caption | Ingredient checklist available, but not the seven saved cooking steps. Successful retrieval must still trigger a missing-method fallback. |
| Dumplings | HTTP 200; matching 373-character caption | Promotional/descriptive text, not the required ingredient quantities and full method. Needs another permitted source or user input. |
| Bang Bang Chicken | HTTP 302 to TikTok homepage | No requested video identity. Homepage was not followed; no caption was attributed to this recipe. |

All three returned captions have the same SHA-256 values as the earlier reviewed captures. Their prior manual content classifications therefore remain applicable. The HTML changed, but caption text did not.

No video, audio, subtitles, comments, thumbnail or profile was downloaded. No user login, exported cookies, proxy switching, browser impersonation or challenge solving was used.

### What was tested with yt-dlp

Installed **`yt-dlp==2026.10.26`**, but used **only its offline TikTok metadata parser** on the exact requested-post structures from retained/fresh HTML:

- All **27 references / 25 videos** preserved the complete `desc` text in the resulting `description`.
- Each retained HTML body and caption was verified against its earlier hash.
- Network access through yt-dlp was explicitly disabled.
- Video-format and subtitle extraction were disabled by using its flat metadata parsing path.

The three fresh examples produced a **72-character video title**, while their descriptions were 373-2,096 characters long. **Use `description`, not `title`, as the recipe-text input.** Neither field is automatically the final recipe title.

This is **not a successful standalone yt-dlp URL/downloader trial**. Its native transport and automatic challenge behavior were not exercised. The private parser helper is a version-sensitive research dependency, not a proposed stable app API.

For a caption-only importer, the simpler observed public-HTML/requested-post adapter already provides the text without downloading a video. Adding a general media downloader is not necessary for the demonstrated cases.

### Existing broader coverage remains unchanged

The earlier study recovered captions for 27 of 35 TikTok recipe references. Nineteen had ingredient and method text; the others were ingredients-only, sparse, comment-referenced or partial.

This pass revalidated a subset; it does **not** add three new references to those totals. Eight short links in the original corpus still lack an identified video, and no new caption-to-recipe parsing accuracy percentage is claimed.

## 3. Practical import architecture

The evidence supports keeping the proposed first stage simple:

| Source | First attempt | Fallback condition |
|---|---|---|
| Ordinary recipe website | Bounded page retrieval -> Recipe JSON-LD -> groups/notes from DOM when needed | Access restriction, missing/ambiguous recipe, or conflicting/incomplete fields |
| TikTok | Resolve a stable post identity where possible -> exact requested-post description | Unresolved short URL, restricted content, sparse caption, missing method/quantities |
| Instagram | Replaceable anonymous caption extractor, as demonstrated separately | Access/rate limit, extractor failure, or insufficient recipe content |

For every source, open an editable draft and preserve attribution. Fallback means an explicit caption paste, expanded-caption screenshots, a creator recipe-page link or other permitted user-supplied evidence. It is needed for **content gaps as well as network failures**.

No paid AI service is necessary for the demonstrated website extraction or caption retrieval. Optional local AI could help structure awkward available prose, but cannot supply facts absent from the source. No AI/OCR/ASR performance or Synology deployment was benchmarked.

## Evidence and reproduction

- [source-tool-validation.json](source-tool-validation.json): package versions, hashes, per-field comparisons, grouping diagnostics, live outcomes and fresh robots records. No caption bodies or account tokens.
- [validate_source_tools.py](validate_source_tools.py): standalone research harness. Uses the existing public-request helpers in [probe_sources.py](probe_sources.py); it is not wired into Nibbles.
- Original pinned snapshot: `0703832e953e41966957650d07b076a67a7bd97d`.
- Original inventory/probe records remain unchanged. The live sample's HTML is stored only in the private session cache.

The harness requires the inspected package versions, the original private cache, a **new** trial-cache directory and a **new** output path. It refuses to overwrite an earlier run:

```powershell
& "<trial-venv>\Scripts\python.exe" `
  "C:\Dev\ReceipeProj\Design\AutoAddResearch\validate_source_tools.py" `
  --private-cache "<original-private-cache>" `
  --trial-cache "<new-private-trial-cache>" `
  --output "<new-local-results-file.json>"
```

Tool execution was local. No collection was uploaded to an external extraction/AI service, nothing was committed/pushed, and no app importer was implemented. Public availability and successful extraction do not independently settle source terms, image reuse or retention rights.
