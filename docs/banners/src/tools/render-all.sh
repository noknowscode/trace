#!/bin/sh
# Renders every banner at 1x and 2x, and the heroes at 1920 x 1080, into ../finals (run from src/tools).
# NODE_PATH must reach playwright.
set -e
cd "$(dirname "$0")/.."
args=""
for n in showcase-desk showcase-phones showcase-daynight focus-pump focus-inspector focus-findings focus-sync stylized-split stylized-fleet stylized-flow focus-pump-titled; do
  args="$args $n.html ../finals/$n@1x.png 1200 630 1 $n.html ../finals/$n@2x.png 1200 630 2"
done
for n in focus-pump stylized-split; do
  [ -f "$n-hero.html" ] && args="$args $n-hero.html ../finals/$n-hero.png 1920 1080 1"
done
node tools/render.js $args
