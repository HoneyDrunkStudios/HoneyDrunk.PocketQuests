# Journal artwork

Approved Pocket Quests adventure-journal direction, October 18, 2026. Generated with OpenAI Image Gen using the approved Home and quest-card mockups as references. These are decorative bundled assets; no account data, UI labels, progress or controls are baked into them.

- landscape.png: contemporary riverside bench and city at dusk, shared scrolling header and subdued rank background.
- traveler.png: illustrative character portrait, not a user photo.
- compass.png, books.png, sketchbook.png: category/attribute/skill artwork.
- work-scene.png and sketch-scene.png: wide illustrated quest-card scenes with dark space for live content.
- work.png: retained original work spot illustration; sketchbook.png remains the Skills artwork.
- rank-shield.png: blank shield; the current rank is native text.

Libre Baskerville is bundled separately under ../fonts with its SIL Open Font License. The Ionicons font is supplied by the SDK-compatible @expo/vector-icons package. Font loading uses expo-font at runtime; it was already present transitively in the existing SDK. No config-plugin or provider change is required for runtime loading.

Runtime PNGs are resampled from the approved originals into Metro's 1x, 2x and 3x density variants. The imports keep the unsuffixed filenames; Metro selects the appropriate density. Base dimensions are 390x210 for the landscape, 112x112 for the portrait, 360x180 for card scenes, 80x100 for the shield, 80x80 for books/compass, and 80x67 for sketchbook/work. All density variants have exactly proportional dimensions. The landscape fills the header behind both the title and settings gear. Card art preserves its 2:1 aspect ratio at the bottom of dynamically sized cards.

The first journal originals remain recoverable from Git commit `8fee082f129d7c712d01e3bf78864f5ebc7d17c7`; they are no longer bundled. The approved city revision uses Image Gen reference-guided assets, with source SHA-256 hashes, runtime dimensions and byte sizes in [the city asset manifest](../../../../docs/design/city-journal-20261018/asset-manifest.json). Card edit prompts are retained beside the manifest. Landscape generation requested clean editorial bench/river/city scenery, canopy and dark navy fade; portrait generation requested an everyday traveler in a navy jacket with the same city palette. Neither contains UI text.

The city assets use Sharp Lanczos3 resizing and PNG compression level 9 with a 256-color palette. The complete journal raster set now has 27 files totaling 2,218,230 bytes, versus 3,373,561 before this revision, despite adding two card scenes. At 3x, decoded RGBA storage is approximately 2.81 MiB for the header, 2.22 MiB per card scene and 0.43 MiB for the portrait. These are pixel-buffer estimates, not measured device memory. Reference and QA images under docs are not runtime imports.

Native memory/scrolling, text scaling and screen-reader acceptance remain deferred device checks before release. Browser and bundle checks do not establish native performance.
