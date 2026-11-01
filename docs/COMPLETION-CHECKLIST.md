# TRACE — Definition of Done

Verified by running the desktop head under `tools/run-desktop.sh`, which drives the real
app in Xvfb with real pointer and keyboard input and captures the frame. Captures live in
`artifacts/`. Domain rules are additionally covered by `Trace.Tests` (36 tests).

Boxes are ticked only where the behaviour was observed at runtime or is covered by a test.
Items that cannot be checked in this container are listed under "Not verified here".

## Screens and navigation
- [x] Today list opens with realistic assigned assets.
- [x] Asset cards open the correct asset.
- [x] Back returns to the previous context without losing an in-progress inspection.
      (A note typed into a point survives Today and back — `check-draft-return.png`.)
- [x] Inspect tab exposes all configured inspection points.
- [x] History tab shows previous events ordered correctly.
- [x] Sync destination exposes pending, failed and synced operations.
- [x] No screen leads to an unimplemented dead end.

## Asset inspection
- [x] Every inspection point has a visible target and label.
      (A marker on the machine and a chip in the strip below it.)
- [x] Selecting a point opens the correct point detail.
- [x] The latest reading, unit and expected range match the point model.
- [x] Entered readings are numeric and validated.
- [x] Out-of-range readings are visibly identified.
- [x] Pass, Watch and Fail can all be selected.
- [x] Fail requires a note or evidence before completion.
- [x] Completed inspection updates the selected point immediately.
- [x] Completed inspection appends a history event.

## Corrective action
- [x] A failed point can create a corrective action.
- [x] Source asset, point and inspection event are carried automatically.
- [x] Priority and assignee are selectable.
- [x] Summary is required.
- [x] Confirmation is shown after creation.
- [x] The action remains present after navigating away and returning.

## Evidence
- [x] Evidence surface can be opened from the inspection point.
- [x] Camera-denied state has a recovery path.
      (Reachable through "Simulate denial"; "Open settings" restores access —
      `check-camera-denied.png`, `check-camera-recovery.png`, `check-camera-granted.png`.)
- [x] At least one realistic example image is shown.
- [x] Long notes wrap without covering actions.
- [x] Empty evidence state is explicit.

## Offline and persistence
- [x] Toggling offline does not block inspection completion.
- [x] Offline writes create queued sync operations.
- [x] Pending-sync count updates immediately.
- [x] Returning online provides a sync action.
- [x] Successful sync clears pending count.
- [x] Failed sync state can be retried.
- [x] State survives a restart.
      (The app persists snapshots as JSON in local storage; a queued operation was still
      pending after the process was restarted — `check-persist-sync.png`. This replaces the
      prototype's localStorage requirement.)

## States and edge cases
- [x] Loading state exists.
- [x] Empty assignments state exists.
      (`check-empty-today.png`, with a route back out through Scan or Assets.)
- [x] Populated state exists.
- [x] Asset-not-found state exists.
- [x] Disabled action state is visually distinct.
- [x] Error feedback explains what must be corrected.
- [x] Long-content sheet remains scrollable.
- [x] Repeated taps do not create duplicate inspection events.
      (Completion is idempotent on the draft's client token; covered by a unit test.)

## Input and accessibility
- [x] Touch targets are at least approximately 44px.
- [x] Primary tasks are usable with touch.
- [x] Keyboard activation works for buttons.
- [x] Focus is visible.
- [x] Status is never communicated through color alone.
      (Every status chip and marker carries a glyph and a word.)
- [x] Text maintains usable contrast in dark and light themes.

## Responsive/adaptive
- [x] 360–430px phone widths remain fully usable.
      (`phone360-asset.png` at 360, `phone-asset.png` at 400.)
- [x] Tablet width exposes wider asset workspace without stretched controls.
- [x] Desktop width provides denser supervisor-friendly layout.
      (At and above 1000px the point inspector docks as a panel instead of a sheet.)
- [x] No horizontal scrolling is required for core task flow.
      (The point chip strip scrolls sideways by design; nothing else does.)

## Product polish
- [x] No lorem ipsum.
- [x] Asset names, units and readings are plausible.
- [x] Components use one spacing and type system.
- [x] Inspection-point markers feel integrated with the asset surface.
      (They are projected from 3D anchors on the solids and dim when they turn away.)
- [x] Motion supports selection/state changes without delaying work.
- [x] No generic dashboard cards are used where the asset itself can carry the information.

## Not verified here

- **Android, iOS, WebAssembly and Windows heads.** Release builds of the Android and
  WebAssembly heads compile here with zero warnings; the iOS and WinAppSDK heads compile
  only in CI (`.github/workflows/ci.yml`). None of the four has been run: the desktop head
  is the only one exercised at runtime on this machine.
- **Real hardware.** Camera, tag scanning, network state and the sync transport are
  deterministic simulations behind interfaces in `Services/`.
