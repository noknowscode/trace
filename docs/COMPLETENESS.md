# Completeness audit: TRACE (2026-11-04, Uno.Sdk 7.0.0-dev.115, heads: desktop, android, ios, windows, wasm)

Audited at `1909a8b` on `feat/production-readiness`. Report mode: no code was changed.

Verdict: **Incomplete**

Safe to show: in a fresh launch, open one asset; inspect, fail and complete a point with a note and a photo; raise one corrective action; go offline, queue, sync, fail, retry; the empty and error states.
Keep off screen:
- opening a second asset in the same session (it shows the first one);
- pressing Create corrective action a second time;
- looking for a recorded photo in History;
- dispatching from Findings;
- phone landscape.

## Inventory

Screens: 16 intended, 14 present, 0 unreachable, 2 not built (source: PRODUCT-SPEC and UNO-BUILD-SPEC).
- The 16 are 6 destinations, 4 asset tabs and 6 transient layers.
- Not built: the evidence viewer (Missing) and the sync conflict dialog (N/A while there is no server; see Unresolved Questions).

Routes: 7 registered (`Main` and 6 nested), 15 triggers, 0 dead references.
- The triggers are 2 × 5 shell tabs, 3 `Navigation.Request="../Asset"`, Scan's `NavigateRouteAsync` and the asset's Back.
- 1 is broken by construction: `Asset` lives in the Visibility region and is not rebuilt for new data.

Interactions: about 60 found (33 commands, 3 navigation requests, 5 text boxes, 2 combo boxes, 24 tab items, Reset view, drag to orbit, feed Retry).
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
| 1 | Screens | Missing | R | 0 | 2 | 0 | No evidence review or viewer. The Documents list is a fixed string. |
| 2 | Navigation | Broken | R | 1 | 0 | 0 | Every asset opened after the first shows the first. |
| 3 | Interactions | Broken | R | 0 | 2 | 1 | A corrective action can be raised twice. No dispatch controls. |
| 4 | Logic | Complete | R | 0 | 0 | 1 | Fail rule, range, idempotent completion and action inheritance are proven. At-risk list is not worst first. |
| 5 | Data | Complete | R | 0 | 0 | 1 | Writes, evidence file, relaunch and journal (tests) are proven. An action can sync before its source event. |
| 6 | States | Complete | R | 0 | 0 | 0 | Error and Retry, empty, asset unavailable, offline, pending, sync failed, camera denied, validation and success are all proven. Loading is C. |
| 7 | Forms | Complete | R | 0 | 0 | 0 | Numeric reading, Fail needs note or photo, summary required, priority and assignee. |
| 8 | Authentication | N/A | C | 0 | 0 | 0 | No accounts in the spec; the technician name comes from config. |
| 9 | Responsive/adaptive UI | Broken | R | 0 | 1 | 0 | Phone landscape clips Complete point. |
| 10 | Platform behavior | Simulated (by design) | C | 0 | 0 | 0 | Camera, QR/NFC, network and sync transport sit behind interfaces. |
| 11 | Failure handling | Partial | C | 0 | 0 | 2 | Photo save and action create have no IO catch. No global handler. |
| 12 | Accessibility | Complete (unproven) | C | 0 | 0 | 0 | Names on every control read. Focus walk not re-run. |
| 13 | Performance | Partial | R | 0 | 0 | 0 | Idle CPU 0–0.3 % on the 3D view. Debug start 3.1 s. Release start X. |
| 14 | Security/privacy | Complete | C | 0 | 0 | 0 | No secrets. Debug hooks and the fault store are inside `#if DEBUG`. |
| 15 | Visual quality | Partial | R | 0 | 0 | 3 | Raw type names on Sync, "Priya S..", markers overlap at 390 px. |
| 16 | Testing | Partial | R | 0 | 0 | 1 | 87 passed, 1 skipped. No UI or workflow tests. Four heads X. |

## Critical workflows by head

| Workflow | desktop | android | ios | windows | wasm | Status |
|---|---|---|---|---|---|---|
| 1. Open assigned asset | R | X | X | X | X | Broken |
| 2. Inspect and complete a point | R | X | X | X | X | Complete |
| 3. Fail to corrective action to Findings | R | X | X | X | X | Partial |
| 4. Offline queue, sync, retry | R | X | X | X | X | Complete (simulated transport, by design) |
| 5. Search or tag code, open asset | R | X | X | X | X | Broken |
| 6. History: measurements and photos | R | X | X | X | X | Partial |
| 7. Supervisor review and dispatch | R | X | X | X | X | Partial |

How each desktop result was proven:
1. AHU-12 opened first and showed AHU-12. Back, tap C-07: AHU-12 showed. Back, tap P-204: AHU-12 showed. Reproduced in two sessions.
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
5. Search worked: "line 3" leaves C-07, "zzq" shows the empty state, and code X-1 shows Asset unavailable. Opening C-07 from search or by code showed the session's first asset.
6. A new event shows with its reading, note, Fail and Pending, but no photo or evidence marker.
7. Corrective work and at-risk assets are listed. Tapping a work row changes nothing, and no control changes assignee or status.

The other heads are X: the container cannot run Android, iOS or WinAppSDK (CI builds them), and WebAssembly was not run in this pass.

## Findings

### Blockers
- [Navigation] Any asset opened after the first in a session shows the first asset's workspace, from every entry point: Today, Assets, Scan and Findings.
  - Where: `MainPage.xaml:122`. `Asset` is a `Region.Name` child of the `Visibility` navigator, which shows the existing child again. The `DataViewMap<AssetPage, AssetModel, AssetSummary>` data does not rebuild the model.
  - Evidence: R. AHU-12 first; then C-07 and P-204 cards and the C-07 code lookup all showed AHU-12. The page kept the earlier tab and confirmation.
  - Effect: a reading taken "on C-07" is recorded against the first asset. COMPLETION-CHECKLIST ticks "Asset cards open the correct asset"; its hooks only ever open P-204 once per launch.
  - Owner: uno-navigation.

### Majors (functional)
- [Interactions] Create corrective action is offered again after success, and a second Create writes a duplicate action and sync operation.
  - Where: `AssetModel.cs:466`, where `RecordedEvent` is kept after success, and `InspectionWorkflow.RaiseCorrectiveActionAsync`, which does not check for an existing action from that event.
  - Evidence: R. `actions.json` held two actions with source `ev-a65ec14d…`, and Findings showed both.
  - Owner: mvux.
- [Screens] Evidence review is missing. A photo shows only in the draft; once the point is completed, it is never shown again.
  - Where: History has no evidence (`AssetPage.xaml:381-451`), and there is no evidence viewer.
  - Spec: Must Have "Evidence capture/review", the "Evidence viewer" layer, and the workflow "compare previous measurements and photos".
  - Evidence: R.
  - Owner: mvux.
- [Interactions] Supervisor dispatch is missing.
  - Where: Findings rows have no command (`SupervisorFindingsPage.xaml:56-97`), and nothing moves `ActionStatus` past creation.
  - The empty-state copy says actions "appear here for dispatch".
  - Spec workflow: "Supervisor reviews failed/watch findings → dispatches work".
  - Evidence: R.
  - Owner: mvux.
- [Screens] The Documents tab is Simulated.
  - The nameplate is real data. The documents section is one fixed string for every asset, "Manuals and drawings sync with the asset record", but the app has no document model or sync (`AssetPage.xaml:496-503`).
  - Evidence: C and R.
  - Owner: mvux.
- [Responsive] In phone landscape (800×360, 844×390), the docked inspector's pinned footer clips below Pass/Watch/Fail. Validation and Complete point are off screen, and the wheel does not reach them (`PointInspector.xaml:212`, `Auto` row).
  - Evidence: R.
  - Owner: uno-toolkit.

### Majors (head)
- Android (Tier 1), Windows WinAppSDK (Tier 1), iOS and WebAssembly (Tier 2): every critical workflow is X. CI builds them; none was run.

### Minors
- [Interactions] A single photo cannot be removed from a draft; only Discard all. `PointInspector.xaml:170`. C.
- [Logic] "Assets needing attention" lists Watch before Fail; the model says "worst first". `SupervisorFindingsModel.cs:29`. R.
- [Data] A corrective action syncs while its source event's upload has failed (queue: action Synced, event Failed). This would break referential order against a real server. R.
- [Failure handling] `AttachPhoto` and `CreateAction` have no IO catch, unlike `CompletePoint`, so a failed write says nothing. `AssetModel.cs:365`, `:450`. C.
- [Failure handling] There is no `UnhandledException` / `UnobservedTaskException` handler. C.
- [Visual] Sync rows show the raw type names `CorrectiveAction` and `InspectionEvent`. R.
- [Visual] The confirmation reads "assigned to Priya S.." with a doubled period. R.
- [Visual] At 390 px the Inspect view's markers overlap each other and the asset title. R.
- [Testing] There are no UI or integration tests for the workflows (UNO-BUILD-SPEC asks for them). The verification hooks open only P-204, once, so the Blocker went unseen. C.

## Not checked (X)
- Android, iOS, WinAppSDK and WebAssembly runs: the container cannot run them, and WASM was skipped for time. Run workflows 1–7 on a device, the Windows head and a browser.
- Release cold start: only Debug was timed (3.1 s to the round). Time `-c Release` to the round.
- A keyboard focus walk and a screen reader: not re-run in this pass. Run `--trace-focus=log` and Narrator or TalkBack.
- The Loading state on screen: the store loads too fast to capture. A slow-store fault mode would show it.

## By-design simulation (not findings)
- Camera capture, QR/NFC scan, network state and sync transport are deterministic implementations in `Infrastructure/Platform` and `SyncQueue`, by design (CLAUDE.md, UNO-BUILD-SPEC: "deterministic simulated implementations").
- The Scan page says "Camera preview is simulated in this build".
- Camera denial is reachable through "Simulate denial", and recovery flips the simulated permission.

## Noted for other skills
- uno-audit: `AssetModel` (12 dependencies, ~500 lines) carries tabs, editor, camera, actions and navigation. `Points` and `WatchPointHistory` are unused.
- gold-standard-pass: the narrow Inspect composition (machine, markers and title fight for 220 px).

## Unresolved Questions
- Authentication is N/A: the spec has no accounts, and the technician comes from `AppConfig`. Confirm that a single technician per device is intended.
- The sync conflict dialog (spec "Offline conflict dialog", `SyncConflictDialog`) is N/A while the transport is simulated, because nothing can conflict. Confirm, or build it with a fake conflict mode.
- Dispatch in Findings: the spec names it as a primary workflow but not in the Must Have list. It is graded a functional Major; confirm it is in scope.
- The workflows above are derived from PRODUCT-SPEC's primary workflows. Workflow 7 counts as Partial because review works and dispatch does not.
- Phone landscape: the spec makes portrait the default task surface, and the checklist claims rotation keeps work on screen. Is landscape inspection required?
- Every X on a supported head: Android and Windows (Tier 1), iOS, and WebAssembly (Tier 2).
