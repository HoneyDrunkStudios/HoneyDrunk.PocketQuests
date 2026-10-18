# Illustrated journal visual QA — October 18, 2026

final result: passed

Scope: the approved Home, active quest cards, shared typography/palette and navigation. This is a visual/component gate, not native-device, authentication-provider or production acceptance.

## Reference and evidence

Evidence is saved in [docs/design/journal-20261018](docs/design/journal-20261018).

- Source visual truth: `redesign-original-ui-refined-art-home.png` and `redesign-original-ui-refined-art-quests.png` (853 × 1844 pixels each).
- Implementation: `redesign-implemented-home.png` and `redesign-implemented-quests.png` (242 × 524 pixels).
- Intended viewport: 390 × 844 CSS pixels. The desktop preview scales this to fit the panel (approximately 0.621); source is interpreted at approximately 2.188 pixels per CSS pixel. Comparisons use corresponding content proportions, not a pixel-difference score. Saved captures contain only the centered app.
- State: Home collapsed, level 4 / 1,240 XP / rank F; Quests Active, one normal and one pending-sync occurrence. This synthetic session uses the production screen components, bundled images/font and icon glyphs. It substitutes the session/router/native adapters outside the production source. Sample balances are not persisted or included in the app.
- The original and implementation images were viewed together. Focused inspection covered portrait/XP, rank disclosure, path rows, quest metadata, pending warning and bottom navigation. The scrolled pending card was inspected in the live browser capture; its later saved capture was blank when the desktop panel was hidden, so that invalid file is excluded. DOM geometry and accessibility state were checked alongside the scaled captures. Additional 320- and 768-pixel layouts were inspected; the narrow character card stacks.

## Required fidelity surfaces

| Surface | Result |
| --- | --- |
| Typography | Bundled Libre Baskerville supplies bold serif headings; system sans remains for body/controls. Hierarchy follows the selected design. Text wraps instead of truncating; native font rendering and dynamic type still require device acceptance. |
| Spacing and layout | Full-width lake heading, left portrait, separate rank panel and three illustrated path rows are retained. All collapsed Home rows fit at 390 × 844 with zero preview safe-area inset. Real safe areas, expanded details and larger text may require scrolling. |
| Colors | Midnight navy, ivory, muted periwinkle, gold selected states, purple Creativity and coral chooser match the direction. Automated theme text/control contrast checks pass; border contrast was increased during review. |
| Images | Clean editorial landscape, traveler, shield and object illustrations are bundled raster assets. Ionicons supplies real navigation/category icons. No UI or personal data is rasterized. Art is contained beside quest metadata to keep variable titles and warnings legible. |
| Copy | API-derived XP/rank/counts remain authoritative. Due day and planned time stay distinct. Pending sync and timing-unverified states retain unconfirmed-reward wording. Existing date and chooser wording remain explicit rather than copying decorative sample text. |

## Comparison history and fixes

1. P2: the initial compact Home still pushed Skills below the default fold. Reduced header spacing, collapsed-row minimum height and Home gaps; recaptured Home with all three rows visible. Controls retain at least 48-pixel touch height.
2. P2: excessive quest-header gaps and metadata sizing obscured the card hierarchy. Reduced header gaps and metadata type/illustration height; recaptured Active and the scrolled pending card. Cards continue to grow with content; no fixed height clips warnings.
3. P2: web XP bars lacked a numeric ARIA value despite native accessibility values. Added explicit normalized ARIA values and a completion/Undo regression test.
4. P2: the timing-unverified card could show a Pending sync badge. It now says Timing unverified, covered by a regression assertion.
5. P2: automatic iOS content insets could combine with the new header's safe-area padding. Header-equipped scroll owners now use explicit header insets; screens retaining native headers retain automatic adjustment.

## Interactions and limitations

- Browser preview: Home/Quests switching, Available/Active selection, rank expansion/collapse, section expansion and scrolling inspected. No captured browser console errors. Preview Calendar/Progress routing and detail links are adapters, not end-to-end acceptance.
- Component suite: 303 tests pass; the final quest-card suite passes 7/7. Existing tests cover live balance updates, Undo, rank highest/missing-catalog cases, recovery warnings and bounded initial rendering of large history.
- Production routes, API calls, persistence, recovery and rewards retain their existing owners. The real API/SQL browser journey is updated for the new labels/layout and runs in CI; it is distinct from this visual fixture.
- Native font loading, screen readers, text scaling, safe areas and asset memory/performance remain deferred device checks. Authentication branding/social providers remain outside this change.

## Accepted differences and follow-up polish

- Existing date formatting, account-zone guidance, 48-pixel actions and full warnings take precedence over the compact mock's fixed content. The second card's warning may require scrolling from the top; both cards remain accessible in the existing single SectionList.
- Generated spot art follows the approved style rather than reproducing every illustrated stroke. Quest art stays separate from text; no painted gradients or decorative text are recreated as controls.
- P3: further art crop/size tuning after a native walkthrough. Preserve readable text and dynamic sizing when tuning.

No actionable P0/P1/P2 visual or component findings remain in the verified scope. Device acceptance remains open.

## PR #28 review follow-up

- The real browser journey exposed a strict selector failure: React Native Web renders both a labeled portrait wrapper and an inner accessibility image. The geometry assertion now selects the explicit image-role wrapper and still checks that the portrait sits to the left of the XP bar.
- Repeating the visual fixture with actual PNG dimensions exposed landscape overflow that the original URI-only fixture missed. Header and rank backgrounds now explicitly fill their dynamic containers; the browser journey checks their image/container bounds at 320 and 430 pixels. Local browser reproduction verified the before/after bounds and character expansion/collapse sequence.
- The decorative assets now ship 1x/2x/3x density variants, reducing the total raster payload by 73%. See [asset sizes and provenance](apps/mobile/assets/journal/README.md). This addresses the automated review's asset-budget finding; representative-device memory/scrolling remains deferred by the user.
- The tab icon renderers now have stable module-level identities, and the quest due-date label no longer nests conditionals. These address the five reported Sonar findings without changing navigation or date semantics.
- All four navigation tabs have explicit accessible labels and their decorative glyphs are hidden from accessibility. This prevents icon characters from becoming part of the tab names. The existing journey's exact-name navigation checks remain intact; local Chromium reproduced the missing exact-name match before the fix and verified all four names afterward.

The original scaled preview captures above document the approved composition. They are not proof of Metro asset sizing or the final API/SQL journey; the latest exact-head hosted browser artifact and checks supply that separate evidence.
