# Decisions

Product and scope decisions for TRACE that the specs leave open. Settled owner decisions are
marked **settled**; calls made during a pass for the owner to confirm are marked **proposed**.
Newest last.

### D-001 · 2026-11-04 · Supervisor dispatch is built, as one status step at a time (proposed)

Decision: corrective work moves Open → Dispatched → Closed from Findings. Each row offers its
next step (Dispatch, then Close); each step is a local write with its own `update` sync
operation, like every other write. `ActionStatus` is now exactly those three values.

Reason: the spec's primary workflows end in "Supervisor reviews failed/watch findings →
dispatches work", the data model carries `CorrectiveAction.status`, and the users include
supervisors "assigning corrective action". The owner rule is build small, cut large: one
status step with a sync operation is small, while scheduling, reassignment or crews are not
in the spec and are not built.

Tradeoff: the old values (Assigned, InProgress, Complete) are gone. Stores written by earlier
builds still load: `ActionStatusJsonConverter` reads Assigned and InProgress as Dispatched and
Complete as Closed, so the actions file is never set aside for them. There is no reassignment
from Findings; the assignee is chosen when the action is raised.

Applies to: Findings, the asset's Open findings (closed work drops off), Sync.
Source: COMPLETENESS.md Major "Supervisor dispatch is missing".

### D-002 · 2026-11-04 · Recorded photos are shown again; comparing them is cut (proposed)

Decision: a photo recorded with an inspection is shown again in three places: as a thumbnail
on its History entry, as "Photos recorded" on the point's last-reading tile when the point is
opened, and full size in a viewer that either thumbnail opens. Side-by-side comparison of
photos over time (the spec's Nice to Have "compare-over-time image scrubber") is not built,
and no screen or copy offers it.

Reason: "Evidence capture/review" is a Must Have screen, and a photo used to disappear once its
point was completed. Showing it again is small. A comparison view is a separate large
feature, and the owner rule is build small, cut large. Comparing measurements is served by
History, which lists every reading at each point with its photos, newest first.

Tradeoff: to compare two photos a technician opens them one after the other. The seed's
failed seal inspection carries one of the bundled sample frames, so the review is visible on a
fresh install.

Applies to: History, the point inspector, the asset page (viewer layer).
Source: COMPLETENESS.md Major "Evidence review is missing".

### D-003 · 2026-11-04 · The Documents tab shows the records the device holds; manuals are cut (proposed)

Decision: Documents shows the asset's nameplate and its inspection plan (each point, what is
read there, the expected band and the guidance), all read from the asset record on the
device. The fixed "No documents on this device / Manuals and drawings sync with the asset
record" tile is removed.

Reason: the tile was the same string for every asset, and TRACE has no document model or
document sync (the spec's data models have none). Manuals and drawings would be a large
feature: a model, storage, a viewer and sync. Build small, cut large: the tab shows what
exists and promises nothing else.

Tradeoff: no manuals or drawings. The tab keeps its spec name, Documents.

Applies to: the asset's Documents tab.
Source: COMPLETENESS.md Major "The Documents tab is Simulated".

### D-004 · 2026-11-04 · The offline conflict dialog is cut while the sync transport is simulated (proposed)

Decision: the spec's "Offline conflict dialog" (`SyncConflictDialog`) is not built. No screen
or copy offers to resolve a conflict.

Reason: TRACE has no server, by design (CLAUDE.md, UNO-BUILD-SPEC: deterministic simulated
implementations), so nothing on another device can change a record this one also changed, and
the simulated transport never reports a conflict. A dialog that no path can open would be
dead UI; a fake conflict mode to open it would be a large feature (a conflict model, a merge
rule, a resolution write and its sync operation) built only to show itself. Build small, cut
large. The completeness audit listed it as N/A without recording the cut; this records it.

Tradeoff: the day a real transport lands, it brings the conflict case with it, and this dialog
with it. Until then every write is the device's own and syncs as queued.

Applies to: Sync, the transient layers. Source: COMPLETENESS.md Unresolved Question "The sync
conflict dialog", A pass 2 review.
