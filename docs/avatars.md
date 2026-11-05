# Full-body avatars

## Layered accessories (November 4, 2026)

Players can layer starter glasses or a linen scarf with their existing gear, including unlocked satchels, toolkits and capes. A duplicate accessory renders once. Eight scarf palettes affect only the scarf artwork, preserving skin, hair and outfit colors. Extras and scarf colors participate in focused randomization, kept traits, Undo/Redo and section resets, and remain previews until saved.

The approved additive storage change widens `Account.AvatarCode` and immutable `QuestCommandHistory.AvatarCode` from `varchar(16)` to `varchar(20)`. The bounded `v3:skin:hairColor:hairStyle:outfit:accessory:face:build:extra:scarfColor` format appends starter extras 0–2 and scarf palettes 0–7. Existing v1/v2 bytes and ordinals retain their meanings. Reward revocation removes only reward clothing/gear; the free extra and scarf color survive. The existing command digest still captures the complete versioned appearance string, so old receipts replay unchanged.

Deploy the widened SQL constraints and v3-capable API before the mobile client. Older clients show their starter fallback for a v3 appearance. No migration rewrites existing profiles or history, and this source slice does not deploy to an existing account database.

The focused styling controls let players randomize or restore only Identity, Hair, Clothing or Accessories, preserving every unrelated draft choice. Existing kept-trait controls apply to both group and whole-look randomization; revoked cosmetics cannot be retained when their group is randomized. Preview Undo and Redo retain at most 20 steps each and are cleared by reset or account unloading. A new edit clears the redo branch. All styling remains a preview until the existing Save avatar command succeeds, including offline use. These controls keep the existing v1/v2 appearance format and add no account fields or schema changes.

The October 28 creator update groups controls into Identity, Hair, Clothing, Accessories and Looks. Looks shows ten illustrated free presets. Switching sections retains the draft. Zoom in on face and Show full traveler change only the view. Compare saved look changes only the displayed figure; Save always submits the draft. Undo preview edit keeps up to 20 local edits, and Reset returns to the currently saved appearance. Preview history is cleared when the editor unmounts and is not stored on the account.

Each appearance slot can be kept during Surprise me. Identity choices remain free. A kept clothing or accessory slot is retained only while its reward is currently available; randomization replaces locked or revoked choices with an eligible item. These controls add no API fields or storage formats.

This is the first implementation of the full-body dress-up feature selected on October 27, 2026. It extends the earlier MVP scope that deferred avatars. The avatar is a saved account preference and a visible expression of existing rewards; it grants no XP or stat bonuses.

## Player experience

Home, Progress and Settings link to **Your traveler**. Onboarding includes naming, the complete avatar creator, interests, starting skills and a first quest. The initial starter avatar can be explicitly saved; continuation waits for confirmed state. Existing names and illustrated portraits remain stored, and avatar saves do not overwrite them. Players can preview every item, choose seven faces, three builds, six skin tones, eight hair colors and eight hairstyles, and save any combination they currently own. Home renders the saved full-body traveler. Preview changes stay local until **Save avatar** succeeds; failed saves retain the preview. Offline previews work, while saving requires a connection and the existing command/recovery flow.

The current artwork follows the approved painted traveler mockups, with natural adult proportions and a straight-on face and body. Eleven bundled PNG atlases provide seven faces in six complexion variants, seven grayscale hairstyles plus a shaved option, six clothing sets in three builds, and five accessories plus an empty slot. The renderer composes these through `react-native-svg`, preserving hair shading with a color matrix. Face and neck use the same complexion source; clothing has transparent neck openings. Gloves, trousers and boots belong to the outfit. Source rectangles exclude neighboring sprites, and filters/clips share one SVG root for native definition lookup. No remote images or per-player asset generation are required. Generation prompts and provenance are recorded in the [asset README](../apps/mobile/assets/avatar/README.md).

The pure `profile/avatar-look` leaf owns stable appearance choices and cosmetic mappings without importing editor state or rendering dependencies. Existing saved `v1` ordinals keep their meaning. The expansion adds free Soft/Angular/Round/Gentle/Defined/Heart/Oval faces and Balanced/Broad/Full builds. Gentle and Defined were created with East Asian inspiration, Heart with Southeast Asian inspiration, and Oval with South Asian inspiration. Every face supports every skin tone and all other customization choices. Ten free starting looks can be adjusted before saving. Surprise me selects only currently owned clothing and accessories and never grants rewards or saves automatically. Individual facial-feature editing, separate tops/bottoms/shoes and animation remain future work.

| Item | Existing reward | Requirement |
| --- | --- | --- |
| Trail tunic, Field shirt, City jacket | Starter | Available immediately |
| Round glasses, Linen scarf | Starter | Available immediately |
| Maker's vest | A Quest of My Own (`PQ-CAT-A01`) | Complete a custom quest |
| Cozy knit | Room to Recharge (`PQ-CAT-A02`) | Five Rest & Recreation completions |
| Scholar's coat | Open Notebook (`PQ-CAT-F01`) | Ten Learning completions and rank E |
| Explorer's satchel | Curious Explorer (`PQ-CAT-B01`) | Three Exploration completions |
| Everyday toolkit | Everyday Care (`PQ-CAT-B02`) | Ten Everyday Life completions |
| Horizon cape | Quiet Horizon (`PQ-CAT-F02`) | Ten Rest & Recreation completions and rank D |

The original badges and frames remain available. Clothing eligibility follows the same current server-calculated entitlements. Undo or rank loss can revoke a cosmetic; only that slot returns to its starter option, preserving free appearance choices. Re-earning the reward does not silently equip the removed item. Locked clothing remains previewable with an explicit explanation and disabled save.

## Persistence and compatibility

`save-avatar` goes through the authenticated command endpoint, account lock, tracked EF updates, immutable history and command receipt. SQL remains authoritative. Both `Account.AvatarCode` and `QuestCommandHistory.AvatarCode` are nullable binary-collated `varchar(16)` columns. No tables or indexes are added. Existing account audit, ownership, replay, export and erasure paths retain their owners.

The atomic version-one value is `v1:skin:hair-color:hair-style:outfit:accessory`. Each slot is a single stable catalog ordinal: skin 0-5; the other slots 0-3. For example, `v1:2:0:0:0:0` is the starter. The expanded value is `v2:skin:hair-color:hair-style:outfit:accessory:face:build`: skin 0-5, hair color/style 0-7, outfit/accessory 0-5, face 0-6 and build 0-2. Existing ordinals are unchanged; new starter outfits/accessories occupy 4 and 5. The client emits v1 for selections entirely representable by v1, and v2 otherwise. The server retains the submitted version exactly in immutable history. SQL checks constrain both supported versions, exact byte lengths (12/16) and ranges; Domain owns entitlement validation. Null preserves the starter for existing accounts and historical commands. Ordinals must never be reordered or reassigned. New formats/ranges require an explicit domain/schema/client compatibility change.

The immutable history check requires a non-null code for `save-avatar` and null for other command kinds. Its EF property rejects mutation after save. A payload on a different action is rejected. Current projections normalize revoked cosmetics before persistence; replay retains this same normalization after each historical command.

Ordinary commands retain their byte-identical version-one canonical digest. Existing name/portrait commands retain their version-two tuple. New avatar commands use digest version three. Earlier avatar version-two receipts retain their original single-avatar-field tuple, selected by action, so both previous implementations remain replayable. Reusing an operation ID with a changed look conflicts. Retries return their original historical result without overwriting the current look.

The OpenAPI-derived `avatarCode` field is optional nullable and omitted when unset, matching the optional name/portrait compatibility policy. Legacy encrypted cache records and queued actions remain valid without rewriting the cache. A configured avatar is included in the ordinary profile/export mapping. Deploy the additive SQL/API change before the updated mobile client. The existing v1/v2 appearance ordinals remain stable; older avatar clients that cannot render the expanded choices show their starter fallback. No command history is rewritten.

## Verification

Run the repository's package-mode schema/SQL suite, endpoint/client contract scripts, backend architecture checks, and complete mobile lint/typecheck/architecture/UI/API/security-patch/test suite. Build an Expo native JavaScript export after changing SVG support. Native device startup, persistence, VoiceOver/TalkBack, real-provider acceptance and signed distribution remain separate from browser fixtures and export success.

The added regressions cover malformed appearances, all six exact entitlement mappings, no progression changes, SQL restart/account isolation, changed-payload retry rejection, historical replay after Undo, re-earning behavior, local drafts, failed saves, offline preview, stale acknowledgements and legacy cache compatibility. Visual fixtures exercise the actual avatar and wardrobe components at 390px, 320px and enlarged text.

The painted-art update also checks that complexion, hairstyle, hair color, clothing and accessory controls independently change their rendered layers without saving a preview; multiple avatars must have independent filter/clip IDs and no nested SVG definition scopes. Visual review covers every outfit/accessory pairing, all hairstyle/color combinations and all six complexions. The web asset resolver needs `@react-native/assets-registry` matching React Native 0.88.0. Browser fixtures and Hermes exports do not establish native-device appearance or performance acceptance.

The variety expansion adds regressions for v1/v2 compatibility and range boundaries, free presets, ownership-aware randomization, face/build/gear saves, reset to the saved v2 appearance, SQL range rejection, and preserving expanded identity fields through Undo. Visual fixtures cover all 42 face/complexion choices, all 18 build/outfit combinations, all 18 build/accessory combinations and representative hair/color/face combinations. Source prompts and limitations are in [VARIETY.md](../apps/mobile/assets/avatar/VARIETY.md).

The additional face catalog appends IDs 3-6 without changing v1 or earlier v2 ordinals. Its prompts and provenance are in [FACES.md](../apps/mobile/assets/avatar/FACES.md). Deploy the expanded SQL CHECK/API face range before this client; older clients that only support faces 0-2 show the starter fallback for newer face IDs. No history rewrite is needed. Save/restart/account-isolation tests include each of the four new faces, and Undo retains the new face while removing a revoked reward slot.
