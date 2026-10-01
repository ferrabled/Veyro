#!/usr/bin/env bash
# Builds the 20 s reel end to end. Run from this folder in Git Bash.
#   ./build.sh          full quality (4-sample motion blur, ~20 min)
#   ./build.sh draft    no motion blur (~5 min)
set -euo pipefail
cd "$(dirname "$0")"
REPO=../../..
SUB=4; [ "${1:-}" = draft ] && SUB=1

# 1. inputs: product art and the game's CC0 SFX are copied from the repo; fonts (OFL) are fetched
mkdir -p img sfx fonts out
cp "$REPO"/screenshots/guide/camera_0{2,4,5}.png img/
cp "$REPO"/site/public/art/skin-{ember,frost,prism}.webp img/
cp "$REPO"/game/Assets/Resources/Audio/Sfx/*.ogg sfx/
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36"
fetch() { [ -s "fonts/$1" ] || curl -sfL -A "$UA" -o "fonts/$1" "$2"; }
fetch anybody.woff2       https://fonts.gstatic.com/s/anybody/v13/VuJxdNvK2Ib2ppdWSKHdOQ.woff2
fetch atkinson-400.woff2  https://fonts.gstatic.com/s/atkinsonhyperlegible/v12/9Bt23C1KxNDXMspQ1lPyU89-1h6ONRlW45G04pIo.woff2
fetch atkinson-700.woff2  https://fonts.gstatic.com/s/atkinsonhyperlegible/v12/9Bt73C1KxNDXMspQ1lPyU89-1h6ONRlW45G8Wbc9dCWP.woff2
fetch fragment-mono.woff2 https://fonts.gstatic.com/s/fragmentmono/v6/4iCr6K5wfMRRjxp0DA6-2CLnB4NHhg.woff2

# 2. tools
[ -d node_modules ] || npm install --silent
[ -d .venv ] || { python -m venv .venv && .venv/Scripts/python -m pip install -q numpy scipy; }

# 3. picture (also writes out/events.json, the cue list the score is cut to), then score, then mux
node render.mjs video --sub "$SUB" --out video_silent.mp4
.venv/Scripts/python music.py
ffmpeg -v error -y -i out/video_silent.mp4 -i out/score.wav -map 0:v -map 1:a -c:v copy \
  -af "loudnorm=I=-14:TP=-1:LRA=11" -ar 48000 -c:a aac -b:a 256k -movflags +faststart -shortest \
  out/veyro-run-reel.mp4
echo "done: $(pwd)/out/veyro-run-reel.mp4"
