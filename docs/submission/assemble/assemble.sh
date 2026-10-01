#!/usr/bin/env bash
# Assembles the <2 min Shipaton demo video from an EDL with ffmpeg only. Git Bash on Windows.
#
#   ./assemble.sh edl.tsv out/veyro-run-demo.mp4 [--captions DIR] [--cropbottom PX] [--vo vo.wav] [--music bed.wav] [--font FILE]
#
# EDL: tab-separated, one segment per line, in order. Blank lines and # comments are skipped.
#   reel   FILE                          used whole (already 1920x1080 60 fps)
#   wide   FILE  IN  OUT  [CAPTION] [CARD]   landscape room footage, trimmed IN..OUT seconds
#   phone  FILE  IN  OUT  [CAPTION] [CARD]   portrait screen recording, centred on paper, caption left
#   card   PNG   SECONDS                     a still, held, silent
# CAPTION: " · " separates lines. With --captions DIR and a CARD number, a phone segment overlays
# DIR/<CARD>.png and a wide segment DIR/<CARD>w.png (1920x1080 with alpha, from the reel pane);
# when that file is missing the caption is drawn with --font (default Bahnschrift).
# --vo   voice track mixed from the end of the reel; the segment audio is ducked under it.
# --music a music bed mixed under everything after the reel at -18 dB.
# KEEP=1 reuses segments already encoded in work/ (only the EDL order/mix changed).
# Fails if the output is 120 s or longer (Devpost cap).
set -euo pipefail

EDL=${1:?edl.tsv}; OUT=${2:?output.mp4}; shift 2
CAPDIR=""; VO=""; MUSIC=""; FONT="C:/Windows/Fonts/bahnschrift.ttf"; CROPB=0
while [ $# -gt 0 ]; do
  case "$1" in
    --captions) CAPDIR=$2; shift 2;;
    --cropbottom) CROPB=$2; shift 2;;
    --vo) VO=$2; shift 2;;
    --music) MUSIC=$2; shift 2;;
    --font) FONT=$2; shift 2;;
    *) echo "unknown option $1" >&2; exit 2;;
  esac
done

W=1920; H=1080; FPS=60
PAPER=0xFBF5E9; INK=0x12454C
VENC=(-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -r $FPS -g 120 -colorspace bt709 -color_primaries bt709 -color_trc bt709)
AENC=(-c:a aac -b:a 256k -ar 48000 -ac 2)

WORK=$(dirname "$OUT")/work; mkdir -p "$WORK" "$(dirname "$OUT")"
LIST="$WORK/concat.txt"; : > "$LIST"

dur() { ffprobe -v error -show_entries format=duration -of csv=p=0 "$1"; }
has_audio() { [ -n "$(ffprobe -v error -select_streams a -show_entries stream=index -of csv=p=0 "$1")" ]; }
# drawtext filter for a caption: text file with real newlines (no escaping games), ink on paper
caption_filter() { # $1 caption text, $2 textfile path, $3 x, $4 y
  printf '%s\n' "$1" | sed 's/ · /\n/g' > "$2"
  # ffmpeg reads the ':' of a Windows drive letter as an option separator even inside quotes,
  # so both paths get their colons escaped.
  local tf ff; tf=$(cygpath -m "$2" 2>/dev/null || echo "$2"); tf=${tf//:/\\:}; ff=${FONT//:/\\:}
  echo "drawtext=fontfile='$ff':textfile='$tf':fontsize=52:fontcolor=$INK:line_spacing=14:x=$3:y=$4:box=1:boxcolor=$PAPER@0.92:boxborderw=22"
}

n=0; REEL_SEC=0
while IFS=$'\t' read -r kind file a b cap cardno || [ -n "${kind:-}" ]; do
  kind=${kind%$'\r'}; [ -z "$kind" ] && continue; [ "${kind:0:1}" = "#" ] && continue
  n=$((n+1)); seg=$(printf "%s/seg_%02d.mp4" "$WORK" $n)
  cap=${cap:-}; cap=${cap%$'\r'}; cardno=${cardno:-}; cardno=${cardno%$'\r'}
  echo "== segment $n: $kind $file ${a:-} ${b:-} ${cap:+[$cap]}"
  # KEEP=1 ./assemble.sh … reuses already-encoded segments (iterating on the mix, not the cut)
  if [ "${KEEP:-0}" = 1 ] && [ -s "$seg" ]; then
    echo "   (kept)"; printf "file '%s'\n" "$(cygpath -ma "$seg" 2>/dev/null || realpath "$seg")" >> "$LIST"; continue
  fi
  case "$kind" in
    reel)
      [ $n -eq 1 ] && REEL_SEC=$(dur "$file")
      ffmpeg -v error -y -i "$file" -vf "scale=$W:$H:flags=lanczos,fps=$FPS,format=yuv420p" "${VENC[@]}" "${AENC[@]}" "$seg";;
    wide|phone)
      len=$(awk -v a="$a" -v b="$b" 'BEGIN{printf "%.3f", b-a}')
      if [ "$kind" = phone ]; then
        # --cropbottom N drops N source rows (the Development Build watermark sits in the
        # bottom-right corner of a dev build; 36 px on the Nord 2's 1080x2400).
        vf="${CROPB:+crop=iw:ih-$CROPB:0:0,}"; [ "$CROPB" = 0 ] && vf=""
        vf="${vf}scale=-2:$H:flags=lanczos,pad=$W:$H:(ow-iw)/2:(oh-ih)/2:color=$PAPER"; cx=90; cy=120
      else
        vf="scale=$W:$H:force_original_aspect_ratio=decrease:flags=lanczos,pad=$W:$H:(ow-iw)/2:(oh-ih)/2:color=$INK"; cx=90; cy=90
      fi
      vf="$vf,fps=$FPS,format=yuv420p"
      # phone shots use DIR/<card>.png (top-left, for the paper panel); wide shots DIR/<card>w.png
      png=""; suffix=""; [ "$kind" = wide ] && suffix=w
      if [ -n "$CAPDIR" ] && [ -n "$cardno" ]; then png="$CAPDIR/$cardno$suffix.png"; fi
      if [ -n "$cap" ] && [ -n "$png" ] && [ -f "$png" ]; then
        extra=(-i "$png"); fc="[0:v]$vf[b];[b][1:v]overlay=0:0:format=auto[v]"
      elif [ -n "$cap" ]; then
        extra=(); fc="[0:v]$vf,$(caption_filter "$cap" "$WORK/cap_$n.txt" $cx $cy)[v]"
      else
        extra=(); fc="[0:v]$vf[v]"
      fi
      if has_audio "$file"; then amap=(-map "0:a"); asrc=(); else asrc=(-f lavfi -t "$len" -i anullsrc=r=48000:cl=stereo); amap=(-map "$(( ${#extra[@]} / 2 + 1 )):a"); fi
      ffmpeg -v error -y -ss "$a" -t "$len" -i "$file" "${extra[@]}" "${asrc[@]}" -filter_complex "$fc" -map "[v]" "${amap[@]}" -t "$len" "${VENC[@]}" "${AENC[@]}" "$seg";;
    card)
      ffmpeg -v error -y -loop 1 -t "$a" -i "$file" -f lavfi -t "$a" -i anullsrc=r=48000:cl=stereo \
        -vf "scale=$W:$H:force_original_aspect_ratio=decrease,pad=$W:$H:(ow-iw)/2:(oh-ih)/2:color=$INK,fps=$FPS,format=yuv420p" \
        -map 0:v -map 1:a -shortest "${VENC[@]}" "${AENC[@]}" "$seg";;
    *) echo "unknown kind '$kind' on line $n" >&2; exit 2;;
  esac
  printf "file '%s'\n" "$(cygpath -ma "$seg" 2>/dev/null || realpath "$seg")" >> "$LIST"
done < "$EDL"
[ $n -gt 0 ] || { echo "empty EDL" >&2; exit 2; }

ffmpeg -v error -y -f concat -safe 0 -i "$LIST" -c copy "$WORK/concat.mp4"

# Final mix: optional VO and music bed, then loudness for YouTube.
REEL_MS=$(awk -v s="$REEL_SEC" 'BEGIN{printf "%d", s*1000}')
in=(-i "$WORK/concat.mp4"); chain=""; cur="[0:a]"; k=1
if [ -n "$VO" ]; then
  in+=(-i "$VO")
  chain+="${cur}volume='if(gte(t,$REEL_SEC),0.35,1)':eval=frame[base];[$k:a]adelay=$REEL_MS|$REEL_MS,volume=0.7[vo];[base][vo]amix=inputs=2:normalize=0[m1];"
  cur="[m1]"; k=$((k+1))
fi
if [ -n "$MUSIC" ]; then
  in+=(-i "$MUSIC")
  chain+="[$k:a]adelay=$REEL_MS|$REEL_MS,volume=0.125[mus];${cur}[mus]amix=inputs=2:normalize=0[m2];"
  cur="[m2]"; k=$((k+1))
fi
chain+="${cur}loudnorm=I=-14:TP=-1:LRA=11[a]"
ffmpeg -v error -y "${in[@]}" -filter_complex "$chain" -map 0:v -map "[a]" -c:v copy "${AENC[@]}" -movflags +faststart -shortest "$OUT"

D=$(dur "$OUT")
printf "done: %s  (%.2f s)\n" "$OUT" "$D"
awk -v d="$D" 'BEGIN{ if (d >= 120) { print "FAIL: " d " s is not under the 2:00 Devpost cap"; exit 1 } }'
