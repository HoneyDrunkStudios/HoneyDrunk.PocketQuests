# Customizable travelers

## Current rendering contract

The November 6 canonical kit replaces the previous independent neck, cuff and hand assembly. Every outfit is painted from one registered body template. Its neck, arms and hands are already attached; collars and cuffs are drawn around that anatomy. New outfits must fit the template without detached neck or hand patches.

`assets/avatar/kit-v7` contains the original three builds, the seven-face atlas and the hair triptychs. `assets/avatar/body-frames` adds Male Petite and four Female silhouettes, each painted in all six outfits and both poses. Both sets share the same jaw and floor anchors. Petite scales the full figure around the floor anchor. All faces and hairstyles share the same skull template, and heads overlap the attached neck. Measured crops and skin masks live in `avatar-kit-data.ts` and `avatar-body-data.ts`.

Six skin palettes affect measured skin regions, including the body's neck and hands. Face masks exclude eye whites and irises. Hair uses eight independent palettes. Glasses, scarf, satchel, toolkit and cape remain registered overlays. Unique SVG definitions include source/geometry identities; all layers share one SVG root and use supported native color-matrix primitives.

Home centers the full traveler in proportional, bottom-anchored day/night scenes, with rank, level, XP and Wardrobe below. Wardrobe shows the full figure and equipped slots. Both body types support Balanced, Broad, Full and Petite. Choosing Female during creation defaults to Petite until the traveler explicitly picks a build. Face and hair remain independent. Outfit previews use the selected body type and build. Pickers show owned choices only.

At ease (pose 0) and Front-facing (2) are free. Relaxed stance (1) requires `PQ-CAT-B01`. Front-facing uses its own registered body, face and hair art for a straight-on view. Floating islands is free and has day/night theme variants. Golden horizon requires `PQ-CAT-A01`; Moonlit sky requires `PQ-CAT-F02`. Theme changes do not equip or grant rewards.

## Quiet saves and ownership

Edits update immediately through `AppearanceAutosaveProvider`. Writes coalesce and serialize with badge/frame commands, retain failed/offline drafts and obey account-owner guards. Ordinary edits have no Save button or loading overlay; onboarding continuation waits for confirmed state. Avatar changes, resets, randomization and Undo/Redo preserve the current room and all four display slots. Room edits preserve every avatar field.

Current server entitlements govern earned choices. Revocation independently clears ineligible slots while preserving eligible identity and appearance. Re-earning does not silently equip an item.

## Persistence

Existing v1-v5 codes keep their original field meanings. Current entitlements determine which cosmetics can be equipped. Account and immutable QuestCommandHistory use `AvatarCode varchar(24)`.

- v1 stores skin, hair color, hair style, outfit and accessory.
- v2 adds face/build and expanded identity ranges.
- v3 adds extra/scarf color.
- v4 adds pose/background.
- `v5:<11 avatar digits>:<room><upperLeft><upperRight><lowerLeft><lowerRight>` packs the same eleven v4 ordinals plus room choices into exactly 20 bytes. `v5:20000000000:00000` is the starter in an empty loft.
- `v6:<12 avatar digits>:<5 room digits>` appends body type (Male 0, Female 1) to the avatar section and adds Petite as build 3. `v6:200000300001:00000` is Female Petite in an empty loft. It uses 21 bytes.

The client emits v6 for Female, Petite or Front-facing. Other looks still use the earlier formats, including v5 for room choices. Client, domain, SQL CHECKs and EF metadata validate the same lengths and ranges. The existing command digest includes the complete string, preserving receipt replay and conflict semantics. Deploy the expanded CHECK constraints and compatible backend before the updated client. Older clients show their starter fallback for unknown encodings. See [rooms](rooms.md).

## Visual acceptance

Inspect enlarged anatomy and actual Home/Wardrobe/Room captures in addition to tests. Current web inspection covers all 36 outfit/build/pose combinations in both extreme skin palettes, 56 face/hair pairs, 18 gear/build cases and actual light/dark screens. Physical Expo Go rendering, native accessibility and device performance remain separate acceptance checks. Source prompts, identifiers, hashes and measurements are in the kit's `provenance.json`.

## Proportion correction after visual feedback

The initial v7 head was oversized and the room figure too small. All face/hair layers and glasses now share a uniform 90/111 refit around the fixed jaw joint `(200,183)`, retaining the attached-neck overlap. The skull crown is 93, head height 90, and facial source scale 90/340. Portrait framing follows the smaller head. Body, collars, cuffs, skin, equipment ownership and saved choices are unchanged. Room figure height is proportional, with no fixed 320px cap. Wide scenes use 76%; the later immersive portrait composition uses 58% of its taller source scene. Each keeps its measured floor anchor.
