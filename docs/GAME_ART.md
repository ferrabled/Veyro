# T-006 — first biome art pass

19 September 2026 · feat-game-design · working art direction, subject to owner review.

## Recommendation

Use a small, coherent CC0 kit as the foundation, then make Veyro recognizable through its
runner, palette, obstacle vocabulary and animation. Finish one biome before adding more.
The working direction is a sunlit sculpture garden: warm paving, teal trees, mint banks,
coral planters, gold jump hurdles and coins. Bridges cross turquoise water; the tunnel chunk
becomes a garden pergola. No automotive imagery.

Free assets save modelling time; they do not replace composition or device checks. The highest
value order is: readable character and running animation → ground and obstacle silhouettes →
scenery and horizon → lighting and UI → sound/feedback in a separate pass. Keep the tilt and
jump tuning fixed while judging the art.

## Sources checked

| Source | Useful for | Choice |
|---|---|---|
| [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) | Low-poly foliage, rocks, modular terrain and props | Seven models imported; one consistent style and very small source files |
| [Kenney Animated Characters Protagonists](https://kenney.nl/assets/animated-characters-protagonists) | Rigged human and idle/run/jump animations | One mesh/rig, three clips, one atlas |
| [Quaternius Ultimate Nature](https://quaternius.com/packs/ultimatenature.html) | Broader nature library | Good alternative if the owner wants a different silhouette family; not imported |
| [Quaternius Modular Men](https://quaternius.com/packs/ultimatemodularcharacters.html) | Modular outfits and more animations | Possible character upgrade; not needed for this first pass |
| [KayKit Character Animations](https://kaylousberg.itch.io/kaykit-character-animations) | Larger locomotion/emote library | Useful later with a compatible rig; not imported |

The selected Kenney packs are CC0, permit commercial use and do not require attribution.
Keep the bundled license files and provenance below. Never use Kenney's logo as our branding.
[Kenney support/license explanation](https://kenney.nl/support),
[CC0 terms summary](https://creativecommons.org/publicdomain/zero/1.0/).

## Website relationship

The site's teal `#12454C`, pink `#EE3D87` and paper `#FBF5E9` remain useful brand anchors.
The game adds quieter greens and stone colors so interactive objects stand out. Its menus,
pause and results now use those anchors too. The website can retain its poster typography
and daily-ticket motif while replacing capsule drawings with posed renders of the actual
runner (T-037). Lead its hero with a real gameplay capture after the art direction is accepted.
There is no need for a website rebrand merely to fit a free asset pack. The public site is not
deployed or rewritten by this art pass.

Alternative directions if the park is not the desired mood: the same meshes under a warm dusk
palette, or a cooler, more abstract sculpture garden. A neon city needs a different environment
kit and more lighting work; it is not the recommended first biome for the current deadline.

## Authoring and stability

- The empty bootstrap scene stays empty. `ParkAssets` references the curated visual prefabs,
  materials and ordered chunk assets; no scene editing is required.
- The 13 `TrackChunkAsset` ScriptableObjects produce engine-free `ChunkDefinition` objects.
  All fields and array ordering match the published library. `greybox-1` / `greybox` remain the
  compatibility identifiers: new art alone must not split the daily leaderboard.
- `ParkArtTests` compares every gameplay field, then 10,000 generated chunk selections. A future
  gameplay edit still requires the existing content-version discipline.
- Decorative variation has a separate stable PRNG. It never advances the gameplay PRNG.
- Each unique chunk's scenery is combined once by material and cached before motion begins.
  Recycled views and duplicate views reuse those meshes. Coins remain individually collectible.
- The runner root and AABB belong to `RunnerController`; animation affects only its visual child.
  The skin service changes the shirt through its existing entitlement rules. Face, trousers and
  shoes retain their texture colors. Purchases, catalog IDs and score economy are unchanged.
- Source binary art is covered by scoped Git LFS patterns. The downloadable packs stay under
  ignored `builds/art-source`; only the curated files enter `Assets/Art/Kenney`.

`Veyro → Art → Rebuild park assets from source` is an explicit editor authoring command. It
regenerates visual prefabs and the initial chunk data from the engine-free library. **Do not run
it after hand-authoring chunk changes without first preserving them.** It does not run on import
or build. `ParkArtPreview.Render` renders the real assets to `builds/art-review/park-preview.png`.

## Download provenance

Downloaded 19 September 2026 from the links on the two official Kenney pack pages.

| Archive | Bundled version | SHA-256 |
|---|---|---|
| `kenney_nature-kit.zip` | Nature Kit 2.1 | `FA7974A0D342BFE63C38664BA9F8EC1A4AAB8EA25F099BDC56870E33588C4D9D` |
| `kenney_animated-characters-protagonists.zip` | Animated Characters Protagonists 1.1 | `EC3787DE70FA2200256848D74201B10F6B6C3126594E9857BF989753312C2B84` |

The character imports as a Humanoid, as specified in
[Kenney's Unity import guide](https://kenney.nl/knowledge-base/game-assets-3d/importing-characters-and-animations).
The three selected takes explicitly bake root transforms into the pose; run and idle loop,
jump does not. The runtime Animator does not apply root motion. Normalize the posed renderer
bounds, not the old FBX bind-pose bounds or a twice-transformed baked mesh.

Source licenses are retained beside the models. Original source files are unchanged; project
materials, shader treatment, composition and generated prefabs are Veyro's additions.

## Acceptance

See the dated STATUS entry for measured tests, device evidence and remaining human checks.
T-006's final acceptance still needs a stranger to recognize the game from ten seconds of footage.
An app startup splash image is optional, not an obstacle to completing the biome.
