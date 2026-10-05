// No remote sink is installed by default. Product data, error messages, stacks,
// tokens and URLs are deliberately absent from this diagnostic contract.
export type CrashDiagnostic = { code: "unexpected_render_error"; at: string };
let sink: ((event: CrashDiagnostic) => void) | undefined;
export function configureCrashReporter(
  reporter?: (event: CrashDiagnostic) => void,
) {
  sink = reporter;
}
export function reportRenderFailure() {
  try {
    sink?.({ code: "unexpected_render_error", at: new Date().toISOString() });
  } catch {
    /* A reporting failure must not replace the recovery screen. */
  }
}
