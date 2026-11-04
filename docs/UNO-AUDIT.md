# uno-audit: TRACE (2026-11-04)

The `uno-audit` pass on `feat/production-readiness`, from 2024dc1. It covers hygiene, pattern conformance, the
semantic lint gate, performance, the build gate and a runtime re-check. Functional gaps from `docs/COMPLETENESS.md`
stay for the next pass.

| Commit | What |
|---|---|
| 83f7157 | refactor: remove dead code, unused usings and template leftovers |
| 221f101 | fix: report photo, corrective-action and sync write failures |
| b05b3c9 | style: bring the semantic lint gate to zero |
| 677d411 | refactor: remove the usings the global usings already cover in the tests and the plain build |
| (this file) | docs: the audit, and the refreshed Asset, Point, Discard and Action evidence sheets |

## Skills applied

| Skill | Pattern it governs here |
|---|---|
| `uno-platform-agent` | Single project on Uno.Sdk, central package management, `Uno0001` as an error, platform symbols (`WINDOWS`, `ANDROID`, `IOS`). |
| `uno-scaffolding` (with `uno-runtime-gotchas.md`) | The code-behind budget, the semantic-first colour ladder, the workaround protocol and the runtime gotchas. The gotchas checked: `StackPanel.Spacing` charging collapsed children (applied to the new evidence message), `SKCanvasElement` caching, `UseStudio()` gating, and `{Binding Refresh}` in an ErrorTemplate (the app uses `FeedRecovery.Retry` through `AncestorBinding` instead). |
| `mvux` | Models are `partial record`. Pages bind with `{Binding}` and never `x:Bind`. Commands are model methods. Errors are states the view binds (`CompletionError`, `EvidenceError`, `ActionError`, `LastResult`). |
| `uno-navigation` | Region navigation in XAML (`uen:Region.*`, `Navigation.Request="../Asset"` with `Navigation.Data`), and routes in `App.xaml.cs`. The asset shares the destinations' Visibility region (CLAUDE.md). |
| `uno-material` | Material roles and type-ramp styles only. The Reset view button moved to `ElevatedButtonStyle`, the Material button that brings its own surface. |
| `uno-toolkit` | `utu:Responsive` for every breakpoint, `utu:TabBar` for both navigation bars, `utu:SafeArea`, and `utu:AncestorBinding` for Try again. CardContentControl versus the styled `Border` surfaces is under Remaining. |
| `winui-xaml` | `ItemsRepeater` for lists, a command instead of a `Click` handler, `AutomationProperties` on the new controls, and a live region on the new error text. |
| `dotnet-csharp` | Exception filters (`when (IsWriteFailure(ex))`) so that only write failures are caught, no `async void` beyond `OnLaunched` (guarded since A pass 2, with `CrashReporter` for what nothing else catches), and unused code removed. |
| `skiasharp-uno` | `SKCanvasElement` on Skia heads and `SKXamlCanvas` on WinAppSDK. Paints, mask filter, offscreen surface and vertex buffers are cached across frames. |

## Semantic lint gate

`xaml-semantic-lint.ps1 -Path Trace`. The before counts are the 2026-11-04 baseline; the after counts are at b05b3c9.

| Rule | Before | After | How |
|---|---|---|---|
| CARD | 4 | 0 | 1 fixed, 3 allowed with reasons |
| HEX | 9 | 0 | fixed: the colour tokens live in a token file |
| TOKENTHEME | 0 | 0 | |
| BACKBAR | 0 | 0 | |
| CODEBEHIND | 8 | 0 | 1 fixed, 7 allowed with reasons (two of them are a WORKAROUND) |
| OVERLAY | 0 | 0 | |
| RESPONSIVE | 0 | 0 | |
| BUILTIN | 1 | 0 | allowed with a reason |
| ICON | 0 | 0 | |
| TOKEN (info) | 3 | 3 | `SheetScrimBrush`, `SceneGroundShadowBrush` and `SceneFillLightBrush`, all in ThemeDictionaries |
| ADAPTIVE (info) | 0 | 0 | 39 `utu:Responsive` |
| TEMPLATE (info) | 3 | 3 | each now says why it is a template |
| WORKAROUND (info) | 0 | 1 | `MainPage.xaml.cs:65` (see below) |

Hit by hit:

- **HEX ×9, `Themes/Brushes.xaml:16-30`**: fixed. The file holds the three colours that no Material role covers: the
  scrim and the ground shadow (the palette has no scrim or shadow role) and a neutral fill light. It is the app's
  colour token file, so it is now named as one: `Themes/ColorTokens.xaml` (rung 4 of the semantic ladder, "a token in
  a palette file"). App.xaml, Tokens.xaml, CLAUDE.md and DESIGN-GRAPH.md follow the rename.
- **CARD, `AssetPage.xaml:210`** (a `Border` plate behind Reset view): fixed. The button uses `ElevatedButtonStyle`,
  which carries its own surface (`AssetPage.xaml:210-215`).
- **CODEBEHIND, `AssetPage.xaml.cs:76` `OnResetView`**: fixed. `AssetSceneView` exposes `ResetViewCommand`
  (`AssetSceneView.cs:59,163`), and the button binds it by element name. The camera is the view's own state, so the
  command lives on the view and not on the model.
- **CARD, `AssetPage.xaml:270` `InspectorHost`**: allowed. One element moves between a bottom sheet and a docked pane,
  so a half-entered reading survives a resize. A `!` flyout route would rebuild it for each open. A
  `CardContentControl` would add pointer-state overlays over a form full of inputs (the card wash-out gotcha).
- **CARD, `PointInspector.xaml:194`** (evidence thumbnail): allowed. It is an image clip with a placeholder fill.
- **CARD, `StatusChip.xaml:16` `Shell`**: allowed. It is a status pill, not a card. `utu:Chip` is a toggle button: it
  takes focus and announces itself as one. The severity visual states repaint this shell.
- **CODEBEHIND, `AssetPage.xaml.cs:30,34`** (`OnKeyboardAreaResized`, `OnWorkspaceResized`): allowed. This is
  measured layout. The SoftInput `SafeArea` measures its content at infinite height, so the frame is pinned to the
  SafeArea's rendered height. The inspector's height cap and the scene's height come from the workspace's rendered
  size. Neither value has a XAML binding that updates.
- **CODEBEHIND, `MainPage.xaml.cs:33`** (reads `AssetRegion.Visibility`): allowed. This is the asset-switch fix from
  b9a3a9c, kept as it is. The navigator owns the asset's model and offers no hook when it hides the asset.
- **CODEBEHIND, `MainPage.xaml.cs:67,74,82,84`** (`RealizeBothNavigationBars`): allowed, and marked with
  `WORKAROUND(TabBar navigates when first laid out)`. See the gotcha record below.
- **BUILTIN, `AssetCardContent.xaml.cs:5`**: allowed. The name matches the card rule, but the control is the content
  inside a `CardButtonStyle` button, which is the card. It draws no surface of its own.

### Gotcha record: TabBar navigates when first laid out

The workaround protocol asks for a gotcha entry. This one is proposed for `uno-runtime-gotchas.md` and was not
written to the shared skill file in this pass.

- **Symptom:** an asset was open on a phone-width window. Crossing 600 px (a phone turned to landscape, or a window
  dragged wider) left the asset and went back to Today.
- **Cause, as measured in bd8288b:** a `utu:TabBar` with `uen:Region.Attached` announces its selected item the first
  time its items are laid out, and `TabBarNavigator` treats that as a navigation request. The bar for the other width
  starts `Collapsed` (`{utu:Responsive}` Visibility), so its first layout happened at the crossing.
- **Versions:** Uno.Sdk 7.0.0-dev.115 (Uno.WinUI 7.0.0-dev.2509, Toolkit 11.0.0-dev.157, Extensions.Navigation
  8.0.0-dev.150), `net10.0-desktop`, X11.
- **Workaround:** on `Loaded`, lay out the hidden bar once (Visible, `UpdateLayout`, Collapsed) while Today is the
  destination anyway.
- **Not done:** reduced to a minimal page, or tried on Toolkit stable 9.1.

## Findings and fixes by category

### Stack currency (reported, not changed)

| Package | Pinned | Newest stable | Note |
|---|---|---|---|
| Uno.Sdk | 7.0.0-dev.115 | 6.7.30 (dev: 7.0.0-dev.132) | Owner decision. It brings Uno.WinUI 7.0.0-dev.2509, Toolkit 11.0.0-dev.157, Themes/Material 9.0.0-dev.61 and Extensions 8.0.0-dev.150. |
| SkiaSharp (via the SDK) | 4.151.1 | 4.154.0 | Follows the Uno.Sdk build. The tests' `SkiaSharp.NativeAssets.Linux` 4.151.1 must match it. |
| SharpGLTF.Core / .Toolkit | 1.0.6 | 1.0.7 | Patch release. |
| Microsoft.NET.Test.Sdk | 17.9.0 | 18.11.0 | |
| NUnit | 4.1.0 | 5.0.0 | Major version. |
| NUnit3TestAdapter | 4.5.0 | 6.3.1 | Major version. |
| coverlet.collector | 6.0.2 | 10.1.0 | Major version. |
| FluentAssertions | 6.12.0 | 8.11.0 | 8.x ships under a commercial licence. 7.2.2 is the newest Apache-2.0 release. |

All heads target .NET 10 (`net10.0-*`), and `global.json` allows no prerelease SDK.

### Code hygiene (83f7157, 677d411)

- **Unused usings:** 87 `using` directives that the implicit or global usings already cover were
  removed across 52 files: 73 in the app and 14 in the tests. They were found with IDE0005 on the desktop head, the
  app's plain `net10.0` build and the test project. The analyzer reports one duplicate per file per build, so it was
  re-run until it found none. Each removed namespace is implicit on every head (Uno.Sdk
  `Uno.Implicit.Namespaces.targets`, and the buildTransitive targets of `Uno.Extensions.Core` and the other
  Extensions packages). Two unused global usings went too: the template's `ApplicationExecutionState` alias and
  `Microsoft.Extensions.Localization`.
  `using Uno.Resizetizer;` in `App.xaml.cs` stays: the desktop head does not need it, but no other head was built here
  to check.
- **Dead members:**
  - `AssetModel.Points`, already noted in COMPLETENESS.md.
  - `TodayModel.ConnectionLabel`.
  - `ScanModel.HasResult` and `Resolved`.
  - `AssetSummary.DueText` and `IsComplete`.
  - `StatusSeverityMap.ToGlyphKey`. StatusChip draws the marks from visual states.
  - `IInspectionRepository.WatchPointHistory`, already noted in COMPLETENESS.md.
  - `ILocalEvidenceStore.CreateNote`. Only a test called it; the test still checks the photo timestamp.
  - `GrantCameraFromSettings` lost its unused token (IDE0060).
- **Template leftovers:**
  - the commented-out logging filters in `App.xaml.cs`, and its narration comments ("Add navigation support…",
    "Switch to Development…", "Configure log levels…", "Enable localization…");
  - the template's doc comment on `App()`;
  - the commented-out `FontOverrideDictionary` and the unused `xmlns:um` in `App.xaml`;
  - "This is the main entry point" in `Main.iOS.cs`;
  - the empty `Directory.Build.targets` and its entry in the solution.
- **Constraint comments kept:** the comments that say why something is done (store commit order, FeedRecovery, the
  SafeArea pin, the renderer caches) all stay.
- **A pass 2 (`8acd211`):** the code review found dead code this pass had missed, now removed:
  `IAssetRepository.GetAsync` (no caller); members only the tests used (`IInspectionRepository.GetHistoryAsync`,
  `ISyncQueue.RetryAsync`, `SyncOperation.HasError` and `IsRetryable`, `InspectionPoint.HasReading` and
  `LatestRangeStatus`, `SyncResult.AnyAttempted`); the template's `AppInfoTests`, `AppConfig.Environment` with its
  settings keys and `appsettings.development.json`, and `Trace/ReadMe.md`; `EvidenceType.Note`; four unused icons.

### Error handling (221f101)

The code grade's lead: "photo and action IO errors are not caught". Four commands let a failed store or evidence
write escape as an exception that the generated command swallowed, so the screen said nothing. Each one now catches
`IOException` and `UnauthorizedAccessException` only, says what happened where the technician is looking, and has a
test over `BreakableFileStore` (`Trace.Tests/TestHarness.cs`):

- **`AssetModel.AttachPhoto`** (`AssetModel.cs:335-378`): the new `EvidenceError` shows in a row under the Evidence
  header (`PointInspector.xaml:134-163`). It is a Grid row, so the hidden message leaves no gap; see the StackPanel
  spacing gotcha. Test: `A_photo_the_device_cannot_keep_is_reported_and_attaches_nothing`.
- **`AssetModel.CreateAction`** (`AssetModel.cs:439-486`): `ActionError` on the sheet. The message says to check Open
  findings first, because the store can still finish a half-written commit on its next read, and a second try would
  then duplicate the action. Test: `A_corrective_action_the_device_cannot_write_is_reported_on_the_sheet`.
- **`SyncModel.SyncNow` and `RetryFailed`** (`SyncModel.cs:65-103`): the screen's result line. Test:
  `SyncModelTests.Sync_commands_that_cannot_update_the_queue_say_so`.

### Pattern conformance

- `{Binding}` everywhere on MVUX pages, with no `x:Bind`. Region navigation is in XAML, and routes are in
  `App.xaml.cs`.
- There are no literal hex values or font sizes in pages or controls. The reading field's size is a token
  (`PointInspector.xaml:231`, `ReadingFontSize`), because a `TextBox` takes no TextBlock type-ramp style.
- Breakpoints are all `{utu:Responsive}` (39 uses), with no `AdaptiveTrigger` and no width code.
- The shared error template retries through `utu:AncestorBinding` to the model's `Retry`, not through
  `{Binding Refresh}` in the ErrorTemplate (the dead-button gotcha). FeedRecovery restarts the live watches, which
  MVUX does not restart on its own.
- Lists are `ItemsRepeater`. The round, Assets and Findings put `uen:Navigation.Request` on the card button inside
  the item template (`TodayPage.xaml:107`, `AssetsPage.xaml:53`, `SupervisorFindingsPage.xaml:138`), not on the
  repeater. It works, and the `Switch` hook proves it from every origin. The navigation skill prefers the request on
  the repeater; this is noted, not changed.

### Performance

- There are no frame pumps: no `CompositionTarget.Rendering`, no `RepeatBehavior="Forever"` and no timers in the app.
  The 3D view draws only when it is invalidated (a drag, a zoom, a theme or palette change, a model load).
- **The renderer** (`Scene3D/SceneRenderer.cs`):
  - The offscreen surface (2× supersample), paints, mask filter and path builder are created once and reused.
  - The vertex and face buffers are pooled.
  - The tail of the buffer is collapsed instead of copied, so a frame allocates only the native `SKVertices` copy and
    the snapshot image.
  - Parametric models are cached per kind (`EquipmentModels.For`).
  - glTF loads run off the UI thread (`Task.Run`), and the newest load wins.
- The renderer state is released on `Unloaded` and rebuilt on the next paint.
- **Idle CPU on the Asset page:** 0.2 % of one core, measured over 10 s, 25 s after launch (Debug, software
  renderer, 1280 × 800). Nothing redraws while the technician is not touching the machine.
- Startup: the store root is opened on first use, through `StorageFolder` (it waits for browser storage on
  WebAssembly).

### Build and tests

- **Release** `net10.0-desktop`: 0 warnings, 0 errors. Warnings are errors, and the `.editorconfig` style is enforced.
- **Unit tests** (`-c Release -p:TraceTargetFrameworks=net10.0`, as CI runs them): 90 passed and 1 skipped (the
  explicit glTF export), against 87 and 1 at 2024dc1. The three new tests cover the write failures.
- **WebAssembly** (Release `net10.0-browserwasm`): 0 warnings, 0 errors.
- **Other heads:** Android was not built here (no Android SDK in this container). The WinAppSDK and iOS heads build
  only in CI. CI builds every head on PR #1.

### Runtime verification (Debug desktop, Xvfb display :455, `TRACE_X11_RENDERER=software`)

- `--trace-route=Switch`: `VERIFY switch result=pass opened=4/4 recordedOn=C-07`, before and after the last commit.
  The asset-switch fix works unchanged.
- **Sync now** (Light, 390 px): pressing it reports "Nothing was waiting to sync.", so the guarded path still runs.
- **Reset view:**
  - It renders as an elevated pill over the machine in Dark at 1280 and 390 px, and in Light at 390 and 1280 px.
  - After a drag that moved 256,402 pixels, pressing it put the scene back to the opening view. Only the hovered
    button differed, by 4,515 px.
- **Add photo with the evidence folder unwritable:** a file was put where the `evidence` folder goes, then
  `--trace-route=Photo` was run in Light at 1280 px. The inspector showed "The photo could not be saved on the device,
  so nothing was attached…", the "No evidence attached" tile stayed, and `VERIFY photo uri=` was empty.
- **Corrective-action sheet (Light, 1280 px):** the scrim from `ColorTokens.xaml` resolves. The page behind it reads
  rgb(96,98,96), against rgb(239,244,241) unscrimmed.
- **Evidence:** `tools/evidence.sh` refreshed the Asset, Point, Discard and Action contact sheets in Dark and Light
  at 360/599/600/800/1080/1920 × 640. These are the routes that show the Reset view button.

## Remaining

The `AssetModel` split and the unguarded store paths that stood here are done (A pass `0bb3835`, `cab8cd2`; A pass 2
`6b3b371`, `92f7e29`: the container builds the workspace's parts), and FluentAssertions is on 7.2.2 (`0b34d92`, owner
decision). What stands:

- **Card surfaces:** 43 styled `Border` surfaces (`CardSurface`, `TileSurface`, `SheetSurface`, `ValueChip`,
  `ScenePlate`, `ConfirmationSurface` in `Themes/Styles.xaml`). The lint passes them because the attributes live in
  styles, but the semantic ladder says such surfaces are `utu:CardContentControl`. Moving the static ones to an
  outlined card style with lightweight keys is a visual pass across every page, with fresh evidence: about half a
  day. The card wash-out gotcha means static cards need a `BasedOn` style with a plain template.
- **`Controls/WrapPanel.cs:14`:** the comment says neither WinUI nor the Toolkit ships a wrapping panel. Windows
  Community Toolkit 8.x ships `WrapPanel` and supports Uno. Swapping it adds a package, so it is a decision to
  propose, not to make in an audit.
- **Test packages:** the updates in the stack table above (NUnit 5, adapter 6, test SDK 18, coverlet 10;
  FluentAssertions 7.2.2 to stay on Apache-2.0).
- **Gotcha entry:** add "TabBar navigates when first laid out" to the shared `uno-runtime-gotchas.md`, and reduce it
  to a minimal page.
