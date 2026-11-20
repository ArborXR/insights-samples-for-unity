# ArborXR Insights Samples

Small, numbered Unity samples that each teach one feature of the
[AbxrLib SDK for Unity](https://github.com/ArborXR/abxrlib-for-unity) (ArborXR Insights) in an
enterprise training scenario, and show exactly what each call sends to Insights.

> **Status:** samples 01–19 are built and run in WebGL and on Android. Quest builds are next. See
> [CONTRIBUTING.md](CONTRIBUTING.md) for how the samples are put together.

## Quick start

1. Clone this repository. Unity fetches [`abxrlib-for-unity`](https://github.com/ArborXR/abxrlib-for-unity) from
   GitHub, pinned in `Packages/manifest.json` to an unreleased 3.0 commit until it ships.
2. Open this project in Unity **6000.2.6f2**, with WebGL Build Support for browser builds (and Android Build
   Support for Android and Quest builds).
3. Open `Assets/InsightsSamples/_Common/Shell/Launch.unity` for the sample browser, or open any sample's scene
   (for example `Assets/InsightsSamples/02. Assessments/Assessments.unity`), and press Play. The shell, the
   Insights Stream, and sign-in create themselves.

**Controls:** click or tap the station's buttons; drag the torch in sample 13; right-drag (or drag a finger)
to look around in sample 14. Each sample has **Run sample** buttons along the bottom that play it for you.

## How a sample works

Each sample is a small room with one station, an intro, and the Insights Stream.

- **The station** is the scenario: the props you click, drag, or look at. Its scripts never call the SDK.
- **The intro** describes the real-world scenario, says what the sample shows, how to reach a pass and a fail,
  what to watch for in the Stream, and which line of the lesson script to read. It steps aside when you use the
  station; **Show intro** brings it back.
- **The lesson script** (`<Concept>Sample.cs`) is the only code in the sample that calls `Abxr`. It's short, and
  its comments say why each call is made the way it is. Copy from there.
- **The Insights Stream** shows each SDK call as a row, with whether its record was sent. Click a row to open
  it: what your code passed, what the SDK adds, and the dashboard view it feeds. The terminal button in the header
  hides the Stream, which gives the station the whole screen; on a narrow screen it starts hidden.
- **The speaker button** in the header mutes the interface sounds or sets their volume.

Samples build on lower-numbered ones without re-teaching them: sample 03's objectives sit inside an
assessment, which sample 02 teaches.

## Samples

| # | Sample | Teaches | Scenario | Lesson script |
|---|---|---|---|---|
| 01 | Sign-in | `OnAuthCompleted`, `StartAuthentication` | Front-desk sign-in | [SignInSample.cs](Assets/InsightsSamples/01.%20SignIn/SignInSample.cs) |
| 02 | Assessments | `EventAssessmentStart`, `EventAssessmentComplete` | Site safety induction quiz | [AssessmentsSample.cs](Assets/InsightsSamples/02.%20Assessments/AssessmentsSample.cs) |
| 03 | Objectives | `EventObjectiveStart`, `EventObjectiveComplete` | Lockout/tagout on a conveyor | [ObjectivesSample.cs](Assets/InsightsSamples/03.%20Objectives/ObjectivesSample.cs) |
| 04 | True/false interactions | `EventInteractionStart`, `EventInteractionComplete`, `InteractionType.Bool` | Spot the hazards in a workshop | [TrueFalseSample.cs](Assets/InsightsSamples/04.%20TrueFalse/TrueFalseSample.cs) |
| 05 | Multiple choice | `InteractionType.Select` | Choose the right gloves for a chemical | [MultipleChoiceSample.cs](Assets/InsightsSamples/05.%20MultipleChoice/MultipleChoiceSample.cs) |
| 06 | Sequencing and numbers | `InteractionType.Sequencing`, `InteractionType.Number` | Clinic hand hygiene and an infusion rate | [SequenceAndNumberSample.cs](Assets/InsightsSamples/06.%20SequenceAndNumber/SequenceAndNumberSample.cs) |
| 07 | Critical events | `EventCritical` | Clear a jammed conveyor | [CriticalEventsSample.cs](Assets/InsightsSamples/07.%20CriticalEvents/CriticalEventsSample.cs) |
| 08 | Timed events | `StartTimedEvent`, `Event` | Warehouse order pick | [TimedEventsSample.cs](Assets/InsightsSamples/08.%20TimedEvents/TimedEventsSample.cs) |
| 09 | Super metadata | `Register`, `RegisterOnce`, `Unregister`, `Reset` | Same task at two sites and languages | [SuperMetadataSample.cs](Assets/InsightsSamples/09.%20SuperMetadata/SuperMetadataSample.cs) |
| 10 | User data | `SetUserData`, `GetUserData`, `GetUserId` | Employee badge at a shared headset | [UserDataSample.cs](Assets/InsightsSamples/10.%20UserData/UserDataSample.cs) |
| 11 | Storage | `StorageSetEntry`, `StorageGetEntry`, `StorageRemoveEntry` | Resume a long equipment course | [StorageSample.cs](Assets/InsightsSamples/11.%20Storage/StorageSample.cs) |
| 12 | Diagnostic logs | `LogInfo`, `LogWarn`, `LogError` | Robot cell loading its job file | [DiagnosticLogsSample.cs](Assets/InsightsSamples/12.%20DiagnosticLogs/DiagnosticLogsSample.cs) |
| 13 | Telemetry | `TrackObject`, `Telemetry` | Weld along a seam | [TelemetrySample.cs](Assets/InsightsSamples/13.%20Telemetry/TelemetrySample.cs) |
| 14 | Gaze targets | `AbxrTarget`, gaze scores | Greet a retail customer | [GazeTargetsSample.cs](Assets/InsightsSamples/14.%20GazeTargets/GazeTargetsSample.cs) |
| 15 | Session lifecycle | `EndSession`, `StartNewSession`, `OnQuit*`, `OnHeadsetPutOnNewSession` | Headset handed to the next trainee | [SessionLifecycleSample.cs](Assets/InsightsSamples/15.%20SessionLifecycle/SessionLifecycleSample.cs) |
| 16 | Module targets | `OnModuleTarget`, `GetModuleList`, `OnAllModulesCompleted` | LMS-assigned forklift pre-shift check | [ModuleTargetsSample.cs](Assets/InsightsSamples/16.%20ModuleTargets/ModuleTargetsSample.cs) |
| 17 | Exit poll | `PollUser` | Confidence rating after training | [ExitPollSample.cs](Assets/InsightsSamples/17.%20ExitPoll/ExitPollSample.cs) |
| 18 | Passcode pairing | `OnPairingStateChanged`, `StartPairing`, `ClearPairing` | Front-desk device setup | [PairingSample.cs](Assets/InsightsSamples/18.%20Pairing/PairingSample.cs) |
| 19 | Custom sign-in UI | `OnInputRequested`, `OnInputSubmitted`, `CancelPairing` | Branded sign-in panel | [CustomSignInUISample.cs](Assets/InsightsSamples/19.%20CustomSignInUI/CustomSignInUISample.cs) |

Samples 18 and 19 are for devices without ArborXR MDM. A device ArborXR manages already knows its organization,
so it signs in (sample 01) without pairing.

Each sample's section in [Assets/InsightsSamples/README.md](Assets/InsightsSamples/README.md) explains what it
shows and what to watch for.

## Signing in

Every sample works without signing in, and each Stream row says whether its record was sent, or why not. To
send data, set your App Token in **Analytics for XR → Configuration** (`Assets/Resources/AbxrLib.asset`), or set
`ABXR_APP_TOKEN` (and `ABXR_REST_URL`, `ABXR_PAIRING_URL` if needed) in the environment Unity builds in, such as
a batchmode build: the build writes them into the asset for that build only. Never commit a token. A WebGL build
made without one [asks for it in the browser](#a-build-with-no-app-token).

On a device ArborXR manages, the SDK gets the organization from the device, and sign-in needs nothing more. In
WebGL and Android builds, a device without ArborXR MDM can pair through a Portal passcode instead of an org
token (**Insights Hub → Pairing Passcodes**); sample 18 shows how. See the SDK's README for setup.

## When records are sent

The SDK queues each record and sends them in batches: every **Send Next Batch Wait** (30 s by default), or as soon
as **Data Entries Per Send Attempt** (32) are waiting. That's why a Stream row can stay queued (a grey dot) for up to half a
minute. Both are under **Network Configuration** in **Analytics for XR → Configuration**, next to the retry
settings (**Send Retries On Failure**, **Send Retry Interval**) and **Maximum Cached Items** (1024), past which new
records are dropped (`Dropped: queue full`). After sign-in, the app's configuration from ArborXR can override any
of them. The samples keep the SDK's defaults, so the timing you see is the timing your app gets. On Android with
the Insights device service (`app.xrdi.client`) installed, the service sends them instead and reports no result,
so those rows stay queued.

## Building for the browser

1. **Insights Samples → Build → WebGL (Development)** builds to `Builds/WebGL-local`.
2. Serve it. Opening `index.html` from disk won't work, and neither will most static servers: the build's files
   are Brotli-compressed, so the server has to send them with `Content-Encoding: br`. This one does, and it
   disables caching:
   ```bash
   python3 tools/serve_webgl.py
   ```
3. Open <http://localhost:8765>, or a deep link such as <http://localhost:8765/?sample=02-assessments>.

### A build with no App Token

A WebGL build with no App Token asks for one when it opens. Visitors bring their own: they paste the App Token
of an Insights app in their organization, or open a link that carries it after the `#`
(`index.html#app_token=…`), which fills it in for them. The token stays in their browser. They then choose how
it connects:

- **Send to my organization** uses the App Token as the Org Token too, so records go to the organization that
  owns the app. It needs nothing else.
- **Pair with a passcode** pairs with a passcode they create for that app in the Portal. Their organization
  needs MDM-less pairing. The build can't tell whether it has it, so a link from an organization without it adds
  `&pairing=off`, which turns the button off.

A link that adds `&connect=direct` or `&connect=pair` connects without asking, for automated runs. Suite and
autopilot links skip the panel and play signed out.

The samples wait for a connection: there's no "Not now" on the token panel or the pairing prompt (run them in
the Editor to explore without an org). Changing the connection after sign-in reloads the page, because the SDK
keeps one identity per launch. This is hosting plumbing, not a lesson: your app ships its App Token in its
build. The code is in `Assets/InsightsSamples/_Hosting`.

To host one copy anyone can try, **Insights Samples → Build → WebGL (Hosted)** builds a Release copy to
`Builds/WebGL-<env>-hosted` and leaves the App Token out even when the environment sets one. The host has to
send each `.br` file in `Build/` with `Content-Encoding: br`, and the `.wasm.br` as `application/wasm`. Those
files are named by a hash of their contents, so they can be cached for as long as you like. Only `index.html`
needs `Cache-Control: no-cache`.

## Building for Android

1. In Unity Hub, add **Android Build Support** (with the Android SDK & NDK Tools and OpenJDK) to 6000.2.6f2.
2. **Insights Samples → Build → Android Flat (Development)** builds
   `Builds/Android-Flat-local/InsightsSamples.apk`, for phones and tablets.
3. Install and run it with the same deep links, as intent extras:
   ```bash
   python3 tools/android_e2e.py --sample 02-assessments --autopilot pass
   ```

On the Android emulator, start the device with Vulkan off (`emulator -avd <name> -feature -Vulkan`): Unity hangs
in the emulator's Vulkan layer, and runs on OpenGL ES 3 instead. Quest builds are next.

## End-to-end runs

Every sample can play itself through the same props a person uses: **Run sample** at the bottom of the sample, or a
deep link. That makes the samples easy to demo and to test with a browser automation tool.

| Deep link | Effect |
|---|---|
| `?sample=05-multiple-choice` | Opens a sample |
| `&autopilot=pass` or `&autopilot=fail` | Plays it to that outcome |
| `?suite=02-assessments,03-objectives` | Plays each listed sample to a pass and a fail |
| `&pace=fast` | Skips the autopilot's cursor moves and pauses |
| `&module=preshift_forks` | Sample 16's LMS-style module link |
| `&stream_log=1` | Turns on the log lines below in release builds |
| `&pairing=skip` | Keeps the pairing prompt closed at launch, for unattended runs. **Pair this device** still opens it |
| `&sound=off` | Silences the interface sounds for this launch, whatever the header's speaker button is set to. `pace=fast` does too |

Each run logs one line when it finishes, for example
`[sample] {"id":"02-assessments","state":"finished","outcome":"pass","result":"pass","records":"ok","problems":[]}`.
`result` is the outcome the station reached. `records` compares the run's Stream rows with the records the
sample expects (names, statuses, and counts), and lists any `problems`. A suite ends with
`[suite] {"state":"finished","runs":34,"passed":34,"failures":[]}`, and each Stream row is logged as an
`[insights-stream]` line. Development builds always log these lines; release builds need `stream_log=1`.

Samples 01, 18, and 19 need a build that can sign in; leave them out of suites otherwise. To start from a clean slate
(no pairing or saved progress), use a fresh browser profile, or pass `--clear-data` to `tools/android_e2e.py`.

## Other APIs

These aren't taught by a sample. See the SDK's README for each.

- `EventExperienceStart` / `EventExperienceComplete` and `EventLevelStart` / `EventLevelComplete`: they overlap
  with assessments (02) and objectives (03).
- `Event(name, position, meta)`: an event with a position in the scene.
- `LaunchAppWithAuthHandoff`: for launcher apps that sign in once and hand off to other apps.
- `RedeemPairingPasscode`: pairing from your own UI with no prompt at all (sample 19 uses the prompt).
- `GetDeviceId`, `GetDeviceSerial`, `GetDeviceTitle`, `GetDeviceTags`, `GetOrgId`, `GetOrgTitle`, `GetOrgSlug`:
  what the SDK knows about the device and organization.
- `GetAccessToken`, `GetRefreshToken`, `GetExpiresDateUtc`, `GetFingerprint`: credentials. Never display or
  log them.
- `TrySetRestUrl`, `SetDeviceId`, `SetOrgId`, `SetAuthSecret`: overrides for integration and testing.

## License

The code and original assets in this repository are dedicated to the public domain under
[CC0 1.0](LICENSE). Copy whatever you need into your own app, no attribution required.

**Trademarks are not included.** "ArborXR", "ArborXR Insights", the ArborXR logo, and other ArborXR
brand assets are trademarks of ArborXR and are not licensed under CC0 (see CC0 §4(a)). Don't use them
in your own app or in a way that suggests ArborXR endorses it. The only brand asset here is the ArborXR icon in
the SDK's World-Space UI sample.

**Third-party content keeps its own license:**

- The Roboto font ([Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)) and Kenney's
  [Furniture Kit](https://kenney.nl/assets/furniture-kit) (CC0) sit in `Assets/Art/ThirdParty/` with their licenses.
- The interface sounds are three clips from Kenney's [Interface Sounds](https://kenney.nl/assets/interface-sounds)
  (CC0), in `Assets/Art/ThirdParty/Kenney_Interface_Sounds/` with its license.
- The room's concrete floor and plaster textures and sample 07's jammed carton are from
  [Poly Haven](https://polyhaven.com) (CC0). Sample 14's customer is built from MakeHuman's CC0 assets.
- The shell's icons are from [Lucide](https://lucide.dev), under the
  [ISC License](ArtSource/Icons/lucide/LICENSE) (some under MIT, from Feather). Their SVGs are in
  `ArtSource/Icons/lucide/`, and `UiIconData.cs` holds the outlines built from them.
- `Assets/TextMesh Pro/` is Unity's TextMesh Pro Essential Resources, under the
  [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license). Its LiberationSans font is
  under the [SIL Open Font License 1.1](https://openfontlicense.org), and its EmojiOne sprites carry EmojiOne's
  terms (see `EmojiOne Attribution.txt`).
- The AbxrLib World-Space UI sample in `Assets/Samples/` is part of the SDK and keeps its
  [Apache 2.0 license](https://github.com/ArborXR/abxrlib-for-unity/blob/main/LICENSE); the ZXing and
  UiRoundedCorners libraries inside it keep the licenses beside them. This project's copy changes one file:
  `KeyboardHandler.cs` makes the sign-in and pairing prompt non-modal, so the header, the Stream, and the sample browser
  stay usable while it's open (stations wait until it closes).

Acme is a fictional company.
