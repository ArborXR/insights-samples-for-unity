# Contributing

How the samples are put together, and the rules a new or changed sample follows. The README covers using them.

## Rules

1. **One feature, one sample.** Each SDK feature is taught in exactly one sample. Other samples may use it as
   context, but never re-teach it, and only use features from lower-numbered samples.
2. **The lesson script is the lesson.** Each sample has one script that calls `Abxr`, named `<Concept>Sample.cs`,
   kept as short as the feature allows (most are under 120 lines), with short comments on why each call is made the
   way it is. Props, input, UI, and the autopilot never reference `Abxr`.
3. **Only two places call the SDK:** lesson scripts, and the Insights Stream (`_Common/InsightsStream/`), which only
   observes the records the SDK creates and never records anything itself. Autopilots and the shell may look at the
   World-Space UI sample's prompts (to press their buttons, or step aside while one is open), but never record.
4. **Correct usage only.** People copy this code. Every name is a stable `snake_case` ID: no joke data, fake
   errors, or free-text event names. Never put `module`, `moduleName`, `moduleId`, or `moduleOrder` in metadata.
   Identity goes in userData; repeating context goes through `Abxr.Register`.
5. **Close what you open.** Leaving a sample mid-run completes anything it started (an assessment, objective, or
   interaction) with the correct status: in the lesson's `OnSampleExit`, or for an app-wide lesson, when its
   station goes away.
6. **Never gate on sign-in.** Every sample works without sign-in, pairing, or a network. The header shows the
   connection state and the Stream shows what happened to each record. Sign-in is taught in sample 01, and pairing
   for devices without ArborXR MDM in sample 18. The one gate is outside the samples: a WebGL build with no App
   Token asks for one before it shows them (`_Hosting`).
7. **Short.** Each sample takes under two minutes, and both a pass and a fail are easy to reach on purpose.
8. **Every sample can play itself.** **Run sample** drives the same props a person uses, so the lesson script
   can't tell the difference. That makes samples easy to demo and to test end to end.
9. **No credentials in the repo.** `Assets/Resources/AbxrLib.asset` is committed with empty tokens.

If anything here disagrees with the SDK's `Runtime/Abxr.cs`, the SDK wins.

## Project structure

```
Assets/InsightsSamples/
  README.md              one section per sample
  _Common/
    Shell/               launch scene, persistent shell, navigation, deep links
    SampleBrowser/       home scene: a shelf of sample cards per series
    InsightsStream/      the Stream (observes the SDK, never records)
    Room/, Props/        the shared room and station props
    UI/, Input/          UI kit, theme, icons, sounds, intro, station input
    Autopilot/           Run sample and its checks
    Catalog/             SampleInfo and the sample catalog
    Editor/              build menu, validator
  NN. Concept/
    Concept.unity
    ConceptSample.cs     the lesson script: the only Abxr caller in the sample
    <Station>.cs         the station's props and UI (for example QuizKiosk.cs)
    ConceptAutopilot.cs  Run sample's script
    Concept.asset        SampleInfo
```

`SampleInfo` holds a sample's id, title, series, icon, intro, the lesson line to read, and the records a run
should produce. The browser, header, intro, Stream notes, deep links, and Run sample's checks all read it. The
catalog also lists each series' icon and hue, and an optional note on who the series is for.

Assembly definitions enforce the layering:

| Assembly | Calls `Abxr` |
|---|---|
| `InsightsSamples.Common` | Never |
| `InsightsSamples.Stream` | Observes only |
| `InsightsSamples` (the sample folders) | In `*Sample.cs` only |

**Insights Samples → Validate** (also run before every build) fails if `Abxr.` appears anywhere else, or a
cataloged sample's folder is missing its scene, its one lesson script, or its autopilot, or the sample's icon
or series isn't one the shell knows.

## Adding a sample

1. Create `Assets/InsightsSamples/NN. Concept/` with the scene, `ConceptSample.cs` (implementing
   `ISampleLifecycle` if it opens anything that must be closed), a station script, `ConceptAutopilot.cs`, and a `SampleInfo` asset.
2. Add it to `_Common/Resources/SampleCatalog.asset`. A new series needs an entry in the catalog's Series list
   (its icon and hue, and optionally a note on who it's for).
3. Give the `SampleInfo` an icon. The shell's icons are [Lucide](https://lucide.dev) outlines: to use one that
   isn't in `ArtSource/Icons/lucide/` yet, add its SVG there and run `python3 ArtSource/Icons/build_icons.py`.
4. Run **Insights Samples → Sync Build Scenes**, then **Insights Samples → Validate**.
5. Add its section to `Assets/InsightsSamples/README.md`.

Calls that create no record don't appear in the Stream, so explain them in the sample's intro.

## Builds and environment values

**Insights Samples → Build** writes `Builds/<target>-<ABXR_ENV>/`. For the build only, it copies these
environment variables (or the same lines in a git-ignored `.env`) into `AbxrLib.asset`, then restores it:

| Variable | Configuration field |
|---|---|
| `ABXR_APP_TOKEN` | `appToken` |
| `ABXR_REST_URL` | `restUrl` |
| `ABXR_PAIRING_URL` | `pairingUrl` |

Batchmode works too:

```bash
ABXR_ENV=local <Unity> -batchmode -quit -projectPath . \
  -executeMethod InsightsSamples.Common.Editor.SampleBuilds.BuildFromCommandLine -target webgl -logFile -
```
