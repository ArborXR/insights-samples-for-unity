# CLAUDE.md

Guidance for Claude Code (and other agents) working in this repository.

## What this is

**ArborXR Insights Samples**: a Unity project of small, numbered samples, each teaching one
**AbxrLib SDK for Unity** (ArborXR Insights) feature in an enterprise training scenario. Every sample shows what
it sends to Insights in the **Insights Stream**.

**Read [CONTRIBUTING.md](CONTRIBUTING.md) before changing a sample.** It has the rules every sample follows, the
project structure, and how to add a sample. The [README](README.md) covers running, building, and deep links.

## Project setup

- **Unity 6000.2.6f2**, URP, with WebGL and Android Build Support.
- **AbxrLib** comes from GitHub (`ArborXR/abxrlib-for-unity`), pinned in `Packages/manifest.json`. To work against a
  local SDK checkout, point the manifest at it with `file:` and don't commit that. If Unity keeps the old SDK after
  a change, request a clean script compile.
- **The World-Space UI sample** in `Assets/Samples/` is a copy Unity makes once, at import; it never follows the
  package. After the SDK changes its `Samples~/World-Space UI`, copy it over again and keep the local patch the
  README lists.
- **`com.coplaydev.unity-mcp`** lets Claude Code drive the editor. Its version must match the
  `mcpforunityserver` version in your MCP client config.
- **Brand assets** (ArborXR logo, wordmark) are trademarks and excluded from CC0; the repo has none of its own.
  Third-party assets are listed in the README's License section; packs with license files sit in
  `Assets/Art/ThirdParty/`. List any new one there.
- **Station props** are models in `Assets/InsightsSamples/_Common/Props/Models/`, made in Blender. Sources are
  `ArtSource/Props/StationProps.blend` and the build, bake, and export scripts in `ArtSource/Props/Scripts/`; run
  bakes headless on CPU. Stations take each model as an optional field and fall back to `StationBuilder` blocks.
  Each prop texture's `.meta` has a WebGL override (1024 max, crunch) that keeps the browser download small; give
  a new texture the same override.
  Don't add AI-generated 3D models: their licenses conflict with CC0.
- **Shell icons** are [Lucide](https://lucide.dev) outlines (ISC), drawn at runtime by `UiIcons`. To add one, put
  its SVG in `ArtSource/Icons/lucide/` and run `python3 ArtSource/Icons/build_icons.py`, which rewrites
  `_Common/UI/UiIconData.cs`. List any other new third-party asset in the README's License section.
- **Interface sounds** (press, pass, fail) are three clips from Kenney's Interface Sounds (CC0) in
  `Assets/Art/ThirdParty/Kenney_Interface_Sounds/`, played by `_Common/UI/UiSounds.cs`, which names each clip and
  its level. The header's speaker button sets the volume. Another clip needs a CC0-compatible license; list a new
  source in the README's License section.
- **Assembly definitions** enforce the layering in CONTRIBUTING.md. Unity logs an error for an asmdef with no
  scripts, so add each asmdef with its first script.
- **Builds:** **Insights Samples → Build** writes `Builds/<target>-<ABXR_ENV>/`; serve WebGL builds with
  `python3 tools/serve_webgl.py`, and run Android builds with `python3 tools/android_e2e.py`. Runs report through
  `[sample]`, `[suite]`, and `[insights-stream]` console (or logcat) lines; the README lists them.
- **WebGL builds are Brotli-compressed** with no decompression fallback, and the files in `Build/` are named by
  content hash. A server has to send `.br` files with `Content-Encoding: br` (`serve_webgl.py` does); a host can
  cache them long-term and needs `no-cache` only on `index.html`.
- **A WebGL build with no App Token** turns on `_Hosting` at launch: it waits until the visitor brings their own
  (pasted, or filled in from `#app_token=` in the link). The token doubles as the Org Token unless they choose to
  pair with a passcode. A link connects without asking only when it adds `&connect=direct` or `&connect=pair`,
  and `&pairing=off` turns the pairing button off. Suite and autopilot links skip the panel and play signed out.
  **WebGL (Hosted)** (`-hosted` in batchmode) leaves the token out even when the environment sets one. `_Hosting`
  calls `Abxr` and is exempt from Validate's lesson-only rule; it isn't a lesson.
- **Batchmode:** `<Unity> -batchmode -quit -projectPath . -executeMethod <Class.Method> -logFile -`.

## SDK behaviors worth knowing

From the SDK's `AGENTS.md`. The SDK (`Runtime/Abxr.cs`) is the source of truth for every `Abxr.*` call: if this
file or CONTRIBUTING.md disagrees with it, the SDK wins.

- **Configuration** is a ScriptableObject at `Resources/AbxrLib.asset` (`Configuration.Instance`).
  Use App Tokens is on by default (appToken required, orgToken optional).
- **`Abxr.OnAuthCompleted(bool success, string errorMessage)`** reports sign-in results, including validation
  errors and wrong-PIN re-prompts. It can fire `false` and later `true` for the same sign-in (offline, the SDK
  retries in the background), so handlers should just record state. Pairing has its own hook,
  `OnPairingStateChanged`, and the SDK never opens the pairing prompt on its own.
- **Auth rejected by the API (401/403)** is not retried. Later `StartAuthentication()` calls fail
  immediately until `StartNewSession()`, `EndSession()` or app restart. Transient failures (network, 5xx) are
  retried.
- **`OnInputRequested`** takes `(type, prompt, domain, error)` with type `text` | `assessmentPin` | `email`, or
  for the pairing prompt `pairingPasscode`, `pairingDeviceName`, `pairingDeviceNameRequired`, `pairingDeviceJoin`.
  Use assignment (`=`), not `+=`. Submit with `Abxr.OnInputSubmitted(value)`. Set it to `null` in
  `OnDestroy`. `error` is empty on the first request and carries the failure message on retry.
- **QR auth:** `IsQRScanForAuthAvailable()`, `StartQRScanForAuthInput(Action<string>)` (callback gets the
  PIN or `null` on cancel), `CancelQRScanForAuthInput()`. On Quest, `GetQRScanCameraTexture()` returns
  the camera texture for your own `RawImage`.
- **`GetUserData()`** returns a copy of the auth response's userData. Session userId is read-only.
- **`StartNewSession()`** clears pending data and auth state, assigns a new session ID, and re-runs auth.
  **`EndSession()`** ends without restarting.
- **Quit / EndSession auto-close:** any open assessment, objective, or interaction is completed with
  meta `quit_reason=application_quit` and `auto_closed=true`. Override recorded values with the
  `OnQuit*` properties (`OnQuitAssessmentStatus`, etc.).
- **Android:** when the ArborInsightsClient APK (`app.xrdi.client`) is installed and the client AAR is in
  `Plugins/Android/`, the SDK swaps transport from REST to the device service. Otherwise it uses REST.
  Check logcat for `[AbxrLib]` and `ArborInsightsClient`.

## Conventions

- Follow established patterns in the codebase unless explicitly asked to deviate.
- **Comments:** outside lesson scripts, prefer self-documenting code and skip comments that aren't useful.
  Lesson scripts (`*Sample.cs`) are teaching material: a short comment explaining *why* an SDK call is
  made a certain way (e.g. the `OnQuitAssessmentStatus` default) is useful and expected.
- **Acronyms as words** in identifiers (`SdkLens`, `XrRig`), except where the SDK's own API
  spells them otherwise (`IsQRScanForAuthAvailable`).
- **Never commit tokens or secrets.** `AbxrLib.asset`'s tokens stay empty in git; builds inject values.
- **Commits:** a concise subject under 70 characters that says why, not just what. Keep commits atomic: if the
  message would use "and", split the commit.
- When you find incorrect information in this file, verify and fix it as part of your work.

## Unity / Android troubleshooting

- **Android emulator:** start the AVD with `-memory 3072 -feature -Vulkan`. Unity hangs in the emulator's Vulkan
  layer; with Vulkan off it runs on OpenGL ES 3.
- **"Unable to reverse network traffic" / "Address already in use" (port 55504):** quit Unity, run
  `adb reverse --remove tcp:55504` and `lsof -i :55504`, kill the holder, reopen. If it persists,
  `adb kill-server` (use Unity's bundled adb) before opening Unity, or delete the project's `Temp/`.
- **Test Runner "No activity ... MAIN and LAUNCHER":** add a custom `Assets/Plugins/Android/AndroidManifest.xml`
  declaring `UnityPlayerActivity` with the MAIN/LAUNCHER intent filter.
- **Test Runner builds use package `com.UnityTestRunner.UnityTestRunner`.** For real builds use
  File → Build Settings → Build, and confirm Player → Android → Package Name before each build.
