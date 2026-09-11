#!/usr/bin/env bash
# Runs the TRACE desktop head on a virtual display, optionally drives it, and captures it.
#
#   tools/run-desktop.sh <output.png> [width] [height] [settle] [step ...]
#
# Steps are applied in order before the capture:
#   click:X,Y   press at window coordinates
#   key:NAME    send a key (xdotool syntax, e.g. Tab, Return)
#   type:TEXT   type text
#   wait:N      pause N seconds
#
# Two things are deliberate here:
#   * Hot Design is switched off. UseStudio never creates a window when no DevServer is
#     reachable, so a capture run would hang with no visible error.
#   * The window is resized through X, twice. The programmatic AppWindow.Resize races the
#     Skia surface at startup and intermittently draws the layout against the old surface;
#     going through the window manager, with a second nudge once the app has settled,
#     always leaves frame and content in agreement.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/artifacts/trace.png}"
WIDTH="${2:-1280}"
HEIGHT="${3:-860}"
SETTLE="${4:-8}"
shift 4 2>/dev/null || shift $#
STEPS=("$@")

DISPLAY_NUM="${DISPLAY_NUM:-99}"
SCREEN_W=$((WIDTH + 80))
SCREEN_H=$((HEIGHT + 80))

mkdir -p "$(dirname "$OUT")" "$ROOT/artifacts"
rm -f "$OUT"

# TRACE_RESET=1 clears the app's local storage so the run starts from a fresh seed. Without
# it a capture shows whatever previous runs left behind, which is the right default for
# checking that state persists but the wrong one for checking the seed.
# Settings/Local.dat holds ApplicationData settings, which is where the theme preference
# lives, so it has to go too or a capture inherits the last run's theme.
if [[ "${TRACE_RESET:-0}" == "1" ]]; then
    rm -rf "$HOME/.local/share/Trace/com.unoplatform.trace/LocalState/trace"
    rm -f "$HOME/.local/share/Trace/com.unoplatform.trace/Settings/Local.dat"
fi

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

# Uno's Skia host does not publish _NET_WM_PID, so find the window by its title.
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
xdotool windowsize "$WIN" "$WIDTH" "$HEIGHT"
sleep 4
xdotool windowsize "$WIN" "$WIDTH" $((HEIGHT - 1))
sleep 1
xdotool windowsize "$WIN" "$WIDTH" "$HEIGHT"
sleep "$SETTLE"

for step in "${STEPS[@]:-}"; do
    [[ -z "$step" ]] && continue
    action="${step%%:*}"
    value="${step#*:}"

    case "$action" in
        click)
            # Move the real pointer and click. Window-targeted synthetic events are not
            # seen by the Uno Skia input stack; events through the pointer are.
            xdotool mousemove "${value%%,*}" "${value##*,}"
            sleep 0.4
            xdotool click 1
            sleep 2
            ;;
        drag)
            IFS=',' read -r x1 y1 x2 y2 <<<"$value"
            xdotool mousemove "$x1" "$y1"
            sleep 0.3
            xdotool mousedown 1
            # Move in steps: a press-and-jump reads as a click with no movement.
            for f in 1 2 3 4 5 6 7 8; do
                xdotool mousemove $(( x1 + ((x2 - x1) * f / 8) )) $(( y1 + ((y2 - y1) * f / 8) ))
                sleep 0.08
            done
            sleep 0.3
            xdotool mouseup 1
            sleep 1.5
            ;;
        scroll)
            # scroll:X,Y,N — N wheel notches at that point, negative scrolls up.
            IFS=',' read -r sx sy notches <<<"$value"
            xdotool mousemove "$sx" "$sy"
            sleep 0.3
            button=5
            if (( notches < 0 )); then button=4; notches=$(( -notches )); fi
            for _ in $(seq 1 "$notches"); do
                xdotool click "$button"
                sleep 0.15
            done
            sleep 1
            ;;
        key)
            xdotool key "$value"
            sleep 1.5
            ;;
        type)
            xdotool type --delay 60 "$value"
            sleep 1.5
            ;;
        wait)
            sleep "$value"
            ;;
        *)
            echo "Unknown step '$step'" >&2
            exit 1
            ;;
    esac
done

if ! kill -0 "$APP_PID" 2>/dev/null; then
    echo "The app exited before it could be captured. Tail of $LOG:" >&2
    tail -40 "$LOG" >&2
    exit 1
fi

import -display ":$DISPLAY_NUM" -window "$WIN" "$OUT"
echo "Captured $OUT ($(identify -format '%wx%h' "$OUT"))"
