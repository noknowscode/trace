# Completeness audit: TRACE (2026-11-04, Uno.Sdk 7.0.0-dev.115, heads: desktop, android, ios, windows, wasm)

Audited at `1909a8b` on `feat/production-readiness`. Report mode: no code was changed.
Blocker fix pass (2026-11-04): the one Blocker is fixed in `b9a3a9c`; see Fixed below.
A pass (2026-11-04, from `f2c8417`): the five functional Majors are built or cut and re-proved at runtime on
the desktop head, and the critical workflow "inspect and complete a point" was run in Chromium on the Release
WebAssembly build. See Fixed below.
A pass 2 (2026-11-04, from `d1e350a`, the code review's closed list): the review's defects and Minors are fixed and
re-proved on the desktop head, and the browser check now presses the Notes field where an earlier build once closed
the inspector. See "Fixed in A pass 2" below.

Verdict: **Complete with gaps** (0 Blockers, 0 functional Majors, 1 head Major, 4 Minors)

Safe to show: open any asset from Today, Assets, Scan or Findings, as many in a session as you like; inspect,
fail and complete a point with a note and a photo, in portrait or landscape; review the photos recorded at a
point and in History; raise one corrective action per failure; dispatch and close it from Findings; go offline,
queue, sync, fail, retry; the empty and error states; the same inspection in a browser.
Keep off screen: nothing known.

## Inventory

Screens: 16 intended, 15 present, 0 unreachable, 1 not built (source: PRODUCT-SPEC and UNO-BUILD-SPEC).
- The 16 are 6 destinations, 4 asset tabs and 6 transient layers.
- The evidence viewer is built (A pass). Not built: the sync conflict dialog, cut while there is no server (D-004,
  proposed).

Routes: 7 registered (`Main` and 6 nested), 15 triggers, 0 dead references.
- The triggers are 2 × 5 shell tabs, 3 `Navigation.Request="../Asset"`, Scan's `NavigateRouteAsync` and the asset's Back.
- 0 are broken. (`Asset` lives in the Visibility region and was not rebuilt for new data; fixed in `b9a3a9c`.)

Interactions: about 66 found (37 commands, 3 navigation requests, 5 text boxes, 2 combo boxes, 24 tab items, Reset view, drag to orbit, feed Retry, and the A pass's Dispatch/Close and photo thumbnails).
- 0 are dead by construction.
- 3 are simulated by design: Scan a tag, Simulate denial, Open settings.

Critical workflows (derived from the spec's primary workflows):
1. Open an assigned asset from Today
2. Inspect a point: reading, disposition, evidence, complete
3. Fail, then raise a corrective action that shows in Findings
4. Work offline, queue, sync, fail, retry
5. Find an asset by search or tag code and open it
6. Review asset history: measurements and photos
7. Supervisor reviews findings and dispatches work

## Completeness by area

| # | Area | Status | Evidence | Blockers | Majors | Minors | Note |
|---|---|---|---|---|---|---|---|
| 1 | Screens | Complete | R | 0 | 0 | 0 | Recorded photos show in History, on the point and in a viewer. Documents shows the nameplate and the inspection plan. |
| 2 | Navigation | Complete | R | 0 | 0 | 0 | Every asset opens its own workspace from Today, Assets, Scan and Findings, and Back returns to where it was opened (fixed `b9a3a9c`). |
| 3 | Interactions | Complete | R | 0 | 0 | 0 | One corrective action per failure, still offered after the point is closed and reopened; Dispatch and Close in Findings; one photo at a time comes off a draft. |
| 4 | Logic | Complete | R | 0 | 0 | 0 | Fail rule, range, idempotent completion and action raising, action inheritance and the status steps are proven. |
| 5 | Data | Complete | R | 0 | 0 | 0 | Writes, evidence file, relaunch, journal and old action statuses (tests) are proven. Corrective work syncs only after its inspection; a failed upload reads Failed in History. |
| 6 | States | Complete | R | 0 | 0 | 0 | Error and Retry, empty, asset unavailable, offline, pending, sync failed, camera denied, validation, draft not saved, records unreadable on Scan and success are proven. Loading is C. |
| 7 | Forms | Complete | R | 0 | 0 | 0 | Numeric reading, Fail needs note or photo, summary required, priority and assignee. |
| 8 | Authentication | N/A | C | 0 | 0 | 0 | No accounts in the spec; the technician name comes from config. |
| 9 | Responsive/adaptive UI | Complete | R | 0 | 0 | 1 | Phone landscape scrolls the inspector as one (fixed). The machine is small in landscape. |
| 10 | Platform behavior | Simulated (by design) | C | 0 | 0 | 0 | Camera, QR/NFC, network and sync transport sit behind interfaces. |
| 11 | Failure handling | Complete | R | 0 | 0 | 0 | Every store write and read a command makes is caught and said on screen; a failed theme switch too. The launch is guarded and `CrashReporter` logs what nothing else caught to `crash.log`. |
| 12 | Accessibility | Complete | R | 0 | 0 | 1 | 21.0 names per 1,000 XAML lines; keyboard walk reaches every control; modal layers take focus. Three Toolkit parts are empty tab stops. |
| 13 | Performance | Partial | R | 0 | 0 | 0 | Idle CPU 0–0.3 % on the 3D view. Debug start 3.1 s. Release start X. |
| 14 | Security/privacy | Complete | C | 0 | 0 | 0 | No secrets. Debug hooks and the fault store are inside `#if DEBUG`. |
| 15 | Visual quality | Partial | R | 0 | 0 | 1 | Markers overlap at 390 px. (Raw type names on Sync and "Priya S.." fixed.) |
| 16 | Testing | Partial | R | 0 | 0 | 1 | 176 passed, 1 skipped, including every page model (Today, Assets and the shell added). No UI tests; the Debug hooks and the browser script drive the workflows. |

## Critical workflows by head

| Workflow | desktop | android | ios | windows | wasm | Status |
|---|---|---|---|---|---|---|
| 1. Open assigned asset | R | X | X | X | R | Complete |
| 2. Inspect and complete a point | R | X | X | X | R | Complete |
| 3. Fail to corrective action to Findings | R | X | X | X | X | Complete |
| 4. Offline queue, sync, retry | R | X | X | X | X | Complete (simulated transport, by design) |
| 5. Search or tag code, open asset | R | X | X | X | X | Complete |
| 6. History: measurements and photos | R | X | X | X | R | Complete (comparison cut, D-002) |
| 7. Supervisor review and dispatch | R | X | X | X | X | Complete |

How each desktop result was proven:
1. At the audit: AHU-12 opened first and showed AHU-12. Back, tap C-07: AHU-12 showed. Back, tap P-204: AHU-12 showed. Reproduced in two sessions. After the fix: each asset shows itself; see Fixed.
2. Seal housing was read at 7.6 with Fail.
   - Complete stayed disabled with "needs a note or a photo" until a photo was added.
   - `events.json` gained the event with `ms-appdata` evidence; the PNG is on disk.
   - `sync-queue.json` gained the operation; the chip, History and Sync badge updated.
   - After a relaunch, 70 °C and "3 open findings" were still there.
   - The `Again` hook: seal events went from 3 to 5.
3. Created as Critical and assigned to Priya S., with the source event id. Then re-offered: a second Create wrote a second action from the same event.
4. Offline: Sync now is disabled, and a Pass completion added a Pending operation.
   - Online sync: 3 synced, 1 failed, with the photo error.
   - Retry and sync: all Synced, and every event's `syncState` reads Synced.
5. Search worked: "line 3" leaves C-07, "zzq" shows the empty state, and code X-1 shows Asset unavailable. At the audit, opening C-07 from search or by code showed the session's first asset; after the fix, code C-07 on Scan opens C-07 after three other assets (see Fixed).
6. A new event shows with its reading, note, Fail and Pending, but no photo or evidence marker.
7. Corrective work and at-risk assets are listed. Tapping a work row changes nothing, and no control changes assignee or status.

A pass, desktop (Debug, Xvfb :465, software renderer, real pointer and keyboard):
3. Created by pointer: the button is gone after Create and `actions.json` holds one action for the event; a second request in the workflow resolves to it (test).
6. History lists the seeded seal failure with its photo; the point shows "Photos recorded 1 d ago"; either thumbnail opens the viewer, and Close closes it.
7. Findings at 1280 and 390 px: Dispatch on the new action and Close on the seeded one, by pointer. `actions.json` reads Dispatched and Closed, `sync-queue.json` gained two `update` operations, and Sync lists them as Action status.

WebAssembly (Release `net10.0-browserwasm`, headless Chromium 1280 x 800, `tools/wasm-check.js`): Today, P-204,
Seal housing, 7.6 mm/s with a note, Fail, Complete point: the confirmation, the Sync badge (1) and the new History
entry (Fail, Pending) showed. `docs/evidence/wasm-complete-point.jpg`. Android, iOS and WinAppSDK stay X: the
container cannot run them (CI builds them).

A pass 2, desktop (Debug, Xvfb :614, software renderer, Dark, 1280 x 800 and 390 x 844, real pointer):
- Today and Assets cards read their condition, progress, due time and findings through the presentation layer;
  Assets search ("compressor") leaves C-07.
- Scan a tag opens P-204, code C-07 opens C-07, code X-99 shows Asset unavailable.
- The point's recorded photo opens the viewer from the inspector. Two photos added, one removed: the other stays, and
  the draft resumes with it after a relaunch.
- Seal housing failed with a photo and completed; in a new session the reopened point still offers Create corrective
  action, and Create raises it ("…assigned to Mechanical Team.").
- Sync now: the inspection reads Failed with its error and the action stays Pending ("1 corrective action change
  waits until the inspection it came from has synced"); History shows Failed on the event. Retry: both Pending, History
  Pending. Sync now: both Synced, History Synced.
- Findings (Dispatch, Close), Overview, Documents and History tabs; the theme switch to Light and back (`VERIFY
  theme=Light actual=Light`, `theme=Dark actual=Dark`).
- `--trace-crash=launch`: no window, the exception in `crash.log` and on the console. `--trace-crash=task`:
  `VERIFY crash=task reported=True`. `--trace-route=Switch`: `result=pass opened=4/4`, no `crash.log` written.

A pass 2, WebAssembly (`tools/wasm-check.js`, Release, Chromium 1280 x 800): reading 7.6, Fail, then the Notes field
pressed while it is mostly scrolled out: it scrolls into view and takes the note (`docs/evidence/wasm-notes-press.jpg`);
Complete point, the confirmation and Create corrective action (`wasm-complete-point.jpg`); History shows the new entry.

## Findings

### Blockers
None open. The one found at the audit is under Fixed.

### Fixed
- [Navigation] Any asset opened after the first in a session showed the first asset's workspace, from every entry point: Today, Assets, Scan and Findings. Fixed in `b9a3a9c`; regression hook in `1dbaf73`.
  - Where: `MainPage.xaml:122`. `Asset` is a `Region.Name` child of the `Visibility` navigator, which shows the existing child again. The `DataViewMap<AssetPage, AssetModel, AssetSummary>` data did not rebuild the model.
  - Evidence at the audit: R. AHU-12 first; then C-07 and P-204 cards and the C-07 code lookup all showed AHU-12. The page kept the earlier tab and confirmation. A reading taken "on C-07" would be recorded against the first asset.
  - Cause: Uno.Extensions 8.0.0-dev.150 `ControlNavigator.InitializeCurrentView` builds a view model only when the shown view's `DataContext` is null or not of the mapped type. The inline Asset child kept the first `AssetViewModel`, so later requests reused it and their `AssetSummary` was dropped.
  - Fix: `MainPage` takes the asset's model off the region when the asset is hidden, so the next request builds one from the asset it carries. The closed model is disposed when the next asset is shown. The region, routes, `NavigationOrigin` and Back are unchanged.
  - Proof: R, Debug desktop, Xvfb, real pointer, one session from a reset store.
    - AHU-12 from Today showed AHU-12. A draft of 9.8 was entered at Drive belt, then Back went to Today.
    - Assets, C-07 card: C-07 showed. Receiver auto-drain was recorded at 5 per hour, Pass. `events.json` gained `ev-94276905…` with `assetId` C-07 and `pointId` C-07-drain. `sync-queue.json` gained its operation. Back went to Assets.
    - Today, P-204 card: P-204 showed, and Back went to Today.
    - Scan, code C-07, Open asset: C-07 showed, with the new 5 per hour Pass. Back went to Scan.
    - Findings, AHU-12 row: AHU-12 showed, and Drive belt reopened with the 9.8 draft. Back went to Findings.
    - After a relaunch, C-07's History still lists 5 per hour, Pass, Pending. The app log has no errors.
  - Regression: `--trace-route=Switch` printed `VERIFY switch result=pass opened=4/4 recordedOn=C-07` at 1280×860 and at 390×844. With the fix stashed it printed `opened=1/4` and `result=fail`, with AHU-12 shown for all four.
  - Still pass: `Action` (sheet opens after a Fail), `Again` (seal events 2 to 4), and `Asset --trace-from=Findings --trace-back=1` (back to Findings).
  - No unit test: the bug lives in the navigator's view-model reuse on a XAML region child, which the `net10.0` test project cannot host. The `Switch` hook covers it instead.
  - Owner: uno-navigation.

### Fixed in the A pass (2026-11-04)
The five functional Majors, each built or cut under the owner rule "build small, cut large". Cuts and calls are
recorded as proposed in `docs/DECISIONS.md`.
- [Interactions] **Duplicate corrective action: fixed** in `e8e840b`. The workflow raises one action per source
  inspection (a repeat resolves to it and writes nothing, checked again inside the store lock), and the inspector
  stops offering Create once the sheet has raised it. Tests: `Raising_work_twice_from_one_inspection_keeps_one_action`,
  `Once_work_is_raised_from_an_inspection_it_is_not_offered_again`. R: after Create the button is gone and
  `actions.json` holds one action for the event. The "Priya S.." confirmation is fixed with it.
- [Screens] **Evidence review: built; comparison cut** in `e000f6c` (D-002). Photos show on their History entry,
  as "Photos recorded" on the point, and full size in a viewer. No copy offers comparison. The seed's seal failure
  carries a bundled sample photo. Tests: `RecordedEvidenceTests`, `A_recorded_photo_opens_full_size_and_closes`. R.
- [Interactions] **Supervisor dispatch: built** in `870b897` (D-001). Open → Dispatched → Closed from each Findings
  row, persisted with an `update` sync operation; a second tap takes one step. Earlier builds' statuses still load.
  Tests: `ActionDispatchTests`, `SupervisorFindingsModelTests`. R at 1280 and 390 px.
- [Screens] **Documents: made honest** in `11964e3` (D-003). The fixed "Manuals and drawings sync" tile is gone; the
  tab shows the nameplate and the inspection plan the device holds. R in Dark at 1280 px and Light at 390 px.
- [Responsive] **Phone landscape: fixed** in `7c6690e`. When its pinned parts do not fit (about 200 px at 800 x 360
  and 844 x 390), the inspector scrolls as one; 360 x 640 stays pinned. R: at both landscape sizes the validation
  message and Complete point were reached by keyboard (Fail, then End). The mouse wheel scrolls no ScrollViewer
  under this Xvfb setup, Findings and History included, so it could not be used as the check.

Also fixed in the pass:
- [Failure handling] Opening a point, the draft autosave, Discard, and Scan's Scan a tag and Open asset report
  store failures on screen (`cab8cd2`, with tests).
- [Logic] Assets needing attention are listed failed first (`870b897`, test).
- [Visual] Sync rows name the change (Inspection, Corrective action, Action status) instead of raw type names
  (`870b897`).
- [Accessibility] 65 automation names in 3,047 XAML lines (21.3 per 1,000, from 16.8); chips and headers read as
  their text; the action sheet and the photo viewer take focus and keep Tab inside (`532b805`). Keyboard walk with
  `--trace-focus=log` on every destination and asset tab at 1280 px and on Inspect at 390 px reaches every control.

### Fixed in A pass 2 (2026-11-04)
The code review's closed list (`d1e350a`). Each with tests where a unit test reaches it.
- [Data] **A failed upload reads Failed, and corrective work waits for its inspection** (`6134fdf`). A sync pass
  writes each inspection's own sync state (Failed while its upload has not gone, Pending again on Retry, Synced once
  sent) and sends corrective work only after the inspection it came from; an action's later change waits for the
  action. `SyncQueueTests`. R (see A pass 2, desktop).
- [Failure handling] **The launch is guarded and nothing else is lost** (`42023cf`, `1ee5a2a`). `OnLaunched` reports
  and rethrows a launch failure; `CrashReporter` attaches to `Application.UnhandledException`,
  `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` and writes to the log and `crash.log`;
  the closed workspace's `DisposeAsync` is awaited in a guarded helper. `CrashReporterTests`. R.
- [Failure handling] **A theme switch that fails says so** (`31ed6e3`): the label and theme stay, and the round
  shows a message. `TodayModelTests`.
- [Interactions] **A failure closed before Create still offers its action** (`f4604b2`): opening a point offers
  Create for its latest failed inspection with no action. `PointInspectorModelTests`. R across sessions.
- [Interactions] **One photo comes off a draft** (`ba3dc29`, `922d492`): Remove on each attached photo; the rest
  of the draft stays. `PointInspectorModelTests`. R.
- [WebAssembly] **Pressing Notes while it is scrolled out no longer closes the inspector, as far as can be shown.**
  It did once in the A pass (Chromium, `s4`→`s5` of that run). In this pass it did not reproduce in 17 presses over
  15 browser sessions, 8 on this head and 9 on a rebuild of `d1e350a` (at the field's sliver, below it, on its label,
  with and without Fail first, and as a short drag). No fix was made, because no cause could be shown;
  `tools/wasm-check.js` now presses the field there on every run (`21a72ea`), so a return would show.
- Structure and quality items from the review, with no behaviour change: the container builds the workspace's parts
  (`AssetWorkspaceParts`; `AssetModel` 12 → 9 dependencies, forwards none), the inspector's photos and camera are
  their own part (`PointEvidenceModel`; `PointInspectorModel` 446 → 376 lines, 4 inputs) and open photos through it,
  each asset tab is its own control (`AssetPage.xaml` 649 → 166 lines), screen text left the domain (`ScreenText`),
  dead members and template files went (`8acd211`), no `!` silences a nullable value (`8c3041e`, `4131c76`), the
  latest photos are looked up once, and Today, Assets (search) and the shell have model tests (`a7198a6`).

### Majors (functional)
None open.

### Majors (head)
- Android (Tier 1), Windows WinAppSDK (Tier 1) and iOS (Tier 2): no critical workflow was run (X). CI builds them.
  WebAssembly (Tier 2) ran workflows 1, 2 and 6 in Chromium in the A pass.

### Minors
- [Visual] At 390 px the Inspect view's markers overlap each other and the asset title. R.
- [Responsive] In phone landscape the docked machine is about 135 px tall under its nameplate and Reset view plates. R.
- [Accessibility] The root ContentControl, the Toolkit's `TabBarSelectionIndicatorPresenter` and `SafeAreaPresenter` are empty tab stops (their styles are set inside the Toolkit's templates, so an implicit style does not reach them). R.
- [Testing] There are no UI tests for the workflows (UNO-BUILD-SPEC asks for them). The page models are tested, and the Debug hooks and `tools/wasm-check.js` drive the workflows at runtime. C.

### Pending owner question (unchanged)
- The set-aside warning on Sync shows only once. The behaviour is left as it is.

## Not checked (X)
- Android, iOS and WinAppSDK runs: the container cannot run them. Run workflows 1–7 on a device and the Windows head.
- Release cold start: only Debug was timed (3.1 s to the round). Time `-c Release` to the round.
- A screen reader: names and focus order were checked with `--trace-focus=log`, not with Narrator or TalkBack.
- The Loading state on screen: the store loads too fast to capture. A slow-store fault mode would show it.

## By-design simulation (not findings)
- Camera capture, QR/NFC scan, network state and sync transport are deterministic implementations in `Infrastructure/Platform` and `SyncQueue`, by design (CLAUDE.md, UNO-BUILD-SPEC: "deterministic simulated implementations").
- The Scan page says "Camera preview is simulated in this build".
- Camera denial is reachable through "Simulate denial", and recovery flips the simulated permission.

## Noted for other skills
- uno-audit: `AssetModel` is split (A pass): the workspace (154 lines before the viewer), `PointInspectorModel` and `CorrectiveActionSheetModel`.
- gold-standard-pass: the narrow Inspect composition (machine, markers and title fight for 220 px).

## Unresolved Questions
- Authentication is N/A: the spec has no accounts, and the technician comes from `AppConfig`. Confirm that a single technician per device is intended.
- The sync conflict dialog (spec "Offline conflict dialog", `SyncConflictDialog`) is cut while the transport is simulated, because nothing can conflict (D-004, proposed). Confirm.
- Dispatch is built as one status step at a time (D-001, proposed); evidence comparison and document sync are cut (D-002, D-003, proposed). Confirm.
- Every X on a supported head: Android and Windows (Tier 1) and iOS.
