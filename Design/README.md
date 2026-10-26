# Nibbles redesign

`Nibbles-redesign.fig` is the editable OpenPencil design, containing the approved
1.2.0 screens and the approved 1.2.1 revisions below. Open it in OpenPencil to
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
only for groups containing checked items.

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
