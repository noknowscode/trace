# HANDOFF — TRACE (2026-11-04, A pass)

Branch `feat/production-readiness` (PR #1), from `f2c8417` to the head of this pass. Pushed as it went.

## What this pass did

| Commit | What |
|---|---|
| `0bb3835` | refactor: `AssetModel` split into the workspace, `PointInspectorModel` and `CorrectiveActionSheetModel` (+ `Controls/CorrectiveActionSheet`) |
| `cab8cd2` | fix: store failures reported when a point opens, a draft saves or discards, and on Scan |
| `e8e840b` | fix: one corrective action per failed inspection (workflow idempotent; not re-offered) |
| `870b897` | feat: Dispatch and Close in Findings (Open → Dispatched → Closed, synced); Sync row labels; at-risk worst first |
| `0b34d92` | chore(deps): FluentAssertions 7.2.2 (owner decision) |
| `e000f6c` | feat: recorded photos in History, on the point, and in a viewer; comparison cut |
| `11964e3` | fix: Documents shows the nameplate and the inspection plan; the fixed "manuals sync" tile is gone |
| `7c6690e` | fix: phone landscape — the inspector scrolls as one when its pinned parts do not fit |
| `532b805` | feat(a11y): names to 21.3 per 1,000 XAML lines; chips/headers read aloud; modal layers take focus |
| `6ba97f7`, `884c6d8` | docs and evidence; `tools/wasm-check.js` (headless Chromium on the Release WebAssembly build) |
| (this file) | handoff, and an allow comment for the sheet form's name |

Numbers: 136 tests pass, 1 skipped (was 90 + 1). `AssetModel` 530 lines and 12 dependencies → the workspace
(182 lines with the photo viewer; it still takes the 4 services it hands to the inspector), `PointInspectorModel`
(446 lines, most of them comments; 7 inputs) and `CorrectiveActionSheetModel` (132 lines, 2 dependencies). Release desktop and WebAssembly build with no
warnings beyond the trim-analysis ones the build gate keeps as warnings.

## State

- `docs/COMPLETENESS.md`: Complete with gaps — 0 Blockers, 0 functional Majors, 1 head Major (Android, iOS,
  WinAppSDK not run here), 8 Minors.
- `docs/DECISIONS.md` is new. D-001 (dispatch built), D-002 (photo comparison cut), D-003 (manuals cut) are
  **proposed**: the owner should confirm them.
- Pending owner question, unchanged: the set-aside warning on Sync shows only once; behaviour left as it is.

## Open items (Minors, in COMPLETENESS)

1. WebAssembly only: after editing the reading and calling Fail, pressing the Notes field while it is mostly
   scrolled out of the inspector's middle closed the inspector (draft kept). Desktop does not. Cause not found;
   start by logging `PointInspectorModel.Close` and `MainPage.OnAssetRegionVisibilityChanged` in the browser.
2. Empty tab stops inside the Toolkit (root `ContentControl`, `TabBarSelectionIndicatorPresenter`,
   `SafeAreaPresenter`); an implicit style does not reach them (tried, reverted).
3. The machine is about 135 px tall in phone landscape.
4. A single photo cannot be removed from a draft; an action can sync before its failed source event; no global
   unhandled-exception handler; markers overlap at 390 px; no UI tests.

## How to check

- Tests: `dotnet test Trace.Tests/Trace.Tests.csproj -p:TraceTargetFrameworks=net10.0`.
- Desktop: `DISPLAY_NUM=<n> TRACE_RESET=1 TRACE_ARGS="--trace-route=Switch" tools/run-desktop.sh out.png` prints
  `VERIFY switch result=pass opened=4/4 recordedOn=C-07`. `--trace-focus=log` with Tab presses walks the focus.
- The mouse wheel scrolls nothing under Xvfb here (any page); check scroll reach by keyboard (Tab, End).
- Browser: see CLAUDE.md "WebAssembly check".

## Next

- CI on the pass's head (macOS jobs are the slow ones). The Android emulator job is the coordinator's to add.
- The owner's answers on D-001 to D-003.
