---
name: release
description: Cut a mod release - bump the version, roll the changelog, build, test, package, tag, and publish the GitHub release with the zip attached. The argument is the new version number, e.g. /release 1.2.0.
argument-hint: <version>
disable-model-invocation: true
---

# Cut a release

The argument is the new semantic version, plain `X.Y.Z` (e.g. `1.2.0`), used everywhere below. The git tag and zip name prefix it with `v`. Stop immediately with a clear message if the argument is missing or not three dot-separated numbers.

## Preflight

Stop and report (publish nothing) if any of these fail:

1. Working tree is clean, on `main`, and in sync with `origin/main` (run `git fetch` first).
2. `gh auth status` shows a logged-in account.
3. Tag `v<version>` does not already exist locally or on origin.
4. The new version is greater than the current `<Version>` in `Directory.Build.props`.

## Version bump

5. Set `<Version>` in `Directory.Build.props` to the new version. That file is the single source of truth: the build generates the `BepInPlugin` version constant from it, and the spoken startup line ("Hand of Fate Access version X.Y.Z loaded") reads the assembly version at runtime. Edit nothing else for the version; in particular do not touch `Plugin.cs`.

## Changelog roll

6. In `CHANGELOG.md`, rename the `## Unreleased` heading to `## <version> - <today's date as YYYY-MM-DD>` and insert a fresh, empty `## Unreleased` heading above it.
7. If Unreleased had no entries AND the changelog already contains a previous version section, stop and ask the user: a release with no player-facing changes is probably a mistake. An empty Unreleased is expected only for the initial release (no prior version sections).

## Commit, build, package

8. Commit `Directory.Build.props` and `CHANGELOG.md` together with the message `Release v<version>` (plus the standard co-author line).
9. Run `release.ps1`. It builds the plugin, runs the full offline test suite, and assembles the zip. Any build or test failure aborts the release: report it, fix nothing silently, publish nothing. The release commit can stay local while the failure is sorted out; do not push or tag.
10. Confirm `release\HandOfFateAccess-v<version>.zip` exists (the version in the zip name comes from the props file, so a mismatch means the bump did not take).

## Tag and publish

11. `git tag v<version>`, then `git push origin main v<version>`.
12. Compose the release notes (template below) into a temporary file and publish:

    gh release create v<version> "release/HandOfFateAccess-v<version>.zip" --title "Hand of Fate Access v<version>" --notes-file <notes file>

## Deploy locally

13. Run `build.ps1 -NoBuild` to copy the just-released binaries into the local game install. `release.ps1` already built them, so `-NoBuild` deploys exactly the bits that were published; the user's own game then runs the released version.
14. Verify the deployed `BepInEx\plugins\HandOfFateAccess.dll` in the game folder reports the new file version.

## Report

15. Report back: the release URL, the notes as published, confirmation of the local deploy, and a reminder that the startup line now speaks the new version number.

## Release notes template

Build every part from the live repo files at release time, never from memory or from this skill file:

1. Opening paragraph: the first paragraph of `README.md` (the text directly under the `# Hand of Fate Access` title), verbatim.
2. `## Installation`: the body of the README's `## Installation` section, with the "Download the latest release zip" step reworded to point at this release's asset: "Download `HandOfFateAccess-v<version>.zip` from the assets below." Keep the rest verbatim, including the merge-with-existing-folders note and the startup line check.
3. `## Changes`: the bullet entries for this version from `CHANGELOG.md`, verbatim. Omit this entire section for the initial release (no entries).

No other sections. Do not add badges, emoji, or a generated-by footer beyond what `gh` adds on its own.
