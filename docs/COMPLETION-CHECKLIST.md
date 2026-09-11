# TRACE — Definition of Done

## Screens and navigation
- [ ] Today list opens with realistic assigned assets.
- [ ] Asset cards open the correct asset.
- [ ] Back returns to the previous context without losing an in-progress inspection.
- [ ] Inspect tab exposes all configured inspection points.
- [ ] History tab shows previous events ordered correctly.
- [ ] Sync destination exposes pending, failed and synced operations.
- [ ] No screen leads to an unimplemented dead end.

## Asset inspection
- [ ] Every inspection point has a visible target and label.
- [ ] Selecting a point opens the correct point detail.
- [ ] The latest reading, unit and expected range match the point model.
- [ ] Entered readings are numeric and validated.
- [ ] Out-of-range readings are visibly identified.
- [ ] Pass, Watch and Fail can all be selected.
- [ ] Fail requires a note or evidence before completion.
- [ ] Completed inspection updates the selected point immediately.
- [ ] Completed inspection appends a history event.

## Corrective action
- [ ] A failed point can create a corrective action.
- [ ] Source asset, point and inspection event are carried automatically.
- [ ] Priority and assignee are selectable.
- [ ] Summary is required.
- [ ] Confirmation is shown after creation.
- [ ] The action remains present after navigating away and returning.

## Evidence
- [ ] Evidence surface can be opened from the inspection point.
- [ ] Camera-denied state has a recovery path.
- [ ] At least one realistic example image is shown.
- [ ] Long notes wrap without covering actions.
- [ ] Empty evidence state is explicit.

## Offline and persistence
- [ ] Toggling offline does not block inspection completion.
- [ ] Offline writes create queued sync operations.
- [ ] Pending-sync count updates immediately.
- [ ] Returning online provides a sync action.
- [ ] Successful sync clears pending count.
- [ ] Failed sync state can be retried.
- [ ] Refreshing the prototype preserves completed events and actions via localStorage.

## States and edge cases
- [ ] Loading state exists.
- [ ] Empty assignments state exists.
- [ ] Populated state exists.
- [ ] Asset-not-found state exists.
- [ ] Disabled action state is visually distinct.
- [ ] Error feedback explains what must be corrected.
- [ ] Long-content sheet remains scrollable.
- [ ] Repeated taps do not create duplicate inspection events.

## Input and accessibility
- [ ] Touch targets are at least approximately 44px.
- [ ] Primary tasks are usable with touch.
- [ ] Keyboard activation works for buttons.
- [ ] Focus is visible.
- [ ] Status is never communicated through color alone.
- [ ] Text maintains usable contrast in dark and light themes.

## Responsive/adaptive
- [ ] 360–430px phone widths remain fully usable.
- [ ] Tablet width exposes wider asset workspace without stretched controls.
- [ ] Desktop width provides denser supervisor-friendly layout.
- [ ] No horizontal scrolling is required for core task flow.

## Product polish
- [ ] No lorem ipsum.
- [ ] Asset names, units and readings are plausible.
- [ ] Components use one spacing and type system.
- [ ] Inspection-point markers feel integrated with the asset surface.
- [ ] Motion supports selection/state changes without delaying work.
- [ ] No generic dashboard cards are used where the asset itself can carry the information.
