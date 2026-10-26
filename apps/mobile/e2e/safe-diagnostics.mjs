import { writeFileSync } from "node:fs";

// This is a closed vocabulary, not a logger. Never serialize the thrown object.
export const checkpoints = Object.freeze([
  "setup",
  "sign-in",
  "onboarding",
  "onboarding-save",
  "custom-quest",
  "save-retry",
  "definition-edit",
  "completion-response-loss",
  "character",
  "export-timer",
  "quest-discovery",
  "offline-undo",
  "clock-undo-retry",
  "fresh-session",
  "calendar-planning",
  "plan-edit",
  "viewports",
  "page-errors",
  "complete",
]);
export function errorCategory(error) {
  try {
    if (error?.name === "TimeoutError") return "timeout";
    if (error?.matcherResult && typeof error.matcherResult === "object")
      return "assertion";
  } catch {
    /* Unknown thrown values must not interfere with the journey. */
  }
  return "unknown";
}
export function createDiagnostics(filename) {
  let checkpoint = "unknown";
  function write(category) {
    if (!filename) return;
    try {
      writeFileSync(
        filename,
        JSON.stringify({ checkpoint, errorCategory: category }),
      );
    } catch {
      /* Evidence is best effort; preserve the original exit and cleanup. */
    }
  }
  return {
    checkpoint(value) {
      checkpoint = checkpoints.includes(value) ? value : "unknown";
      write(checkpoint === "complete" ? "none" : "unknown");
    },
    fail(error) {
      write(errorCategory(error));
    },
  };
}
