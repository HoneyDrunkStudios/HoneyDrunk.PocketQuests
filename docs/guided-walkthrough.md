# Watch-along Android walkthrough

The founder watches the visible emulator; the agent operates it and explains each screen. Use a named `agent-device` session bound to the isolated `pocketquests-agent` AVD. This is an interactive walkthrough, with assertions and optional replay after selectors have been verified. It does not require the founder to click every screen.

## Preparation

1. Start the existing development build and the current source through Aspire. Aspire owns Metro port 8081; do not start a competing Expo server. Verify `/health` on the API and `/status` on Metro.
2. Confirm the selected database and account are isolated test fixtures. Preserve data and pending work. No uninstall, clear-data, factory reset or schema reset is part of this flow.
3. Check [customer sign-in setup](customer-sign-in-setup.md). Establish the real authenticated session before attempting Home; do not substitute the synthetic browser journey's identity.

The CLI was verified on Windows with Node and Android SDK using pinned `agent-device@0.21.30`. `npx` obtains this version from npm; it is development tooling, not a mobile runtime dependency. For a new host, verify that version and SDK availability before running it.

```powershell
$env:ANDROID_HOME = 'C:/Users/tatte/AppData/Local/Android/Sdk'
$env:PATH = "$env:ANDROID_HOME/platform-tools;$env:ANDROID_HOME/emulator;$env:PATH"
npx --yes agent-device@0.21.30 devices --platform android
npx --yes agent-device@0.21.30 boot --platform android --device pocketquests-agent --timeout 60000
npx --yes agent-device@0.21.30 --session pocket-quests-walkthrough open com.honeydrunk.pocketquests --platform android --device pocketquests-agent --foreground
```

The path/AVD are this workstation's verified values, not universal defaults. Inspect discovery output on another host. If the installed development client displays its launcher, inspect its input, connect to the verified Metro address (`http://10.0.2.2:8081` for this emulator), and re-inspect. Do not reuse old element references.

## Drive and watch

- Use the current snapshot's element reference or an unambiguous accessible label for each action. Follow it with `--settle` or an explicit condition; stop on an unexpected result.
- Describe the next action, perform it, and leave the resulting screen visible for discussion. The founder can interrupt or ask to revisit a screen. Do not run a fast, blind batch through the entire app.
- The founder handles credentials, MFA, account selection when ambiguous, legal terms and consent. Pause recording and screenshot collection during sensitive input. Do not save auth URLs/codes/tokens in a replay or report.
- Resume agent control after a verified return to Pocket Quests. Run Home/character/rank, Available/Active/Completed quests, details, Calendar, timer and settings. Obtain explicit agreement before recording completions or editing/deleting data on an account that has not been identified as disposable.
- Verify completion/XP, Undo and restart with isolated test data. Inspect the ledger as required by the existing native smoke contract; visible labels alone are insufficient.

Example interactive inspection (replace refs from the current output):

```powershell
npx --yes agent-device@0.21.30 --session pocket-quests-walkthrough snapshot -i
# press <current ref> --settle, then inspect the returned screen diff
```

Keep evidence under ignored `apps/mobile/reports/`. Record the source commit plus whether it has local changes, installed native build/device/OS, backend/database, and actual passed/failed/unrun steps. Record the signed-out and post-auth phases separately; never record a provider credential or consent exchange. Close the session when finished, without `--shutdown` if the founder wants to keep the emulator open.

### Restore the keyboard before owner sign-in

On October 22, the automation IME (`com.callstack.agentdevice.imehelper/.TestInputMethodService`) remained selected, and `show_ime_with_hard_keyboard` was `0`. The founder could not enter credentials normally. Restoring the already-installed Gboard input method and enabling the on-screen keyboard fixed visibility; `agent-device keyboard status` reported Gboard visible, and a fresh snapshot exposed its controls with the Google email field focused. Physical keyboard entry was not independently verified.

Before a private-input handoff, inspect the current session/device and installed IMEs. For the verified `emulator-5554` device, the reversible fix was:

```powershell
adb -s emulator-5554 shell ime set com.google.android.inputmethod.latin/com.android.inputmethod.latin.LatinIME
adb -s emulator-5554 shell settings put secure show_ime_with_hard_keyboard 1
npx --yes agent-device@0.21.30 --session pocket-quests-walkthrough keyboard status
```

Do not reuse that serial or IME blindly on another device. Restore the normal keyboard again if later automated text entry changes it. Do not collect screenshots or type through automation while the founder enters private credentials.

## Relationship to end-to-end tests

The existing browser/API/SQL journey is an automated regression lane. The `.maestro/quest-slice.yaml` flow is an older, unexecuted native proposal with specific fixture assumptions. It is not automatically valid for the redesigned UI. Update any replay/selectors only from observed native controls, keep assertions, and run against the stated isolated fixture before claiming a pass. No complete native walkthrough has been verified by this setup alone.

## October 22 signed-out verification

Base source `e18daa1c0c846cff8360bb9da5a80c03a22b94dd` plus local sign-in changes, existing `com.honeydrunk.pocketquests` development build, Android 16 `pocketquests-agent` emulator, deployed development Identity, local API and retained `PocketQuestsAndroid20261012` database. The installed native binary was reused; its original build revision was not established. No account data was reset.

- Reproduced a maximum-update-depth error in native navigation before the root route guards were added. The signed-out tabs redirect and root index share `/`; excluding authenticated routes from the signed-out stack prevents the observed loop.
- Verified the welcome screen after the change and again after a full app relaunch.
- Initially observed the old HoneyDrunk Customers hosted branding and missing social buttons. After the cloud configuration changes, reopened the real sign-in journey and verified the Pocket Quests wordmark, "Sign in to Pocket Quests", "Your everyday adventure continues here." and "Sign in with Google" button over a secure connection.
- Pressed the Google button and reached the real `accounts.google.com` sign-in screen. Google's screen currently identifies the application by `ciamlogin.com`; public Google brand verification remains pending. The founder was asked to complete private authentication using the approved studio test identity. No provider credential entry was recorded.
- Closed the browser tab without entering credentials. Verified return to the welcome screen and an enabled sign-in button after session refresh completed.
- Android bundle export passed with explicit development endpoints. This is bundle verification, not a release deployment.

At that checkpoint, authenticated return, Google token exchange, signed-in navigation, completion/Undo, restart persistence and notification/offline acceptance were unrun. The later checkpoint below supersedes only the steps it explicitly verifies. Do not discard recorded actions or silently merge identities to make the walkthrough easier.

## October 22 authenticated layout and onboarding checkpoint

The founder subsequently completed private sign-in and reported returning to the app. The agent observed the authenticated account's incomplete setup. This is evidence of that sign-in return, not an independent token/refresh or provider-matrix audit.

- Replaced the embedded profile editor with a dedicated three-step onboarding route: introduction, optional interests, and first-quest guidance. Optional skill assessments remain in the profile. Existing server commands and recovery controls still own persistence; finishing waits for confirmed account state.
- Verified all three screens and forward/back navigation on the Android 16 emulator, without selecting interests, finishing onboarding, changing quests, or signing out. Left the emulator on the introduction.
- Corrected the full-width image container and Android custom-font fallback. The extra overlapping gear was Expo's floating Tools button; it was hidden using the developer menu. This is a development-build overlay, not a second product settings control.
- Added the full-screen illustrated app welcome and centered sign-in/create-account panel. The ready state was inspected with the real welcome component and isolated auth fixtures in a phone-width browser preview. The actual web app rendered its recovery state when provider setup was unavailable; this does not establish a fresh native sign-in pass for the new layout.
- Updated the existing browser journey for the separate onboarding route and later profile assessment. The API/SQL journey was not rerun here because its fixed fixture ports are occupied by the retained watch-along stack. Run that isolated lane before publishing/merging; do not repoint it at the retained database.

Next walkthrough: complete onboarding with the founder's chosen interests, inspect Home/Available/Active/Completed/Calendar/Progress/profile, then verify completion/Undo, restart, offline recovery and notifications using explicitly disposable quest data. Physical device, iOS, large text and screen-reader acceptance remain open. These local changes are not a release or a completed full-app walkthrough.
