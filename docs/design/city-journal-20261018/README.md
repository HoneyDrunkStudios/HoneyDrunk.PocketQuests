# City journal visual QA

final result: passed

Approved scope: blend the original rich navy/ivory/gold adventure journal and illustrated quest cards with contemporary bench/river/city scenery. The illustration extends to the top behind Pocket Quests and the settings gear. This approval authorizes Pocket Quests product styling; shared HoneyDrunk.UI ownership remains unchanged.

## Comparison evidence

The `home-comparison.png` and `quests-comparison.png` composites show the approved mockup on the left and implemented production components on the right, both at 390x844. Reference images were normalized from 853x1844. Implemented captures use the real bundled font, assets and components with a synthetic local session/router adapter outside production source. They are visual evidence, not API, device or provider acceptance. `pending-320.png` verifies a narrow, scrolled pending card.

Typography retains Libre Baskerville headings and native body/control fonts. Rich navy, ivory and gold remain; the chooser now uses the shared gold primary token and Creativity retains purple. City artwork fills the full header, and card scenes sit behind live text with protected action/warning backgrounds. Controls retain their existing minimum sizes, copy and dynamic heights. No sample balances or UI labels are rasterized or persisted.

## Review and corrections

- P2 resolved: the first generated card scene placed bright objects behind long titles. Revised art reserves dark space for labels and anchors the smaller vignette to the right.
- P2 resolved: narrow pending-card action text crossed bright sketchbook details. Added a dark action background; pending, frozen and accepted-loss text also retain a solid readable surface.
- Preserved proportional card artwork rather than stretching it with variable card height. Decorative art has no pointer events and is excluded from accessibility.
- Checked Home/Quests navigation, Active selection, narrow wrapping and the full pending warning. The real browser journey now navigates through the header gear to exercise its Expo route and accessibility name.
- Explicit defect-first author review covered navigation, layering, safe-area behavior, live metadata, pending/Undo paths, asset density/payload, shared UI boundaries and unchanged API/session contracts. No remaining actionable P0/P1/P2 finding in this scope.

## Deliberate differences and limits

The app keeps its actual date/account-zone guidance, full warnings, 48-pixel actions and dynamic content. These need more space than fixed mockup content, so the second card requires scrolling. At narrow widths the brand/title may wrap. Art is a clean stylistic adaptation rather than a pixel-identical illustration. Existing rank/path assets are retained.

TypeScript, lint, architecture, UI/API snapshot and security-patch checks plus the 303-test component suite passed during implementation. Web/Android/iOS exports passed. Exact-head browser/API/SQL evidence is recorded separately by the existing CI harness and the PR. Native safe areas, dynamic type, screen readers, scrolling/memory and live provider login remain deferred founder/device acceptance. Sign-in provider branding is a separate next step.

Asset provenance and size details are in [the runtime artwork README](../../../apps/mobile/assets/journal/README.md) and `asset-manifest.json`. Earlier lake-style approval and PR #28 evidence remain in `../journal-20261018` and the historical portion of root `design-qa.md`.
