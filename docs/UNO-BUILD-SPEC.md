## Uno implementation standards
- .NET 10 and current stable Uno Platform template at implementation time.
- C# + XAML, single-project Uno application.
- Dependency injection via Microsoft.Extensions.DependencyInjection.
- MVUX recommended; MVVM acceptable only if used consistently.
- Prefer Uno/WinUI/toolkit controls over custom-drawn controls.
- Centralize design tokens in XAML resources.
- Platform capabilities live behind interfaces.
- Seed/prototype data must be replaceable without rewriting presentation logic.
- Responsive VisualStates must express real composition changes, not simple scaling.
- Windows and the primary mobile/tablet target must build and run before expanding targets.
- Add unit tests for domain rules and integration/UI tests for critical workflows.

### Suggested repository
```text
src/
  App/
  Presentation/
    Pages/
    Views/
    Components/
    Dialogs/
    Overlays/
  Domain/
    Models/
    Enums/
    Rules/
  Application/
    Services/
    Navigation/
  Infrastructure/
    Persistence/
    Repositories/
    Platform/
    Seed/
  Resources/
    Styles/
    Themes/
    Icons/
    Images/
tests/
  Unit/
  Integration/
  UI/
docs/
prototype/
```

# TRACE — Uno Platform implementation specification

## Build target
Production-shaped .NET 10 Uno Platform sample. Tier 1: Android phone/rugged tablet and Windows desktop/tablet. Tier 2: WebAssembly. Expand to iOS/macOS/Linux after platform-service validation.

## Architecture
Use a single-project Uno app, C# + XAML, dependency injection, and preferably MVUX. Separate Presentation, Domain, Application and Infrastructure concerns. All camera, QR/NFC, network and file-system behavior must sit behind interfaces with deterministic simulated implementations for desktop/demo.

### Pages
- TodayPage
- AssetPage
- SyncPage
- SupervisorFindingsPage

### Asset subviews
- AssetOverviewView
- SpatialInspectionView
- AssetHistoryView
- AssetDocumentsView

### Transient layers
- InspectionPointSheet
- ReadingEntryDialog
- EvidenceSheet
- CorrectiveActionSheet
- SyncConflictDialog
- PermissionRecoveryDialog

### Reusable controls
AssetCard, SpatialAssetCanvas, InspectionPointMarker, ReadingMetric, DispositionControl, EvidenceTile, ConditionTimeline, SyncStatusBadge.

`SpatialAssetCanvas` is the justified purpose-built control. Use existing Uno/WinUI controls for ordinary buttons, fields, tabs and lists.

## Domain rules
- Fail requires a note or evidence.
- Reading retains value and unit.
- Out-of-range prompts explicit disposition; it does not silently force Fail.
- Offline completion commits locally first.
- Duplicate completion is idempotent.
- Corrective action inherits asset, point and source inspection event.

## Models
Asset, Assignment, InspectionPoint, InspectionEvent, Evidence, CorrectiveAction, SyncOperation.
Enums: Disposition, SyncState, EvidenceType, CorrectivePriority, ActionStatus.

## Services
IAssetRepository, IAssignmentRepository, IInspectionRepository, ICorrectiveActionRepository, ISyncQueue, INetworkStatusService, ICameraService, IAssetScanService, ILocalEvidenceStore.

## Persistence
Use durable local structured storage. Evidence lives in app-local files and is referenced from records. Drafts survive navigation. Sync queue survives process termination.

## Navigation contract
Today → Asset → Point → Complete → optional Corrective Action. Back restores the exact asset/tab context. Dirty reading entry confirms discard only when data would be lost. Offline state never changes navigation.

## Adaptive composition
Phone: bottom nav + single workspace + bottom sheet.
Tablet: canvas + pinned point inspector.
Desktop: assignments/findings rail + canvas + inspector.

## Seed scenario
P-204 Centrifugal Pump, Area B Water Treatment. Points: motor body, seal housing, coupling, drive-end bearing. Seal reading 7.2 mm/s against 0–2.8 mm/s. Include three historical inspections and one corrective action.

## Tests
Reading validation; Fail rule; event persistence; offline queue; idempotent replay; corrective-action inheritance; navigation restoration; adaptive-state smoke tests.

## Explicit boundaries
No full CMMS backend, predictive ML or mandatory 3D. Native QR/camera can follow the complete simulated workflow.
