# TRACE — Real Equipment Assets

Replace the parametric equipment models with imported glTF assets while keeping one
renderer on every target. This is the spec for the first slice: the import pipeline, the
anchor contract, and the renderer changes that let it scale.

## Architecture Brief

**Module structure**
- `Presentation/Scene3D/GltfEquipmentLoader.cs` — reads a `.glb` stream into the existing
  `SceneModel` (parts + anchors). Pure, synchronous, no UI types.
- `Presentation/Scene3D/EquipmentModels.cs` — stays as the fallback for assets without a
  model file, and as the source for the round-trip test asset.
- `Presentation/Controls/AssetSceneView.cs` — gains a `ModelSource` dependency property
  (an `ms-appx:///` URI string). When set, the view loads the file off the UI thread,
  swaps the model in, and invalidates. Until it arrives, and if it fails, the parametric
  model for `Kind` is drawn. This mirrors how `Image.Source` behaves: the surface owns
  loading its own content.
- `Domain/Models/Asset.cs` — gains `string? ModelUri`. Null means "no file, draw the
  parametric model", which keeps every persisted `assets.json` readable.

**State model**
- No new MVUX state. `AssetModel.ModelUri` is a derived `IFeed<string>` off `Asset`;
  empty string means none. The view holds the loaded `SceneModel` as a private field —
  it is render data, not app state.

**Data flow**
- Seed → `Asset.ModelUri` → `AssetModel.ModelUri` feed → `AssetSceneView.ModelSource` →
  `StorageFile.GetFileFromApplicationUriAsync` → `GltfEquipmentLoader.Load(stream)` →
  `SceneModel` → `SceneRenderer`.
- Anchors flow from named nodes in the file, so the asset owns its inspection points.

**Anchor contract**
- A glTF node whose `Name` equals an `InspectionPoint.Id` and that carries no mesh is an
  anchor; its world-space translation is the anchor position. Any node name that is not
  a point id is ignored. Missing anchors fall back to nothing — the marker parks
  off-screen, which is the existing behaviour for an unknown id.

**Geometry contract**
- Every mesh primitive with `POSITION` and triangle topology becomes one `MeshPart`,
  transformed by its node's `WorldMatrix`. Material albedo comes from the PBR
  `BaseColor` factor; textures are ignored (the shader is flat-shaded with lighting).
- glTF is right-handed, Y-up, metres, counter-clockwise front faces — the same
  conventions the renderer already assumes, so no axis flip is applied.

**Dependencies**
- `SharpGLTF.Core 1.0.6` (MIT; ships `net10.0` and `netstandard2.0`; depends only on
  `System.Text.Json`). Decision recorded below.

**Platform constraints**
- The renderer remains a CPU rasteriser over `SKVertices` with a painter's sort and no
  depth buffer. That sets the asset budget: roughly 8–15k triangles, parts kept convex
  and non-interpenetrating. Real assets must be prepared to that budget; the pipeline
  does not decimate.
- The view draws through `SKCanvasElement` on every Skia-rendered head (desktop, Android,
  iOS, WebAssembly) and through `SKXamlCanvas` on the WinAppSDK head, the only build that
  compiles that fallback in. `SKCanvasElement` hands over the window canvas in logical
  units with the display scale already applied, so the renderer takes a pixel-density
  argument and sizes its supersampled surface for real pixels; `SKXamlCanvas` reports
  device pixels directly.

**Testing**
- Round trip: export the parametric pump through the glTF writer, import it, and assert
  the triangle count, part count and all four anchor ids match.
- Foreign file: import a Khronos sample locally (not committed — licence) and assert it
  yields triangles and renders non-background pixels through the real renderer.
- Renderer: an offscreen render test that draws a model into an `SKSurface` and asserts
  the frame is not blank, so the allocation refactor is covered.
- Runtime: the app draws P-204 from `Assets/Models/p-204.glb` with markers anchored from
  the file's nodes, verified by capture on the desktop head.

## Design Brief

- No visual change is intended. A file-sourced pump must be indistinguishable from the
  parametric one at rest; that is the acceptance bar for the round trip.
- Material albedo maps from the file's base colour factor; gloss stays a per-part
  constant (`Material.Machined`) since glTF roughness is not yet read.
- Loading is silent: the parametric model is drawn until the file arrives, which on a
  bundled asset is a few milliseconds. No spinner.

## Interaction Brief

- No new interaction. Orbit, zoom, marker selection and the inspector are unchanged.
- Failure state: an unreadable or missing file logs a warning and leaves the parametric
  model in place. The technician is never shown an empty viewport.
- Runtime verification: open P-204, confirm the model and marker positions match the
  parametric capture; open a second asset with no `ModelUri` and confirm the fallback.

## Implementation Plan

1. `MeshPart` precomputes per-face normals; `SceneRenderer` caches its surface, paints,
   mask filter and path, stops copying the vertex buffers per frame, and disposes on
   `Unloaded`. This is the prerequisite: every one of those costs scales with triangles.
2. Add `SharpGLTF.Core`; write `GltfEquipmentLoader.Load(Stream) → SceneModel`.
3. Add a small `GltfEquipmentWriter` used by tests and by a one-off export of the
   parametric pump to `Assets/Models/p-204.glb`.
4. `Asset.ModelUri`; seed P-204 with the bundled file; `AssetModel.ModelUri` feed;
   `AssetSceneView.ModelSource` with async load and fallback.
5. Tests: round trip, foreign file (local), offscreen render.
6. Runtime capture on desktop; commit.

## Decisions

Decision: import glTF 2.0 via SharpGLTF.Core.
Reason: the runtime-delivery standard, one managed dependency, ships `net10.0`, and
lets a modeller place inspection points as named nodes so the asset file owns them.
Tradeoff: a new package; its System.Text.Json use is reflection-based and will need a
trimming check on the Android head.

Decision: keep the Skia CPU renderer for this slice.
Reason: it is the only renderer that runs unchanged on every target including Android.
Tradeoff: no depth buffer, so real assets must be prepared to a budget rather than
dropped in raw.

Decision: the view loads its own model file, like `Image.Source`.
Reason: geometry is render data, not application state; routing it through MVUX would
generate bindable proxies for `SceneModel` and its arrays.
Tradeoff: a control performs IO. It does so off the UI thread and only for `ms-appx`.

## Status

Implemented and verified on the desktop head (2026-09-12):
- P-204 loads from `Assets/Models/p-204.glb` — 5,404 triangles, 20 parts, 4 inspection
  points from named nodes — and is indistinguishable from the parametric pump.
- AHU-12 (no `ModelUri`) draws the parametric air handler with no load attempt.
- A foreign Khronos sample (Duck, 4,212 triangles, one part, no anchors) loaded and
  rendered in the app from a local copy; it is not committed, for licence reasons. It
  drew white: its colour lives in a texture and its base colour factor is (1, 1, 1), which
  is exactly the "textures are ignored" limitation above made visible. Assets prepared for
  TRACE should carry their colour in the base colour factor, or the loader needs to sample
  the base colour texture.
- Round trip, anchor, material, offscreen render, dispose-and-reuse and frame-cost tests
  cover the loader and the renderer.
- The view draws through `SKCanvasElement` on the desktop head (the log reports
  `drawing through WindowCanvasSurface`). Against the `SKXamlCanvas` frame of the same
  scene, 1.9% of pixels differ, all along triangle edges and none beyond a 12% tolerance
  except 17 — anti-aliasing at facet boundaries from the direct blit, with silhouette,
  scale and every marker position identical. Orbit by drag works through the host.

## Unresolved Questions

- Asset provenance. Manufacturer CAD → Blender decimation → `.glb` is the expected
  pipeline; who prepares them, and under what licence for a public sample repo?
- Should the loader read glTF roughness/metallic into `Material.Gloss`, or is a per-part
  constant enough for the flat-shaded look?
- The Android and WebAssembly heads build, but the loader has not been run on them.
  SharpGLTF.Core reports trim warnings (IL2104) in the WebAssembly publish, so the
  WebAssembly linker config keeps it whole; Android has not had a trimmed runtime check.
- The WinAppSDK fallback (`SKXamlCanvas`) is compiled by CI's WinAppSDK build check but
  has not been run; it is the path that was in use before the switch, unchanged apart
  from the plumbing.
