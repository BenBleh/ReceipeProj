# Nibbles local draft-review prototype

Open **http://127.0.0.1:8765/** on this PC while the local server is running.

Enter a new website, Instagram or TikTok URL and click **Fetch live URL**. Compare
the draft with the evidence, edit the title/ingredient groups/method, tick the
review checkbox and export JSON. A failed/restricted lookup opens the pasted-text
panel and keeps the URL for attribution; it does not invent a replacement draft.

**Build from capture** still replays the original 39 sources offline: 11 websites,
25 TikTok videos and 3 Instagram posts. Fetch live URL always makes a fresh lookup,
even for a URL in that catalog. New live results are not added to the capture
catalog or written to disk. Pasted text also works without any live requests;
its attribution URL is optional.

## Boundaries

- No Nibbles application changes, rfiles writes, GitHub credentials, NAS
  deployment, source login, image downloads or paid/local AI.
- Extraction reads source evidence, not saved recipe ingredients or
  steps. Saved titles label the sample picker only; they do not supply draft
  titles.
- Each draft gets a new UUID. Export uses Nibbles' recipe field names, including
  `FQIngrediantDescription`, group references and zero-based step numbers.
  Export is one recipe JSON object, not a Nibbles backup or an automatic import.
- A nonempty title, ingredient list and method, consistent groups and an explicit
  review are required for export. Editing invalidates the review checkbox.
  Structural validity does not establish that the recipe is complete or correct.
- Edits stay in the browser tab until exported. Reloading discards them.
  The service has no recipe-storage endpoint.
- The server binds to loopback only. Do not expose this development server through
  the NAS/reverse proxy.

## Live lookup behavior

Website/TikTok HTML retrieval checks robots rules, public DNS addresses, response
sizes and requested-post identity. Only public HTTPS destinations on port 443 are
accepted. Private/loopback/reserved addresses are refused, including redirect
destinations. Connections pin a validated address while retaining the original
TLS hostname and certificate verification; environment proxies are not used.

Website redirects are bounded and checked before following. TikTok short links
are accepted only when they resolve to a specific supported video URL; homepage
redirects and changed video identities are refused. Website drafts still require
one unambiguous Recipe JSON-LD object. Unsupported cards/pages need pasted text.

Instagram uses **Instaloader 4.16's anonymous metadata path**, not generic HTML
crawling. Both its initial session and copied GraphQL sessions use the bounded
transport; only the normal bootstrap and metadata endpoints are permitted.
No personal account, imported cookies or saved login is loaded. Temporary anonymous
bootstrap cookies are discarded with the lookup. Redirects, login/challenge
responses, rate limits and unsupported endpoints stop the attempt; no alternate
endpoint or login workaround is tried after a restriction.

There is one live lookup at a time, request pacing, no transport/tool retries,
at most 10 website/TikTok requests or 4 Instagram requests, bounded redirects,
8 MB response limits (1 MB for robots) and decoded-size checks for compression.
Connect/read timeouts are at most 10 seconds, with a 60-second request/read
deadline; OS DNS lookup time is not a hard-capped operation. Longer crawler
delays, missing policies other than HTTP 404, or policy exclusions produce a
paste fallback.

HTTP 401/403/429 and network failures stop further requests to that host for the
server session. Instagram also stops on any HTTP error or redirect. The prior
Simple Home Edit HTTP 403 stop is retained. These safeguards are not a guarantee
of access or permission to reuse a creator's content. The social endpoints remain
unsupported dependencies; the earlier research's terms/retention caveats still
apply. This is a personal development experiment, not a deployable service.

## Starting it again

The existing isolated research environment already has the required packages.
The source captures deliberately remain outside the project. From PowerShell:

```powershell
Set-Location 'C:\Dev\ReceipeProj\Design\AutoAddResearch\Prototype'
$files = 'C:\Users\benja\.copilot\session-state\74577d18-1f92-430a-8fbb-9c2549c143ff\files'
$env:PYTHONDONTWRITEBYTECODE = '1'
& "$files\instagram-tool-trial\venv\Scripts\python.exe" .\server.py `
  --cache "$files\autoadd-research-20261113" `
  --instagram-captions "$files\instagram-tool-trial\captions"
```

Stop a foreground instance with Ctrl+C. If port 8765 is already occupied, use
`--port 8766` and the corresponding browser URL rather than stopping an unrelated
process. `/api/health` identifies this service as `nibbles-draft-prototype`.

Python 3.13 was used. `requirements.txt` pins the direct extraction dependency;
the prototype also imports parsing helpers from the parent `probe_sources.py`
without running its network-probe entry point. Keep the parent research JSON/CSV
manifests, HTML cache and three caption files available. This directory alone is
not a self-contained portable capture bundle.

## What this experiment shows

### Fresh URL checks

[live-validation.json](live-validation.json) records bounded actual lookups:

| Source | Outside the 39 captures? | Returned draft |
|---|---|---|
| RecipeTin Eats lasagna | Yes | Title, 24 ingredients, 18 actions; three section headings excluded from actions. |
| Instagram creamy pesto chicken tortellini | Yes | Caption-derived title, 16 ingredient rows, 6 steps; manual review still required. |
| TikTok cheddar/chive scones | No, but fetched fresh | Caption-derived title, 7 ingredient rows, 9 steps; the known inline-boundary correction remains. |

The initial TikTok attempt rejected a username-free `/@/video/ID` permalink before
making source requests. That supported form was then enabled and the fresh lookup
succeeded. Both attempts remain in the report. These results establish retrieval
and field presence, not recipe correctness or universal social-platform access.
No new full source bodies/captions were retained on disk.

### Original offline sample checks

[quality-results.json](quality-results.json) records the current extraction
diagnostics for every source without copying full captions into this folder.
These counts measure **field presence, not semantic accuracy or successful
imports**:

| Source | Captures replayed | Title, ingredients and method all nonempty | Method text identified |
|---|---:|---:|---:|
| Websites | 11 | 11 | 11 |
| Instagram | 3 | 3 | 3 |
| TikTok | 25 | 10 | 16 |

No capture failed parsing or its integrity check. Nevertheless, TikTok drafts
still need significant review: 12 have no confidently inferred title, 9 no
identified method and 4 no identified ingredient list; these categories overlap.
Some missing fields reflect absent source material, while others reflect the
limits of these rules.

Representative source checks:

- **Chicken Pot Pie:** 18 ingredients and 17 actions. The three method-section
  headings are evidence, not additional cooking steps.
- **Focaccia:** retains the source's eight ingredients and seven steps, with a
  warning that quantified water occurs in the method but not the ingredient list.
  Missing water is not silently added.
- **Instagram creamy chicken:** 12 ingredients and 8 steps; nutrition and serving
  text are excluded from ingredients.
- **Instagram burrata pasta:** 10 ingredients and 7 cooking steps, excluding
  the numbered "Enjoy" sign-off.
- **Instagram bao buns:** 11 listed ingredients plus an optional-garnish row,
  and 7 steps. Price annotations and the source's thigh/coleslaw choices remain;
  saved-recipe substitutions are not copied.
- **TikTok scones:** seven rows still combine two unquantified items with
  preceding quantified ingredients. Split out black pepper and garnish salt
  during review. All quantities remain visible, and the inline-splitting warning
  identifies this limitation.
- **Ingredient-only/sparse captions:** no replacement method is generated.
  Cooking-language boundaries inferred in other captions are explicitly flagged.

Website Recipe JSON-LD is the strongest deterministic route. Caption rules are
useful on clearly structured posts, but collapsed lists, interleaved instructions,
nutrition, component headings, buried titles and source errors remain correction
work. This prototype does not demonstrate that a local model is required or that
one would solve those cases; it supplies concrete drafts for that later comparison.

The evidence panel retains original text/Recipe JSON, unassigned text and parsing
flags. It is not a rendered copy of the complete website, and referenced source
footnotes may still need checking on the original page. Flags describe extraction;
they are not automatically dismissed when you manually supply missing material.

## Verification

```powershell
& "$files\instagram-tool-trial\venv\Scripts\python.exe" -m unittest -v test_prototype
node --check .\app.js
```

Thirty-one focused tests cover source-only parsing, Unicode, fractions/ranges,
group references, absent methods, source integrity/video identity, malformed
exports, local HTTP guards, live routing, robots exclusions, public-IP pinning,
Instagram session copies, restriction stops, unsafe redirects and encoded/decoded
response limits. All 39 offline samples were also replayed with network access
forbidden, preserving their ingredient/step/group counts. To produce another
offline diagnostic run, use a **new** output path:

```powershell
& "$files\instagram-tool-trial\venv\Scripts\python.exe" .\server.py `
  --cache "$files\autoadd-research-20261113" `
  --instagram-captions "$files\instagram-tool-trial\captions" `
  --evaluate .\quality-results-next.json
```

Actual Chromium interaction also exercised sample selection, caption drafting,
title/ingredient editing, review invalidation, export initiation, a rejected
missing-method export, adding a group and multiline method editing. The narrow
scenario used a 390 x 844 viewport. Both owned browser processes were cleaned up.
The browser harness deliberately cancels downloads; actual JSON response/schema
validation was checked separately through the running service. It did not verify
an OS download dialog or saving a file to a user's chosen directory.

Private session artifacts retain the full captures and mobile screenshot:
`prototype-browser-desktop.json`, `prototype-browser-mobile.json` and
`prototype-mobile.png`. Browser Evidence IDs:
`ev_2dc73aeadfe2074e23d09de17cbadb068edb90be5ec0d0cb423752ee893a1917`
and `ev_5d3c589c726d5eb0b028e486cb3ecb611710974338c7fe8af2844894ab607354`.

The live-extension browser scenario fetched the new lasagna URL, checked fresh
provenance, rejected a private-address lookup, preserved the previous draft and
successfully built pasted-text fallback. All actions completed, with no page
exceptions, and the owned browser was cleaned up. Its private session artifact is
`prototype-live-browser.json`, Evidence
`ev_29acaf96f4b4a628861b7744bba62b79be90d0d28f6c4b011a792f1a5c80a7e6`.
