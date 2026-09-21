# Stock Layers SDK migration assessment — 21 September 2026

Owner requested this assessment before replacing the SDK. This is an assessment,
not a completed migration or a change to the approved data-collection scope.

## Why the SDK was patched

The 20 September integration guide identified a mismatch between the proposed
analytics-only experience and upstream defaults. The 21 September implementation
chose a local consent/privacy patch. It was not needed to send our gameplay events
or to make Unity compatible with Layers, and no contest requirement calls for it.
Distribution permission remained unresolved in OPEN_QUESTIONS 22; an engineering
reason for a patch was not evidence of permission to distribute it.

Compared against upstream v3.3.2, commit
`7d28dcda555ab3ab3f0901c6c6e77f8710613f4b`, three SDK files differ:

| Local change | Purpose | Stock consequence |
| --- | --- | --- |
| Initial consent ordering and Android attribution switch | Set advertising consent false before managed transport starts; skip GAID/referrer modules. | Consent requires initialization; Android attribution starts automatically. |
| Dedicated persistence directory | Isolate SDK files from game saves. | Uses the shared application data directory. |
| Revoke-and-shutdown operation | Stop transports, reset pending data, then allow safe folder removal. | Public Reset attempts an HTTP flush first; Shutdown preserves pending data. Neither is an equivalent purge operation. |
| Conditional initialization-timing event | Honor disabled performance capture for this additional event. | Initialization timing is emitted even with automatic performance capture disabled. |
| Advertising-ID/referrer dependency removal | Keep these libraries and their AD_ID permission out of the app. | Dependency resolution includes them; audit the final Android manifest. |

These are source findings. Native consent enforcement, offline replay and actual
provider receipt must be verified on a device; a successful C# test is insufficient.

## What can stay

The game's event names, run start/completion hooks, notification-to-Daily routing,
campaign labels, separate support ID and default-off initialization can stay behind
the existing adapter. OneSignal and gameplay need not be rewritten. The current
run/campaign tests remain useful. Standard configuration can still disable the
automatic exception, deep-link and general performance modules.

## Work required for the official package

1. Use the official Git UPM dependency at an exact reviewed revision; remove the
   embedded package and only its two LFS rules. Preserve the art LFS rules.
2. Rewrite the adapter's three patch-only API references (persistence directory,
   attribution toggle, revoke-and-shutdown). Use documented public SDK operations.
3. Decide what behavior is promised for advertising attribution and unsent events.
   Public stock configuration does not reproduce the current guarantees. Merely
   calling advertising=false after initialization is not proof that no ID lookup
   occurred. Removing AD_ID alone does not disable install-referrer processing.
4. Replace patch-specific consent assertions with adapter and stock-SDK checks.
   Verify enable, disable, relaunch, offline queue and re-enable behavior. Preserve
   local player progress; never delete the game's shared persistent-data directory.
5. Handle any old `veyro-layers` directory deliberately, preserving the saved choice
   and support ID. Do not migrate previously discarded telemetry into a new queue.
6. Reconcile in-game copy, privacy/support pages, store checklist and Play declarations
   with the observed behavior. Rebuild Android, compare permissions/size, and verify
   Events receipt and the OneSignal-to-run measurement loop on the real device.

## Recommendation

Keep the official SDK unmodified and keep analytics opt-in. Before changing the
current no-advertising and discard-on-opt-out promises, obtain the owner's scope
decision. If those promises remain requirements, request supported controls or an
upstream fix from Layers. Project-level dependency exclusions are another avenue
to investigate, but are not equivalent to a supported SDK switch and have not been
validated here. Do not replace the fork with reflection/private-native workarounds.

The code migration is bounded; the unresolved part is the privacy behavior and its
verification. No SDK files were changed during this assessment.

Sources: [official installation](https://layers.com/docs/sdk/installation),
[SDK at the reviewed tag](https://github.com/layers/layers-sdk-unity/tree/v3.3.2).
Detailed comparisons are retained locally under `builds/layers-stock-audit/`.
