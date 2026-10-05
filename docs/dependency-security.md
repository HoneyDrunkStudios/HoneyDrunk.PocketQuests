# PocketQuests dependency assessment - October 3, 2026

## October 4 remediation and remaining release blockers

A clean install now uses **decode-uri-component 0.5.0**, the patched release for [GHSA-vcc3-ghjq-m6fr](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr). Expo Router's query-string 7.1.3 consumer expects a CommonJS callable; the checked postinstall script adjusts its single import to the decoder's default export. `scripts/query-decoder-patch.json` pins the reviewed consumer hash and both the patch and its check fail on unexpected source. This is a narrow compatibility patch, not a claim that an upstream Expo update contains the fix.

The full `npm audit` now reports **19 high, zero moderate affected package entries**, from the braces and node-forge advisories below. Their current registry versions remain unpatched. No alert is suppressed and the `eas-build-post-install` security audit blocks release builds while these findings remain. Functional CI and local fixture bundles do not certify a safe release.

Validation: clean `npm ci`; the actual query-string consumer round-trips Unicode, repeated parameters and encoded auth/deep-link callback values; malformed long percent-encoded input completes within the bounded regression test. Android, iOS and web JavaScript exports succeed with the patched dependency graph. Installed-device links, browser-provider callbacks and signing have **not** been run by this change. Repeat those checks before a release. Use `npm run check:security-patch` and `npm run audit:security` to reproduce the compatibility and remaining security status.

## Preserved October 3 baseline evidence

The app's full and production-only npm audits each report **19 high and 3 moderate affected package entries**, from three underlying advisories. Both audits remain failing. The older three-moderate count in `review-and-verification.md` is historical and is superseded here. This task did not change the app lockfile or dependency versions. Every affected lock entry (including integrity, resolution and flags) exactly matches preservation baseline `ec3e2de`.

| Advisory | Installed path | Actual app evidence | Status |
| --- | --- | --- | --- |
| [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) | Expo/RN Metro tooling -> micromatch 4.0.8 -> braces 3.0.3 | Neither braces nor micromatch appears in Android, iOS or web production bundle source maps. Both installed Metro watcher implementations call `micromatch.some`, which uses Picomatch rather than the vulnerable braces walkers. Build tooling still contains the package. | No patched version listed; unresolved. |
| [GHSA-86w9-cpqp-85rv](https://github.com/advisories/GHSA-86w9-cpqp-85rv) | Expo CLI/code-signing-certificates -> node-forge | Absent from all three runtime bundle maps. The installed certificate tool does call certificate/public-key verification. Build/signing tooling remains in scope of a release assessment; this work did not sign or publish updates. | No patched version listed; unresolved. |
| [GHSA-vcc3-ghjq-m6fr](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr) | Expo Router 57 -> query-string 7.1.3 -> decode-uri-component 0.2.2 | **Included in Android, iOS and web runtime bundles.** Router's `getStateFromPath` calls `queryString.parse(query)`; query-string calls the vulnerable decoder. This is a plausible externally supplied deep-link/query path, not merely a tooling-only advisory. No harmful payload was exercised. | Fixed decoder 0.5.0 has a changed module contract; this consumer's callable CommonJS dependency cannot be assumed drop-in compatible. A supported SDK/Router update or a separately reviewed compatible patch with deep-link regression tests is required before release. |

Evidence: ignored `artifacts/mobile-audit.json`, `mobile-audit-production.json`, `braces-path.json`, `decode-path.json`, `dependency-reachability.json`, and `export-final/` source maps. The exported Android/iOS artifacts are Hermes JS bundles, **not installed/signed native apps**. Absence in these runtime maps narrows reachability; it does not remove build-tool findings or prove every deployment safe. The app uses the standard Expo bundler configuration; no consumer Metro plugin or direct app import of the three vulnerable packages was added.

The shared UI source was reviewed via its owner's `docs/dependency-security.md` and adopted only through the pinned snapshot workflow. Its smaller nine-entry audit is not the app's audit. UI adoption adds source files, not a new dependency graph.

No forced downgrade to Expo 44 / RN 0.72, indiscriminate SDK major upgrade, audit suppression, dependency alias, auth change, signing change or CI security change was made. Leave publication blocked on a supported remediation and repeat bundle/deep-link/native validation. Any explicit release-risk decision belongs to a separately authorized release review; passing functional tests is not a clean security result.
