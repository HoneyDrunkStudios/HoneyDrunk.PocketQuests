# Painted frontal avatar assets

Generated using the built-in image-generation tool on 2026-10-27, from the user-approved Pocket Quests traveler concept. These are original generation outputs, copied without raster modifications. The renderer selects source rectangles and composites them at fixed anchors. Background RGB may contain color under zero alpha; always preserve PNG alpha.

- outfits.png: 1774 × 887; four front-facing clothing/boot/glove sets.
- faces.png: 1536 × 1024; six independently painted complexion variants in row-major order.
- hair.png: 2172 × 724; four grayscale hair shapes, colorized at render time with a luminance-preserving color matrix.
- accessories.png: 1774 × 887; satchel, utility belt, cape.

Stable v1 appearance slot IDs and reward requirements are unchanged. Gloves are part of each outfit. The original four atlases provide the Balanced build and Soft face. The [variety expansion](VARIETY.md) adds Angular/Round faces, Broad/Full builds, more hair, and free starter gear; all original ordinals stay stable.

The outfit atlas also received the precise neckline extraction edit recorded below. The renderer uses a single SVG definition scope for native clip/filter resolution. A face and neck share one complexion source. The back collar passes behind the neck, and atlas crop bounds exclude neighboring sprites. Source rectangles and placements are in `src/features/profile/avatar-art.ts`.

### Final outfit neckline edit

Make one precise edit to this four-clothing-layer atlas: remove every exposed SKIN pixel inside the neck openings of the tunic, vest/shirt, and coat/shirt. Those little tan neck/throat areas must become fully transparent holes so a separate skin-tone neck can show behind each garment. The purple sweater should remain unchanged. Preserve the neckline edges, collar fabric, garments, gloves, boots, pose, positioning, dimensions and ALL other pixels as closely as possible. Do not add a neck, head, face, background, labels, shadows or any new object. Preserve existing transparent background. The result is the same four interchangeable clothing sprites with empty transparent neckline openings, no skin anywhere.

## Generation prompts

### outfits.png — initial generation

Create a production 2D paper-doll CLOTHING sprite atlas for Pocket Quests, referencing the attached approved painterly traveler concept. Four separate HEADLESS clothed adult bodies arranged in exactly FOUR equal columns across one landscape transparent canvas. Each body is facing STRAIGHT FORWARD, torso and feet square to camera, symmetrical relaxed standing pose, arms slightly out from hips, gloved hands relaxed pointing down, no turn, no profile. All four use IDENTICAL underlying body geometry, shoulders, arm position, waist, legs, gloves, boot position and scale, so they are interchangeable wardrobe layers. Natural adult proportions, shoulders narrower than exaggerated heroic style. This is a headless mannequin clothing asset: no head, no hair, no exposed skin, no neck stump, no face anywhere. Clothing starts at its neckline at top; will receive separate head and neck layer in code. Dark brown leather full-finger gloves cover hands and wrists completely. Dark charcoal practical trousers and brown lace-up walking boots identical across four bodies. Preserve detailed realistic painted cloth folds, soft materials, understated warm edge light and navy shadows matching reference. Not vector, not chibi, not cartoon, no outline.
COLUMN 1 navy-blue long-sleeve Trail tunic with small open V neckline, brown waist belt, subtle linen texture.
COLUMN 2 ochre Maker's vest over ivory long-sleeve shirt with small collar, brown waist belt.
COLUMN 3 muted dusty purple Cozy knit long-sleeve sweater with small ribbed crew neckline, realistic fine cable knit, brown waist belt.
COLUMN 4 navy Scholar's coat, long open-front coat ending around knees, muted teal lining and discreet brass buttons, ivory under-shirt visible at small neckline, brown waist belt.
No bags, no cape, no accessories. Exactly one body per column and generous empty padding between, centered within equal cells, identical neckline baseline, waist baseline and boot baseline across all four. Do not crop boots or gloves. True transparent background; no background panel, no labels, no text, no floor or ground shadow. Use almost the full canvas height for neck-to-boots, wide enough each figure never overlaps another.

### faces.png

Production 2D paper-doll HEAD sprite atlas for Pocket Quests. Use attached approved concept only for painterly style and warm approachable adult traveler facial identity. Create exactly SIX isolated bald heads with ears and long necks, in a precise 3 columns by 2 rows grid on genuine transparent background. Every cell shows the EXACT SAME adult face with same geometry, natural small eyes and soft smile, facing perfectly STRAIGHT FORWARD, both eyes and both ears equally visible, nose centered, no tilt or rotation. No hair, no brows that depend on hair color (use subtle neutral brown brows), no shoulders, no torso, no clothes, no accessories. Same frontal lighting, same portrait size, same pose, same face across all six. Each neck extends straight downward beneath chin and ends in a shallow rounded U, for placing inside a shirt neckline. Leave generous padding around heads and necks; each stays in its own equal cell.
Only skin tone changes between cells, in row-major order: (1) light warm ivory, (2) warm light tan, (3) medium golden brown, (4) rich warm brown, (5) deep warm brown, (6) very deep rich brown. Make all six tones clearly distinguishable, rich and natural. Maintain consistent realistic face shape, luminous dark brown eyes with light sclera, naturally colored lips and individual subtle skin shading for every tone. These are six complexion options of one character, not six different faces or ages.
Art: beautifully detailed semi-realistic painted game portrait, natural adult anatomy, realistic nose bridge, cheekbones, eyelids, lips, warm soft edge light from upper left, navy-tinted shadows. Match reference illustrated world, avoid photograph, flat vector shapes, chibi, oversized eyes, cartoon outlines, shiny 3D plastic. Completely transparent background, no panels, no text or labels, no checkerboard pixels.

### faces.png (background extraction)

Edit this six-head sprite sheet. Remove ALL background, brown glow, halos and backdrop between and around the heads. Output a TRUE transparent PNG with alpha=0 everywhere outside the six head-and-neck silhouettes, including the area surrounding ears. Preserve all six faces, necks, skin tones, shapes, coordinates, resolution and spacing exactly. Do not move or resize any head. Keep skin lighting inside the silhouettes only. No new elements, no text. This is clean background extraction for modular game sprites.

### hair.png

Create a game paper-doll HAIR atlas. Exactly FOUR separate HAIR-ONLY cutout sprites, arranged horizontally in four equal cells across one wide canvas. TRUE TRANSPARENT BACKGROUND. Absolutely no faces, skin, heads, necks, mannequins or backdrop. Hair forms an empty opening where the frontal face goes. All are viewed DEAD STRAIGHT FROM FRONT, with identical head width, same center of forehead and same ear position. They fit one natural adult female traveler face like the reference. Painterly semi-realistic individually painted hair locks, detailed softly shaded strands, no flat vector outlines. Render all hair as neutral SILVER GRAYSCALE, monochrome, to enable runtime colorization with preserved shadow and highlight detail. No colored light, no gold glow. Four styles left-to-right:
1 Swept: short side-swept layered hair, full scalp coverage, side sections to ears, open face below fringe.
2 Curls: natural rounded springy curls around crown and temples, full scalp coverage, open face.
3 Long: center-parted long wavy hair draping down BOTH sides to upper chest with large fully empty central opening for the entire face and neck. Keep long tresses at the sides, never crossing the empty face.
4 Cropped: close-cropped textured pixie, slightly tousled top and short tapered sides, full scalp coverage, open face.
Each hair sprite is a clean wig cutout floating on transparent canvas, front elevation, same adult head scale and aligned scalp crown/forehead positions, no full person. Consistent soft upper-left lighting. Leave ample empty separation, no text, no labels, no frame, no ground, no glow or background color.

### accessories.png

Production paper-doll ACCESSORY atlas on true transparent background for the attached Pocket Quests clothing sprites. Exactly THREE separate accessories in three equal columns; no people, heads, bodies, skin or mannequin visible. The wardrobe reference is a straight front view; match this frontal camera and detailed semi-realistic painterly style, brown leather grain, navy shadows, soft golden rim light. These are wearable overlays for the same adult figure.
Left column: Explorer's satchel. A brown leather bag with flap and brass buckle hanging at wearer's left hip (image right), with a single long diagonal leather strap extending up to opposite shoulder (image left). Front view, strap is diagonal from upper left to lower right; no loop over the top and no second strap. The whole diagonal strap plus hip bag is one cutout. Shoulder-to-hip height roughly 400 units, bag width roughly140 units.
Middle column: Everyday toolkit. A brown leather waist belt with modest brass buckle, two small utility pouches hanging at both hips, pencil and small folded notebook tucked in one pouch, no weapons. Front view, belt horizontal, around 300 units wide and100 units tall. Center vertically in its column, no body or legs.
Right column: Horizon cape. Entire flowing dark muted teal cloth cape viewed STRAIGHT FRONT, fitted onto invisible shoulders, open center exposing the missing torso. Wide at hem, soft folds and rich painted cloth texture, understated warm rim light, no hood, no ornament. Narrow shoulder collar at top, broad A-shaped silhouette below, cloth drapes behind arms and legs; this is the BACK layer of a dress-up avatar. Approximately680 units tall and420 units wide, no person or clothes visible through it. A short brass chain connects the two shoulder attachments near the top.
All parts completely isolated with generous empty separation, no overlap between columns. No text, labels, shadows on ground, backdrop, panels or glow.

The [additional face set](FACES.md) appends four Asian-inspired identities, each with all six skin tones, for seven total face options. All selections remain independently customizable.
