# Project Instructions

## Overview

TRACE — field-first maintenance and inspection app for technicians working on physical equipment (pumps, compressors, air handlers). The asset itself is the primary navigation surface: spatial inspection points on an annotated equipment view, reading capture with Pass/Watch/Fail disposition, evidence, corrective actions, and offline-first sync. Product-quality Uno Platform sample.

**Source of truth lives in `docs/` — read in this order before implementing anything:**
1. `docs/PROJECT-BRIEF.md`
2. `docs/PRODUCT-SPEC.md`
3. `docs/UNO-BUILD-SPEC.md`
4. `docs/COMPLETION-CHECKLIST.md`
5. `docs/DESIGN-GRAPH.md`
6. `prototype/prototype.html` (behavioral/visual reference only, not the architecture)
7. `docs/PROTOTYPE-VERIFICATION.md`

Written specs win over the prototype. Complete Must Have before Nice to Have.

## Architecture

- Pattern: MVUX (locked in — do not switch mid-project). MVUX pages bind with `{Binding}`, never `x:Bind`.
- Theme: Material (`MaterialToolkitTheme`, Uno.Themes). Dark is the default for field use (an owner decision: TRACE does not follow the system theme); the Light/Dark toggle on Today persists through `IThemeService`. The look comes from the theme, so Uno Platform Studio's Themes tab drives it:
  - Palette: one file, `Styles/ColorPaletteOverride.xaml`, all 33 colour keys in `Light` and `Default` (Dark), read through `MaterialToolkitTheme.Colors` (`ThemeColors.OverrideSource`). Studio's Themes tab edits it. Pages, styles and templates use the role brushes it generates (`OnSurfaceBrush`, `OnSurfaceVariantBrush`, `PrimaryBrush`, `SurfaceBrush`, `SurfaceVariantBrush`, `OutlineBrush`, `OutlineVariantBrush`, `ErrorBrush`/`ErrorContainerBrush`, `SecondaryContainerBrush`… with their `On*` pair, and `OnSurfaceHoverBrush`/`OnSurfacePressedBrush` for state layers) through `{ThemeResource}`.
  - Colours with no palette role live in App.xaml's `Light` and `Default` theme dictionaries, the only other place a hex colour may appear: `Success*` and `Watch*` (the Pass and Watch statuses, with container and `On*` pairs; a failure is the Error role, a pending sync the Secondary container), `ScrimColor` (behind a sheet, and the 3D view's ground shadow), `SceneFillLightColor`, and `Equipment*Color` (the parametric machines' paint, which `AssetSceneView` reads for its own theme; a glTF model carries its own colours).
  - Type: the Material type styles; the app's text roles in `Themes/Styles.xaml` are based on them and add only a role brush. Code-placed sizes use the type-scale keys (`TitleLargeFontSize`).
  - Spacing, radius and sizes: the theme's tokens, picked by measured value (on this theme `SpaceN00` = N × 8 px, `RadiusN00` = N × 4 px): `Space025`…`Space300` and `Space*Thickness` with its Top/Bottom/Right/Horizontal forms, `Radius050`…`Radius500CornerRadius`, `ControlHeightMediumLarge` (the 44 px gloved-hand target), `IconSizeSmall`/`IconSizeLarge`, `LowOpacity`. `Themes/Layout.xaml` keeps only what no token covers.
  - No hex colour, `FontSize`/`FontFamily` or `CornerRadius` literal in pages, controls or styles, and no colour literal in C#. Code that draws (the equipment view) takes theme brushes through its style and repaints on `ActualThemeChanged` and when a role brush is recoloured in place.
- Responsive: `utu:Responsive` / `utu:ResponsiveView` on the Toolkit's default breakpoints — Narrow (below 600), Normal (600), Wide (800), Widest (1080). No `AdaptiveTrigger`s and no device checks. Bottom bar below 600, rail from 600; the point inspector is a sheet below 800 and docks from 800. Each page states its four layouts in a comment at its top.
- Navigation: region-based via Uno.Extensions Navigation (`ViewMap`/`RouteMap` in App.xaml.cs). Contract: Today → Asset → Point → Complete → optional Corrective Action; back restores exact asset/tab context. The destinations and the asset share one Visibility region with no back stack, so `NavigationOrigin` records the destination last shown (from `IRouteNotifier`, in App.xaml.cs) and the asset's Back returns to it.
- DI: Microsoft.Extensions.DependencyInjection via Uno.Extensions.Hosting.
- Targets: `net10.0-android`, `net10.0-ios`, `net10.0-windows10.0.26100` (WinAppSDK), `net10.0-browserwasm`, `net10.0-desktop`. Tier 1 = Android + Windows; desktop head is the dev/verify loop on non-Windows machines. iOS and WebAssembly run the same simulated platform services as every other head. Uno.Sdk skips the heads the build OS cannot build (iOS off macOS, WinAppSDK off Windows); `-p:TraceTargetFrameworks=<tfm>` builds one head.
- Uno 7 defines `ANDROID`, `IOS`, `WINDOWS` (WinAppSDK), `__WASM__` and `__DESKTOP__`; there is no `__ANDROID__`/`__IOS__`. `Uno0001` (API not implemented on a head) is a build error.
- All hardware/backends (camera, QR/NFC scan, network status, sync) sit behind interfaces in `Services/` with deterministic simulated implementations. Seed data must be replaceable without touching presentation.

## Project Structure

- `Trace/` — single-project Uno app (Uno.Sdk 7.0.5, pinned in `global.json`)
  - `Trace/Domain/` — `Models/` (immutable records: Asset, Assignment, InspectionPoint, InspectionEvent, Evidence, CorrectiveAction, SyncOperation), `Enums/` (Disposition, SyncState, EvidenceType, CorrectivePriority, ActionStatus), `Rules/` (reading validation, Fail-requires-evidence, idempotent completion)
  - `Trace/Services/` — application service interfaces + simulated implementations (IAssetRepository, IInspectionRepository, ISyncQueue, INetworkStatusService, ICameraService, IAssetScanService, …)
  - `Trace/Infrastructure/` — `Persistence/`, `Repositories/`, `Platform/`, `Seed/` (P-204 pump scenario per UNO-BUILD-SPEC). The store keeps one JSON file per section under `LocalFolder/trace`; a mutation that changes several sections goes through a journal (`commit.json`, `StoreCommit`) so it lands whole, and a file that cannot be read is renamed `<file>.unreadable-<time>`, kept, and reported on Sync. Evidence photos are referenced as `ms-appdata:///local/trace/…`. `CrashReporter` logs what nothing else caught (the launch, the UI thread, the process, unobserved tasks) to the app log and `crash.log` in the local folder.
  - `Trace/Presentation/` — pages, MVUX models, sheets/dialogs (TodayPage, AssetPage, SyncPage, SupervisorFindingsPage + subviews). The asset workspace is `AssetModel` (the asset, its tabs, markers, history, findings and Back) plus parts that `AssetWorkspaceParts`, a container service, builds over their own services: `PointInspectorModel` (the inspection at the open point, bound to `Controls/PointInspector`) with its `PointEvidenceModel` (the draft's photos, the photos last recorded at the point, the camera), `CorrectiveActionSheetModel` (bound to `Controls/CorrectiveActionSheet`) and `EvidenceViewerModel` (`Controls/EvidenceViewer`). `AssetViewModel.cs` adds the parts' generated view models to the page's (`Inspector`, `Inspector.Evidence`, `ActionSheet`, `Viewer`); those partials carry `[ReactiveBindable(false)]` so MVUX does not take them for models. Each asset tab is its own control (`Controls/Asset{Overview,Inspect,History,Documents}Tab`). The words the screens put on domain values (card labels, button labels, Sync row kinds, confirmations) live in `ScreenText`, reached from XAML through converters; the domain keeps facts.
  - `Trace/Presentation/Scene3D/` — the CPU 3D pipeline over SkiaSharp: `SceneRenderer`, `SceneCamera`, parametric `EquipmentModels` (each part names an `EquipmentPaint`, coloured from App.xaml per theme), and `GltfEquipmentLoader` for real assets (`docs/REAL-ASSETS-SPEC.md`)
  - `Trace/Assets/Models/` — bundled glTF equipment (`p-204.glb`)
  - `Trace/Themes/` — `Layout.xaml` (only the layout values no theme token covers: two-axis chip and row insets, the rail's hairline, content and panel widths, part-specific sizes; literals only), `Styles.xaml` (surfaces, text roles, the shared feed progress/error templates), `Markers.xaml` (the point marker and disposition button templates), icons, converters. Pages and controls carry no hex colours and no literal spacing, radii or type sizes.
  - `Trace/Styles/` — `ColorPaletteOverride.xaml`, the palette (hand-owned: there is no DSP JSON or `Dsp` feature)
- Tests: the unit tests live in the development repository (noknowscode/trace); this folder is the single app project Studio opens
- `docs/` — the product handoff package (specs, checklist, design graph)
- `prototype/` — runnable HTML interaction reference
- `.studiolive/` — the showcase info file (`sample.json`, the Studio agent context and conversation, the icon and four screenshots); keep `agent-context.json` current with the code. `README.md` at the root says how to run it.

Note: there is deliberately no `Application/` folder — a `Trace.Application` namespace would shadow `Microsoft.UI.Xaml.Application` inside namespace `Trace` and break `App : Application`. Application-layer services live in `Services/`.

## Domain rules (from UNO-BUILD-SPEC)

- Fail requires a note or evidence.
- Out-of-range prompts explicit disposition; it does not silently force Fail.
- Offline completion commits locally first; sync status is explicit, never inferred.
- Duplicate completion is idempotent.
- Corrective action inherits asset, point and source inspection event.
- One failed inspection raises one corrective action; a repeated request resolves to it.
- Corrective work moves Open → Dispatched → Closed from Findings (`IActionDispatch`), one step per request, each with its own sync operation (docs/DECISIONS.md D-001).

## Build & Verify

- Dev loop (Linux/CI container): `dotnet build Trace/Trace.csproj -f net10.0-desktop`
- Build gate (`Directory.Build.props`): warnings are errors, `.editorconfig` style is enforced in the build, NuGet audit and trim-analysis warnings stay warnings. CI runs in the development repository.
- Hot Design and App MCP run in Debug on Uno.Sdk 7.0.5. `App.xaml.cs` still compiles `UseStudio()` out when a build defines `NO_UNO_STUDIO`, and `APP_NO_HOTDESIGN=1` skips it at run time for headless runs.
- Runtime verification in this container: `tools/run-desktop.sh <out.png> [w] [h] [settle] [steps…]` (Xvfb + xdotool + capture; `TRACE_RESET=1` clears the store and the theme preference; `TRACE_ARGS` passes the hooks below). `tools/evidence.sh <work dir>` runs every route in Dark and Light at 360/599/600/800/1080/1920 × 640 through `tools/matrix.sh` (one launch, live-resized) and writes contact sheets to `docs/evidence/`. Both set `TRACE_X11_RENDERER=software`: under Xvfb, Mesa's llvmpipe OpenGL leaves parts of the window unpainted. The `uno-app` MCP registered in `.mcp.json` exposes no tools here (it needs a signed-in Uno account), so these scripts are the verification path on this box; `uno_app_start` applies on the Windows machine.
- WebAssembly check: publish Release `net10.0-browserwasm`, serve `wwwroot`, then `NODE_PATH=<playwright> node tools/wasm-check.js <url> <out dir>` drives Today → P-204 → Seal housing → reading, Fail, then a press on the scrolled-out Notes field and a note → Complete → History in headless Chromium and saves each step (`docs/evidence/wasm-notes-press.jpg`, `wasm-complete-point.jpg`). `DISPLAY_NUM=<n>` pins the Xvfb display for `run-desktop.sh`, `matrix.sh` and `evidence.sh` on a shared machine.
- Product decisions the specs leave open are in `docs/DECISIONS.md` (proposed until the owner confirms).
- Debug-only verification hooks (`App.Verification.cs`, never in Release): `--trace-theme=light|dark`, `--trace-route=Today|Assets|Scan|Sync|Findings|Asset[:Overview|Inspect|History|Documents]|Point|Discard|Action|Again|Photo|Switch` (`Switch` opens AHU-12, C-07, P-204 and C-07 by code in one launch, with Back after each, records a reading on the second, and prints `VERIFY switch result=pass|fail`), `--trace-from=Assets|Scan|Sync|Findings` (open the asset from there), `--trace-back=1` (press the asset's Back, print where it landed), `--trace-seed=empty`, `--trace-fault=load` (store reads fail for 20 s; Try again recovers after; with an asset route, the Inspect error state), `--trace-focus=log`, `--trace-crash=launch|task` (throws in the launch, which leaves no window and writes `crash.log`, or in a task nobody awaits, and prints whether `crash.log` got it). Each prints `VERIFY …` lines to stderr.
- Android needs the `android` .NET workload (`dotnet workload restore`) plus an Android SDK and JDK; WebAssembly needs `wasm-tools`. The WinAppSDK head builds on Windows and the iOS head on macOS (CI covers both).
- App MCP: `.mcp.json` registers the `uno-app` devserver — start sessions rooted in this folder so it loads; on a machine where it exposes tools, launch the app with `uno_app_start`, not bare `dotnet run`.
