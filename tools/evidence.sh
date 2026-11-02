#!/usr/bin/env bash
# Evidence matrix: every destination and transient layer at the breakpoint widths, in Dark and Light,
# from a Debug desktop build (the --trace-* hooks are Debug only). Builds nothing: run
#   dotnet build Trace/Trace.csproj -c Debug -f net10.0-desktop -p:TraceTargetFrameworks=net10.0-desktop
# first. Raw captures go to <work dir>; one contact sheet per route and theme goes to docs/evidence/ as a JPEG.
#
#   tools/evidence.sh <work dir> [route ...]     routes default to the full list below
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK=${1:?work dir}; shift
ROUTES=("$@")
# Empty and Error are Today with an empty round, and with store reads failing (the Try again state);
# InspectError is the asset's Inspect tab with its record unreadable.
[ ${#ROUTES[@]} -eq 0 ] && ROUTES=(Today Assets Scan Asset Point Discard Action Asset:History Sync Findings Empty Error InspectError)
WIDTHS="360 599 600 800 1080 1920"
SHEETS="$ROOT/docs/evidence"
mkdir -p "$WORK" "$SHEETS"

for theme in dark light; do
  for route in "${ROUTES[@]}"; do
    name="$(echo "$route" | tr ':' '-' | tr 'A-Z' 'a-z')-$theme"
    args=(--trace-theme=$theme)
    case "$route" in
      Today) ;;
      Empty) args+=(--trace-seed=empty) ;;
      Error) args+=(--trace-fault=load) ;;
      InspectError) args+=(--trace-fault=load --trace-route=Asset) ;;
      *) args+=(--trace-route=$route) ;;
    esac
    "$ROOT/tools/matrix.sh" "$WORK" "$name" "$WIDTHS" 640 16 -- "${args[@]}" \
      || "$ROOT/tools/matrix.sh" "$WORK" "$name" "$WIDTHS" 640 16 -- "${args[@]}"
    grep VERIFY "$WORK/$name.log" | sed "s/^/  $name: /"
    # Each capture at half size, labelled with its width, in one row.
    tiles=()
    for w in $WIDTHS; do
      convert "$WORK/$name-$w.png" -resize 50% -background '#777' -fill white -gravity north -splice 0x18 \
        -pointsize 13 -annotate +0+2 "${w} px" "$WORK/.tile-$w.png"
      tiles+=("$WORK/.tile-$w.png")
    done
    montage "${tiles[@]}" -tile "${#tiles[@]}x1" -geometry +3+0 -background '#777' -quality 72 "$SHEETS/$name.jpg"
    rm -f "${tiles[@]}"
  done
done
echo "EVIDENCE-DONE"
