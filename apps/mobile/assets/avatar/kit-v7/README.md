# Canonical traveler kit

PNG sources were generated with built-in Image Gen and copied unchanged. `provenance.json` records prompts, source identifiers, references, hashes and registration measurements.

- Outfits: 1086x1448, three builds across and two poses down, including attached neck and hands.
- Faces: 1774x887, four columns by two rows; seven heads and an empty eighth cell.
- Hair: 2172x724 triptychs of canonical bald head, worn hair and hair-only layer. Runtime uses panel three.

Source coordinates, masks and uniform transforms live in `src/features/profile/avatar-kit-data.ts`. All faces and hair share one skull registration. New art must fit that template and pass enlarged checks against the supported face/build/pose/outfit matrix. Correct anatomy in source art rather than adding detached neck/hand patches.
