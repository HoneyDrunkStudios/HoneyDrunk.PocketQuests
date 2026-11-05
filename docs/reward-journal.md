# Reward journal

The Home and Progress screens include a compact milestone summary with the earned total, an unearned reward suggestion and a link to the dedicated reward journal. The journal explains all six starter triggers and supports All rewards, In progress and Earned filters.

Completion bars use the account's confirmed counts and rank requirements. Each reward names its wardrobe unlocks and earned items link to the avatar editor. Choosing clothing, badges and frames still requires an explicit save. The completion celebration also names newly confirmed wardrobe unlocks.

## Ownership

The account snapshot owns counts, required counts, required rank and earned state. Presentation copy maps the six stable starter entitlement IDs to their qualifying quest categories, as defined in the Architecture starter catalog and `Progression.Entitlements`. No completion history is recounted and no reward is granted locally. A full completion bar does not imply that a frame has been earned while its rank requirement is unmet.

Filtering and suggestions are transient display state. The journal uses only `useAccountSnapshot`, introduces no commands/storage/network calls and unmounts its filter state when account data is unloaded. The Home suggestion sorts a filtered copy by completion fraction, then stable ID; it never changes the server array or the selected reward.

## Verification and acceptance

Component regressions cover server thresholds, filters, rank gates, Undo, over-threshold progress, appearance preservation, account unloading and wardrobe names. The November 2 feature-batch report records current build, test and browser evidence. Native iPhone/Android interaction, VoiceOver/TalkBack and device font scaling remain separate acceptance lanes.
