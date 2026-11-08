# Traveler rooms (V1)

Room replaces Explore in the primary tabs. Explore's map, routes and saved-location implementation remain available for V2 with `href: null`; Home's exploration call to action is removed. The introductory tour visits Room. Previously saved direct Explore URLs still resolve.

The scene contains the current traveler and four interactive display surfaces. Tap Decorate, then a shelf or table to place an owned keepsake or clear the display. Placing a keepsake already displayed elsewhere moves it. These are cosmetic selections with no inventory quantities, XP or stat effects. Empty accounts see the free loft and an explanation of how to earn decorations. Unearned previews are hidden.

Room changes use the existing quiet appearance command. Selecting rooms preserves avatar fields and decorations; decorating preserves the room and avatar. Error/retry, offline drafts, account isolation and serialization use the shared appearance provider.

| Reward | Room | Keepsake |
| --- | --- | --- |
| Starter | Traveler's Loft | Empty displays |
| Overall level 2 | | Maker's Crest |
| Overall level 3 | | Restful Keepsake |
| Overall level 4 | | Scholar's Books |
| Overall level 5 | | Explorer's Compass |
| Overall level 6 | | Everyday Toolkit |
| Overall level 7 | | Horizon Lantern |
| Rank E | Sunlit Workshop | |
| Rank D | Moonlit Study | |

Current server entitlements use overall level for decor and global rank for rooms. Undo removes a placed decoration or active room when the resulting progress falls below its gate. Re-earning leaves normalized slots empty until selected again. Receipt replay uses the rules recorded at the time of the command.

## Persistence decision

This is a bounded account-owned appearance preference. Compact v5 and v6 encode three room styles and four display slots alongside the traveler. The existing `AvatarCode` columns widen from 20 to 24 bytes and retain their earlier formats. The mutable Account retains its owner and rowversion concurrency model. QuestCommandHistory remains immutable command history with its existing ownership, append-only lifecycle and retention/erasure rules. Writes use the authenticated save-avatar command and receipts.

The change expands two CHECK constraints and their column descriptions. SQL project, schema contract and EF mappings must remain in parity. Disposable SQL verification covers shape, constraints, descriptions, restart, account isolation, reward reversal and replay. Production rollout requires the constraint update and compatible backend before the client.

## Art registration

Three opaque 1312x1199 rooms share display baselines `(378,412)`, `(942,412)`, `(319,684)`, `(995,684)`. Decorations use measured transparent-atlas crops, proportional rendering and bottom alignment. Traveler feet land at `(656,1115)`. Wallpapers contain empty furniture; earned objects are live layers. Prompts, sources and hashes are in `assets/room/provenance.json`.

The normal view is a full-height scene above the tabs, without a navigation header, page padding, heading or explanatory footer. Rooms opens a native modal sheet with owned room choices and an Unlock more rooms view. Rank requirements come from current account entitlements, and Find a quest opens the quest board. Decorate reveals the four empty display targets; Done hides them. Item choices open in a dismissible sheet. Quiet appearance saves and retry/recovery remain available.

Portrait rooms use the new tall illustrations, measured surface anchors and proportional cover sizing. The avatar uses 58% of portrait source scene height; wide artwork retains 76%. The shared adult head/body proportions are unchanged. Room geometry comes from the actual available layout size, and floating controls respect the top safe area. Modal content scrolls within a 70% height bound and includes close, backdrop and platform Back dismissal.

Portrait registration is recorded in the asset README and provenance. Wide screens preserve the original full composition with a blurred background filling the remaining space.
