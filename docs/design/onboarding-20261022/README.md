# October 22 welcome, onboarding and reference correction

final result: passed

Scope: the requested local visual/onboarding correction. This is not full-app, physical-device, production or fresh-provider acceptance. No push or PR is included.

October 25 publication note: the captures below remain dated evidence. The current welcome helper text is provider-neutral; the historical ready-state capture's Google/email wording is superseded and does not establish provider availability.

## Findings and fixes

- Resolved P1: Android headings used a system fallback instead of the journal serif. Bundled a renamed static bold face and selected its registered runtime style. Native welcome-step evidence confirms the serif now renders.
- Resolved P1: the native header image did not span its full container. Moved padding into the masthead child and kept the image container at its actual aspect ratio. The city art now spans the screen behind the live masthead.
- Resolved P1: setup was embedded in Quests alongside profile assessments. Introduced a separate guided route with optional interests, first-quest guidance and existing recovery controls. Only confirmed server state unlocks the tabs.
- Resolved P2: the enlarged quest artwork reduced contrast for wrapped titles, status and metadata at 320 pixels. Added dark text backings; retained the full unconfirmed-reward warning. See `quests-320.png`.
- Resolved P2: initial Home spacing pushed Skills below the default fold. Reduced header/card/rank/path gaps and recaptured all three collapsed path rows above navigation.
- Developer-only overlap: the second gear was Expo's floating Tools button. Hid it through the developer menu; no duplicate product control was removed or painted out.
- P3: object silhouettes, portrait and rank foliage differ from the supplied illustrations. They retain the existing clean city asset family; further exact-art refinement is optional.

## Reference and comparison evidence

Source: the founder's Photo 1 (Quests) and Photo 2 (Home), supplied October 22. The Home photo includes image-viewer close/download controls; those are excluded from the product target. Earlier direction explicitly requested artwork behind the Pocket Quests title and settings gear, so the implementation retains that treatment rather than the photos' separate solid masthead.

Source and implementation were compared locally (reference left, implementation right), including the full composition and visible heading, XP/rank, path-row, title/metadata and navigation regions. Founder-supplied reference images and comparison composites are retained locally and are not included in this repository. The implementation-only browser captures render production components with an isolated Level 4/1,240 XP account fixture; their routing/auth adapters do not establish end-to-end behavior. Screens use a 390 x 844 CSS viewport scaled by the desktop panel to about 242 x 524 pixels. Captures were cropped to the app and normalized to 390 x 844. This introduces resampling softness; it is not an asset-resolution measurement or pixel-difference score. Native onboarding evidence is a separate 1080 x 2400 capture.

- `home.png`, `quests.png`: final production-component fixture captures.
- `quests-320.png`: scrolled 320-pixel pending-card state, including complete warning and detail action.
- `signin-fixture.png`: actual Welcome/JournalEntry components with a ready auth fixture; the sign-in control is not connected in this visual preview.
- `onboarding-android.png`: real signed-in first-run introduction; account setup remains incomplete.
- `welcome-onboarding.png`: ready-state welcome fixture alongside real Android onboarding.

## Fidelity surfaces

| Surface | Review |
| --- | --- |
| Typography | Bold serif headings now verified on Android. Body text remains system sans. Titles wrap; no truncation added. The preview's missing body font reset was corrected before final Home/quest comparisons. Large native text and screen readers remain untested. |
| Spacing/layout | Full-width art, portrait/XP panel, illustrated rank and three paths preserve the selected hierarchy. All collapsed Home rows fit at the default fixture size. The taller full-background header and minimum 48-pixel controls make Active quests taller than the photo; scrolling is intentional and the pending card remains reachable. |
| Colors/tokens | Existing navy/ivory/gold palette and purple pending Creativity accents remain. Local dark backings protect text over brighter scenery. No shared UI token package was edited. |
| Image quality | Existing city/character/object assets reused; card scenes enlarged without distorting aspect ratio. New portrait welcome art is bundled JPEG with recorded provenance. No text, controls, status or XP is rasterized. Native header capture confirms the former crop defect is fixed. |
| Copy/content | XP, rank, dates and warnings remain data-driven. Date formatting, exact reward wording and 24-hour planned time retain the existing domain behavior rather than copying decorative sample values. Sign-in uses the real shared sign-in/create-account flow. |

## Interaction and validation

- Android: observed authenticated setup, stepped through introduction/interests/first-quest guidance and back; no selections, completion, quest changes or sign-out were submitted. No onboarding tab bar or settings gear leaks into the flow.
- Browser: inspected Home, Active, narrow pending warnings, rank expansion/collapse, full-screen Welcome ready fixture and actual web recovery state. Actual web provider setup was unavailable during this run; do not interpret the fixture's button as provider acceptance.
- Full mobile suite: 309/309 passed. Focused post-change character/onboarding/slice tests: 21/21; rank: 4/4; quest journal: 7/7.
- Typecheck/UI snapshot, lint, architecture, API-contract and security-patch checks passed. Android Hermes export passed with explicit development endpoints; it is not a release build.
- API/SQL browser journey selectors were updated but the lane was not rerun: its fixed fixture ports are occupied by the retained watch-along stack. Run it in isolation before publication. Native large text, iOS, physical-device performance, completion/Undo, restart and offline acceptance remain open.

## Explicit code-review pass

Reviewed the route guards, state-loading/recovery paths, finish command acknowledgement, optional saved interests, returning-account redirect, font registration/license, image sizing, touch targets and updated journey selectors. Confirmed no local XP grant, account reset, provider credential change or shared UI snapshot edit. Found and fixed the narrow artwork/text contrast issue and Home spacing. Recovery remains visible during loading, offline and pending states. No remaining P0/P1/P2 finding in this scoped review; unrun acceptance lanes are listed above.

Checklist: implementation and scoped checks complete; local evidence saved; account preserved; full walkthrough and isolated browser journey pending; changes unpublished.
