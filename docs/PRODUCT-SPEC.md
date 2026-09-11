# TRACE
## Product definition

TRACE is a field-first maintenance and inspection application for technicians responsible for physical equipment such as pumps, compressors, air handlers, production machines and utility assets.

### Primary users
- Maintenance technicians and millwrights performing inspections and corrective work.
- Reliability technicians capturing vibration, temperature and visual condition data.
- Maintenance supervisors reviewing findings and assigning corrective action.

### Problem
Traditional CMMS software organizes work around records, folders and forms. In the field, technicians think about the physical machine in front of them: which component is abnormal, what changed, what evidence proves it, and what must happen next.

TRACE makes the asset itself the primary navigation surface.

### Core user goals
1. Identify an asset quickly.
2. Understand its current condition.
3. Inspect known points with minimal navigation.
4. Capture defensible evidence.
5. Escalate a failed condition into corrective work without re-entering context.
6. Keep working with unreliable or absent connectivity.
7. Review an asset’s condition history spatially and chronologically.

### Primary workflows
- Scan asset QR/NFC tag → open asset.
- Select inspection point → enter reading → attach evidence → Pass/Watch/Fail.
- Fail an inspection → create corrective action → assign owner and priority.
- Review asset history → compare previous measurements and photos.
- Work offline → queue events → synchronize later.
- Supervisor reviews failed/watch findings → dispatches work.

### Realistic environments
- Mechanical rooms
- Manufacturing floors
- Water and wastewater facilities
- Rooftops
- Utility sites
- Basements and low-connectivity buildings
- Outdoor installations in rain, cold, glare and while wearing gloves

### Platforms and form factors
- Primary: rugged phone, standard Android/iPhone, 7–10 inch rugged tablet.
- Secondary: desktop/web review experience for supervisors.
- Portrait phone is the default task surface.
- Tablet expands spatial asset view and evidence simultaneously.
- Desktop shifts toward fleet triage and comparison.

### Differentiation
- Machine-centric rather than record-centric information architecture.
- Spatial inspection points attached directly to the equipment view.
- Field evidence and condition history embedded at the point of inspection.
- Offline-first with explicit sync confidence.
- Corrective action created from the failed point, carrying evidence automatically.

### Boundaries
TRACE should:
- Support inspection, condition capture, evidence, history, corrective action and offline synchronization.
- Represent assets and their inspection points clearly.

TRACE should not:
- Become a full enterprise CMMS replacement.
- Handle procurement, inventory costing, payroll, work scheduling optimization or ERP.
- Invent predictive-maintenance conclusions without sufficient data.
- Require a 3D asset model; 2D annotated imagery is a first-class supported mode.

## Must Have

### Screens / views
- Today / assigned work list
- Asset detail
- Spatial asset inspection view
- Inspection point detail sheet
- Evidence capture/review
- Corrective action creation
- Asset history
- Offline sync queue/status
- Supervisor findings view

### Core capabilities
- Asset scan/search
- Spatial inspection points
- Reading entry with units and expected range
- Pass / Watch / Fail disposition
- Photo and note evidence
- Required-field validation
- Corrective action creation from a failed point
- Local persistence
- Offline event queue
- Sync state visibility
- Inspection history
- Realistic sample assets and readings

### Required states
- Loading
- Empty assignment list
- Populated assignment list
- Asset unavailable
- Offline
- Pending sync
- Sync failed
- Permission denied for camera
- Valid reading
- Out-of-range reading
- Required evidence missing
- Action created successfully

## Nice to Have
- Bluetooth vibration sensor pairing
- Voice dictation
- OCR of nameplates
- NFC asset identification
- Thermal camera attachment
- Compare-over-time image scrubber
- Supervisor annotations
- Limited trend-based anomaly hints
- Haptic confirmation for Pass/Watch/Fail

## Application architecture

### Navigation
Bottom navigation on phone:
- Today
- Assets
- Scan
- Sync
- More

Asset detail:
- Overview
- Inspect
- History
- Documents

Transient layers:
- Inspection-point sheet
- Add reading dialog
- Evidence viewer
- Corrective-action sheet
- Offline conflict dialog

### Data models
Asset
- id
- name
- type
- site
- image
- status
- inspectionPoints[]

InspectionPoint
- id
- assetId
- label
- x/y position
- readingType
- unit
- normalMin
- normalMax
- latestReading
- disposition

InspectionEvent
- id
- assetId
- pointId
- timestamp
- technician
- reading
- unit
- disposition
- notes
- evidence[]
- syncState

CorrectiveAction
- id
- sourceInspectionEventId
- assetId
- pointId
- priority
- summary
- assignee
- status
- createdAt

Evidence
- id
- type
- localUri
- caption
- createdAt

SyncOperation
- id
- entityType
- entityId
- operation
- createdAt
- status
- error

### Persistence
- Local-first storage for asset assignments, event drafts, completed inspections and sync queue.
- Remote source simulated in prototype.
- Local writes happen immediately.
- Sync status is explicit, not inferred.

### Permissions
- Camera required only for photo capture and scan.
- Optional microphone for future dictation.
- Network optional during field task.

### Responsive behavior
- Phone: single-task flow, sheets over asset.
- Tablet: asset and selected point visible together.
- Desktop: asset/finding rail + detail workspace.

### Themes
- Dark default for field use.
- Light supported for office review.
- High-contrast status colors always paired with labels/icons.
