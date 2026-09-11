# Build TRACE in Uno Platform

Build the application in this package as a complete .NET Uno Platform app.

Read, in order: PROJECT-BRIEF.md, PRODUCT-SPEC.md, UNO-BUILD-SPEC.md, COMPLETION-CHECKLIST.md, DESIGN-GRAPH.md, prototype.html, PROTOTYPE-VERIFICATION.md.

## Rules
- Written specifications are source of truth.
- Complete Must Have before Nice to Have.
- Do not recreate screenshots only. Implement navigation, state, models, persistence, validation, overlays and complete workflows.
- Prefer existing Uno/WinUI/toolkit controls. Do not custom-draw ordinary controls.
- Build custom UI only for product-defining surfaces explicitly identified in UNO-BUILD-SPEC.md.
- Use realistic seed data.
- Keep hardware/backends behind interfaces and provide deterministic simulated implementations where integrations are outside scope.
- Make primary targets functional before expanding platforms.
- Implement deliberate responsive/adaptive compositions.
- Add tests for domain rules and critical state transitions.
- Do not mark a checklist item complete because a screen merely exists.

## Final pass
Run the app on the required Tier-1 targets. Evaluate every item in COMPLETION-CHECKLIST.md as Complete, Partially Complete, Missing or Not Applicable. Resolve all incomplete Must Have items. Then perform a normalization, harmonization, hardening and polish pass and update DESIGN-GRAPH.md to match what is actually implemented.
