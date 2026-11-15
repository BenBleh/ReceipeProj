# Nibbles: adding recipes from a source URL

**Research and a standalone review prototype - 13 November 2026. No production importer, application changes or recipe changes have been made.**

**Approved prototype:** the [local browser draft reviewer](Prototype/README.md) now supports explicit live website/Instagram/TikTok URL lookups as well as 39 offline captures and pasted text. It offers editable ingredient groups/methods and reviewed JSON export, with no rfiles writes. [Fresh lookup results](Prototype/live-validation.json) include an uncaptured website and Instagram post plus a fresh TikTok lookup. [Offline diagnostics](Prototype/quality-results.json) measure field presence, not import accuracy; collapsed TikTok captions still need substantial correction.

**Instagram follow-up:** an explicitly approved [anonymous Instaloader trial](instagram-instaloader-trial.md) recovered captions for **all three attempted source URLs**, without a login or business account. Each contained an ingredient list and numbered method. That trial left eleven references untested; the later live prototype recovered one additional caption, leaving ten untested. [Caption-first research](instagram-caption-research.md) covers official API limitations, tools, paste/OCR fallback and the NAS.

**Website/TikTok validation:** [Actual tool results](source-tool-validation.md) cover 11 cached website pages, three fresh website successes, 27 retained TikTok caption references, three fresh direct-link captions and one unresolved short link. The validation found meaningful instruction-section and missing-method cases; returned text is not automatically a complete recipe.

## 1. Findings and recommendation

A useful URL-to-draft feature is feasible, but **a URL cannot reliably reproduce every saved recipe exactly**. Retrieval and recipe extraction are different problems, and the saved collection contains personal edits, unit conversions, shortened instructions, added groups and some historical anomalies that the original URL does not describe.

The strongest practical approach is a **hybrid, source-first pipeline**:

1. Extract ordinary recipe websites deterministically, using Recipe JSON-LD first and recipe-card HTML for missing information, notes and groups.
2. Obtain social-post text through an allowed, explicit route. TikTok captions were much more useful in this investigation than a generic "video requires AI" assumption suggests, but their public page format is an unsupported, policy-sensitive dependency.
3. Offer optional AI to structure available caption/prose text, not to guess an inaccessible recipe.
4. When information is genuinely missing, accept pasted text, screenshots, a creator's recipe-page link, or permitted media. Use OCR/transcription only where needed.
5. Always open an editable, evidence-backed draft. Do not silently save, overwrite an existing recipe, invent quantities or claim a partial import is complete.

**Do not market a first version as "paste any Instagram/TikTok URL and get the same recipe".** Most source-linked entries are social posts, so a website-only feature would be valuable but would not address most of this collection.

### Actual collection examined

The read-only `BenBleh/rfiles` snapshot was pinned to:

`0703832e953e41966957650d07b076a67a7bd97d`

| Source category | Recipe references | What was actually observed |
|---|---:|---|
| Ordinary recipe websites | 14 | 11 public pages supplied Recipe JSON-LD containing title, ingredients and instructions. One Simple Home Edit request returned HTTP 403; the other two on that host were deliberately not retried. |
| TikTok | 35 | The exact requested post's caption was recovered for 27 references, representing 25 distinct videos. Eight short links redirected to the TikTok homepage without identifying a video. |
| Instagram | 14 | The original HTML crawler was excluded by observed robots rules. A separate authorized Instaloader trial recovered captions with ingredients/methods for 3/3 attempted references; the later live prototype recovered another, leaving ten untested. |
| YouTube | 1 | The public description linked to the creator's recipe website. That page supplied Recipe JSON-LD, but for a broader Nutella-crepe recipe rather than the saved plain-crepe version. |
| No usable source URL | 8 | Six have no source text; two contain only a non-URL source label. These are outside the URL-only evaluation. |
| **Total** | **72** | **64 source-linked recipes; 64 distinct stored URL strings, or 63 addresses after tracking cleanup.** |

TikTok and Instagram together account for **49 of 64 source-linked recipes**. There are two confirmed pairs of saved recipes that point to the same underlying TikTok video despite using different URL strings.

### What those observations do - and do not - establish

Of the **27 recovered TikTok captions**:

| Caption content | Recipe references | Implication |
|---|---:|---|
| Ingredient information and cooking directions | 19 | Promising for text-to-draft; some directions are prose, quantities are ambiguous or source lists contain mistakes. Not a complete-import accuracy result. |
| Ingredients but no cooking method | 3 | Caption parsing alone cannot reproduce the saved steps. |
| Sparse promotional/descriptive caption | 3 | Ingredients and method require another allowed source. |
| Recipe said to be in comments | 1 | The caption is not the recipe; comments were not fetched. |
| Ingredient ratio and partial oven timings | 1 | Useful evidence, but not a fully specified recipe. |

The original HTML study exposed both ingredient and method text for **11 website references plus 19 TikTok references**, with a further linked-page candidate for the YouTube reference. The later [Instagram tool trial](instagram-instaloader-trial.md) adds **three** references with ingredient lists and numbered methods. These are **source-text availability observations, not 33 or 34 proven successful imports**. No production importer, AI recipe-extraction model, OCR engine or transcription model was benchmarked.

For the 11 website recipes, a conservative comparison decoded HTML entities, collapsed whitespace and ignored case:

| Field equal to the saved recipe under that normalization | Count |
|---|---:|
| Title | 8 / 11 |
| Entire ordered ingredient list | 1 / 11 |
| Entire ordered method | 2 / 11 |
| All three together | **0 / 11** |

This is **not** a claim that website importing has 0% accuracy. Much of the mismatch is harmless presentation, while other differences are substantive edits. It shows why exact reproduction of an already-edited collection is the wrong single measure for a future importer.

## 2. Evidence and per-recipe coverage

Every recipe is represented in [per-recipe-feasibility.csv](per-recipe-feasibility.csv), including the eight without a URL so exclusions are explicit. Filter by `platform`, `observed_outcome` or `same_source_recipe_ids`. Rows contain the saved title/counts, source URL, observed retrieval result, suggested route, AI role and fidelity caveats.

That CSV preserves the original study's observations. The later Instagram extractor results are separate evidence in [instagram-instaloader-trial.json](instagram-instaloader-trial.json), not replacements for the original crawler's exclusion records.

The underlying records are:

| File | Purpose |
|---|---|
| [source-inventory.csv](source-inventory.csv) | All 72 recipes, public source addresses, source categories and saved ingredient/step/group counts. |
| [corpus-summary.json](corpus-summary.json) | Pinned repository commit, acquisition time and domain/platform totals. |
| [source-probes.json](source-probes.json) | Initial policy checks and per-URL HTTP/structured-data observations. |
| [redirect-probes.json](redirect-probes.json) | Separate checks of seven identifiable TikTok short-link destinations and the YouTube destination. |
| [youtube-linked-probe.json](youtube-linked-probe.json) | Actual creator-website follow-through from the YouTube description. |
| [extraction-assessment.json](extraction-assessment.json) | Coverage/equality totals, requested-versus-returned TikTok IDs, caption hashes and reviewed caption classifications. |
| [social-platforms.md](social-platforms.md) | Official API/embedding restrictions, user-mediated alternatives and primary documentation. |
| [instagram-caption-research.md](instagram-caption-research.md) | Focused low/no-cost Instagram investigation: Business Discovery prerequisites, full-caption distinctions, tool limitations, sharing/OCR and NAS role. |
| [instagram-instaloader-trial.md](instagram-instaloader-trial.md) | Actual anonymous 3/3 caption retrieval, ingredient/method review, limitations and recommended fallback architecture. |
| [instagram-instaloader-trial.json](instagram-instaloader-trial.json) | Execution evidence, dependency versions, HTTP statuses and caption hashes; no caption bodies or account tokens. |
| [trial_instagram_captions.py](trial_instagram_captions.py) | Standalone bounded experiment using Instaloader 4.16; not an app importer. |
| [source-tool-validation.md](source-tool-validation.md) | Website/TikTok tool validation, source-field fidelity, fresh samples and completeness limitations. |
| [source-tool-validation.json](source-tool-validation.json) | Cached/live results, package versions, hashes, grouping diagnostics and per-field comparisons. |
| [validate_source_tools.py](validate_source_tools.py) | Standalone website parser and TikTok caption validation; no app or storage integration. |
| [Prototype/README.md](Prototype/README.md) | Running the approved browser prototype, explicit live lookup behavior, offline extraction findings and limitations. |
| [Prototype/quality-results.json](Prototype/quality-results.json) | Diagnostics for 39 deduplicated retained sources; no full caption bodies or semantic-accuracy claim. |
| [Prototype/live-validation.json](Prototype/live-validation.json) | Actual bounded live lookup results, including two sources outside the capture catalog; no full source bodies. |
| [methodology.md](methodology.md) | Probe boundaries, reproducibility, comparison definitions, private-cache handling and limitations. |

Full private recipes and downloaded page bodies are **not** in this research folder. They were retained in a separate local session cache. No private collection was sent to an external extraction/AI service, and nothing was committed or pushed.

## 3. Ordinary websites: the most dependable non-AI route

### Observed website results

Ingredient and step counts below are **source -> saved**, not an accuracy score.

| Saved recipe | Site | Ingredients | Steps | Important difference |
|---|---|---:|---:|---|
| Blueberry & Lemon Buttermilk Pancakes | CarnalDish | 13 -> 13 | 3 -> 3 | Saved flour/buttermilk use metric conversions; saved ingredient groups are not reproduced simply by taking the JSON ingredient array. |
| Chicken Alfredo Lasagne | Chef Jon Watts | 15 -> 15 | 9 -> 9 | Normalized steps match; ingredient differences are mainly punctuation/preparation-note presentation. An especially good deterministic pilot. |
| Chicken meatballs cooked with a creamy garlic and paprika sauce | Chef Jon Watts | 14 -> 15 | 8 -> 8 | Different saved title, a stray saved ingredient row and copied ingredient text within a saved step. Do not teach an importer to recreate accidental data. |
| Chicken Pot Pie | RecipeTin Eats | 18 -> 18 | 17 -> 18 | Saved version adds a notes step and uses clarified ingredient names in instructions. |
| Chicken rissoles | RecipeTin Eats | 11 -> 9 | 6 -> 2 | Saved version omits oil/ketchup rows and greatly abbreviates the method. URL alone cannot know this preference. |
| Chicken Soup | Ambitious Kitchen | 15 -> 15 | 5 -> 5 | Normalized ingredients match, but the saved title is shortened and couscous is clarified in the method. |
| Creamy Mushroom Gnocchi | Spend With Pennies | 12 -> 12 | 7 -> 7 | Saved quantities convert ounces to approximate grams and alter preparation-note formatting. |
| Curried sausages | RecipeTin Eats | 12 -> 12 | 9 -> 9 | Mostly punctuation, spacing and removal of recipe-note references; source notes still matter. |
| Easy No-Knead Focaccia | Bon Appetit | 8 -> 10 | 7 -> 11 | Water comes from the source method, butter uses were separated, temperatures clarified and long actions split. |
| Garlic Parmesan Chicken and Pasta (One-Pan) | Munchin With Maddie | 14 -> 15 | 7 -> 7 | A saved row is a heading rather than food; wording and a cooking-time range differ. |
| Polenta Chips | I'm A Foodie | 7 -> 7 | 7 -> 7 | Normalized steps match; ingredient unit/fraction/preparation formatting differs. |
| Chicken Boscaiola Pasta | Simple Home Edit | Not retrieved | Not retrieved | HTTP 403. No workaround or repeated request. |
| Creamy Lemon Chicken Risoni | Simple Home Edit | Not attempted | Not attempted | Same-host stop following the 403. |
| Creamy Mushroom and Bacon Fettuccine | Simple Home Edit | Not attempted | Not attempted | Same-host stop following the 403. |

### How website extraction should work

Public recipe pages often already contain the necessary machine-readable data. [Schema.org Recipe](https://schema.org/Recipe) and [Google's recipe documentation](https://developers.google.com/search/docs/appearance/structured-data/recipe) specify fields such as `name`, `recipeIngredient`, `recipeInstructions` and `image`.

The future parser should handle:

- Multiple JSON-LD scripts, `@graph`, nested objects, `@type` arrays, references and multiple recipe candidates. Match the main page recipe, not a recommended recipe or navigation card.
- Instructions represented as strings, arrays, `HowToStep`, `HowToSection` and nested `itemListElement`. Preserve section order and distinguish a section heading from an actual action.
- HTML inside text, entities, nonbreaking spaces, fractions, ranges, alternative quantities and optional ingredients. Avoid converting units or rounding during initial extraction.
- Ingredients/preparation notes, recipe notes, component headings and serving information in the recipe-card DOM that may not be present in the flat JSON-LD array.
- Missing/contradictory time metadata and ingredients used only in the method. Flag discrepancies rather than silently "fixing" them.

Nine of the eleven accessible website pages had `wprm-recipe` card markers; CarnalDish had `tasty-recipes` markers. This gives a particularly concrete non-AI opportunity to recover groups and notes using shared card adapters rather than eleven unrelated page scrapers. Custom card adapters were **not** implemented in the original study. The follow-up validation and offline prototype exercise existing library grouping, accepting it only when the ingredient multiset matches the source metadata.

**Metadata is not a completeness certificate.** Google's rich-result requirements list name/image as required while ingredients/instructions are recommended. Seeing a Recipe object, title or thumbnail is not sufficient to declare import success.

The focaccia example is instructive: JSON-LD was publicly returned in this probe, but its ingredient array omits water that appears in the method. The stored recipe also deliberately improves structure. That supports a separate **review/source-consistency** stage, not a claim that JSON-LD always reproduces the saved version.

## 4. Social sources: retrieve text first, video only when necessary

### TikTok: a useful observed opportunity, with limits

The public responses contained `__UNIVERSAL_DATA_FOR_REHYDRATION__`. Reading the exact requested video's `itemStruct.desc` recovered 27 captions without a browser, account, cookies, hidden API calls, proxies or media downloads. Requested and returned numeric video IDs were checked; recommendation captions elsewhere in the page were not substituted.

This is **observed access today**, not an official supported content API or a guarantee for another device, country or future request. Robots permission is not a substitute for terms/rights review. An eventual public-page adapter needs an explicit policy decision, maintenance ownership, fixtures and a clean failure path.

| Example from this collection | Caption evidence | Best proposed route |
|---|---|---|
| Chicken boscaiola | Ingredients/components and 12 numbered actions; saved method has 5 steps. A mushroom quantity lacks a unit in the source. | Deterministic headings/numbering or optional text AI; preserve ambiguity and review grouping/step structure. |
| Chicken with curry sauce | Ingredient components and seven numbered steps. | Text-first draft; no need to start with video analysis. |
| Sticky Date Pudding | Pudding/sauce lists and cooking directions use similar bullet conventions. | Component-aware caption parsing; AI can help locate the ingredient/method boundary. |
| Mango chia pudding | Six ingredients and five numbered directions; saved method is a single block. | Source-faithful text import followed by optional user-controlled step merging. |
| Roasted Sweet Potato & Chickpea Salad | Directions are interleaved with ingredient/dressing text. | Prose/heading parser or optional text AI; retain the stated oven/time details. |
| Creamy Chicken and bacon pasta soup; Slow cooker beef stroganoff; Spicy Chicken Nuggets | Ingredient lists but no saved cooking method in captions. | Do not generate plausible methods. Ask for permitted transcript/media or supplied steps. |
| Dumplings; Salty chocolate brownie balls; Hot Honey Parmesan Carrots | Sparse captions; carrots credits another creator. | User material or a verified creator source is needed; no full-recipe claim from caption metadata. |
| One Pan Chicken & Broccoli Lasagna | Caption references multiple meals and says recipes are in comments. | User supplies the relevant creator comment or another explicit recipe source. No comments were fetched. |
| Yorkshire pudding | Ratios and partial oven timings rather than a full method. | Preserve the ratio, flag missing detail; video/user assistance would be needed. |
| Eight short-link recipes | Returned homepage without video identity. | Request a fresh direct URL or source text. AI cannot resolve a missing identity by guessing the dish. |

**Exact-reconstruction counterexamples:** Chicken rice & broth and Gut friendly chicken, rice & broth resolve to video `7538351446840904967`, but have different titles, ingredient counts and groups. Sweet chilli chicken and Sweet Chilli Chicken Bowl resolve to `7371381861379067144`, but have three versus five saved steps. One source cannot uniquely specify both sets of personal presentation choices.

Store both the original source address and a stable content identity where available. Do not automatically merge these existing variants merely because a video ID matches.

### Instagram: positive anonymous caption trial, not universal coverage

The original crawler was excluded by the observed robots rules for all fourteen URLs. A subsequently approved [Instaloader 4.16 trial](instagram-instaloader-trial.md) recovered the correct captions for all three attempted references without authentication. All three had ingredient lists and numbered methods; one recipe's ten ingredient strings match the saved list after case/whitespace normalization. Eleven other captions, all creator account eligibility and video-only information remain unverified.

Official Instagram account APIs have professional-account, authorization, permissions and review constraints. Some public professional-account discovery is possible, but it is not a general arbitrary-personal-post URL lookup. **Instagram oEmbed is an embedding facility and explicitly restricts use for extracting/persisting its content for other purposes**; it should not be repurposed as a recipe-caption API. See [the documented distinctions](social-platforms.md#instagram).

Given the user's personal account and preference for free tools, investigate a bounded anonymous caption attempt first, keeping the unsupported extractor replaceable. On failure or insufficient recipe text, offer caption paste, expanded-caption screenshots or a creator's website link. AI can structure supplied text, but cannot recover inaccessible facts or infer historical personal edits.

### YouTube: use creator links before transcribing

The single saved crepe recipe demonstrates a useful route:

`YouTube URL -> public description -> explicitly linked creator recipe page -> Recipe JSON-LD`

This was exercised successfully up to source-field retrieval. However, the current linked page is **Nutella Crepes, 11 ingredient lines/12 steps**, while the saved recipe is **Crepes, 6/2**. Selecting a base-batter component and omitting filling/garnish is a user-intent decision, not an automatic exact match.

Public title/description also has an official YouTube Data API route with an application API key. Caption-track metadata is not caption text; official caption download requires authorization and edit permission on the video. A caption track was advertised on the tested page, but it was not downloaded.

## 5. Options considered

Effort, cost and reliability below are **relative engineering assessments**, not measured latency, contractual pricing or tested product rankings. "No AI" means deterministic extraction; OCR/ASR usually involve machine learning even if no generative/cloud AI is used.

| Approach | URL-only potential / fidelity | AI / deployment / cost | Principal limitation and recommendation |
|---|---|---|---|
| Recipe JSON-LD | High when present and complete; source wording/quantities can be preserved | No AI; local .NET; low operating cost | Best first route. Does not recreate personal edits or necessarily include groups/notes. |
| Microdata / RDFa / microformats | Alternative structured sources when JSON-LD absent | No AI; local or backend | More formats/edge cases; no additional corpus coverage measured here. |
| Recipe-card DOM adapters | Can recover quantities, preparation notes, headings and method sections | No AI; .NET HTML parser; moderate maintenance | Strong complementary route for observed WP Recipe Maker/Tasty cards. Avoid treating headings as ingredients. |
| Public print/recipe-only view | Sometimes a cleaner representation of the same recipe | No AI; HTTP/browser | Follow only an explicit public view; not a paywall/access workaround. Not tested here. |
| Public CMS/creator API or feed | Potentially clean first-party recipe data | No AI; per-site integration | Many feeds/REST endpoints contain only summaries or exclude plugin data; permission/schema vary. No undocumented endpoint probing performed. |
| Embedded page/hydration data | Effective for the 27 recovered TikTok captions | No AI for retrieval; fragile site adapter | Unsupported format and policy-sensitive; caption may be incomplete. Optional/conditional, not universal foundation. |
| Ordinary rendered DOM | Useful when accessible content is inserted by JavaScript | No AI; browser/WebView; higher resource cost | Does not authorize login/paywall/CAPTCHA bypass. Not exercised here. |
| User-triggered browser clipper/bookmarklet | Can capture permitted displayed text with a little user action | No AI; extension or visible WebView | Not URL-only; browser permissions and mobile support matter. Avoid session-cookie export or scraping inaccessible frames. |
| Official account/platform APIs | Reliable supported fields within authorized scope | No AI; OAuth/app setup; quotas/review | Instagram/TikTok account scope does not generally cover arbitrary supplied URLs; YouTube public description route is clearer. |
| Social oEmbed/player | Good for display/provenance, sometimes metadata | No AI; embedding | Not a recipe API. Meta explicitly restricts extraction use; player UI captions are not an extraction interface. |
| Follow explicit creator recipe link | Often turns a social source into an ordinary website recipe | No AI; local/server | Actually found for YouTube here. Verify same dish/version/components; don't substitute a similar recipe. |
| Creator/profile search or search engine | May locate an official recipe alternative when a link is incomplete | Optional AI; network/search API | Candidates need identity confirmation; snippets are not a full recipe or a dependable import source. Not exercised. |
| Paste caption/text/HTML/JSON-LD | High fidelity to supplied source; broad platform fallback | No AI required; local; small effort | Not URL-only, but often much less work than retyping. Full JSON-LD supplied by the user was already effective for focaccia. |
| Copy a creator's recipe comment | Can fill the "recipe in comments" gap | No AI or optional text AI | Needs user selection/source attribution; arbitrary comment extraction not established by this study. |
| Existing recipe manager as intermediary | May offer import/export or a self-hosted API | Depends on product; added app/server/subscription | Useful benchmark or manual bridge, not evidence that a reusable permitted importer SDK exists. |
| Local OCR on screenshots | Recovers on-screen quantities/cards without cloud disclosure | ML, not generative AI; platform OCR/Tesseract | Fractions, columns, overlay text and missing screens need correction. Does not infer unwritten instructions. |
| Local speech recognition | Recovers spoken quantities/actions from permitted media | ML; local Whisper; compute/model download | Does not obtain social media files or see silent visual actions; mobile performance untested. |
| Captions/transcript supplied by user | Can avoid downloading/transcribing video | No AI for parsing, optional text AI | Availability/accuracy/rights vary; timing and ingredient lists may still be missing. |
| OCR + transcript + rule-based merge | Combines two complementary evidence channels | Local ML plus deterministic logic; higher effort | Conflict resolution, frame order and deduplication are difficult; absence is still absence. |
| Hosted text LLM with structured output | Useful for awkward captions, component assignment and prose steps | Generative AI; API/backend or user key; usage cost | Schema-valid output can still invent quantities or omit actions. Does not fix retrieval. Require consent and grounded review. |
| Local text LLM | Similar structuring without submitting recipes to a cloud provider | Generative AI; desktop/self-hosted; hardware cost | Model capability, licensing, model size and mobile deployment need trials; not assumed equivalent to cloud quality. |
| Multimodal image/video LLM | Can combine visible text, audio and demonstrated actions | Generative AI; local capable hardware or cloud; highest variable cost | Most complex route; action/quantity ambiguity and unsupported inference remain. Only use permitted supplied media. |
| Commercial page/social extraction service | May reduce retrieval/adaptor maintenance | Vendor API; recurring cost and disclosure | Claims untested on this corpus; check policy, rights, retention and failure behavior. Not an automatic approval to bypass protections. |
| Human/creator-assisted completion | Highest ability to resolve genuine omissions/intent | No AI required; user effort | Essential last resort. A tool should ask rather than create a different recipe. |

### Concrete tooling and licensing

- **Instagram captions:** [Instaloader 4.16](https://github.com/instaloader/instaloader/releases/tag/v4.16) returned captions for 3/3 sampled source links in an isolated anonymous trial. It is an unsupported-interface dependency, not an official Meta API. See [actual results and bounds](instagram-instaloader-trial.md); no second downloader was installed.
- **In-process .NET:** `HttpClient`, `System.Text.Json` and [AngleSharp](https://github.com/AngleSharp/AngleSharp) are a natural lightweight fit. AngleSharp provides portable HTML DOM/CSS selectors and has an [MIT license](https://raw.githubusercontent.com/AngleSharp/AngleSharp/devel/LICENSE). It is not by itself a complete browser or recipe extractor. MAUI trimming/AOT/platform behavior still needs future validation.
- **Mature Python parsing:** [recipe-scrapers](https://docs.recipe-scrapers.com/) has an [MIT license](https://raw.githubusercontent.com/hhursev/recipe-scrapers/main/LICENSE) and a `scrape_html` route with retrieval managed separately. Version 15.13.0 was subsequently exercised on all eleven retained allowed website pages and three fresh pages, extracting title/ingredients/method in each. Section headings and source/editorial differences still require care; see [actual validation](source-tool-validation.md). A separate service is more natural than embedding Python into every MAUI target.
- **Generic metadata:** [extruct](https://github.com/scrapinghub/extruct) supports JSON-LD, Microdata, experimental RDFa, microformats and OpenGraph; its [license](https://raw.githubusercontent.com/scrapinghub/extruct/master/LICENSE) is BSD-3-Clause. It extracts metadata, not necessarily a complete recipe. The inspected `recipe-scrapers` schema implementation currently selects JSON-LD/Microdata; do not assume every advertised metadata format is used in every parser path.
- **Rendering when actually needed:** [Playwright for .NET](https://github.com/microsoft/playwright-dotnet) runs desktop browser engines on Windows/macOS/Linux; a server/desktop helper is more natural than assuming it runs inside Android/iOS. [MAUI WebView](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/webview?view=net-maui-10.0) is a separate, visible on-device option. Neither was used to bypass a source restriction here.
- **Existing products:** [Mealie](https://mealie.io/documentation/getting-started/features/) documents URL/HTML/JSON import and AI-assisted text/image/video routes; [Tandoor](https://docs.tandoor.dev/) documents structured website import and [interchange formats](https://docs.tandoor.dev/features/import_export/). Mealie's [AGPLv3 license](https://raw.githubusercontent.com/mealie-recipes/mealie/mealie-next/LICENSE) needs consideration before code reuse/network distribution. These are useful reference applications, not drop-in MAUI importer libraries. [Paprika](https://www.paprikaapp.com/) advertises social/text/scanning import as Premium features; no export round-trip or corpus accuracy was tested.
- **OCR/ASR:** [ML Kit text recognition](https://developers.google.com/ml-kit/vision/text-recognition/v2), [Tesseract](https://github.com/tesseract-ocr/tesseract) and [Whisper](https://github.com/openai/whisper) are representative candidates. Tesseract/Whisper require native/model packaging decisions. No claim is made that maintained MAUI bindings or acceptable phone performance have already been verified.
- **Structured generative output:** [OpenAI Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) and [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs) can constrain JSON shape. Neither establishes factual truth or source access. The Ollama page explicitly distinguishes local support from its cloud offering.
- **Hosted retrieval:** [Firecrawl's current scrape/JSON interface](https://docs.firecrawl.dev/features/scrape) and the [Apify Instagram/TikTok services](social-platforms.md#commercial-services) are representative vendor options, not endorsed or exercised integrations. Firecrawl's older [`/extract`](https://docs.firecrawl.dev/features/extract) documentation announces removal on 10 December 2026; a new design should not copy that soon-obsolete example. Paid vendor/platform contracts and rights need review before use.

No exact financial total is justified without a selected vendor/model, input size, media duration and allowed access route. Budget using:

`successful import cost = retrieval + rendering + text tokens + audio minutes + image/video processing + retries + hosting/storage`

A text-first cascade minimizes both cost and unnecessary disclosure. Provider prices, API fields, licenses and availability must be rechecked when development begins.

## 6. Proposed future Nibbles workflow - not implemented

### User experience

`Paste/share URL -> resolve identity -> retrieve allowed source -> extract -> check gaps -> editable draft -> user confirms -> existing save flow -> recipe details`

For a well-structured website, the user's effort could be just the URL and review/save. For an incomplete social caption, clearly explain the missing fields and offer Paste text, Add screenshots, Provide creator recipe URL, or Attach permitted media. Sharing a URL does **not** mean the sender also shares caption text.

Keep "faithful extraction" separate from optional "improve formatting", "suggest groups", "convert units" or "split long steps". Show such edits explicitly. Do not silently standardize the original author's units, choose one of two salt brands, double a quantity, invent a cooking temperature or reconcile conflicting sources.

### Mapping to the current app

The existing free-text ingredient model is an advantage: there is no need to solve quantity/unit parsing before adding URL import.

| Extracted concept | Existing Nibbles destination | Proposed rule |
|---|---|---|
| Recipe name | `Title` | Preserve source name initially; user can rename. A personal shortened name cannot be inferred. |
| Ingredient line | `Ingredients[].FQIngrediantDescription` | Preserve full readable source line, including ranges, alternatives and optional status. Existing misspelling is schema compatibility, not a new field. |
| Ingredient component heading | `IngredientGroups` and ingredient `GroupId` | Preserve explicit headings; assign stable draft group IDs. AI-inferred groups must be labeled suggestions. Do not save headings as food rows. |
| Method action/section | `Steps[].Instructions`, zero-based `Num` | Preserve action order. Include important section context without turning headings into empty steps; merging/splitting is separate review. |
| Source and attribution | Existing `Source` / suitable notes | Preserve provenance. Additional structured audit fields would require an explicit future schema/design decision. |
| Notes, substitutions, resting/freezing guidance | `Notes` or appropriately contextualized method | Do not leave "see Note 4" unresolved or drop safety/serving details. |
| Time | `TimeToComplete` | Preserve known active/elapsed/resting distinctions; unknown/conflicting values require review. |
| Optional picture | Existing recipe-photo mechanism | Prefer recipe image metadata, then permitted video cover or user image. Do not block a good text import because a picture fails. |

A new draft gets a new recipe UUID only when appropriate; do not reuse a source site's recipe ID as a Nibbles ID. Run the existing recipe preparation/validation and conflict-safe GitHub save path **after** user confirmation. Do not introduce a second repository writer or revive the retired API.

### Architecture choices

| Architecture | Advantages | Costs/constraints | Fit |
|---|---|---|---|
| On-device .NET deterministic import | No new server, private local parsing, fits current direct-GitHub architecture | Platform network behavior, parser maintenance, no protected server API secrets | **Recommended starting point** for allowed websites and pasted content. |
| On-device parser plus optional backend | Shared adapters, Python parser/rendering/AI possible, provider keys kept server-side | Authentication, hosting, deletion, quotas, monitoring and a new trust boundary | Add only when the chosen fallback actually needs it. Backend returns a draft; it does not need the recipe-repository PAT. |
| Desktop/self-hosted companion | Local model/media processing and reusable mature tools without cloud AI | Not always available when importing from a phone; hardware/setup burden | Good optional personal deployment, not a universal mobile dependency. |
| Visible user-assisted browser/share extension | User chooses accessible text/attachments without exporting sessions | Per-platform work, sender payload variability, terms/accessibility constraints | Useful complement, not a promise of silent arbitrary-site automation. |

Cloud AI/vendor access keys should not be embedded in APKs/installers. Backend processing is not a reason to send private GitHub credentials or the entire recipe collection to a provider. Sharing URLs, captions, media and personal notes also discloses information; require explicit opt-in and inspect retention/deletion terms. "Not used for training by default" is not equivalent to "never stored".

### Quality and trust gates

Treat web pages/captions as untrusted data, not instructions to the importer or AI. Restrict URL schemes, public destinations, redirects and response sizes; revalidate image and backend fetches as well. A production backend must guard against private-network/metadata-service requests and DNS/redirect tricks. Do not execute arbitrary embedded scripts just to read JSON-LD.

An intermediate draft should distinguish **extracted**, **user-supplied**, **inferred/suggested**, **conflicting** and **missing** facts. A schema-valid nonempty recipe is not proof of completeness. Require review of missing quantities, ingredients used only in steps, duplicate ingredient lines, unlisted water/oil, ambiguous oven units and OCR/ASR disagreements.

Optional AI should receive only the necessary allowed source material, return a restricted draft schema plus evidence references/gap flags, and have no direct save/write capability. Ground quantities and actions in source spans or media timestamps. Keep original values available to the reviewer; model confidence alone is not a reliable quality gate.

## 7. Sensible development order and acceptance criteria

This is a proposed order, **not an implementation commitment**.

| Stage | Scope | Exit evidence required |
|---|---|---|
| 1 | Local Recipe JSON-LD + card groups/notes + explicit preview/save + paste-text/JSON fallback | Fixtures for the 11 retrieved website recipes; independently reviewed source-faithful expectations; no accidental omissions or unsupported conversions. Clear blocked/missing-field behavior. |
| 2 | Optional allowed TikTok caption adapter and/or user-supplied social text; optional text AI | Policy decision before adapter release. Evaluate the 27 captured captions, including ingredient-only, comment-only, ratio-only, renamed-author and duplicate-video cases. Never auto-fill missing methods. |
| 3 | Screenshots/OCR and permitted audio/video assistance | Fraction/temperature/quantity/action-order tests, permission/retention decisions, measured phone/backend latency and cost, cancellation and partial-result UX. |
| 4 | Broader platform APIs/vendors and more sites | Actual account/review eligibility, permitted reuse/storage, vendor retention/pricing and representative live fixtures, not a vendor feature list. |

Maintain two separate gold datasets: **faithful source extraction** and **optional Nibbles editorial transformation**. The saved recipes are useful comparison data but not infallible source-transcription truth. Do not score an improved import as wrong simply because a saved recipe has an accidental heading/stray row or abbreviated method.

Measure ingredient identity/quantity/unit/range/optional/component fidelity; cooking action order, temperature and time coverage; title/provenance accuracy; unsupported additions; missing-field detection; user correction effort; and retrieval coverage separately. Retain normalized exact-string checks as diagnostics, not the only result.

Use offline source fixtures for repeatability and a small permitted live canary set for drift. Future app integration should verify Android and Windows preview/cancel/save/offline/conflict behavior and iOS on a Mac. No such importer UI/runtime tests were performed in this research.

### Remaining unknowns

- Captions for the eleven untested Instagram references, video-only information and all fourteen creators' account eligibility.
- What the eight TikTok short links originally identified; a fresh direct URL may resolve this.
- Video-only or creator-comment evidence for the eight recovered TikTok captions without both ingredient and method text.
- Whether each platform/vendor permits the intended extraction, transformation, image reuse and private retention.
- Real AI/OCR/ASR accuracy, processing cost and latency on representative permitted material.
- Current MAUI-compatible OCR/model bindings and Android/iOS/Windows receiving-share payload behavior.
- Which editorial transformations the user would want by default. Exact historical personal wording cannot be recovered from source URLs alone.

**Bottom line:** combine deterministic website extraction with caption-first social retrieval and explicit user-assisted fallback. The positive Instagram trial supports investigating free tools without requiring a business account, but does not establish universal reliability or deployment permission. Optional text AI can reduce formatting effort on available captions; video AI is a later fallback. Preserve source evidence, expose uncertainty and let the existing editor/save flow remain the final authority.
