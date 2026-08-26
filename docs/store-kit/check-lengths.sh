#!/usr/bin/env bash
# T-030 — verify Play listing copy against Google's character limits before anyone pastes it
# into the Console. Limits: app name 30, short description 80, full description 4000.
# Source: https://support.google.com/googleplay/android-developer/answer/9859152
#
# Usage: docs/store-kit/check-lengths.sh
#
# Counts CHARACTERS, not bytes — Play counts full-width and half-width characters the same,
# so `wc -m` under a UTF-8 locale is the right measure and `wc -c` is not.

set -euo pipefail
export LC_ALL=${LC_ALL:-C.UTF-8}

fail=0

check() {
  local label=$1 limit=$2 text=$3
  local n
  n=$(printf '%s' "$text" | wc -m | tr -d ' ')
  if [ "$n" -le "$limit" ]; then
    printf '  OK    %-22s %4s / %s\n' "$label" "$n" "$limit"
  else
    printf '  OVER  %-22s %4s / %s  <-- trim %s\n' "$label" "$n" "$limit" "$((n - limit))"
    fail=1
  fi
}

echo "Play listing character check"
echo

# --- v1.0 listing (keep in sync with LISTING.md) ---
check "app name" 30 \
'Veyro Run: Motion Runner'

check "short description" 80 \
'Play hands-free with the camera, or tilt to steer. A new endless track daily.'

check "full description" 4000 "$(sed -n '/^Your phone is the controller\.$/,/^feels wrong the fastest way to change it is to tell us\.$/p' "$(dirname "$0")/LISTING.md")"

# --- alternates from §2, checked so a swap never needs a re-measure ---
check "short desc (alt: body)" 80 \
'Move your body or tilt your phone. A new endless track every single day.'

check "short desc (alt: modes)" 80 \
'Hands-free camera mode or tilt controls. One new endless track every day.'

echo
if [ "$fail" -ne 0 ]; then
  echo "FAIL — at least one field is over its limit."
  exit 1
fi
echo "All fields within limits."
