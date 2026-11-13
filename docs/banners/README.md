# TRACE gallery banners

Banners for the Uno Platform Studio samples gallery, made from real captures of **TRACE** (`trace`, branch `studio`,
Uno.Sdk 7.0.0-dev.132) running as a Release WebAssembly build in headless Chromium. Nothing in them is redrawn: the
UI, the 3D equipment, the data and the copy are the app's seeded P-204 scenario plus one inspection round recorded
through the UI as a technician would. Styling is only around and on top of the captures: CSS device frames, crop,
perspective, falloff masks, blur, light and backdrop.

Open `index.html` for the contact sheet.

## Finals

All in `finals/`: every banner at 1x (1200 × 630) and 2x (2400 × 1260), and two heroes at 1920 × 1080.

All captures come from **one capture session** at **DPR 3** (`src/tools/session.json`), resized live between desktop
1600×1000, tablet 1366×1024 and phone 390×844. TRACE ignores the browser colour scheme and starts Dark; Light comes
from the theme switch on Today, pressed in the session. Stored at 3x or downscaled to 2x (`src/tools/crop.py`).

| Banner | Family | What it shows | Source captures (state · theme · viewport · DPR) |
|---|---|---|---|
| `showcase-desk` | showcase | Desktop window with P-204's Inspect tab, the Seal housing reading ready to complete (Dark), between a Today phone and a Motor body point-sheet phone (Light), on a day backdrop | P-204 › Inspect › Seal housing draft · Dark · 1600×1000 · 3 (stored 2x) ; Today · Light · 390×844 · 3 ; P-204 › Motor body draft · Light · 390×844 · 3 |
| `showcase-phones` | showcase | Three phones after the round: Today with 5 changes waiting (Dark), the Sync queue (Light, centre, raised), Findings with the new corrective action (Dark), on field green | Today (after) · Dark ; Sync · Light ; Findings · Dark · all 390×844 · 3 |
| `showcase-daynight` | showcase | Two field tablets turned toward each other: Today's round (Light) and the pump inspection (Dark), on a day-to-night green | Today · Light · 1366×1024 · 3 ; P-204 › Inspect › Seal housing draft · Dark · 1366×1024 · 3 |
| `focus-pump` (+ hero) | focused | The CPU-drawn 3D centrifugal pump P-204 with its four inspection points (1 Motor body Pass, 2 Seal housing Fail, 3 Coupling Watch, 4 Drive-end bearing Pass), laid back 9° and let go into the night | P-204 › Inspect, default view · Dark · 1600×1000 · 3, crop 1180×626 CSS px |
| `focus-inspector` | focused | The point inspector at the moment of the call: note, fresh photo, 7.6 mm/s, Fail, Out of range, Complete point; the pump behind it out of focus | P-204 › Seal housing draft · Dark · 1600×1000 · 3, inspector crop 418×898 CSS px (3x), scene crop (2x) |
| `focus-findings` | focused | Findings' corrective work: the new High action raised from the seal housing (Open, Dispatch) above yesterday's (Dispatched, Close); "Assets needing attention" blurred behind | Findings (after the round) · Light · 1600×1000 · 3, crops 622×394 (3x) and 622×446 (2x) CSS px |
| `focus-sync` | focused | The offline queue: four inspections and the corrective action, each Pending, laid back from its bottom edge | Sync (after the round) · Dark · 1600×1000 · 3, crop 978×575 CSS px |
| `stylized-split` (+ hero) | stylized | One inspection in Dark and Light, cut on a single diagonal through window and backdrop | P-204 › Seal housing draft · Dark, then Light (same draft, same session) · 1600×1000 · 3 (stored 2x), page to y 990 |
| `stylized-fleet` | stylized | The round's three machines in 3D as a cover flow: C-07 compressor (left) and AHU-12 air handler (right) turned away, P-204 in front, on a grid floor | P-204, C-07, AHU-12 › Inspect, default views · Dark · 1600×1000 · 3, scene card 1462×781 CSS px (2x) |
| `stylized-flow` | stylized | The round phone by phone, Light and Dark alternating on one turned plane: Today, Motor body point, Sync, Findings, Today again | Today (before, after) · Light ; Motor body draft · Dark ; Sync · Light ; Findings · Dark · all 390×844 · 3 |
| `focus-pump-titled` | titled (**optional**) | `focus-pump` with the display name TRACE in Roboto Medium | as `focus-pump` |

Strongest: **`focus-pump`** and **`stylized-split`** (both have heroes).

- `focus-pump` is the one thing only TRACE has: the machine itself as the way in, drawn by the app's own CPU 3D
  pipeline, with the Pass / Watch / Fail markers pinned to it. It reads at thumbnail size and stays sharp at 2x
  (the 3x crop is shown at under 0.6 of its native pixels, even in the hero).
- `stylized-split` tells the product in one frame: the machine on the left, the decision on the right (reading,
  Fail, Out of range, Complete point), and both themes, with every value agreeing because it is one draft.

Names follow Cargo's where they fit. TRACE has no section stack or wall of sections to show, so Cargo's
`stylized-stack` and `stylized-mosaic` became `stylized-fleet` (the three machines) and `stylized-flow` (one round,
phone by phone).

## Session and state

The session recorded one round on P-204 through the UI: Seal housing 7.6 mm/s, Fail, a note ("Seal weeping at the
gland, drive end vibration up.") and a photo from the simulated camera; Motor body 73 °C, Pass; then Complete point,
Create corrective action (High, Mechanical Team), Coupling 1.8 mm Watch and Drive-end bearing 66 °C Pass. Captures
marked "draft" or "before" come from before the round was completed, "after" from after; within each banner the
captures agree. The app shows the real date (Monday 9 November) and stamps the photo 3:24 PM; Findings says "3 min
ago" for the new action.

## Palette

Backdrops, glows and the seam come from `Trace/Styles/ColorPaletteOverride.xaml` and the no-role colours in
`Trace/App.xaml`; they are listed with their roles in `src/frames.css`:

- Dark: Background #08100F, Surface #101A18, SurfaceVariant #17231F, OutlineVariant #1E2E29, Outline #2C403A,
  OnSurface #E8F3EE, Primary #6EE7AE (also Success, Pass), PrimaryContainer #17352C, PrimaryVariantDark #0C6B48,
  Secondary #7FC9FF, SecondaryContainer #123449, Error #FF7A70 (Fail).
- Light: Background #EFF4F1, SurfaceVariant #E4ECE8, OutlineVariant #C7D6D1, PrimaryContainer #9FF2CB, OnSecondary-
  Container #001E2E, WatchContainer #FFDEA6 (App.xaml), ErrorContainer #FFDAD5.
- The intermediate gradient stops (#0B1513, #0E1916, #0F1C19, #13211D, #0D1F1A, #1A3D32, #0B2738, #F4F8F6, #F2F6F4,
  #EEF4F1, #E9F0EC, #E1ECE6, #DFE8E3, #9FD7BC) are mixes of the values above, used so gradients pass between palette
  colours without going grey.
- The backdrop motif is TRACE's own: a surveyor's grid (field-notebook squares) in OutlineVariant.
- The titled variant uses Roboto Medium (`src/fonts/`, from the `Uno.Fonts.Roboto` package the app ships): TRACE
  uses Material's type styles and has no display face of its own.

## Rules kept

- **Real pixels.** Every screen is a full-page or element-box capture of the running app; nothing is retouched.
  The device frames are plain CSS: a window with a neutral bar, and graphite phones and tablets with no notch,
  camera, button or logo.
- **No enlarging.** Captures are taken at DPR 3, stored at 3x or downscaled to 2x, and every composition shows them
  at or below native pixels in the 2x export and the heroes, including the near edge of each perspective (perspective
  planes pivot on the edge that stays at 1:1, or stay below 0.9 of native).
- **Clean states.** Seeded data, every frame captured only once two frames 1.2 s apart were identical, pointer parked
  in the empty top-right corner. No spinners, focus rings or scroll indicators.
- **Safe area.** Key content sits inside the central 90%.
- **No third-party brands.** None appear in the app's content. No real people: the only person name in the app
  (the technician "Alex R." in History) is in no banner.

## Rebuild

`src/*.html` are the compositions (one per banner, heroes as their own files), `src/cap/` the captures they use,
`src/frames.css` the shared frames and palette, `src/fonts/` the titled variant's face.

1. Publish: `dotnet publish Trace/Trace.csproj -f net10.0-browserwasm -c Release -p:TraceTargetFrameworks=net10.0-browserwasm -o <dir>`,
   serve `<dir>/wwwroot` with `python3 -m http.server`.
2. Capture: `node src/tools/cap.js <url> <raw dir> 1600x1000 3 src/tools/session.json` replays the session
   (`--serve <port>` instead of the steps file drives it interactively). Fresh browser storage is required: the app
   keeps its data and theme on the device.
3. Crop: `python3 src/tools/crop.py <raw dir> src/cap`.
4. Render: `sh src/tools/render-all.sh` (Playwright; `NODE_PATH` must reach `playwright`).

## Uncertain

- Text inside the full-window device frames (`showcase-desk`, `showcase-daynight`, `stylized-split` at 1x, the
  phones in `stylized-flow`) renders below 10 px at 1x, as in the house-style references; the focused banners carry
  the readable text. At 2x and in the hero it reads.
- On the WebAssembly build, a navigation rail or bottom-bar item that was pressed kept a pressed/selected highlight
  after navigating elsewhere (for example Sync still lit while Today is shown). Captures showing it were retaken after
  a cancelled press on the item cleared it; the published frames show only the current item selected. This may be a
  headless-pointer artifact rather than an app defect.
- Relative times move on during the session; the two Findings captures used (Light desktop, Dark phone) were taken
  minutes apart and both read "3 min ago" for the new action, so they agree, but only to the minute.
- `stylized-fleet` repeats the pump of `focus-pump`; C-07's fourth point is hidden behind the pump's card.
