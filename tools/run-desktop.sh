#!/usr/bin/env bash
# Launches the TRACE desktop head on a virtual display, sizes its window, and captures it.
#
#   tools/run-desktop.sh <output.png> [width] [height] [settle-seconds]
#
# Two things are deliberate here:
#   * Hot Design is switched off. UseStudio never creates a window when no DevServer is
#     reachable, so a capture run would hang with no visible error.
#   * The window is resized through X rather than through AppWindow.Resize. The programmatic
#     resize races the Skia surface at startup and intermittently renders the layout clipped
#     against the old surface; going through the window manager path always relayouts.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/artifacts/trace.png}"
WIDTH="${2:-1280}"
HEIGHT="${3:-860}"
SETTLE="${4:-9}"
DISPLAY_NUM="${DISPLAY_NUM:-99}"
SCREEN_W=$((WIDTH + 80))
SCREEN_H=$((HEIGHT + 80))

mkdir -p "$(dirname "$OUT")" "$ROOT/artifacts"
rm -f "$OUT"

cleanup() {
    [[ -n "${APP_PID:-}" ]] && kill "$APP_PID" 2>/dev/null || true
    [[ -n "${XVFB_PID:-}" ]] && kill "$XVFB_PID" 2>/dev/null || true
    wait 2>/dev/null || true
}
trap cleanup EXIT

Xvfb ":$DISPLAY_NUM" -screen 0 "${SCREEN_W}x${SCREEN_H}x24" -nolisten tcp >/dev/null 2>&1 &
XVFB_PID=$!
sleep 1

export DISPLAY=":$DISPLAY_NUM"
export TRACE_NO_HOTDESIGN=1

LOG="$ROOT/artifacts/trace-run.log"
dotnet "$ROOT/Trace/bin/Debug/net10.0-desktop/Trace.dll" >"$LOG" 2>&1 &
APP_PID=$!

# Wait for the window to exist before touching it. Uno's Skia host does not publish
# _NET_WM_PID, so the window is found by its title rather than by process id.
WIN=""
for _ in $(seq 1 60); do
    WIN="$(xdotool search --name '^Trace$' 2>/dev/null | head -1 || true)"
    [[ -n "$WIN" ]] && break
    sleep 0.5
done

if [[ -z "$WIN" ]]; then
    echo "No window appeared for the app. Tail of $LOG:" >&2
    tail -40 "$LOG" >&2
    exit 1
fi

xdotool windowmove "$WIN" 0 0

# The Skia surface can trail the first resize, leaving the layout drawn against the old
# surface. A second nudge after the app has settled always lands, so the capture is of a
# window whose frame and content agree.
xdotool windowsize "$WIN" "$WIDTH" "$HEIGHT"
sleep 4
xdotool windowsize "$WIN" "$WIDTH" $((HEIGHT - 1))
sleep 1
xdotool windowsize "$WIN" "$WIDTH" "$HEIGHT"

sleep "$SETTLE"

if ! kill -0 "$APP_PID" 2>/dev/null; then
    echo "The app exited before it could be captured. Tail of $LOG:" >&2
    tail -40 "$LOG" >&2
    exit 1
fi

import -display ":$DISPLAY_NUM" -window "$WIN" "$OUT"
echo "Captured $OUT ($(identify -format '%wx%h' "$OUT"))"
