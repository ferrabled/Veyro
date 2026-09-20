# Season 1 asset shortlist — zero purchase cost, commercial use

20 September 2026 · feat-game-design · T-025 source research; implementation follow-up recorded in SEASON_PASS_IMPLEMENTATION.md.

The existing design is ten levels with one free and one paid-track reward at each level.
Both columns can use the same free source assets. The paid track buys the curated in-game
reward, effects and progression; it does not require buying a commercial asset pack.
The source assets are not exclusive to Veyro. Only our authored combination can be described
as a Season 1 exclusive within this game.

## Recommended sources, checked and downloaded

| Source / creator | Concrete usable files | Where it fits | Work still required |
|---|---|---|---|
| [Hats / Clothing / Props — Teh_Bucket](https://opengameart.org/content/hats-clothing-props) | `snapback hat.fbx`, `top hat.fbx` from the individual ZIP downloads | Free level 5 cap; paid level 3 top hat. Headphones/beret are optional later choices, not extra Season 1 slots | Fit to the Kenney head bone; check hair clipping; use Veyro solid materials and simplify if needed. The FBX files were inspected as downloads, not yet imported into the game |
| [Low Poly Crown — BullHornGames](https://opengameart.org/content/low-poly-crown) | `crown.fbx`, plus Blender source; creator reports 208 triangles | Paid level 7 crown, reused in the level 10 Prism preset | Fit to head bone; replace its material with warm gold/teal. No UVs supplied, which is fine for a solid material |
| [Particle Pack — Kenney](https://kenney.nl/assets/particle-pack) | Transparent `circle_01.png`, `light_01.png`, `spark_01.png`, `star_01.png`, `trace_01.png` and variants | White/Shadow trails, Cyan Pulse/Twin/Aurora, Firefly/Comet, Confetti | These are sprites, not finished Unity effects. Author small particle/trail prefabs and a shipped URP-compatible transparent material; atlas/downsize them and profile overdraw |
| [Animated Characters Protagonists — Kenney](https://kenney.nl/assets/animated-characters-protagonists) | The already-shipped skater atlas and runner mesh | All shirt colors and outfit finishes | Keep skin, hair and shoes intact. Chrome/emission need an extension to the current outfit shader; the current shader does not already implement those finishes |

All four source pages explicitly list **CC0**. CC0 permits modification and commercial
distribution, so these can be used for rewards in either track of the paid game/pass.
No attribution is mandatory; retain creator/source records anyway.
[Kenney's commercial-use confirmation](https://kenney.nl/support) and
[CC0 terms summary](https://creativecommons.org/publicdomain/zero/1.0/).

Teh_Bucket's page credits CC0 texture/icon sources and records the removal of previously
unsuitable textures in 2020. Use the current downloads; the recommended treatment uses the
hat meshes with our own materials and no stock logo. Avoid importing the bundled
`.unitypackage`: it can bring old materials and extra content into the project.

## How the current 10 × 2 ladder maps to these assets

The T-025 implementation uses the existing item names in COSMETICS_CATALOG. These remain
in-game rewards, not individual store products. The table records the art direction;
STATUS and SEASON_PASS_IMPLEMENTATION record implementation and verification.

| Level | Free reward | Paid-track reward |
|---|---|---|
| 1 | **Mint** — matte mint shirt on the existing runner | **Neon Lime** — lime shirt with a bright accent treatment; make its value visible without relying on bloom |
| 2 | **White trail** — one short soft white ribbon using a Kenney trace/circle | **Cyan Pulse** — cyan ribbon with a restrained pulsing accent |
| 3 | **Sunset Orange** — warm shirt variant | **Top Hat** — Teh_Bucket mesh, teal fitted mesh |
| 4 | **Sky Blue** — cool shirt variant | **Firefly** — a few gold drifting light sprites |
| 5 | **Disc Cap** — downloaded snapback mesh fitted as a sporty teal cap; existing display name retained | **Chrome** — metallic outfit panels; keep human skin/hair unchanged |
| 6 | **Plum** — deep purple shirt | **Twin** — two short offset ribbons, reusing the same source texture |
| 7 | **Shadow** — short dark-teal trail | **Crown** — BullHornGames crown in warm gold |
| 8 | **Charcoal** — dark shirt | **Aurora** — teal/pink/lime gradient trail, controlled so it does not hide hazards |
| 9 | **Confetti** — one short crash burst of small colored shapes/sprites | **Comet** — sparse star/spark tail and aura |
| 10 | **Matte Gold** — warm gold shirt finish | **Prism** — existing proposed combination: Chrome + Aurora + Crown + Comet; animated iridescent outfit finish |

The free track should look intentional and include a real accessory and effect. The paid
track should add distinct silhouettes and animation, with short trails and sparse particles
that preserve obstacle visibility. No reward changes runner size, hitbox, speed or jump timing.
The paid pass's level-1 reward still unlocks immediately. T-025 implements the recorded-XP gates, collection and locker; do not create individual
store products for these items.

## Lane-change animation research and integration

[KayKit Character Animations](https://kaylousberg.itch.io/kaykit-character-animations) 1.1's
**free** download includes `Rig_Medium_MovementAdvanced.fbx`: `Dodge_Left`, `Dodge_Right`,
`Running_Strafe_Left` and `Running_Strafe_Right`. Its bundled License.txt explicitly permits
commercial use under CC0. The paid SOURCE tier is only needed for Blender source files.

For the current quick 0.14-second lane crossing, a short lateral dodge is a better fit than a
looping strafe. The two dodge takes are retargeted onto the existing Humanoid and played over
0.22 seconds including the settling pose. Horizontal travel is extracted and discarded;
the controller still owns the lane movement. Jumping takes priority over a dodge. These core
steering animations belong to every player, not behind the pass.

[Quaternius Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html)
is another CC0 humanoid option; it was researched but not imported. Its different downloadable
tiers would need checking per clip before choosing a replacement.

## Provenance and delivery boundary

Downloads are under ignored `builds/art-source/`. The earlier runner polish imported the
selected KayKit animation and license into `Assets/Art/KayKit/`. T-025 imports the selected
three meshes and three particle textures into
`Assets/Art/Season/`; generated fitted prefabs and effects reference only that curated subset.
Licenses are confirmed at the linked creator pages; fit/material/performance evidence is recorded
in STATUS as the implementation is verified.

| Download | SHA-256 |
|---|---|
| KayKit free animations 1.1 | `65882F31F905AD2E953819648A59287CDEAB8F623908D5EF701971D3758BE20F` |
| Snapback ZIP | `7E52A5C440D3DB95B896093E5E078B08BBDE502A26E10012583CF1FE9E6AFE34` |
| Top-hat FBX ZIP | `95FA383375520CC8F57F65A413E5AC30D018C7CE40C220709386260E3BFC2E7F` |
| Low Poly Crown ZIP | `D56FD72DCC0831A7AF0EEE975AF19BA4AF4A5C153F3C26A57B5AE1B9F3C644D2` |
| Kenney Particle Pack ZIP | `B631D4B07F7002549FDCF155F01141AD482F79F3440E4E301EED49CE5F1D8958` |

Not selected: the paid Kenney Animated Characters Bundle (CC0 does not mean its download is
free), KayKit paid character extras/source files, or Mixamo/account-dependent acquisition.
An alternative [CC0 magician hat by Lucian Pavel](https://opengameart.org/content/magician-hat-stick)
was downloaded but is less convenient here: its archive supplies Blender models and PBR maps,
whereas the selected top hat already has an FBX and suits a flat-color treatment.
