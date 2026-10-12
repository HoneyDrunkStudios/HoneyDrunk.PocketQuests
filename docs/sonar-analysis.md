# Sonar analysis

The [validation workflow](../.github/workflows/validate.yml) runs one CI-based
analysis for trusted pull requests and pushes to main.
The Sonar project is `HoneyDrunkStudios_HoneyDrunk.PocketQuests` in organization
`honeydrunkstudios`. Disable automatic analysis before enabling this caller.
The existing organization `SONAR_TOKEN` must include this repository, and the
Sonar GitHub App must be bound to it for external check reporting.

SonarScanner for .NET surrounds the existing Release solution build. Its base
directory is the product checkout: pinned sibling Identity sources remain
Identity's responsibility. Multi-language analysis includes authored mobile,
SQL and tooling files. SQL is explicitly T-SQL, not Oracle PL/SQL.

Both disposable SQL lanes and API/frontend checks remain mandatory. The
Identity integration lane collects real OpenCover; schema-only tests do not
produce coverage. Mobile `npm run test:coverage` uses c8 around the existing
Node tests. TypeScript CommonJS hooks emit inline source maps. LCOV includes
unexecuted authored mobile files, and CI normalizes its paths to the product
root before import. Native-provider and real-device acceptance remain separate.

Exclusions are dependency/build output, exact shared UI source snapshots
(owned and analyzed by HoneyDrunk.UI), and generated API contracts. Authored
wrappers, app features, backend and SQL stay in scope. The scanner waits for
the real gate; a successful upload alone is insufficient. Review actual
new-code findings on the exact head before merge, and record any mapping gaps.

Fork pull requests retain validation without secret-backed scanning. Missing
credentials fail trusted scans explicitly. No `pull_request_target`, fabricated
coverage, threshold reductions or issue suppressions are used. Test evidence
is retained even when analysis fails.
