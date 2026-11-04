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
