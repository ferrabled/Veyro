# Veyro integration patch — 21 September 2026

Upstream: https://github.com/layers/layers-sdk-unity, tag `v3.3.2`, commit
`7d28dcda555ab3ab3f0901c6c6e77f8710613f4b`. Embedded package name/version are
unchanged so the native ABI remains identifiable. Native binaries are unmodified.
Large native binaries are tracked with Git LFS.

Local changes, deliberately limited to the consent boundary:

- `Runtime/LayersConfig.cs`: allow a dedicated persistence directory; Android
  advertising/install attribution defaults to disabled.
- `Runtime/Layers.cs`: set analytics consent true / advertising false before
  starting transport; use the dedicated directory; skip Android attribution unless
  explicitly enabled; honor disabled performance capture for `layers_init_timing`.
  Add `RevokeConsentAndShutdown`: withdraw consent, abort HTTP/config work, reset
  the native event queue, then shut down. Unlike public `Reset()`, it must not
  perform a pre-reset HTTP flush. The app removes only its dedicated SDK directory.
- `Editor/LayersDependencies.xml`: remove unused advertising-ID and install-referrer
  dependencies. Veyro always keeps Android attribution disabled.

The app initializes this package only after explicit analytics consent. Do not
replace it with stock upstream without checking all the above properties. A
normal process shutdown may persist consented offline events; withdrawing consent
must discard them. An already delivered HTTP request cannot be recalled.

Upstream distribution audit: the downloaded tag has no LICENSE file, no package
license field, and GitHub reports no recognized license. Official Layers docs
instruct application integration, but do not settle redistribution of this locally
modified source. No license is inferred or added here. Obtain vendor confirmation
for this patch before publishing the modified package/source or distributing it
in the release; keep the provenance and native copyright notices intact.

Verification and release gates: `docs/SDK_PRIVACY_RELEASE.md` in the game repository.
