# Quaternius — Universal Animation Library (Standard)

- **Source:** Quaternius, *Universal Animation Library* — https://quaternius.com/packs/universalanimationlibrary.html
  (mirror used for the download: https://opengameart.org/content/universal-animation-library)
- **Archive:** `universal_animation_librarystandard.zip`, downloaded 21 September 2026
- **SHA-256:** `18FF1A7215F4852B320203E8AAF02A1578B5C8EEF9027FBAEDFCEDC7B85A3AC2`
- **Licence:** CC0 1.0 Universal (`License.txt` beside the model, verbatim from the archive). No attribution required.
- **File kept:** `Unity/AnimationLibrary_Unity_Standard.fbx` only (the Unreal export and the rest of the archive are not in the repo).

## Takes used

Imported as Humanoid and retargeted onto the Kenney runner and the KayKit adventurers by
`Assets/Editor/RunnerCrashBuild.cs` (menu `Veyro → Art → Update crash and result animations`).
Root position/rotation/height are baked into the pose: the runner's collision box never moves
when it dies, and the result card's preview stage is a fixed frame.

| Take | Controller state | Loops | Used for |
|---|---|---|---|
| `Hit_Chest` | `crashWall` | no | ran into a full-height block — stopped dead and recoiling |
| `Roll` | `crashTrip` | no | clipped a hurdle — trips and tumbles forward |
| `Dance_Loop` | `celebrate` | yes | result card, run was a new record |
| `Death01` | `defeat` | no | result card, run was not a record — falls to the ground |

The other takes in the FBX are not referenced and therefore not shipped (Unity strips
unreferenced sub-assets); the ~20 MB source stays under Git LFS like the other FBX files.
