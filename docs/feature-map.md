# Pocket Quests feature map

## Browser

Use the [browser workflow](browser-journey-ci.md) and [testing contract](testing-contract.md) to prepare Expo, pinned Identity, Chromium and disposable SQL. On the prepared Windows checkout set `PQ_EXPECTED_HEAD` to `git rev-parse HEAD`, then run `pwsh -File scripts/Test-BrowserJourneyCI.ps1` at the root. Read `artifacts/browser-journey/run.json` and the allowlisted screenshots. The journey uses a test OIDC provider.

| User entry | Result to check |
| --- | --- |
| Sign in → onboarding → Home | Redirect and save complete, character loads in a fresh session. |
| Quests → Available → Active → Complete → Undo | A lost receipt or double tap still yields one completion. Undo reverses that completion and the fresh session reads the persisted result. |
| Calendar → plan a quest | A plan persists after saving and can be edited. |
| Home → See your progress | Character, rewards and rank render in the narrow and wide fixtures. |

The current browser checkpoint names and assertions live in [the journey](../apps/mobile/e2e/journey.mjs).

## Installed Android or iOS build

Use a seeded disposable account and an explicitly selected device. Follow [the Maestro fixture prerequisites](../apps/mobile/.maestro/README.md), then call `pwsh -File scripts/Test-NativeJourney.ps1 -DeviceId <id> -BuildCommit <sha> -DisposableFixture`. Keep screenshots, JUnit and `run.json` private under `artifacts/native-journey/`. Check the isolated ledger for one completion and one matching Undo. Record the build and OS for the device used. Real-provider sign-in has its own [release checks](mobile-release.md).

When navigation, labels, data setup or behavior changes, update the affected journey and this map together. Recheck native selectors on the installed build when they change.
