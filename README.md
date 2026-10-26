# ReceipeProj
Vehicle to practice API creation and Agular/Typescript sites!

The approved Nibbles redesign, assets, ingredient-group schema, and deployment
notes are in [Design](Design/README.md).


## Nibbles direct GitHub storage (1.3.0)

Nibbles now downloads and saves recipes directly to the private
`BenBleh/rfiles` repository, branch `main`. It does not contact the home API or
file server and has no connection/setup screen. Downloaded recipes, photos,
search, cooking and scaling work offline, but adding, editing and saving do not.
An interrupted editor retains its in-memory draft until reconnection or discard;
there is no offline upload queue.

Before saving, the app checks the recipe, its master-list entry and replaced
photos against the editor's original revision. Unrelated changes are preserved.
Recipe JSON, the latest merged `MasterList.csv` and changed JPEGs publish in one
non-force commit. Conflicts keep edits and offer explicit discard/reload.
An uncertain publication keeps a durable commit record and requires checking
the save outcome before resubmission. Local-cache failure after a confirmed
publication is reported as **Saved to GitHub**, not a failed save.

### Private build input

Before any build or publish, set a local environment variable:

```powershell
$env:NIBBLES_GITHUB_TOKEN_FILE = 'C:\private\NibblesAccessToken.txt'
```

The file must contain only a fine-grained personal access token: resource owner
`BenBleh`, selected repository `rfiles`, **Contents: Read and write**, and
**No expiration**. Never commit this file. The build embeds it directly from
the external input and rejects missing or malformed credentials without
printing them; `-p:NibblesGitHubTokenFile=...` is also supported.
Use private build machines and avoid publishing build outputs/logs.

**The token can be extracted from the app. Keep APKs, Windows installers and
publish folders private.** Non-expiring tokens can still be revoked or removed
by GitHub; replacing one requires rebuilding and installing the app.
Do not upload these installers as public release assets.

The legacy API and its backup worker were reconciled with GitHub and paused
with the owner's approval during cutover, with automatic startup disabled.
IIS configuration, old recipe files and API source are retained. The old local
`C:\rfiles` checkout is no longer the live store: new saves update GitHub, not
that directory. Do not restart legacy writers against that stale checkout.
To roll back, first stop GitHub-backed clients, reconcile all JSON/index/photos
and the old outbox with the current GitHub branch, and only then re-enable the
API site/application pool. Do not reset or force-push over app changes.

iOS uses the same storage implementation and its native image encoder; building
and signing iOS still requires a Mac.

## Legacy API setup (not required by Nibbles 1.3.0)

1.Install IIS
2. set up to host ASP sites
	https://learn.microsoft.com/en-us/aspnet/core/tutorials/publish-to-iis?view=aspnetcore-9.0&tabs=visual-studio
3.Add 2 websites
	1. I called it recipeAPI, must be http, and on port 8080
	2. I called it rfiles, must be port 8081
	
4.open ports
5.may need to add user permissions
6.Publish api to recipeAPI site
	I didn't need to do anything else for this to work this time
7.Copy over rfiles if you have them
	I enabled directory browsing, may not be nessicary
8. if end point url has changed, you may need to update the app

## Nibbles Android development

Open the `Nibbles` folder in VS Code with Microsoft's .NET MAUI extension enabled.
Select an Android emulator or device using `.NET MAUI: Select Android Device`, then
select the `.NET MAUI Android` launch configuration and press F5.
If prompted for a startup project, select `Nibbles`. The launch profile uses the
loaded startup project rather than a `${workspaceFolder}` project path, which the
MAUI extension tries to resolve before VS Code expands the variable.
The project uses Mono for Android; Meteor 10 requires .NET 11+ / CoreCLR and is not
compatible with this .NET 10 Mono debugging setup.

To build from PowerShell:

```powershell
Set-Location C:\Dev\ReceipeProj\Nibbles
dotnet build Nibbles.csproj -f net10.0-android -c Debug
```

If Android resource extraction fails with `XARLP7000`, stop active builds/debugging
and regenerate the intermediate resources:

```powershell
dotnet build Nibbles.csproj -f net10.0-android -c Debug -t:Rebuild
```

## Nibbles Windows debugging

Open the `Nibbles` folder in VS Code, select `.NET MAUI Windows` in Run and Debug,
and press F5. This builds and debugs the unpackaged Windows app in Debug mode.
If prompted, select `Nibbles` as the startup project and the Windows target.
The existing Android launch configuration is unchanged.

## Nibbles installable builds

Run these commands from the repository root with the private token-file
environment variable set. The current release is 1.3.0, Android version code 8;
increment those values in `Nibbles.csproj` for future releases.

### Android APK

```powershell
dotnet publish .\Nibbles\Nibbles.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk -p:EmbedAssembliesIntoApk=true -p:AndroidUseSharedRuntime=false -p:AndroidKeyStore=false -o .\artifacts\Nibbles\1.3.0\android
Copy-Item .\artifacts\Nibbles\1.3.0\android\com.companyname.nibbles-Signed.apk .\artifacts\Nibbles\1.3.0\Nibbles-1.3.0-Android.apk
```

This produces a standalone APK for ARM64 phones and x64 emulators, signed with
the existing .NET Android development key in
`%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore`. Keep this key backed up:
Android updates must use the same signing certificate. This signing setup is for
personal sideloading, not Play Store distribution. Open the APK on the phone and
allow installation from that source if Android prompts.

### Windows setup EXE

Install the Inno Setup 6 compiler, then run (adjust the compiler path for a
per-user or portable installation):

```powershell
dotnet publish .\Nibbles\Nibbles.csproj -f net10.0-windows10.0.19041.0 -p:TargetFrameworks=net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained true -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' /DAppVersion=1.3.0 .\Nibbles\Packaging\Nibbles.iss
```

The private installer is `artifacts\Nibbles\1.3.0\Nibbles-1.3.0-Windows-x64-Setup.exe`.
It supports 64-bit Windows 10 version 1809 or later, bundles .NET and the Windows
App SDK, and installs into `%LOCALAPPDATA%\Programs\Nibbles` without elevation.
It adds a Start menu shortcut, an optional desktop shortcut, and an uninstaller;
uninstalling does not delete cached recipes or settings. The installer is not
code-signed, so Windows may show an unknown-publisher warning.
The Windows publish command scopes restore to the Windows framework to avoid
applying its `win-x64` runtime identifier to the Android/iOS targets.

Both apps use GitHub over HTTPS; the home network and VPN are no longer required.

## App-styled dialogs

App-owned confirmations, group-name prompts and action menus share the approved
cream/forest styling in light and dark themes. Prompts validate names inline;
destructive actions remain explicit, outside taps do nothing, and Back/Escape
cancels. Phone action menus use bottom sheets; Windows dialogs are centered and
width-limited. Long content scrolls separately from the action buttons, including
when the phone keyboard is open. Native photo pickers and permission prompts,
and the existing paste/scaling workflows, are unchanged. No API update is needed
for this release.

## Cooking, search and scaling

Start cooking from recipe details to use the foreground-only keep-awake view.
Windows shows ingredients and the existing numbered method side by side at wide
widths; phones retain separate tabs and scroll positions. Clear checked is fixed
outside the ingredient scroll area and is shown only when something is checked.
Progress is stored only on the device and can be resumed on the same local
calendar day. A later-day return starts fresh; actively cooking across midnight
is not interrupted. Changed recipes require review rather than restoring stale
ingredient indexes.

Search indexes the available recipe JSON after refresh, across titles,
ingredients/group names, notes and method. Every query term must occur in the
same recipe, ignoring case and accents. Results show excerpts and open the
matching section; an empty query keeps the normal library.
Search-result tiles retain the recipe image above the title and matching excerpt;
Windows uses a responsive grid and phones use full-width result cards.

Half, original, double and custom positive scaling are temporary display
changes. Review the original and scaled amounts before applying. Ambiguous,
ranged or unquantified quantities remain unchanged and flagged. The saved recipe
and method text, temperatures and durations are never scaled.

## API-only GitHub backup operations

`GitBackup` is disabled in the checked-in defaults. Enable it in the server's
private production configuration only after configuring the existing recipe
repository and a noninteractive GitHub credential for the IIS identity.
There are no backup endpoints, app indicators or notifications.

```json
"GitBackup": {
  "Enabled": true,
  "QueuePath": "C:\\ProgramData\\Nibbles\\BackupQueue",
  "GitExecutable": "C:\\Program Files\\Git\\cmd\\git.exe",
  "Remote": "origin",
  "Branch": "main"
}
```

Keep the queue and any credentials outside both IIS web roots and `C:\rfiles`.
Grant the API pool identity Modify access to the queue and recipe repository
(including `.git`), and only the credential access it needs. A repository-scoped
write deploy key can be used with server-only `SshCommand` and `UseGitHubSsh: true`;
the latter selects SSH transport for the existing GitHub HTTPS remote without
changing its saved URL. Provision trusted GitHub host keys separately and keep
strict host-key checking enabled. Never commit private keys or tokens.
At startup, a noninteractive dry-run push verifies the IIS identity's transport
without committing or uploading anything; its server-only result is recorded in
`access-check.json` in the private queue.

API writes durably record intended file fingerprints in a private outbox before
atomically persisting the affected JSON, images and CSV index. The worker checks
the completed write under the same lock, commits only verified API-owned paths,
then releases the write lock before pushing. GitHub outages do not block recipe
saves; pending jobs and committed-but-unpushed work survive restarts.

Unrelated unstaged manual files are left alone. Manual staged work, unpushed
manual commits, conflicting changes to owned paths, authentication failures and
non-fast-forward rejections pause backups with server logs and `paused.json` in
the private queue. Review/resolve the underlying condition and delete only that
pause marker to resume. Do not delete job files to retry, or reset/stash/force-push
the repository. Transient push failures use bounded exponential retry delays.
`last-success.json` is written only after a confirmed push; a local commit alone
is not a remote backup. Recovery remains manual through GitHub/Git, including
images and `MasterList.csv`, followed by an app refresh.

An edit that was already uncommitted before an API save is not silently adopted
as API-owned work. Review and commit/push that file before removing its pause
marker. If queued API edits were deliberately reverted instead, an administrator
must reconcile those jobs with the current files; retain reviewed obsolete jobs
and the pause record in a private recovery archive rather than deleting jobs to
force a retry. Never blanket-approve unrelated files or disable ownership checks.

Verify backups with a real update through the deployed API: check the worker's
new `last-success.json`, the matching commit on GitHub, the changed recipe
contents, and the absence of pending jobs. A successful startup/dry-run access
check does not exercise save-to-queue, commit, or completed backup.
