# HANDOFF — TRACE (2026-11-04, A pass 2, cleanup and showcase info file)

Branch `feat/production-readiness` (PR #1). A pass 2 ran from `d1e350a` on the code review's closed list
(`uno-lab/docs/production-readiness/reviews/2026-11-04-a-pass/trace.json`). Pushed as it went.

## What this pass did

| Commit | What |
|---|---|
| `31ed6e3` | fix(today): a theme switch that fails is reported; `TodayModelTests` |
| `a7198a6` | test: `AssetsModelTests` (search) and `MainModelTests` (the shell) |
| `6b3b371`, `92f7e29` | refactor: `AssetWorkspaceParts` (a container service) builds the inspector, its evidence part (`PointEvidenceModel`), the action sheet and the viewer; the inspector opens photos through its own part |
| `1c655e1` | refactor: each asset tab is its own control (`AssetPage.xaml` 649 → 166 lines) |
| `fb3e701` | docs: D-004, the offline conflict dialog cut while sync is simulated (proposed) |
| `224eb15`, `9678591` | refactor: screen text out of the domain into `ScreenText` (Findings, Sync, the confirmation, the asset cards) |
| `42023cf`, `1ee5a2a` | fix: the launch is guarded; `CrashReporter` takes what nothing else caught; the closed workspace's dispose is guarded |
| `8acd211` | chore: the dead code and template files the review found |
| `6134fdf` | fix(sync): a failed upload reads Failed in History; corrective work waits for its inspection |
| `8c3041e`, `4131c76` | refactor: no `!` silences a nullable value; the last photos are looked up once |
| `f4604b2` | fix: a failure closed before Create still offers its corrective action |
| `ba3dc29`, `922d492` | feat: remove one photo from a draft |
| `21a72ea` | test(wasm): the browser check presses the scrolled-out Notes field |
| (docs) | COMPLETENESS, UNO-AUDIT, CLAUDE.md, this file |

Numbers: 176 tests pass, 1 skipped (was 136 + 1). `AssetModel` 182 → 167 lines and 12 → 9 dependencies, forwarding
none. `PointInspectorModel` 446 → 376 lines, 4 inputs; `PointEvidenceModel` 168. `AssetPage.xaml` 649 → 166, the
tabs 100–200 each. 21.0 automation names per 1,000 XAML lines (66 in 3,137). Lint gate: 0 gating hits.

## Cleanup (after A pass 2)

| Commit | What |
|---|---|
| `da1f779` | refactor: unused usings (IDE0005 in a one-off analyzer build; IDE0051/52/60, CA1823, IDE0035 found nothing else) |
| `737280d` | refactor: unused members (`Asset.Location`/`Designation`, `InspectionEvent.HasNotes`, `SyncModel.HasPending`, the range-status severity); `LocalFileStore(string)` is the tests' internal seam |
| `5aa43e2`, `d7db119` | chore: four unused tokens; template leftovers (SharedAssets.md, the sample Resources.resw, Android About*.txt and "Hello" string, the Platforms/Windows placeholder, `.gitkeep` files) |
| `29093ae` | chore(deps): the unused Serialization feature and coverlet.collector |
| `62c872d`, `6e3e05b` | docs: banners and restating comments; history-style comments say why in the present; doc references to files that never existed |

## Showcase info file

| Commit | What |
|---|---|
| `71a0e8a` | feat(icon): a gauge on the primary colour in place of the template's icon and splash (D-005, proposed) |
| `fa0ea2c` | feat: `.studiolive/` (sample.json, agent-context.json, user-conversation.json, icon.png, four Dark screenshots); absolute URLs are D-006 (proposed) |
| `b902f12` | docs: README at the root |

## State

- `docs/COMPLETENESS.md`: Complete with gaps — 0 Blockers, 0 functional Majors, 1 head Major (Android, iOS,
  WinAppSDK not run here), 4 Minors.
- `docs/DECISIONS.md`: D-001 to D-006 are **proposed**; the owner should confirm them.
- The WebAssembly "Notes press closes the inspector" Minor did not reproduce in 17 presses over 15 browser sessions,
  at this head and on a rebuild of `d1e350a`. No fix was made; `tools/wasm-check.js` presses the field there now.
- Pending owner question, unchanged: the set-aside warning on Sync shows only once.

## Open items (Minors, in COMPLETENESS)

1. Markers overlap at 390 px.
2. The machine is about 135 px tall in phone landscape.
3. Empty tab stops inside the Toolkit (root `ContentControl`, `TabBarSelectionIndicatorPresenter`,
   `SafeAreaPresenter`).
4. No UI tests (the page models are tested; the Debug hooks and the browser check drive the workflows).

## How to check

- Tests: `dotnet test Trace.Tests/Trace.Tests.csproj -p:TraceTargetFrameworks=net10.0`.
- Desktop: `DISPLAY_NUM=<n> TRACE_RESET=1 TRACE_ARGS="--trace-route=Switch" tools/run-desktop.sh out.png` prints
  `VERIFY switch result=pass opened=4/4 recordedOn=C-07`. `--trace-crash=task` prints `VERIFY crash=task reported=True`.
- The mouse wheel scrolls nothing under Xvfb here; a taller window (`run-desktop.sh out.png 1280 1200`) shows the
  inspector's evidence section.
- Browser: see CLAUDE.md "WebAssembly check".
- Showcase: `python3 -I validate_studiolive.py <repo root> trace` (the coordinator's validator) prints OK.

## Next

- CI on this head (the Android emulator job included).
- The owner's answers on D-001 to D-006, and the Studio team's on relative URLs in `sample.json` (uno-private#2444).
