# Reward journal

The Home and Progress screens include a compact milestone summary with the earned total, an unearned reward suggestion and a link to the dedicated reward journal. The journal explains all six starter triggers and supports All rewards, In progress and Earned filters.

Overall levels 2–7 each unlock a room decoration. Rank E unlocks Maker’s vest and Sunlit Workshop, rank D unlocks Cozy knit and Moonlit Study, and rank C unlocks Scholar’s coat. Custom quests still use the existing XP rules. The journal shows the level or rank target and names the unlocked items. The completion celebration names the cosmetics newly made available by the confirmed progress.

## Ownership

The account snapshot owns progress, requirements and earned state. Quest achievements, badges and frames still use the six starter triggers. Level and rank milestones are additional achievement catalog rows with stable IDs. The server grants them from overall level and current global rank. Current eligibility is recalculated after Undo. Historical receipts retain the cosmetic rules in effect when the command was accepted.

Filtering and suggestions are transient display state. The journal uses only `useAccountSnapshot`, introduces no commands/storage/network calls and unmounts its filter state when account data is unloaded. The Home suggestion sorts a filtered copy by completion fraction, then stable ID; it never changes the server array or the selected reward.

## Verification and acceptance

Component regressions cover server thresholds, filters, rank gates, Undo, over-threshold progress, appearance preservation, account unloading and wardrobe names. The November 2 feature-batch report records current build, test and browser evidence. Native iPhone/Android interaction, VoiceOver/TalkBack and device font scaling remain separate acceptance lanes.
