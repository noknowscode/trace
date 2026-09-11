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
- Theme: Material (dark default for field use, light supported).
- Navigation: region-based via Uno.Extensions Navigation (`ViewMap`/`RouteMap` in App.xaml.cs). Contract: Today → Asset → Point → Complete → optional Corrective Action; back restores exact asset/tab context.
- DI: Microsoft.Extensions.DependencyInjection via Uno.Extensions.Hosting.
- Targets: `net10.0-android`, `net10.0-windows10.0.26100`, `net10.0-desktop` (+ plain `net10.0` for tests). Tier 1 = Android + Windows; desktop head is the dev/verify loop on non-Windows machines. WASM/iOS come later, after platform-service validation.
- All hardware/backends (camera, QR/NFC scan, network status, sync) sit behind interfaces in `Services/` with deterministic simulated implementations. Seed data must be replaceable without touching presentation.

## Project Structure

- `Trace/` — single-project Uno app (Uno.Sdk 6.7.22, pinned in `global.json`)
  - `Trace/Domain/` — `Models/` (immutable records: Asset, Assignment, InspectionPoint, InspectionEvent, Evidence, CorrectiveAction, SyncOperation), `Enums/` (Disposition, SyncState, EvidenceType, CorrectivePriority, ActionStatus), `Rules/` (reading validation, Fail-requires-evidence, idempotent completion)
  - `Trace/Services/` — application service interfaces + simulated implementations (IAssetRepository, IInspectionRepository, ISyncQueue, INetworkStatusService, ICameraService, IAssetScanService, …)
  - `Trace/Infrastructure/` — `Persistence/`, `Repositories/`, `Platform/`, `Seed/` (P-204 pump scenario per UNO-BUILD-SPEC)
  - `Trace/Presentation/` — pages, MVUX models, sheets/dialogs (TodayPage, AssetPage, SyncPage, SupervisorFindingsPage + subviews)
  - `Trace/Styles/` — design tokens (spacing 4/8/12/16/24/32, radii 10/14/18, status roles) as XAML resources
- `Trace.Tests/` — unit tests (`net10.0`) for domain rules and workflows
- `docs/` — the product handoff package (specs, checklist, design graph)
- `prototype/` — runnable HTML interaction reference

Note: there is deliberately no `Application/` folder — a `Trace.Application` namespace would shadow `Microsoft.UI.Xaml.Application` inside namespace `Trace` and break `App : Application`. Application-layer services live in `Services/`.

## Domain rules (from UNO-BUILD-SPEC — test these)

- Fail requires a note or evidence.
- Out-of-range prompts explicit disposition; it does not silently force Fail.
- Offline completion commits locally first; sync status is explicit, never inferred.
- Duplicate completion is idempotent.
- Corrective action inherits asset, point and source inspection event.

## Build & Verify

- Dev loop (Linux/CI container): `dotnet build Trace/Trace.csproj -f net10.0-desktop`
- Tests: `dotnet test Trace.Tests/Trace.Tests.csproj`
- Android/Windows heads build on the Windows 11 dev machine (Android SDK is not present in the web container; the `android` .NET workload must be installed — `dotnet workload restore`).
- App MCP: `.mcp.json` registers the `uno-app` devserver — start sessions rooted in this folder so it loads; launch the app with `uno_app_start`, not bare `dotnet run`.
