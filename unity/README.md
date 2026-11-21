# Unity projects: ORBITA GALAXY and frozen Arcade Rush

This folder currently hosts two Unity codebases sharing the same 2022.3.74f1 project:

- **`Assets/OrbitGalaxy/`** (branch `galaxy-main`) — the active ORBITA GALAXY single-player
  .io arena. Namespace `Orbit.Galaxy` for the pure C# core and `Orbit.Galaxy.Unity` for
  uGUI presentation. Tests: `Tests/Galaxy/Orbit.Galaxy.Core.Tests.csproj`. Releases: `g*`.
- **`Assets/Orbit/`** (branch `unity-main`) — the frozen Arcade Rush rewrite from
  `u4.0.0`. Namespace `Orbit.Core` / `Orbit.Unity`. Tests:
  `Tests/Orbit.Core.Tests.csproj`. Releases: `u*`.

The two cores are isolated by `.asmdef` files; the native Android/iOS v3.8 project and its
`v*` release pipeline are unchanged. A Galaxy build must be produced with
`Orbit.Galaxy.Editor.GalaxyBuildScript`, whose player settings give the product a name
containing `GALAXY`; the legacy bootstrap then stays dormant.

See [`../docs/GALAXY_MIGRATION.md`](../docs/GALAXY_MIGRATION.md) for the migration audit.

## Legacy Arcade Rush architecture (frozen)

- **`Assets/Orbit/Core/`** — pure C# game logic in namespace `Orbit.Core`. It has no
  `UnityEngine` dependency (it only uses `Newtonsoft.Json`), so it can be compiled and
  tested outside Unity with `dotnet`.
- **`Assets/Orbit/Scripts/`** — Unity presentation layer in namespace `Orbit.Unity`.
  It drives the core through uGUI (`com.unity.ugui`) and handles input, layout and
  rendering. This layer is owned by a separate workstream and is not part of the
  core test suite.
- **`Assets/Orbit/Scenes/Main.unity`** — generated bootstrap scene. It is created by
  `Orbit.EditorBuild.BuildScript.EnsureMainScene()` and registered as the only enabled
  build scene, both locally and in CI.
- **`Assets/Editor/`** — build entry points (`BuildScript.cs`) and optional editor menu
  helpers (`MenuTools.cs`). No gameplay code lives here.
- **`Tests/`** — xUnit project (`Orbit.Core.Tests.csproj`, `net8.0`) that compiles the
  core sources directly and runs them on the .NET SDK.

The Unity editor version is pinned in `ProjectSettings/ProjectVersion.txt` to
**2022.3.74f1** (`f81e190e19ca`). Package dependencies are declared in
`Packages/manifest.json`.

## Run the core tests (no Unity or license needed)

From the repository root:

```bash
dotnet test unity/Tests/Orbit.Core.Tests.csproj --configuration Release --nologo
```

This is exactly what `.github/workflows/unity-core.yml` runs on pushes to `unity-main`
and on pull requests that touch `unity/**` or the workflow file itself. The job is named
**Core tests** and uses `actions/setup-dotnet@v4` with .NET 8.0.x.

Galaxy core tests run separately (the Galaxy core has no Newtonsoft dependency):

```bash
dotnet test unity/Tests/Galaxy/Orbit.Galaxy.Core.Tests.csproj --configuration Release --nologo
```

Both suites are executed by `.github/workflows/galaxy-core.yml` on `galaxy-main`, on `g*`
tags and by `.github/workflows/galaxy-build.yml` before a release is published.

## Open the project locally

1. Install Unity Hub and the **2022.3.74f1** editor (with Android Build Support and,
   on macOS, iOS Build Support).
2. In Hub: **Add → Add project from disk** and select `/root/orbita/unity`.
3. On first open Unity generates `ProjectSettings/ProjectSettings.asset`, `Library/`,
   and `.meta` files. Generated folders are git-ignored; commit the `.meta` files if
   you add new assets.
4. Use **Orbit → Scenes → Regenerate Main Scene** to recreate `Main.unity` if it is
   missing or out of date.

Builds from the editor menu (**Orbit → Build → Build Android APK / Build iOS Xcode
Project**) are written to:

- `unity/build/Android/Orbit.apk`
- `unity/build/iOS/Orbit.xcodeproj`

**Orbit → Build → Open Build Folder** reveals that directory. The `build/` output is
ignored by Git.

## CI and required GitHub secrets

`.github/workflows/unity-build.yml` runs on tags matching `u*` (for example `u4.0.0`)
and on manual `workflow_dispatch`. It builds Android on `ubuntu-latest` and iOS on
`macos-latest` via `game-ci/unity-builder@v4`, caching `unity/Library` between runs.
A `credentials` job detects secrets before either build starts. **Right now no GitHub
Actions secrets are configured**, so the Android/iOS jobs are skipped (not failed) and
the `u*` tag publishes a source-only release with a warning that explains how to enable
binary builds. The core test workflow does not need any secrets and always runs,
including on `u*` tags.

To enable binary builds, add one of these authentication methods in
**Settings → Secrets and variables → Actions**:

| Secret | Purpose |
| --- | --- |
| `UNITY_LICENSE` | Contents of a Unity license file (`.ulf`). Preferred for Personal licenses. |
| `UNITY_SERIAL` | Unity Pro serial number. Requires `UNITY_EMAIL` and `UNITY_PASSWORD` for activation. |
| `UNITY_EMAIL` | Unity account email (used together with `UNITY_SERIAL`). |
| `UNITY_PASSWORD` | Unity account password (used together with `UNITY_SERIAL`). |

Either `UNITY_LICENSE` **or** `UNITY_SERIAL` must be present; the email/password pair
is needed only for the serial path, but all four are forwarded to the builder when set.

On `u*` tags a `release` job either downloads both artifacts and renames them to
`orbita-<tag>-unity-debug.apk` and `orbita-<tag>-unity-ios-xcode.zip`, or, when no
credentials are configured, archives the tagged commit as
`orbita-<tag>-unity-source.zip`. Both variants are published with
`softprops/action-gh-release@v2`.

## Tag convention — do not reuse `v*`

- Native v3.8 releases use `v*` tags and the existing `.github/workflows/build.yml`.
- Unity releases use **`u*`** tags, e.g. `u4.0.0`, `u4.1.0`. This prevents the native
  workflow from picking up Unity tags.
- The existing native **`v4.0` / `v4.1` tags and their releases are untouched** by the
  Unity work. Do not delete, rename, or republish them from this branch.
