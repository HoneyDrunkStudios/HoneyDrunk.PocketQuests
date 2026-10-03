# Local native smoke proposal

`quest-slice.yaml` uses the ADR-0047 Maestro choice. It has **not run**. Maestro is unavailable. Android SDK adb was found outside PATH; its read-only inventory showed no attached devices and an existing `pocketquests-agent` AVD, which was not launched in this pass. No installed native build was validated. YAML parsing is not a device pass. No cloud service, CI permission change, auth bypass or provider configuration is added.

Before running, install a local development build on an isolated emulator/device, connect it to isolated nonproduction API/Identity/SQL, and sign in to a disposable test account. Do not point this flow at real user data. Use the existing native sign-in path; the browser journey's external-provider double is not native sign-in evidence.

Fixture preconditions: onboarding complete; exactly 90 overall XP from nine completed small F downtime quests; Health & Fitness and Strength Training still at zero; no active quests. Save one custom definition named `Maestro slice quest`, F/Small, Health & Fitness, with 100% Strength Training. Leave it available. Start from Home. The flow should earn 10 overall/category/skill XP, explicitly show level-ups, undo that completion, and retain 90 overall XP after restarting. Independently inspect the isolated ledger for one completion and one matching Undo; UI labels alone do not prove ledger correctness.

From `apps/mobile`, run `maestro test .maestro/quest-slice.yaml`. Capture output, screenshots, device/OS/build versions, and exact Git commit. Retest selectors against the real accessibility hierarchy before considering this harness accepted. The final restart assumes the app resumes Home; adapt only that navigation expectation if native restoration correctly resumes details.

Additional device acceptance cases (still unrun):

- Small and large phones, 200% font scale, narrow widths: every reward, level-up, tab and action readable and reachable without clipping. Verify portrait and stats wrap coherently.
- VoiceOver and TalkBack: meaningful selected tab, modal focus containment, one completion announcement after the modal opens, readable reward/level sequence, Undo, and sensible focus after dismissal. Reduced-motion users receive all information without animation.
- Optional timer: one-minute expiry awards nothing; pause/resume/reset work; opening the Android notification drawer, backgrounding and leaving the quest pause it. Returning requires explicit resume. A process restart may reset this intentionally ephemeral timer.
- Offline native persistence: accept while online, disconnect, explicitly complete, verify pending status and unchanged XP, kill/relaunch without clearing data, reconnect and synchronize. Exactly one server completion and one set of rewards; repeat synchronization changes nothing. This exercises real encrypted-file/SecureStore behavior, which the memory-only browser adapter cannot establish.
- Completion response lost after server commit, auth/session expiry during synchronization, failed save preserving entered fields, two queued completions, matching Undo, repeated Undo, and stale Undo after recompletion. Verify account-bound isolation using disposable accounts.

Command references: [launchApp](https://docs.maestro.dev/reference/commands-available/launchapp.md), [tapOn](https://docs.maestro.dev/reference/commands-available/tapon.md), [scrollUntilVisible](https://docs.maestro.dev/reference/commands-available/scrolluntilvisible.md).
