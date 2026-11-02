# Avatar variety expansion

Generated with the built-in image-generation tool on 2026-10-27. Original PNG outputs are copied without raster modifications. Atlas crops, colors and placements are composed at runtime by avatar-art.ts and avatar-figure.tsx. All assets have RGBA transparency; preserve alpha.

| Atlas | Dimensions | Contents |
| --- | --- | --- |
| faces-expanded.png | 2172 x 724 | Angular / Round faces, six skin tones each |
| outfits-builds.png | 1774 x 887 | Four original outfits in Broad / Full builds |
| outfits-starters.png | 1536 x 1024 | Field shirt / City jacket, Balanced / Broad / Full builds |
| hair-expanded.png | 2172 x 724 | Tied back / Braided / Coils grayscale cutouts |
| accessories-starters.png | 1774 x 887 | Round glasses / Linen scarf |

Shaved uses no hair sprite. Colors are applied through the existing color matrix. Identity choices and new starter gear are free; original reward ordinals retain their meaning. All six outfits have all three builds. A single accessory slot means glasses and scarf cannot be worn together yet. Face variants are whole faces; individual eyes/nose/mouth are not editable.

Source bounds avoid neighboring rows, including the close boot/neck boundaries. Full-body and portrait viewboxes include the new updos. Native-device appearance and performance require device validation; web fixture renders and native JavaScript exports are separate evidence.

See asset-manifest.json for source generation filenames and SHA-256 hashes.

## Prompts

### faces-expanded.png

Create a production character-creator HEAD atlas, genuine transparent background, wide 3:1 landscape canvas ideally 3072x1024. Exactly TWELVE isolated adult bald heads with ears and necks, in SIX COLUMNS and TWO ROWS. Use attached head atlas as reference for painterly material, frontal orientation, skin-tone progression and neck construction, NOT a face to duplicate.
Top row: the SAME new ANGULAR MASCULINE adult face six times: square jaw, stronger brow, straight nose, slightly wider mouth, natural male facial proportions, clean shaven, warm composed expression, age around30.
Bottom row: the SAME new ROUND ANDROGYNOUS adult face six times: rounded jaw, fuller cheeks, gentle eyes, broader soft nose, friendly slight smile, distinct from both the top-row person and the reference face, age around30.
Within EACH row the six columns change ONLY complexion, left to right: light warm ivory; light tan; medium golden brown; rich warm brown; deep warm brown; very deep rich brown. Preserve each row's identity, face proportions, scale and geometry across all six skin tones.
All heads stare EXACTLY FORWARD, both eyes/ears equally visible, no tilt. Bald, no hair, no facial hair, no clothing, no shoulders. Long neck ends in a rounded U, like reference. Same scalp top, chin, eye-line and neck-end baseline within every cell. Natural realistic-sized eyes and head anatomy, subtle painterly skin, soft daylight with very gentle warm highlights, dimensional shading. Sophisticated hand-painted game character art matching reference. No glossy plastic, no photo, no cartoons.
Generous empty margins and separation. Every head fully contained in its own equal cell. No labels, words, panels, colored backdrop or glow outside silhouettes. Completely transparent pixels outside each head/neck.

### outfits-builds.png

Expand the attached Pocket Quests clothing atlas into TWO new adult BODY BUILDS. Production interchangeable clothing sprites, TRUE TRANSPARENT background. Exactly EIGHT headless clothed figures arranged FOUR COLUMNS by TWO ROWS on a wide 2:1 canvas.
Top row all four outfits on the same BROAD build: noticeably broader shoulders, straighter waist, strong thick arms and legs, natural masculine body silhouette, not bodybuilder armor.
Bottom row all four outfits on the same FULL build: softly rounded larger belly and hips, full thighs and upper arms, a genuinely plus-size adult silhouette, confident and comfortable, not just a uniformly stretched thin person.
Columns in BOTH rows match the original outfits exactly: 1 blue Trail tunic and brown belt, 2 ochre Maker's vest over ivory long-sleeve shirt, 3 muted purple Cozy knit sweater with belt, 4 navy Scholar's long coat with teal lining and brass details. All have identical dark practical trousers, brown lace-up boots and full-finger brown leather gloves. Garments fit each build naturally with appropriate draping and tailoring.
All figures perfectly FRONT FACING, same relaxed A-pose with arms slightly out and gloved hands down. Same neck, waist, glove and boot baselines in each cell. Each cell equal size with clear padding, boots fully visible. No head, face, hair or neck. NO EXPOSED SKIN: every neckline opening must be fully transparent, including throat and back-collar center, for a separate head/neck layer. No accessories, bags, cape, weapons, text or labels.
Maintain the reference's realistic painted fabric/leather, adult anatomy, dimensional folds, warm subtle edge light and navy shadows. Do not make flat-vector cartoons. The two builds must be visibly distinct while all clothing stays recognizable. Never overlap adjacent cells.

### outfits-starters.png (initial)

Production Pocket Quests paper-doll clothing atlas, genuine transparent background. Exactly SIX headless clothed bodies arranged THREE COLUMNS by TWO ROWS. All dead-straight FRONT VIEW, adult realistic proportions, relaxed arms slightly away from hips, full-finger dark brown gloves, dark trousers and lace-up brown walking boots. No visible skin or neck: neckline holes entirely transparent, no heads, no hair, no accessories, no text.
Rows are two new FREE starter outfits:
TOP ROW: Field shirt — a soft sage-green long-sleeve button-up field shirt, sleeves rolled only over gloved wrists, small chest pockets, brown waist belt, charcoal trousers, brown boots.
BOTTOM ROW: City jacket — muted teal-blue short practical canvas jacket over a cream shirt, simple open front, brown belt, charcoal trousers, brown boots. No bags or hood.
Columns are three distinct builds, each outfit tailored to fit:
COLUMN1 Balanced: the slim-average silhouette in reference image1.
COLUMN2 Broad: wider shoulders and straight waist, sturdy arms and legs, as top row of reference2.
COLUMN3 Full: rounded belly, fuller hips/thighs/arms, natural plus-size build, as bottom row of reference2.
Use IDENTICAL stance, neck center, waist and boot baselines in every cell, equal figure height. Clear generous padding between all six cells; boots fully visible. Same natural adult body ratios, not shortened legs or enormous gloves. Match reference hand-painted game art, softly dimensional cloth and leather, subtle warm edge lighting, navy shadows. Do not switch to vector, chibi or plastic 3D style. Cutout alpha=0 outside clothes.

### outfits-starters.png (neck and background extraction)

Precise production cutout edit: remove all background/glow around and between these six clothing figures, and make all six neckline openings fully transparent. Remove the solid tan/olive neck-stump-shaped fill inside each collar, including the short triangular throat area. Preserve garment collars and all cloth, trousers, gloves and boots. Preserve exact canvas dimensions, figure locations, poses, sizes and colors. Do not move or resize anything. No skin, neck or head added. The result must have alpha=0 outside every clothing silhouette and inside every neckline hole.

### hair-expanded.png

Production game character HAIR-ONLY sprite atlas, exactly THREE distinct hairstyles in three equal columns on genuine transparent background. Match the attached painted hairstyle reference, all neutral SILVER GRAYSCALE for runtime tinting. No faces, no heads, no skin, no mannequin. Each wig has a fully transparent face opening and complete scalp coverage, facing DEAD STRAIGHT FORWARD. All share the same human adult head width, eye-line and forehead location. Generous separation.
LEFT: Tied back — smooth practical hair pulled into a high ponytail visible behind the crown, soft side-parted fringe and small temple strands; no face.
CENTER: Braided — neat cornrow braids pulled backward into a bun visible atop/back of head, fine realistically painted braid texture, natural rounded hairline, unobstructed face opening.
RIGHT: Coils — a full rounded natural afro with dense small coils, rich strand texture, slightly asymmetrical organic outline, carefully shaped forehead opening. Make it clearly different from loose curls.
Detailed semi-realistic painterly locks and dimensional shading in neutral gray. Soft highlights, no gold/brown tint, no colored glow, no backdrop, no labels, no text. Keep every hairstyle wholly inside its cell, never touching neighbors. Full scalp wigs, not individual loose clumps.

### accessories-starters.png

Create exactly TWO production paper-doll accessory sprites on a transparent landscape canvas, separate equal left and right cells. Sophisticated semi-realistic painted game art matching the reference, warm amber edges and navy shadows, real materials.
LEFT: front-facing round tortoiseshell eyeglasses for an adult face. Two round lenses connected with a small bridge, subtle dark brown rims, tiny folded side arms. Lenses must be transparent with only a very subtle highlight so the eyes show clearly beneath. Perfect frontal orientation, symmetrical, no face/head/skin.
RIGHT: front-facing rust-orange neck scarf, softly wrapped at the base of a neck with a loose small knot and two short tails draping toward upper chest. Natural linen texture and folds, compact and wearable with any shirt, visible open/transparent neck hole. No head, body, skin, shoulders or mannequin.
Each object entirely isolated, no overlap, plenty of clear margin. No labels, text, panels, backdrop, ground or glow. True alpha transparency everywhere outside objects and inside the lenses/neck opening.
