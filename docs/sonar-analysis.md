# Sonar analysis onboarding

The [candidate validation workflow](../ci/sonar-validate-candidate.yml) is inactive
until the public Sonar project, selected GitHub App access and existing
`SONAR_TOKEN` repository binding are approved and verified. It replaces the
active validation file once configured; it is not a second independent analysis.
Do not run automatic and CI analysis concurrently. Current coverage remains
missing until an actual exact-head server result is inspected.

The .NET scanner surrounds the existing Release solution build and includes
authored mobile TypeScript/JavaScript through multi-language scanning. The
project base directory is the product checkout, so the separately pinned
Identity source remains that project's responsibility. Both existing disposable
SQL test lanes, API contract verification and frontend validation remain.

The Identity integration lane can now collect OpenCover using its existing
Coverlet collector. `-CollectCoverage` requires `-IdentityIntegration`, fails
when no report is produced, and retains fixture cleanup and test failures. The
candidate imports those actual reports. It does not invent coverage for the
schema-only lane, mobile tests, native device tests or any unexecuted code.
Mobile LCOV collection/import remains a separate gap; its TAP report is test
execution evidence, not coverage. Inspect the server's indexed file inventory
and coverage mapping before claiming the API/mobile source surface is covered.

Exclusions cover dependency/build output, generated API files, and the exact
shared UI snapshots under `apps/mobile/packages` (owned and scanned in
HoneyDrunk.UI). Authored API wrappers, mobile features, domain/data code and
SQL/tooling remain in scope. Do not broaden exclusions to hide findings.

Fork PRs retain normal validation but receive no secret-backed scan. Report
that limitation; never use `pull_request_target` to run untrusted code with the
token. A missing token on a trusted run fails explicitly. The scanner waits up
to 300 seconds for the real server gate; the separate external
`SonarCloud Code Analysis` result still requires the correct GitHub binding.
Passing the gate does not prove zero new issues. Before readiness, inspect its
new issues/hotspots for the exact PR head, then retain the normal native/provider
and visual acceptance requirements.
