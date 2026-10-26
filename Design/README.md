# Nibbles redesign

`Nibbles-redesign.fig` is the editable OpenPencil design, containing the approved
1.2.0 screens and the approved 1.2.1/1.2.2 revisions below. Open it in OpenPencil to
continue designing. `Nibbles-overview.png`, the screen overviews,
and `previews` provide quick references; `assets` contains the approved mascot,
recipe-photo references, app icon, and launch artwork.

The implementation is in `..\Nibbles` and `..\RecipeAPI`. It uses the warm
cream/forest palette, matching dark theme, chef-cat branding, paw ingredient
checks, a responsive recipe library, cooking tabs, and a three-section editor.
Wide Windows windows also show an editing rail and live cooking preview.
The bundled Open Sans fonts keep body text consistent across platforms; headings
use the platform's serif font (Georgia on Windows and Apple platforms).

There are **no source filters or source labels on recipe tiles**. Original-source
links are available on recipe details. The original pink paws are retained in
dark mode, with berry-coloured versions in light mode.
Cooking time is no longer displayed or editable. Existing `TimeToComplete`
metadata is retained only for lossless compatibility with stored recipes.

## Direct GitHub storage

**Approved and implemented in 1.3.0.** Page **10 - Direct GitHub storage** has
12 boards: eight phone states, two Windows examples, and two implementation
handoffs explicitly marked **design notes, not app UI**.
`github-storage-overview.png` and `previews/github-*.png` provide previews.
The design artifacts contain no credentials.

The app reads and writes **BenBleh/rfiles**, branch **main**, through
GitHub's HTTPS API instead of the home recipe API and file server. The user
chose a **personal build with no setup UI**: fix the endpoint, repository and
branch in the app, and inject a repository-scoped, fine-grained personal access
token **during packaging from a private build input**, never committed source.
The token needs **Contents: Read and write**, with access only to `rfiles`;
do not reuse the server deploy key or grant admin/workflow permissions.
Set its expiration to **No expiration**, as requested. If a GitHub policy blocks
that choice, report it rather than silently substituting an expiring token.
Non-expiring access can still be revoked or removed by GitHub, including after
a year of inactivity, so screen 06 remains an exceptional access-failure fallback.
There are no connection settings, account menu, token-entry or disconnect
screens. Launch opens the recipe box automatically.

**Accepted tradeoff:** a packaged token can be extracted from an APK or EXE,
even if the source repository is private or the app also uses secure storage.
The user explicitly selected this approach after that warning. Treat installers
as private, sensitive artifacts. Never put tokens in Git, recipe files, logs,
command output, URLs or design exports; no real token is used in this design.
Missing build credentials must fail packaging rather than produce a broken
installer or silently restore the home-API connection.

### UI and offline behaviour

- No setup steps or new header controls. On first launch, show the normal
  recipe-box loading state while downloading automatically. Existing cached
  recipes remain browsable whenever GitHub cannot be reached. If there is no
  usable cache, show the actual error and Retry, not a connection wizard.
- Offline remains **read only**: disable Add, hide Edit and prevent saves.
  Local search, cooking, same-day progress, checks, temporary scaling and
  copying displayed scaled ingredients continue to work. If connectivity is
  lost inside the editor, retain its current in-memory input, lock editing and
  saving, and allow reconnect or explicit discard. No offline write queue or
  automatic background upload is added.
- Rejected/expired/revoked credentials show **GitHub access needs updating**,
  keep cached recipes read only, and advise installing an updated Nibbles
  build. Rotating the token requires a new build/install, not an in-app form.
  Revoke a compromised token on GitHub, then privately rebuild with its
  replacement. Missing write permission, protected branches, rate limits and
  connectivity failures need accurate, distinct messages; not every denied
  request is an expired token. Never use a write probe during automatic launch.
- A conflict keeps edits in place. **Keep editing** is the safe default;
  **Discard edits & reload** explicitly abandons the draft and reloads the
  latest recipe. If the recipe was deleted, return to the refreshed library
  rather than silently recreate it. There is no force-overwrite action.
- An interrupted save with an unknown outcome uses **Check save** instead of
  blindly resending. A confirmed remote save followed by local-cache failure
  says **Saved to GitHub**, with refresh guidance, not "Could not save".
- Existing app-themed dialogs, native photo/permission pickers and the
  no-backup-status-UI rule remain. No repository-switching, cache-mixing or
  account-management workflow is introduced.

### Conflict and publication contract

Keep the recipe's edit baseline: commit ID, recipe blob ID, current master-list
entry and relevant image blob IDs. Before **each** publication attempt, read
the latest branch and check both the recipe and **MasterList.csv**, plus any
photo being replaced. Additions must still have an unused UUID and no existing
index entry; do not overwrite an unrelated file on a create retry.

If the recipe or relevant image changed, block publication. If the index blob
changed only because another recipe was added/edited, rebuild from that latest
valid index and update only this recipe's entry, preserving everything else.
A changed/deleted/conflicting entry for this recipe is a visible conflict.
Malformed or duplicate index entries fail explicitly, not by dropping data.

Create the recipe JSON, index and changed photos in **one commit** based on the
latest tree. Publish with a **non-force, fast-forward-only branch update**.
This final check is essential: another device can commit after the precheck.
On a rejected race, re-read and revalidate with bounded retries; never force
push, erase unrelated changes or keep retrying indefinitely.

Keep the assigned UUID and minimal save-outcome metadata so a timed-out branch
update can be checked, including whether the intended commit appears in later
branch ancestry. This is outcome verification, not an offline upload queue.
Do not resend until the prior outcome is resolved; a previously accepted save
must not become a duplicate create or overwrite newer edits.

### API behaviour that must move into the app

The review covered the API controllers, file storage and validation, together
with app refresh/cache/search, editor saves and online-only editing.

| Existing behaviour | Direct-GitHub requirement |
| --- | --- |
| UUID JSON, `MasterList.csv`, photos and thumbnails | Preserve paths and schema; no recipe migration. Split index lines at the first comma so titles can contain commas. |
| Required title, ingredients and steps | Move normalization/validation into reusable client-side code before publication. Preserve title cleanup, legacy amount/unit fallback and zero-based step numbering. |
| Group IDs, order and references | Keep stable IDs, trimmed names, valid references and protection against erasing existing group metadata. |
| JPEG conversion and 120 x 120 thumbnail | Process on Windows, Android and iOS without Windows-only `System.Drawing`; retain cover/thumbnail paths and supported legacy step-image handling. |
| Original source, notes and legacy fields | Retain them, including hidden `TimeToComplete`; no new cooking-time UI. |
| Device-only checks/scaling/paths | Never serialize temporary amounts, paw checks or device paths into recipe JSON; do not leave image-upload base64 in persisted recipes. |
| Local cache, optional photos and search | Fetch at one pinned commit; stage/validate before promoting a complete snapshot, retain the prior usable cache on failure and rebuild search. Missing photos use the chef-cat fallback. |
| Retry-safe saves and cache warnings | Preserve edits on failure, confirm remote publication before success, prevent duplicate UUID creates and distinguish remote success from cache failure. |

Before cutover, drain/reconcile the existing backup queue and verify GitHub
contains the latest local JSON, index and photos. Stop legacy API writes and
the backup worker during cutover to avoid competing writers. Do not remove
IIS sites, API source or support for other clients without separate approval.
Acceptance must verify real GitHub additions, edits and photo changes,
multi-device conflicts/races, uncertain-save recovery, read-only offline
behaviour and existing cooking/search/scaling/grouping/paste regressions.

This page supersedes home-server connection/save wording in 1.3.0.
The earlier approved/released pages below remain as historical handoffs.

## Approved app-styled dialogs and menus

**Approved and implemented in 1.2.2.** Page **09 - App-styled dialogs and menus**
contains 11 boards: eight phone examples, two Windows examples, and a behaviour
handoff. `dialogs-overview.png` shows the page; `previews/dialog-*.png` contains
the individual views.

Replace app-owned platform alerts, name prompts and action sheets with a shared
visual family: cream/forest palette, white or deep-green surfaces, serif titles,
Open Sans body text, rounded cards, dimmed app context and 48px action targets.
Light/dark examples cover discard changes, start fresh, add/rename group with
inline validation, group actions, destination selection, remove-heading
confirmation, changed-recipe review, and server-saved/cache-refresh warnings.
Ingredient and method-step action menus use the same menu treatment.

Phones use centered confirmations/prompts and bottom action sheets; Windows uses
centered width-limited overlays. Long text and option lists scroll without
hiding the actions. Retain context-dependent options and the existing outcomes.
Destructive actions have explicit labels and danger styling; safe actions remain
easy to find. Escape/Android Back cancels, focus stays inside the overlay and
returns to its invoking control, and outside taps never discard/reset/remove.
Name fields retain input and show validation inline; keyboard and large-text
layouts must keep fields and actions reachable.

Paste ingredients and scaling retain their existing app-themed custom screens,
previews and workflows. OS photo/file pickers and permission dialogs remain
native. This update changes only the app, not the API or backup behaviour.
Board 11 is explicitly labelled as design notes, not content shown in the app.

## Next features: cooking, search, scaling and GitHub backup

The editable document now includes **13 feature boards** on these pages:

| OpenPencil page | Contents |
| --- | --- |
| 06 - Cooking mode and full-recipe search | Entry from recipe details, paw checklist, current scrolling numbered method in dark mode, resume session, matched search excerpts, no-match state, empty search with placeholder |
| 07 - Temporary recipe scaling | Multiplier and preview, uncertain-amount review, scaled checklist |
| 08 - Windows cooking and feature handoff | Side-by-side Windows cooking workspace, photo search results, and interaction/API design specification |

These pages are the approved reference for the **1.2.0 app/API features**. They
supersede the older browse/cooking interaction examples for these additions;
the existing editor, paste behavior, branding and no-cooking-time rule remain.
All new boards have matching PNGs in `previews`. The `feature-behaviour.png`
board contains the full interaction handoff. `cooking-search-overview.png` and
`scaling-overview.png` show the new Android flows at a glance.

### Approved revision: feedback from Windows 1.2.0

- Keep recipe photos visible in search-result tiles, with the title, matching
  section and excerpt below each photo. Updated **06 / 05** covers phones;
  new **08 / 03 - Windows - search results with photos** shows the desktop
  layout. Preview: `previews/windows-search-photos.png`.
- The user reports that an edited recipe reaches disk but is not committed or
  pushed to GitHub. This is an API/operations defect, not a request for backup
  UI. Investigation found a manual-edit safeguard had paused backups before
  the test recipe's pending edits were reverted. Reviewed obsolete jobs are
  preserved in a private recovery archive, not silently adopted or discarded.
  Acceptance uses a real update through the deployed API and confirms the
  resulting commit and updated recipe on the remote branch; startup or dry-run
  access checks alone are insufficient.

These revisions are approved for implementation. The earlier design pass changed
design artifacts only; backup recovery and a real API-to-GitHub verification are
separate implementation work.

### Dedicated cooking mode

- Start from recipe details. Keep the screen awake only while cooking is in the
  foreground; release the wake request on exit, backgrounding or loss of the
  active Windows cooking window. This happens without a visible
  "Screen stays awake" status label.
- Keep the current app's scrolling numbered method display: wrapping 17sp
  instructions, existing line spacing and step labels. There are no text-size
  controls, focused-step cards, previous/next-step buttons or new Finish action.
  Phones switch between Ingredients and Method without losing scroll position;
  Windows can show the same method list beside ingredients. Keep the original
  paw checks and accessible hit targets. Do not infer which ingredients belong
  to a method step.
- Omit the ingredient/group-count summary from the recipe cooking-entry view.
  Keep **Clear checked** in a fixed bottom action area outside the ingredient
  scroll view, with a minimum 48dp hit target. Show it only when at least one
  ingredient anywhere in the recipe is checked. It clears all recipe checks,
  then disappears; Back to method remains anchored without a layout jump.
  Unchecked ingredients use the original outline paw, never a plus/add icon.
- Save recipe ID/revision, selected tab, scroll positions, checks, multiplier
  and the device-local calendar date on the device, not in the recipe files.
  Offer Resume after leaving/restarting **only on that same local calendar day**,
  not for a rolling 24-hour window. On a return on a later day, discard stale
  cooking progress and use the normal Start cooking view at 1x with cleared
  checks and positions. Do not interrupt an active session at midnight.
  Same-day Start fresh asks before resetting progress. If the recipe changed,
  require review before resuming. Saved recipes remain usable offline.

### Search inside recipes

- Search titles, ingredient descriptions and group names, personal notes, and
  method instructions. Match case/accent-insensitively; every search word must
  occur somewhere in the same recipe.
- Use "Titles, ingredients, notes and method" as the empty search box's
  placeholder only. Once a query is entered, show the query without a separate
  search-scope helper line underneath. Keep a magnifying-glass search icon,
  never a paw. The empty-query view keeps the normal recipe library visible;
  `previews/search-empty.png` shows the full placeholder in the input.
- Rank title matches first, then ingredients, notes and method. Show matching
  excerpts and the relevant section; opening a result reveals that section.
  Keep the available recipe photo visible on each result tile, using the
  normal library's image treatment, with title and match details below it.
  Windows retains a responsive tile grid; photos are not replaced by excerpts.
  Do not show explanatory panels about excerpts/tapping or connecting to the
  home server to refresh. No source-filter chips or source labels are reintroduced.
- Search the recipe JSON available on the device through a local index,
  updated on refresh.
  Show indexing progress and cancel obsolete searches as typing continues.
  Do not infer which recipes are missing, report download-completeness counts,
  or claim the local collection is complete. Do not show an offline/device-search
  status banner: searching on the device is the normal behavior. No-match offers
  Clear search.
  Index errors retain the previous usable index and explain the actual error.

### Temporary recipe scaling

- Offer half, original, double and a custom positive finite multiplier.
  Preview changes before using them. The original recipe is never overwritten
  or saved to the API; selecting 1x restores original amounts.
- Multiply only confidently understood leading numeric amounts, including
  recognised decimals and fractions. Preserve units and wording; do not
  silently convert units. Keep original amounts accessible beside scaled ones.
- Leave unquantified, ambiguous, ranged or alternative amounts unchanged,
  visibly flagged for review. Indicate approximate rounding instead of silently
  reducing tiny amounts to zero.
- Method text, cooking temperatures and durations remain original. Display an
  advisory when cooking at a different multiplier, because instructions can
  contain quantities too. The design includes explicit uncertain-line review.

### API-managed GitHub backups; manual recovery

The user already manages `C:\rfiles` as a Git repository. The API automation
uses that **existing repository, remote and branch**, not a second backup store.
Recovery remains manual through GitHub/Git; there is no in-app history, restore,
repository-reset or undo-delete interface.
Backups are **API-only**: no app screens, navigation, status labels,
notifications or retry controls. Backup failures are operational matters for
the server owner, not messages to the app user.

- After successfully persisting a recipe change, durably queue its backup.
  Saving succeeds independently of GitHub availability. Internal backup jobs
  and server logs track progress. The queue survives API restarts without the app remaining
  open. Track all affected recipe JSON, image and index files; burst edits may
  be coalesced without dropping pending changes.
- A single worker serializes Git operations with API writes. Stage only
  validated API-owned changes. Existing manual edits or staged work must not
  be silently included or overwritten; pause when ownership cannot be verified.
  Do not use blanket `git add .`, force-push, reset, stash or automatic conflict
  resolution. Keep the existing manual Git workflow viable.
- Internally distinguish queued, committing, pushing, successfully pushed and
  action-needed states. Only a confirmed remote push counts as backed up.
  A local commit or queued write alone is not a remote backup.
- Verify the deployed save-to-backup path end to end: an actual API recipe
  update must reach disk, produce a scoped commit and appear on the configured
  GitHub branch under the IIS identity. Check durable jobs and server logs when
  a saved update does not commit or push. Do not treat a successful startup
  credential check or dry-run push as proof that recipe updates are backed up.
- Retry transient network failures with bounded backoff. Auth/permission
  problems, conflicting manual work and non-fast-forward rejections pause for
  server-side resolution. Retry uses existing commits and never resubmits the
  recipe save. Record last successful push, timestamp/timezone, pending jobs and
  failures in server logs or operational tooling; none is exposed in the app.
- Credentials belong to the server's IIS/service identity with scoped repository
  access, outside the app, public web root and recipe repository. Never expose
  credentials or arbitrary Git execution in the app. No new client-facing
  backup status/retry endpoints are needed. Resolve operational issues and
  trigger any manual retry through authorized server-side tooling.
- Recovery through Git must include any affected index and photos, followed by
  refresh in Nibbles. Unpushed changes are not guaranteed recoverable on GitHub.

Automation is disabled in checked-in defaults and enabled through private server
configuration; deployment and credential requirements are in the root README.
The design itself does not change recipe data or commit existing manual edits.

## Ingredient groups and existing data

Recipes retain their flat `Ingredients` array for compatibility. Optional
`IngredientGroups` contains `{ "Id": "...", "Name": "Marinade", "Order": 0 }`;
each ingredient has an optional `GroupId`. IDs remain stable when headings are
renamed or reordered. Free-text `FQIngrediantDescription` is preserved, including
unparsed amounts and units.

Existing JSON needs **no migration**. Recipes with no groups remain a plain list
without an invented heading. Ungrouped ingredients in a grouped recipe appear
under **Other ingredients**. No files in `C:\rfiles` were rewritten as part of
the redesign.

The editor supports adding and renaming groups, moving headings and ingredients
up/down, moving ingredients between groups, and removing headings while moving
all their ingredients to an explicitly selected destination. Per-group paste
accepts one ingredient per line, trims whitespace and leading list markers
(`-`, `*`, bullets, and en/em dashes), and ignores lines left empty. The same
cleaned wording appears in the required preview and is added on confirmation.
Appending keeps filled ingredients and removes empty rows only in the target
group (including the flat list); cancelling leaves everything unchanged.
It never replaces the existing list or guesses headings. Paw checks are local
to the device. Dedicated cooking mode saves same-day progress locally, never in
the recipe JSON.
Copy includes only checked ingredients, in visible group order, with headings
only for groups containing checked items. When a temporary scale is active, copy
the displayed scaled amounts, including any approximation marker. Quantities
flagged for manual review retain their original wording, just as on screen.
Returning to Original copies the original amounts again. Copying never changes
the saved recipe. This behaviour needs no new controls or layout changes.

The API validates titles, ingredient descriptions, method steps, group IDs and
references before writing. An older client can still read grouped recipes, but
attempting to edit one without its group metadata returns HTTP 409 rather than
silently deleting the groups. Flat recipes remain editable by older clients.

## Deploying

1. Back up the recipe storage as usual and publish the updated `RecipeAPI` to
   the existing Windows/IIS API site **before installing the updated app**.
   Keep the existing `RecipeFilePath` and file-server configuration.
2. Build/install Nibbles for the target platform. The existing home-network
   addresses remain `http://192.168.1.110:8080/` for the API and port `8081`
   for recipe files and photos.
3. Refresh the recipe box while connected. Downloaded JSON, photos, and an
   offline index are cached locally. Offline browsing/cooking remains available;
   offline recipes are read-only. Refresh reconnects and re-enables editing.

The app retains edits on save failure and requires an explicit retry. New
recipes supply a UUID so a timed-out create cannot create a second copy on retry.
If a retry reports that the first create reached the server, a further explicit
Save updates that same recipe. A successful server save followed by a cache
failure is reported separately, without asking the user to create another copy.

## Development checks

```powershell
dotnet test ..\RecipeAPI.Tests\RecipeAPI.Tests.csproj
dotnet build ..\Nibbles\Nibbles.csproj -f net10.0-android
dotnet build ..\Nibbles\Nibbles.csproj -f net10.0-windows10.0.19041.0
```

The tests exercise API HTTP responses, JSON round-trips, legacy data, grouping,
checked copying, paste parsing, safe heading removal, retry protection, index
updates, and photo uploads. To additionally read-check existing recipe files
without modifying them:

```powershell
$env:NIBBLES_COMPATIBILITY_RECIPES = 'C:\rfiles'
dotnet test ..\RecipeAPI.Tests\RecipeAPI.Tests.csproj
```

iOS device builds, signing, and launch-screen verification require a Mac/Xcode.
The live API/IIS deployment is not modified by these development checks.
