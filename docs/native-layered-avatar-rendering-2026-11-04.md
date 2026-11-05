# Native layered-avatar verification — November 4, 2026

Android development-build testing exposed two defects: SVG filters hid hair and recolored scarves, and switching hair atlases could retain the preceding clip. The correction uses canvas filter coordinates on Android while retaining atlas coordinates on web/iOS, and changes clip references with atlas regions. Appearance codes, colors, ownership, assets and saved history are unchanged.

## Source and runtime

- Base head: `48f9f74606e12dd681dd885fe53afe7d58cebc8e`, with this commit's correction. Native-tested production `avatar-figure.tsx` Git blob: `3b7ffcfe56fd3adf516c359da8f477ddbf6e9694`.
- Render-only [fixture](../apps/mobile/tests/native/avatar-render-fixture.tsx) Git blob: `671613b91700d0377ffa3d98801dd24a0af68204`. It imports production `AvatarFigure`; it has no account/session/API dependency.
- Android 16 / API 36, x86_64 emulator, 1080 × 2400, React Native 0.88.0, Expo 58.0.19, react-native-svg 15.16.0.
- Separate package `com.honeydrunk.pocketquests.avatarqa`, debug APK SHA-256 `BD1129D7B18A35E042C65BE10A4E9719E57C7964DCCF4D9ED676B312BD10174E`. Metro supplies the stated JavaScript source; APK identity alone does not identify that source.
- Native `com.horcrux.svg` image/filter views were present. These images came from the Android application, not `elements.web.js`.

## Results

Each color was checked beside a constant original-linen reference, with satchel, glasses and cape compositions. Skin/face, chestnut hair, brown vest, cream shirt, trousers and accessory colors remained unchanged; only the scarf tint changed. All four previews rendered together with independent filter IDs.

| Scarf | Native result |
| --- | --- |
| Original linen | [Visible](validation/native-avatars-2026-11-04/android-fixed-original.png) |
| Moss | [Visible](validation/native-avatars-2026-11-04/android-fixed-moss.png) |
| Ocean | [Visible](validation/native-avatars-2026-11-04/android-fixed-ocean.png) |
| Plum | [Visible](validation/native-avatars-2026-11-04/android-fixed-plum.png) |
| Rust | [Visible](validation/native-avatars-2026-11-04/android-fixed-rust.png) |
| Midnight | [Visible](validation/native-avatars-2026-11-04/android-fixed-midnight.png) |
| Cream | [Visible](validation/native-avatars-2026-11-04/android-fixed-cream.png) |
| Gold | [Visible](validation/native-avatars-2026-11-04/android-fixed-gold.png) |

[Before the correction](validation/native-avatars-2026-11-04/android-before-fix.png), filtered hair and the Moss scarf were absent. The unfiltered original scarf and other accessories remained visible, isolating the filter defect.

[Portrait rendering](validation/native-avatars-2026-11-04/android-fixed-portrait.png) also passed. Cycling Swept → Curls → Long → Cropped → Tied back reproduced a stale crop; changing the region's clip reference restored the complete hair atlas. The [final Tied back/Moss portrait](validation/native-avatars-2026-11-04/android-fixed-tied-portrait.png) records that corrected transition with all four compositions.

The eight-color matrix used the final Android filter configuration before adding its platform conditional and the clip-reference correction. The final Android branch preserves those filter settings; the last portrait replay used the native-tested source blob above. The subsequent analyzer cleanup qualifies `Number.parseInt`; that is the same standard parsing function, and leaves filter settings, matrices and clip geometry unchanged. Browser portrait checks separately confirmed web atlas filtering, including 320px controls and doubled text. Component regressions verify platform-specific filter coordinates, preserved alpha, independent references and clip replacement; they supplement the native images.

## Repeat the check

1. Create an isolated Expo development application using the product's locked Expo/React Native/react-native-svg versions and a distinct package ID. Do not overwrite the product entrypoint or install over an existing account-bearing app.
2. Add this checkout's mobile directory to Metro `watchFolders`, resolve the locked mobile dependencies, and register `AvatarRenderFixture` as the isolated application's root with Expo `registerRootComponent`.
3. Build/install the native development client with SVG autolinking. Start Metro on an owned loopback port and connect the emulator. On Windows, the test used an x86_64-only debug build and a short CMake staging directory with `CMAKE_OBJECT_PATH_MAX=240` to avoid long object-file paths; no dependency source was patched.
4. Cycle every scarf color, keeping the reference avatar visible. Check satchel/glasses/cape, toggle portrait, and cycle hair across the original and expanded atlases. Capture after image loading settles; compare unchanged garments/skin against the reference.
5. Close the automation session and remove its owned port mapping. No account or backend is needed.

## Unrun checks

iOS, physical devices, screen readers, a real account walkthrough and signed distribution remain unrun. This Windows session provided Android development-build evidence only. The iOS acceptance limitation remains explicit; no release or universal native acceptance is claimed. Fresh hosted CI, actual Sonar findings and native Codex review remain separate merge checks.
