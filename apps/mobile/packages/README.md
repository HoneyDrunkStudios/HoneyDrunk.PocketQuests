# HoneyDrunk UI foundation

Local npm workspace snapshots from [HoneyDrunk.UI](https://github.com/HoneyDrunkStudios/HoneyDrunk.UI), the shared implementation owner. See upstream.json for the exact reviewed source revision. Pocket Quests is the first consumer. Both packages are private and unpublished. Run npm ci from apps/mobile; no extra package manager or build step is required. Expo consumes the TypeScript source.

ui-tokens contains a platform-neutral semantic Theme contract and sizing defaults. ui-native contains ThemeProvider, Button, Label, Card, Input, Loading and Notice, with React/React Native supplied as peers by the app. No navigation, session, quests, XP, fonts, assets or service dependencies belong in these packages.

Each app supplies a full Theme to ThemeProvider. Pocket Quests owns src/theme.ts and its journal/status-panel decoration. createStyles supports the existing style-based screens; those styles are generated from the same app theme. A future runtime theme switch must also regenerate app-level styles and navigation options.

The browser uses React Native Web; there is no separate HTML/CSS component package. Native device interaction remains a separate validation step.

Synchronize generic source from a reviewed upstream revision deliberately, updating upstream.json and rerunning app checks. The snapshots keep fresh app setup and CI working before any registry release; product theming remains in src/theme.ts.

## Reproducible updates

Run npm run check:ui to verify the complete generic file inventory and SHA-256 hashes against upstream.json. Typecheck runs this check automatically, including in existing CI. Text hashes normalize CRLF to LF so Windows and Linux agree. Added, removed, or locally edited generic package files fail the check. Product theme edits do not affect it.

To restore/synchronize the pinned commit from a local HoneyDrunk.UI checkout:

```sh
node scripts/ui-snapshot.cjs --source PATH_TO_HONEYDRUNK_UI
```

To intentionally update to a separately reviewed commit, provide --revision EXACT_40_CHARACTER_SHA. The script validates source origin, reads committed files with git show (ignoring working-tree edits), refuses to overwrite an already divergent local snapshot, and records the new revision/file hashes. It performs no fetch, install, commit or publish. Then run npm run typecheck, npm run lint and npm run test:logic, review the consumer diff, and accept those changes explicitly. npm ci is needed only if package dependencies change.

Before extraction the app had no root JavaScript workspace. Keeping the small reviewed source snapshot avoids requiring a sibling checkout or unpublished registry package during app setup/CI. HoneyDrunk.UI remains the source owner; generic fixes belong upstream, followed by this explicit snapshot update.
