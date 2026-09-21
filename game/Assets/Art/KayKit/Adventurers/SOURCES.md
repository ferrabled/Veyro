# Ember and Frost character sources

Kay Lousberg, **KayKit Adventurers 2.0 — FREE tier**: https://kaylousberg.itch.io/kaykit-adventurers
Downloaded 20 Sep 2026. The bundled `License.txt` explicitly grants personal, educational and commercial use under CC0 1.0.

- `Barbarian.fbx` + `barbarian_texture.png` → Ember (bear headdress, beard, broad explorer silhouette).
- `Mage.fbx` + `mage_texture.png` → Frost (robe, cape and pointed hat, icy fabric treatment).

These are different meshes and rigs, not textures of the default Kenney runner. We normalize their presentation height, retarget the existing CC0 idle/run/jump/lane-dodge controller, and fit head/back sockets. A selected pass hat replaces native headwear; a back accessory replaces the native cape. Body dyes affect atlas clothing regions, preserving skin, hair and facial features. Colliders and movement stay on the existing gameplay root.

Only free-tier files were used. No paid EXTRA/SOURCE content, no new Unity packages. FBX/palette sources are unchanged; generated prefabs/materials and `CosmeticCharacterBuild` express the adaptations.

20 Sep fitting follow-up: generated prefabs use an 80% head bone and mild X/Z compression (86%/90%), then normalize to the original Runner's measured bare-head height and foot level. Source FBXs remain unchanged. FrostCape.asset adds reverse-facing triangles and normals to the original 84-triangle cape (168 triangles total), so both sides render with the existing shared outfit material. Per-hat opening dimensions and torso-surface sockets are authored from the posed meshes; animation and collider roots are unchanged.

Owner proportion refinement: the earlier 68% head and 64%/70% X/Z adaptation was too narrow and looked vertically stretched. The current proportions restore rounded faces and fuller bodies while retaining the same normalized bare-head height. Hat and back sockets are regenerated from this geometry.
