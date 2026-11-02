#!/usr/bin/env bash
# Screenshot matrix for the desktop head under Xvfb: one launch, live-resized through the widths, which also
# exercises the responsive transitions. Debug builds only (the --trace-* hooks live in App.Verification.cs).
#
#   tools/matrix.sh <out dir> <name> "<widths>" <height> <settle s> -- <app args...>
#
# Writes <out>/<name>-<width>.png per width and <out>/<name>.log (the app's output, with its VERIFY lines).
# Each run starts from a fresh store and theme preference, like TRACE_RESET=1 in run-desktop.sh. Height stays
# fixed: without a window manager, Xvfb does not apply height changes to the Uno X11 window reliably.
# After each resize the capture waits until two frames a second apart agree and the frame is painted: the X11
# host sometimes drops a resize under Xvfb and leaves the new area black, so the size is nudged when it stalls.
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT=$1; NAME=$2; WIDTHS=$3; H=$4; SETTLE=$5; shift 6
DLL="$ROOT/Trace/bin/Debug/net10.0-desktop/Trace.dll"
mkdir -p "$OUT"
rm -rf "$HOME/.local/share/Trace/com.unoplatform.trace/LocalState/trace"
rm -f "$HOME/.local/share/Trace/com.unoplatform.trace/Settings/Local.dat"

DN=$(( 100 + RANDOM % 800 )); export DISPLAY=:$DN TRACE_X11_RENDERER=software
Xvfb :$DN -screen 0 2200x1400x24 -nolisten tcp >/dev/null 2>&1 & XP=$!
sleep 1
dotnet "$DLL" "$@" > "$OUT/$NAME.log" 2>&1 & AP=$!
WID=""
for _ in $(seq 1 60); do
  WID=$(xdotool search --onlyvisible --name '^Trace$' 2>/dev/null | head -1)
  [ -n "$WID" ] && break; sleep 1
done
if [ -z "$WID" ]; then echo "no window for $NAME"; kill $AP $XP 2>/dev/null; exit 1; fi
xdotool windowmove "$WID" 0 0
xdotool windowsize "$WID" "${WIDTHS%% *}" "$H"
sleep "$SETTLE"
T="$OUT/.tmp-$NAME"; mkdir -p "$T"
for W in $WIDTHS; do
  xdotool windowsize "$WID" "$W" "$H"; sleep 2
  # Root capture cropped to the window, so anything drawn in its own X window is included.
  import -window root -crop "${W}x${H}+0+0" +repage "$T/prev.png" 2>/dev/null
  ok=0
  for i in $(seq 1 18); do
    sleep 1
    import -window root -crop "${W}x${H}+0+0" +repage "$T/cur.png" 2>/dev/null
    diff=$(compare -metric AE -fuzz 4% "$T/prev.png" "$T/cur.png" null: 2>&1 | cut -d' ' -f1)
    painted=$(convert "$T/cur.png" -fuzz 2% -fill white +opaque black -fill black -opaque black -format '%[fx:mean]' info: 2>/dev/null)
    if [ "${diff%.*}" -lt 3000 ] 2>/dev/null && awk "BEGIN{exit !($painted > 0.9)}"; then ok=1; break; fi
    if [ "$i" = 6 ] || [ "$i" = 12 ]; then xdotool windowsize "$WID" $((W+1)) "$H"; sleep 1; xdotool windowsize "$WID" "$W" "$H"; fi
    cp "$T/cur.png" "$T/prev.png"
  done
  cp "$T/cur.png" "$OUT/$NAME-$W.png"
  [ $ok = 1 ] || echo "unsettled $NAME-$W"
done
rm -rf "$T"
kill $AP 2>/dev/null; sleep 1; kill -9 $AP 2>/dev/null; kill $XP 2>/dev/null
echo "ok $NAME"
