# Nibbles redesign

`Nibbles-redesign.fig` is the approved editable OpenPencil design. Open it in
OpenPencil to continue designing. `Nibbles-overview.png`, the screen overviews,
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
to the current recipe view and are not saved.
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
