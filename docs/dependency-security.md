# PocketQuests dependency assessment

## October 25 publication preflight

The SDK 58 slice pins shared UI revision `956bc95ec8b6f6b0170ee1b74b54aec1bc71339b` from [UI PR #7](https://github.com/HoneyDrunkStudios/HoneyDrunk.UI/pull/7), based on current UI main. This preserves the owner's later security, coverage and package-boundary updates. The October 24 local-only pin below is historical.

The fresh lockfile audit reports five high affected-package entries and no critical findings. [GHSA-mrhx-c7xc-m3w5](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5) requires Zod 4.6.6 or later. The root resolves 4.6.7, but Expo CLI 58.1.15 requires `^3.25.76` and installs a separate 3.25.76 copy. Current main's CLI 57.0.27 also requires and resolves 3.25.76. The installed CLI constraint is not compatible with the published patched major; no unsupported major override or Expo 54 downgrade is applied. This inherited tooling finding remains open.

The latest node-forge remains 1.4.0, and [GHSA-86w9-cpqp-85rv](https://github.com/advisories/GHSA-86w9-cpqp-85rv) still lists no patched release. The existing source-hash-pinned malformed-DigestAlgorithm mitigation and regression checks remain enabled; npm audit does not infer remediation from a local source patch. Neither this mitigation nor functional SDK validation is a clean audit or native-signing acceptance. Release audit gates and advisory visibility are unchanged.

## October 24 SDK 58 compatibility

The iPhone's Expo Go installation requires SDK 58. Pocket Quests now aligns to Expo SDK 58, React 19.3 and React Native 0.88. The shared native UI snapshot is imported from reviewed local owner commit `d684cf2346af7ba2a28ae3f87b803c446aed4b01`; its peer range retains 0.86 support and permits 0.88. No generic component source changed. That owner commit is local and has not been pushed.

Expo Router 58 removes both `query-string` and `decode-uri-component` from the installed lockfile graph. The obsolete decoder override, compatibility patch and CI invocations were removed. Regression tests now exercise SDK 58 Router's actual query parser for callback values, Unicode, repeated parameters and bounded malformed-percent input, and reject reintroduction of the old decoder dependencies. The braces and node-forge source-hash guards and release audit remain enabled.

Clean `npm ci` applied the remaining mitigations. Verification passed 315 mobile tests, 36 shared UI contract tests with both the previous and SDK 58 dependency installations, TypeScript, lint, UI/API and security-patch checks, 20/20 Expo Doctor checks, workflow actionlint and an iOS development bundle export. These are local compatibility checks; physical iPhone sign-in and saved-progress acceptance remain user-run.

The full npm audit still exits nonzero with five high affected-package entries and zero critical findings. This is not a clean security audit or release approval. The earlier SDK 57 assessments below remain dated evidence.
## October 18 recheck

GitHub still reports alerts #3 (`braces`, GHSA-vfj7-8cjw-p6xm) and #2 (`node-forge`, GHSA-86w9-cpqp-85rv) open, with no patched version listed. npm still publishes braces 3.0.3 and node-forge 1.4.0 as latest. Latest metro-file-map 0.87.1 still requires micromatch, and @expo/code-signing-certificates 0.0.7 still requires node-forge. Forge PR #1152 remains open and unmerged. No supported dependency upgrade found in this recheck closes either alert.

A clean locked install and the explicit checked-in patches passed all seven dependency regression/patch-guard tests, including bounded parser/AST depth, malformed DigestAlgorithm rejection, valid signatures/certificates, tampering, unexpected versions and duplicate copies. The full audit still exits 1 with 18 high affected-package entries and zero critical findings. Package versions, lockfiles, alert visibility and the release audit remain unchanged. These local mitigations do not establish native signing or device acceptance.

## October 15 PKCS dependency remediation

`Microsoft.Data.SqlClient` 6.1.6 introduces a lower bound of `System.Security.Cryptography.Pkcs` 9.0.11. The Data project now directly requires 9.0.21, the patched 9.x version for [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) and [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7). The six consuming package-mode lockfiles are regenerated; audit checks and warning-as-error behavior remain enabled.

Package mode now resolves the unchanged Identity Abstractions/Client version `0.1.0-alpha.4` from nuget.org. The previous lock hashes described historical local candidate packages, not the published artifacts. The new hashes come from restoring the actual published packages. Their embedded repository metadata identifies commit `9438ef8e5417e5144587da4b83bc9c7fc14ee665`, matching the [Identity alpha.4 release and publication evidence](https://github.com/HoneyDrunkStudios/HoneyDrunk.Identity/releases/tag/v0.1.0-alpha.4). No package was republished, renamed or fabricated, and no Identity package version or client contract changed.

Identity source-mode integration is separately pinned to security-fix commit `5cfc1da16f075b566e845df8ba81de5f0e4347a8` in the validation workflow and uses generated ignored source-mode lockfiles. Updating a package-mode lock does not repair an older external Identity source checkout; both dependency graphs must be verified. The browser fixture explicitly supplies only its two loopback CORS origins in the Testing host, without relaxing deployed Identity origin validation.

## October 13 local mitigations and remaining upstream alerts

The current registry still publishes braces 3.0.3 and node-forge 1.4.0 with no patched release for alerts #3 and #2. The full audit remains **18 high affected-package entries, zero critical**, and exits 1. These counts are not 18 independent vulnerabilities. Neither alert is dismissed, hidden, renamed or accepted, and the release audit remains unchanged.

The app now owns temporary, source-hash-pinned mitigations in `apps/mobile/scripts/dependency-patches.json`, applied by `patch-dependencies.cjs` after installation:

- Braces parsing and the compile/expand/stringify walkers reject nesting beyond 100 before exhausting the JavaScript call stack. This implements the bound recommended in [upstream issue #70](https://github.com/micromatch/braces/issues/70), including caller-supplied ASTs that bypass parsing. Ordinary alternatives, ranges and escaped literals retain their behavior. Excessively nested patterns deliberately fail with a bounded validation error; callers must still handle invalid input.
- Forge validates the nested DigestAlgorithm element count, following [upstream PR #1152](https://github.com/digitalbazaar/forge/pull/1152). Extra unconsumed children are rejected while valid RSA signatures and certificate verification remain supported. This is a local backport of an unmerged fix, not a published upstream security release.

The guard checks exact package versions, every original/patched source hash and the lockfile's installed-copy inventory before writing any patch. Unexpected source, upgrades or extra nested copies fail closed for review. `--check` detects missing or altered patches; repeated application is idempotent. Native package/build signing remains an additional acceptance gate.

CI uses `npm ci --ignore-scripts`, then explicitly runs only the two checked-in patch scripts. Expo export invokes the locked local CLI directly rather than allowing `npx` to download a tool. A normal developer `npm ci` invokes the same patches through postinstall. An ignored-script install must explicitly run both patch scripts before using the tooling. Run `npm run check:security-patch` to verify the installation.

Regression tests failed on both original dependencies and pass on the patched sources. Coverage includes excessive parser/AST depth, valid brace patterns, malformed nested DigestAlgorithm data, valid signatures, PEM certificate round trips, and the actual Expo certificate-validation/sign-and-verify helper with synthetic keys. Patch application tests cover absent patches, tampering, idempotence, all-input validation before writes, and duplicate dependency copies. These are local tooling checks, not signed-device or live-provider acceptance.

Remove these backports only after compatible upstream releases or dependency removal are reviewed, installed and verified with the same regression tests, exports and native release checks. Keep registry alerts and release blockers visible until that reconciliation is complete.

## October 10 parent-path investigation and watcher remediation

[Expo's upstream removal](https://github.com/expo/expo/pull/51093) is published as **@expo/metro-file-map 57.0.4**, within CLI 57.0.27's existing `^57.0.3` range. `npm update @expo/metro-file-map --package-lock-only --ignore-scripts` changes only that package's lock entry and removes its micromatch dependency. The published implementation replaces internally constructed globs with extension, basename and prefix checks.

After clean installation, both full and `--omit=dev` audits report **18 high, zero critical affected-package entries** (previously 19 high); @expo/metro-file-map is no longer reported. Both audits still exit 1. Braces remains installed through Metro, so this improvement does **not** close alert #3. Node-forge remains unresolved. GitHub alert counts must be read separately after merges.

### Exact installed paths and affected functionality

| Advisory | Installed dependency paths after watcher update | Actual functionality and exposure |
| --- | --- | --- |
| [braces GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) | `expo 57.0.25 -> @expo/metro 56.0.2 -> metro-file-map 0.84.5 -> micromatch 4.0.8 -> braces 3.0.3`; independently `react-native 0.86.3 -> @react-native/community-cli-plugin 0.86.3 -> metro 0.84.5 -> metro-file-map 0.84.5 -> micromatch -> braces` | Metro file-watcher pattern matching. The installed watcher calls `micromatch.some`, which invokes Picomatch, not braces' recursive compile/expand walkers. Globs come from configured extensions plus fixed package/health-check patterns; filenames are candidate strings. Untrusted deeply nested patterns reaching the braces APIs would still be unsafe. |
| [node-forge GHSA-86w9-cpqp-85rv](https://github.com/advisories/GHSA-86w9-cpqp-85rv) | `expo 57.0.25 -> @expo/cli 57.0.27 -> node-forge 1.4.0`; also `@expo/cli -> @expo/code-signing-certificates 0.0.6 -> node-forge 1.4.0` | Expo CLI parses local Apple signing certificates and uses the certificate helper for development/update signing. The helper really calls certificate and RSA signature verification, so the vulnerable functionality is not an unused declared dependency. Project signing validates a configured local certificate/key pair; manifest signing verifies a locally generated signature. A forged remote signature reaching these paths was not demonstrated. |

Fresh Android/iOS/web JavaScript export maps contain **no braces, micromatch or node-forge sources**. The three main bundles do include Expo CLI's `build/metro-require/require.js` loader; that does not pull in certificate verification or node-forge. Source maps describe these exported JavaScript artifacts, not signed native binaries, all plugins or every release workflow. Committed app config has no `updates.codeSigningCertificate`, signing metadata or EAS project ID, but externally supplied release configuration was not inspected. No keys were generated or signing services contacted.

Bounded local probes exercise both original watcher implementations with five ordinary cases each; a counted braces wrapper records zero calls, while a direct micromatch.braces positive control records a call. The replacement watcher passes 13 predicate cases and a real local filesystem smoke for `.env`, compound extensions, package.json, health-check prefixes and literal braces in filenames, with an unrelated image excluded. These checks narrow demonstrated exposure and verify the replacement's behavior; they do not patch the remaining installed braces package.

### Supported options checked

- **Compatible parent updates:** Expo 57.0.27 / CLI 57.0.28 retain both direct node-forge and the certificate helper. Latest certificate helper 0.0.7 also requires node-forge. Published Metro 0.84.6 still requires micromatch; upgrading only that line does not remove braces. The watcher patch above is the available supported path reduction.
- **New framework lines:** React Native 0.87.1 still brings Metro 0.87.1 -> metro-file-map 0.87.1 -> micromatch. Expo's next SDK 58 / CLI 58.1.6 still brings node-forge. Those migrations would not solve these remaining alerts, and SDK 57 officially targets RN 0.86.
- **Unused-package removal:** Removing expo-dev-client or a lint package cannot remove the independent root Expo and React Native paths. Both frameworks are used by application code and native modules. HoneyDrunk.UI likewise imports React Native and declares it as a required host peer; its root dev dependency also provides real native typings. Removing or making that peer optional would change the supported contract, and its production-only audit still contains braces.
- **Replacement:** Upstream Metro [#2000](https://github.com/react/metro/pull/2000) removes micromatch and [#2024](https://github.com/react/metro/pull/2024) fixes dotfile behavior, but the current registry's highest stable Metro release, 0.87.1, does not contain that removal. A published compatible backport is the smallest remaining remedy. Aliasing legacy metro-file-map to Expo's fork or micromatch to Picomatch is not an established compatible substitution. Replacing forge requires adapting Expo's PEM/key/certificate/CSR and signature contracts; an app crypto swap does not alter CLI dependencies. A custom backport/fork would create separately reviewed security-patch maintenance, not an incidental lockfile fix, and no such patch was applied.

The remaining decision is whether to wait for compatible published Metro and node-forge fixes while retaining release blockers, or explicitly select and resource a maintained security patch with upstream regression and native/build/signing verification. A locally patched package retaining its affected version may still be reported by Dependabot. No release-risk acceptance, dismissal, suppression or framework migration is implied by this investigation.

## October 10 remediation and live Dependabot inventory

The GitHub organization alert API reported exactly five open alerts on October 10, 2026: four in PocketQuests and one in HoneyDrunk.UI. These are GitHub alert records, not npm's count of affected packages. PocketQuests' default-branch alerts were:

| Alert | Advisory / package | Locked remediation or remaining blocker |
| --- | --- | --- |
| [#5](https://github.com/HoneyDrunkStudios/HoneyDrunk.PocketQuests/security/dependabot/5) | [GHSA-pqg4-j6r4-53mv](https://github.com/advisories/GHSA-pqg4-j6r4-53mv), critical, shell-quote | 1.10.0 -> 1.12.0; first patched release is 1.11.0. |
| [#4](https://github.com/HoneyDrunkStudios/HoneyDrunk.PocketQuests/security/dependabot/4) | [GHSA-68fv-2mgg-jv7q](https://github.com/advisories/GHSA-68fv-2mgg-jv7q), high, source-map-js | 1.2.1 -> patched 1.2.2. |
| [#3](https://github.com/HoneyDrunkStudios/HoneyDrunk.PocketQuests/security/dependabot/3) | [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm), high, braces | Published latest 3.0.3 remains affected; no patched version. |
| [#2](https://github.com/HoneyDrunkStudios/HoneyDrunk.PocketQuests/security/dependabot/2) | [GHSA-86w9-cpqp-85rv](https://github.com/advisories/GHSA-86w9-cpqp-85rv), high, node-forge | Published latest 1.4.0 remains affected; no patched version. |

From `apps/mobile`, `npm update shell-quote source-map-js --package-lock-only --ignore-scripts` regenerated only those two lock entries within their consumers' existing ranges. A subsequent clean `npm ci` reapplied the existing query-decoder compatibility patch. No manifest, Expo/RN version, override, signing configuration or scanner setting changed.

The fresh full audit dropped from **20 high and 1 critical affected package entries** to **19 high and zero critical**, with neither fixed advisory present. Its exit status remains 1 because braces and node-forge are unresolved. The release audit remains active; functional validation does not establish a clean security result. GitHub closes alerts only after the corrected lockfile reaches the default branch and its dependency scan processes it; branch-local audit results do not prove closure.

Registry and upstream checks still show micromatch 4.0.8 requiring braces `^3.0.3`, latest metro-file-map 0.87.1 requiring micromatch `^4.0.4`, and latest @expo/code-signing-certificates 0.0.7 requiring node-forge `^1.4.0`. The [braces depth-guard issue](https://github.com/micromatch/braces/issues/70) remains open, and the [node-forge candidate fix](https://github.com/digitalbazaar/forge/pull/1152) is open and unmerged. npm's proposed Expo 44 / RN 0.72 downgrade is incompatible with the selected SDK 57 / RN 0.86 stack. A supported patched release or qualified upstream removal is still required; no advisory was dismissed or hidden. The earlier reachability evidence below remains dated evidence, not a fresh native-device assessment.

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
