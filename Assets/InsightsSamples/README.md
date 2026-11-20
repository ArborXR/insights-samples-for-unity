# Insights samples

Each numbered folder teaches one AbxrLib feature. A sample has one scene, one **lesson script**
(`<Concept>Sample.cs`, the only file that calls the SDK), a station (the props you click), and an
autopilot that plays it for you. Shared plumbing lives in `_Common/`.

Open any sample's scene and press Play, or start from `_Common/Shell/Launch.unity` for the sample browser.
The **Insights Stream** on the right shows every record the SDK creates, newest first.

> The Stream reads the SDK's `AbxrObserver` API: each record as the SDK built it, what your code passed,
> what the SDK added, and what happened when it was sent. Calls that create no record (`StartTimedEvent`,
> `Register`, `PollUser`, and others) have no row; the Stream shows their effect on the records they shape.
>
> Some rows are the SDK's own. While no assessment is open, the SDK can run a placeholder assessment named
> `DEFAULT`: it records its start about a minute after sign-in and sets `module` to it. Ending the session
> (`EndSession`, `ClearPairing` on a signed-in device, or quitting) completes it with the `OnQuit` values, like
> anything else left open: `Fail` by default, and `Incomplete` here because sample 15 sets
> `OnQuitAssessmentStatus` at launch. Starting your own assessment drops it, even if its start was already sent.

## 01 · Sign-in

Scenario: a front-desk device that ArborXR manages, starting the day.

`SignInSample` subscribes to `Abxr.OnAuthCompleted` in `Awake`. With **Enable Auto Start Authentication** on
(the default), the SDK signs in one frame after launch, and `OnAuthCompleted` reports how it went. A `false`
isn't always final: when the device can't reach the backend, the SDK reports `false` once, keeps retrying, and
reports `true` when a retry succeeds, so the handler just records the state.

The app passes nothing in. The SDK gets the organization from ArborXR device management, from an org token,
or from a pairing, and the screen says which. A device with none of them reports one
`OnAuthCompleted(false, "No organization identity")`. Without ArborXR MDM that means "pair first" (sample
18), not that something is broken.

Sign-in applies to the whole app, so the shell creates this lesson at launch. The sample's scene shows the
live state, with a button to sign in again (`StartAuthentication`).

Ending a session raises no `OnAuthCompleted`, so the lesson stops showing "Signed in" itself: the samples that
end one (`EndSession` in 15, 16, and 19; `ClearPairing` in 18) tell it through the shell.

**Run sample** signs in again, and the device decides the result: neither a pass nor a fail can be forced.

**Watch the Stream for:** `OnAuthCompleted` rows (System filter).

## 02 · Assessments

Scenario: a three-question site safety induction quiz. The pass mark is 70.

`AssessmentsSample` reports the quiz as one assessment named `site_induction`:

- `EventAssessmentStart` when the quiz begins.
- Exactly one `EventAssessmentComplete` when it ends, with the 0–100 score and `Abxr.EventStatus.Pass` or
  `Fail`. Insights reports pass rates from the status, so the lesson decides it rather than relying on the
  default (`Complete`).
- Leaving mid-quiz completes the assessment with `Abxr.EventStatus.Incomplete`, because not finishing isn't
  the same as failing.

The SDK times the assessment, so the complete carries its duration.

**Watch the Stream for:** the start row, then the complete row with the score and status.
**In the dashboard:** assessment results (pass rate, scores, time to complete).

## 03 · Objectives

Scenario: lockout/tagout before maintenance on a conveyor. Uses assessments (02).

`ObjectivesSample` reports each of the five steps as an objective (`loto_notify_affected`, `loto_shut_down`,
`loto_isolate_energy`, `loto_apply_lock_tag`, `loto_verify_zero_energy`):

- `EventObjectiveStart` when a step becomes the current one, so each objective's duration is the time spent on
  that step.
- `EventObjectiveComplete` with 100 and `Pass`, or 0 and `Fail` when the learner skipped the step or it failed.
  Doing a later step first skips the current one.
- The assessment `lockout_tagout` is scored as the mean of the step scores; the pass mark is 80. Skipping the
  main disconnect fails two steps, because the start test then starts the conveyor.
- Leaving mid-procedure completes the open objective, then the assessment, as `Incomplete`.

**Watch the Stream for:** a start and a complete row per step, then the assessment complete.
**In the dashboard:** objective results per step, inside the assessment results.

## 04 · True/false interactions

Scenario: a workshop safety inspection. Uses assessments (02).

`TrueFalseSample` asks "is this a hazard?" about four things as `InteractionType.Bool` interactions:
`hazard_frayed_cable`, `hazard_blocked_exit`, `hazard_oil_spill`, and the decoy `decoy_guarded_grinder`.

- All four interactions start when the inspection starts.
- Flagging an item completes its interaction right away with the response `"true"`, so the duration is the time
  it took to spot. At **Finish inspection**, the rest complete with `"false"`.
- The result is `Correct` when a hazard was flagged or the decoy was left alone.
- The assessment `hazard_spotting` scores hazards found out of 3, minus 34 per false flag; the pass mark is 67.
- Leaving mid-inspection completes the unanswered interactions as `Neutral`, with no response.

**Watch the Stream for:** four start rows, a complete row as you flag each item, the rest at Finish.
**In the dashboard:** interaction results: how often each hazard is missed, and time to answer.

## 05 · Multiple choice

Scenario: choosing gloves before degreasing with mineral spirits. Uses assessments (02).

`MultipleChoiceSample` reports the choice as one `InteractionType.Select` interaction, `ppe_glove_selection`.
The response is the chosen option's ID (`glove_nitrile`, `glove_latex`, `glove_cotton`, or `glove_none`), not
its display text, so answers group cleanly and survive rewording. Nitrile is `Correct`.

**Watch the Stream for:** the interaction start when the question appears, then its complete with the option ID.
**In the dashboard:** interaction results: how often each option is chosen.

## 06 · Sequencing and numbers

Scenario: a clinic: hand hygiene, then an infusion pump. Uses assessments (02).

`SequenceAndNumberSample` uses two more interaction types:

- `hand_hygiene_sequence` (`InteractionType.Sequencing`): the learner orders five step cards. The response is
  their order as comma-separated IDs, e.g. `"wet,soap,scrub,rinse,dry"`. It's `Correct` only when it all matches.
- `infusion_rate_ml_per_h` (`InteractionType.Number`): the learner sets the pump for 500 mL over 10 hours. The
  response is the entered number, formatted with the invariant culture. 50 is `Correct`; 500 is the classic
  mistake.
- The assessment `clinic_skills_check` passes only when both are right.

**Watch the Stream for:** the two interaction completes and their responses.
**In the dashboard:** interaction results: accuracy and the responses learners give.

## 07 · Critical events

Scenario: clearing a jammed conveyor. Uses assessments (02).

`CriticalEventsSample` calls `EventCritical("hand_in_running_conveyor")` when the learner reaches into the
running conveyor. The SDK records it as `CRITICAL_ABXR_hand_in_running_conveyor`, which puts it in Insights'
Critical Choices chart. The assessment `conveyor_jam_clearing` then fails, even if the learner goes on to press
the emergency stop and clear the jam.

Use critical events for actions that could seriously hurt someone. Ordinary mistakes belong in scores and
statuses.

**Watch the Stream for:** the critical row, then the assessment complete with `Fail`.
**In the dashboard:** the Critical Choices chart.

## 08 · Timed events

Scenario: picking a three-line order in a warehouse.

`TimedEventsSample` calls `StartTimedEvent("order_pick")` when the order is accepted. That sends nothing; it
notes the time. At scan-out, `Event("order_pick", meta)` sends `items_picked` and `mispicks`, and the SDK adds
the `duration` since the start.

- Metadata values are strings with stable snake_case keys. Make a new dictionary for each call: the SDK adds its
  own fields to the one you pass.
- Don't time assessments, objectives, or interactions this way. They're timed already.
- Nothing needs closing on exit: an unfinished timed event was never sent.

**Watch the Stream for:** the event with its metadata, and the `duration` the SDK added from `StartTimedEvent`,
which makes no record of its own.
**In the dashboard:** custom events, with their metadata.

## 09 · Super metadata

Scenario: a print-and-apply labeller at the end of an outbound conveyor, run at two sites in two languages.

`SuperMetadataSample` registers context once, and the SDK adds it to every later event, log, and telemetry
entry:

- `Register("site", …)` and `Register("language_code", …)` when the sample opens and whenever the learner
  switches. `Register` replaces a value.
- `RegisterOnce("first_site_seen", …)` keeps the first value it's given, so switching sites doesn't change it.
- **Print label** calls `Event("label_printed")` with no metadata of its own: the SDK merges the context in.
- **Unregister site** calls `Unregister("site")`. The next carton is still labelled, but its event has no site,
  so reports can't split it by site. **Reset** clears all
  super metadata, including the `module` the SDK sets from the last assessment's name.
- Leaving the sample unregisters its keys, so later events elsewhere don't carry them.

`module`, `moduleName`, `moduleId`, and `moduleOrder` are reserved; `Register` refuses them.

**Watch the Stream for:** the event with the registered keys under *added by the SDK* in the open row. `Register` and the other
context calls make no record of their own.
**In the dashboard:** the keys appear as metadata on every record.

## 10 · User data

Scenario: an employee badge at a shared-headset cart.

`UserDataSample` identifies the learner with `SetUserData("E-1042", userData)`, where `userData` holds
`first_name` and `site_id`. Identity lives in userData, never in event metadata: every record in the session is
tied to the user.

- `SetUserData` syncs by signing in again in the background, so it needs a signed-in session. Signed out, the
  SDK ignores it.
- **Check user data** shows `GetUserId()` and `GetUserData()`: a copy of the user data from the last sign-in, or
  null when signed out.
- If the app also starts a new session for the learner, call `StartNewSession()` first and `SetUserData` once
  `OnAuthCompleted` reports success (sample 15). A new session clears user data.

**Watch the Stream for:** an `OnUserDataSyncCompleted` row once the SDK has signed in again (System filter).
`SetUserData` makes no record of its own.
**In the dashboard:** sessions and records attributed to the learner.

## 11 · Storage

Scenario: resuming a three-chapter forklift maintenance course.

`StorageSample` saves the learner's place with Insights storage and reads it back when the sample opens:

- It picks the scope before each read and save. `StorageScope.User` follows the learner to any headset, and
  needs a session that knows the user. `StorageScope.Device` stays with one headset or browser, and is all an app
  has when its sign-in identifies nobody: then `GetUserId()` is null, and the sample uses `Device`.
- `StorageGetEntry("course_progress", scope, callback)` runs as a coroutine. The callback gets null when storage
  can't be read (it needs a signed-in session).
- Each completed chapter calls `StorageSetEntry("course_progress", …, StoragePolicy.KeepLatest)`, which
  replaces the saved place, and `StorageSetEntry("attempt_history", …, StoragePolicy.AppendHistory)`, which
  keeps every entry.
- **Reset progress** calls `StorageRemoveEntry("course_progress", scope)`; the history stays.
- Saves go out with the next batch (**Send Next Batch Wait Seconds**, 30 by default), while reads and removes go
  out at once. A read before the batch is sent returns the previous value, and a save still waiting to be sent
  reaches Insights after a remove, restoring the entry. Wait for the save rows to show a green dot (sent) first.
- The console says where progress goes: to the learner, to this device, or nowhere while signed out.

**Watch the Stream for:** two `StorageSetEntry` rows per chapter. Reads and removals make no record.
**In the dashboard:** storage isn't an analytics record; it's app data, per learner or per device.

## 12 · Diagnostic logs

Scenario: a pick-and-place robot cell loading its job file from `StreamingAssets/RobotJobs`.

`DiagnosticLogsSample` logs for developers, not about learners:

- `LogWarn` when `job_b.json` leaves out the optional `speed_pct` and the robot runs at the default speed.
- `LogInfo("Job file loaded", …)` when a file loads and the robot starts its program. `job_a.json` is complete, so
  it logs only this.
- `LogError("Job file failed to load", …)` with the parser's message when `job_c.json`, which is genuinely
  malformed, fails to parse. The robot stays stopped.

Keep messages fixed and put the details in metadata, so logs group and search well. Learner mistakes and
performance are events, never logs.

**Watch the Stream for:** the Logs filter: info for `job_a.json`, warn and info for `job_b.json`, error for
`job_c.json`.
**In the dashboard:** logs by level and message.

## 13 · Telemetry

Scenario: welding along a seam.

`TelemetrySample` measures technique while the learner drags the torch along the seam:

- `Telemetry("torch_technique", data)` five times a second while welding, with `travel_angle_deg` and
  `distance_to_seam_mm`. Sample at a steady rate, and only while the measurement matters.
- A `TrackObject` component on the torch has the SDK record its position on its own, as
  `weld_torch Position`, once per **Telemetry Tracking Period** in the Configuration (10 seconds by default).
  It records only when the object has both moved and turned since its last record.

Telemetry values are strings: format numbers with the invariant culture.

**Watch the Stream for:** the Telemetry filter: one live `torch_technique` row that updates as you weld, and
`weld_torch Position` rows from TrackObject.
**In the dashboard:** telemetry over time, per session.

## 14 · Gaze targets

Scenario: greeting a customer at a shop counter.

`GazeTargetsSample` marks three things with `AbxrTarget`: the customer's face (`customer_face`), the product
shelf (`product_shelf`), and a phone on the counter (`phone`). Each target lives on its own child object, as
**Analytics for XR → Create Abxr Target** makes it. Targets made in code need their trigger collider sized
before they wake: the SDK sizes it to the object only in the Editor, and the 1 m default can hide other targets
behind it.

**Greet the customer** calls `Event("customer_greeted")` with no metadata. The SDK adds
`gaze_score_<target>` for each target: 1 when the view points straight at it, 0.5 at a right angle, and 0 when
it's directly behind the learner or something blocks it. A target near the edge of the view still scores high, so compare
targets rather than using a fixed cutoff. It also sends a `<target> Gaze` telemetry entry per target. On desktop the centre of the
view stands in for gaze: right-drag, or drag a finger on a touch screen, to look around.

The Configuration's **Target Gaze Tracking** settings set the maximum distance and whether targets get trigger
colliders.

**Watch the Stream for:** the event's gaze scores under *added by the SDK* in the open row, and a gaze row per target.
**In the dashboard:** gaze scores on events, and gaze telemetry.

## 15 · Session lifecycle

Scenario: a shared headset handed from one trainee to the next. Session behaviour applies to the whole app, so
the shell creates `SessionLifecycleSample` at launch.

- At launch it sets `Abxr.OnQuitAssessmentStatus = EventStatus.Incomplete`. When a session ends or the app quits
  with an assessment still open, the SDK completes it for you, and by default records `Fail`. Stopping early
  isn't failing.
- **Hand to next trainee** calls `EndSession()`, then `StartNewSession()`. `EndSession` completes anything still
  open with the `OnQuit` values (and `auto_closed=true`) and sends what's queued. `StartNewSession` on its own
  would discard anything not yet sent.
- **End session** calls `EndSession()` alone; the headset stays signed out until `StartNewSession` or
  `StartAuthentication`.
- It subscribes to `OnHeadsetPutOnNewSession`: on an XR headset, when the headset comes off and goes back on,
  the SDK asks whether it's the same person and signs the newcomer in if not. It only asks when something
  subscribes. The app closes what the last trainee left open.

The station's short drill (an assessment, as in sample 02) gives the SDK something to close.

**Watch the Stream for:** after a mid-drill hand-over, the drill's assessment complete made by the SDK, with
`auto_closed=true`, then a new sign-in. `EndSession` records what it closes (the SDK's `DEFAULT` assessment, if
no assessment is open) and, when it closed anything, one `LogInfo` with how many; `StartNewSession` makes no
record.
**In the dashboard:** one session per trainee.

## 16 · Module targets

Scenario: an LMS-assigned, three-part forklift pre-shift check (`preshift_tires`, `preshift_forks`,
`preshift_controls`). The LMS can send a module target as soon as sign-in finishes, so the shell creates
`ModuleTargetsSample` at launch.

- `StartModule(moduleId)` is the single entry point: the module buttons, `OnModuleTarget`, and the
  `?module=` deep link all go through it.
- Each module is its own assessment, named by the module ID. Never put `module`, `moduleName`, `moduleId`, or
  `moduleOrder` in metadata: the SDK sets them, and metadata you pass overrides them. In an LMS sequence all four
  come from the LMS's current module, so a module opened from its button out of the LMS's order carries the
  LMS's current one; otherwise `module` and `moduleName` come from the last assessment's name, and stay on every
  later record until another assessment starts or the session ends.
- `GetModuleList()` orders the modules when an LMS assigned a sequence; otherwise the app's own order is used.
- An unknown target calls `LogWarn` and shows the module list.
- In an LMS sequence, completing an assessment moves the SDK on: it calls `OnModuleTarget` with the next module
  before `EventAssessmentComplete` returns. The lesson clears its current module first, so that call can start
  the next one.
- That happens for *any* assessment the app completes during the sequence, not only the modules. Samples 02–07
  and 15 mark their assessment closed before completing it, and this lesson opens its sample a frame later, so
  the sample that finished isn't left mid-call.
- `OnAllModulesCompleted` shows a summary, then `EndSession()` and `StartNewSession()` (sample 15).

Try `?sample=16-module-targets&module=preshift_forks`, or `&module=preshift_brakes` for the unknown-target path.

**Watch the Stream for:** an assessment start and complete per module; `OnModuleTarget` rows when an LMS
assigns modules.
**In the dashboard:** assessment results per module.

## 17 · Exit poll

Scenario: feedback at the end of fire-extinguisher practice.

`ExitPollSample` asks three questions with `PollUser`, which shows the SDK's poll UI (the World-Space UI sample,
or your own registered with `AbxrUi.RegisterPollUi`):

- `PollType.Rating`: answers `"1"` to `"5"`.
- `PollType.Thumbs`: answers `"up"` or `"down"`.
- `PollType.MultipleChoice` with three responses: answers with the chosen response.

Polls queue and show one at a time, and the callback hands the answer to the app. The World-Space UI sample's poll
UI records each answer as a `poll` event with `prompt` and `answer`; a poll UI of your own records nothing unless it
calls `Abxr.Event` itself. An open poll outlives a scene load, so the callbacks check the station is still there. A
confidence rating of 3 or lower flags the learner for a refresher.

**Watch the Stream for:** a `poll` event per answer, made by the SDK's poll UI. `PollUser` makes no record of its
own.
**In the dashboard:** poll answers by prompt.

## 18 · Passcode pairing

Scenario: a front-desk device that isn't enrolled in ArborXR device management, being set up. This sample and
the next are only for devices without ArborXR MDM: a managed device, or an app with an org token, already has
its organization (`PairingState.Managed`) and never pairs.

On WebGL and Android, a device with no organization identity can pair with a passcode from the Portal
(**Insights Hub → Pairing Passcodes**). `PairingSample` subscribes to `Abxr.OnPairingStateChanged` in `Awake`.
The SDK never opens the pairing prompt on its own: the sample checks `GetPairingState()` after subscribing
and calls `StartPairing()` when the device is unpaired.

Pairing applies to the whole app, so the shell creates this lesson at launch. The sample's scene shows the
live state, with buttons to pair, clear the pairing (`ClearPairing`), and sign in again as in sample 01.
Anywhere else, the header's **Not paired** pill opens this sample.

On a managed device, or a build that sends with an org token, **Pair this device** and **Clear pairing** are
off, and **Run sample: fail** has nothing to do, so the station says so. A WebGL build with no App Token of its
own switches to pairing from the header's **App** button: choose **Pair with a passcode** (see the README's
section on a build with no App Token).

`ClearPairing` ends a signed-in session the way `EndSession` does, and neither raises `OnAuthCompleted`, so
the lesson tells sample 01's sign-in state through the shell. A pairing revoked in the Portal is found the
next time the device signs in: `OnPairingStateChanged` reports `Unpaired` (`Revoked`), then that sign-in
reports `OnAuthCompleted(false, "Pairing revoked")`.

A wrong passcode settles nothing, so it raises no `OnPairingStateChanged` and has no row: the prompt shows
the error and asks again. `GetLastPairingRedeemResult()` says what happened (`InvalidPasscode` covers wrong,
expired, and revoked, on purpose). The fail run clears the pairing, enters a wrong passcode, and leaves the
prompt open, so you can pair again straight away.

**Watch the Stream for:** `OnPairingStateChanged` and `OnAuthCompleted` rows (System filter).

## 19 · Custom sign-in UI

Scenario: Acme's own branded sign-in panel in place of the SDK's prompt. It needs a build with an App Token
(see the README's build section): without one, the SDK never asks for input, and the panel says why. The
pairing requests only come to a device without ArborXR MDM; the sign-in requests (a PIN, an email, or a
login) come to any device whose app asks for one.

`CustomSignInUISample` assigns one handler: `Abxr.OnInputRequested = HandleInputRequested` (assignment, not
`+=`), and sets it back to `null` in `OnDestroy`. While it's set, the SDK never shows its own prompt. In your
app, assign it at launch, before sign-in starts one frame later; this sample assigns it when it opens, because
the other samples use the SDK's prompt.

The handler gets `(type, prompt, domain, error)` and shows the matching input:

- `text`, `assessmentPin`, `email`: the app's sign-in. For `email`, ask for the username only; the SDK adds
  `@` and `domain` when the answer has none.
- `pairingPasscode`: the six-digit passcode from the Portal. **Not now** calls `CancelPairing()`.
- `pairingDeviceName` (with a skip, answered with `""`) or `pairingDeviceNameRequired` (no skip): a name for
  the headset, when the passcode allows one.
- `pairingDeviceJoin`: a paired device in the organization already has the name, and `domain` carries it.
  Answering with that name adds this app to that device, which may be a different headset; `"**skip**"` asks for
  another name.

After a wrong answer the same type comes back with `error` set, and the panel shows it. Answers go back with
`Abxr.OnInputSubmitted(value)`. **Scan badge** appears only when `IsQRScanForAuthAvailable()` is true (Quest
and Pico, during sign-in), and `StartQRScanForAuthInput` hands the scanned value to the same answer path.

To pair from your own UI with no prompt at all, call `RedeemPairingPasscode` instead; its result says when to
ask for a device name.

Leaving with a prompt open closes it (`CancelPairing`, or a fresh sign-in for an open sign-in prompt), so
nothing waits on a panel that's gone. **Run sample: fail** mistypes the passcode and sees the error; **pass**
needs a real passcode or PIN, so it waits for you to enter one.

**Watch the Stream for:** `OnPairingStateChanged` and `OnAuthCompleted` rows (System filter). Input requests
and answers aren't records; the panel shows them. **Sign in again** ends the session first, as in sample 15, so
it also records what `EndSession` closes, such as the SDK's `DEFAULT` assessment (`Incomplete`).
**In the dashboard:** nothing directly; sign-in starts the session the other samples' records belong to.
