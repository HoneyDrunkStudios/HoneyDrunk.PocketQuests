# Journal artwork

Approved Pocket Quests adventure-journal direction, October 18, 2026. Generated with OpenAI Image Gen using the approved Home and quest-card mockups as references. These are decorative bundled assets; no account data, UI labels, progress or controls are baked into them.

- landscape.png: dusk lake, shared scrolling header and subdued rank background.
- traveler.png: illustrative character portrait, not a user photo.
- compass.png, books.png, sketchbook.png: category/attribute/skill artwork.
- work.png and sketchbook.png: quest-card spot illustrations.
- rank-shield.png: blank shield; the current rank is native text.

Libre Baskerville is bundled separately under ../fonts with its SIL Open Font License. The Ionicons font is supplied by the SDK-compatible @expo/vector-icons package. Font loading uses expo-font at runtime; it was already present transitively in the existing SDK. No config-plugin or provider change is required for runtime loading.

Runtime PNGs are resampled from the approved originals into Metro's 1x, 2x and 3x density variants. The imports keep the unsuffixed filenames; Metro selects the appropriate density. Base dimensions are 390x245 for the landscape, 112x112 for the portrait, 80x100 for the shield, 80x80 for books/compass, and 80x67 for sketchbook/work. All density variants have exactly proportional dimensions. The larger landscape supports the full-width header; spot art remains sized for the 72x48 and 80x52 controls.

The originals remain recoverable from Git commit `8fee082f129d7c712d01e3bf78864f5ebc7d17c7`; they are no longer bundled. Production variants were exported with Sharp's Lanczos3 resampling and PNG compression level 9, retaining alpha and the approved composition. The total raster payload, including every density, fell from 12,340,827 to 3,373,561 bytes (73% smaller). At 3x, decoded RGBA pixel storage is at most 0.43 MiB for the portrait, 0.28 MiB for the shield and 0.22 MiB for each spot illustration, compared with roughly 6 MiB per original. These are pixel-buffer estimates, not measured device memory.

Native memory/scrolling, text scaling and screen-reader acceptance remain deferred device checks before release. Browser and bundle checks do not establish native performance.
