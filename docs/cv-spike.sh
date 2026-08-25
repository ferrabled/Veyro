#!/usr/bin/env bash
# Turns the T-010 CV spike on or off.
#
#   docs/cv-spike.sh on    # add com.unity.ai.inference + fetch the BlazePose weights
#   docs/cv-spike.sh off   # remove the package again (the default, committed state)
#
# Why this is a toggle rather than just "in the manifest":
#
#   Inference Engine ships a large compute-shader library in its own Resources folder, and Unity
#   puts that in the APK whenever the package is in the manifest — whether or not a single line of
#   code references it. Measured 22 Aug: leaving it in cost the release APK 8.8 MB for a feature
#   that is not in v1.0 and may never be (see OPEN_QUESTIONS 7). Gating MotionRunner.Cv with
#   defineConstraints removes the code and the spurious CAMERA permission, but not those shaders.
#
#   Whichever way OPEN_QUESTIONS 7 goes, the package does not belong in the release manifest:
#   parking camera mode makes it dead weight, and Path B replaces Inference Engine with MediaPipe.
#
# The weights (~20 MB of Apache-2.0 ONNX from Hugging Face unity/inference-engine-blaze-pose) are
# gitignored for the same reason the package is not committed, plus there is no Git LFS yet
# (D2 / T-006). anchors.csv *is* committed: 81 KB of text the pipeline cannot run without.
#
# After "on", build the spike with:
#   Unity -batchmode -quit -projectPath game -buildTarget Android \
#         -executeMethod MotionRunner.EditorTools.CvSpikeBuild.BuildAndroid
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manifest="$root/game/Packages/manifest.json"
models="$root/game/CvModels"
package="com.unity.ai.inference"
version="2.6.1"
base="https://huggingface.co/unity/inference-engine-blaze-pose/resolve/main/models"
face_base="https://huggingface.co/unity/inference-engine-blaze-face/resolve/main/models"

mode="${1:-}"

add_package() {
  if grep -q "\"$package\"" "$manifest"; then
    echo "have   $package in the manifest"
    return
  fi
  # Insert as the first dependency; the file stays valid JSON and diffs to one line.
  python - "$manifest" "$package" "$version" <<'PY'
import collections, io, json, sys
path, name, version = sys.argv[1], sys.argv[2], sys.argv[3]
doc = json.load(io.open(path, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
deps = collections.OrderedDict()
deps[name] = version
for key, value in doc['dependencies'].items():
    deps[key] = value
doc['dependencies'] = deps
io.open(path, 'w', encoding='utf-8', newline='\n').write(json.dumps(doc, indent=2) + '\n')
PY
  echo "added  $package@$version"
}

remove_package() {
  if ! grep -q "\"$package\"" "$manifest"; then
    echo "clean  $package already absent"
    return
  fi
  python - "$manifest" "$package" <<'PY'
import collections, io, json, sys
path, name = sys.argv[1], sys.argv[2]
doc = json.load(io.open(path, encoding='utf-8'), object_pairs_hook=collections.OrderedDict)
doc['dependencies'].pop(name, None)
io.open(path, 'w', encoding='utf-8', newline='\n').write(json.dumps(doc, indent=2) + '\n')
PY
  echo "removed $package"
}

fetch_models() {
  mkdir -p "$models"
  for model in pose_detection pose_landmarks_detector_lite pose_landmarks_detector_full; do
    if [ -s "$models/$model.onnx" ]; then
      echo "have   $model.onnx"
      continue
    fi
    echo "fetch  $model.onnx"
    curl -fL --retry 3 -o "$models/$model.onnx" "$base/$model.onnx"
  done
  # T-010b: BlazeFace short-range, the model that actually clears the 30 ms gate on the Nord 2
  # (STATUS 22 Aug). Same licence (Apache-2.0), different HF repo.
  if [ ! -s "$models/blaze_face_short_range.onnx" ]; then
    echo "fetch  blaze_face_short_range.onnx"
    curl -fL --retry 3 -o "$models/blaze_face_short_range.onnx" "$face_base/blaze_face_short_range.onnx"
  else
    echo "have   blaze_face_short_range.onnx"
  fi
}

case "$mode" in
  on)
    add_package
    fetch_models
    echo
    echo "CV spike enabled. Open the project once so Unity resolves the package, then build with"
    echo "  MotionRunner.EditorTools.CvSpikeBuild.BuildAndroid"
    ;;
  off)
    # camera-feature branch: com.unity.ai.inference is a hard dependency of the shipping
    # MotionRunner.CameraInput assembly now, not a spike toggle — removing it breaks the compile.
    # "off" only ever mattered for keeping the package out of the v1.0 release manifest, which is
    # exactly what main still does.
    echo "keep   $package — MotionRunner.CameraInput (camera mode) requires it on this branch."
    echo "       On main (v1.0), the package stays out of the manifest as before."
    echo
    echo "game/CvModels is left alone (gitignored); delete it to reclaim ~33 MB of pose weights."
    ;;
  *)
    echo "usage: $(basename "$0") on|off" >&2
    exit 2
    ;;
esac
