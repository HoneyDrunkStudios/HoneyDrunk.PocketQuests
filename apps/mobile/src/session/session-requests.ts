import { apiUrl, requestTimeoutMs } from "../config/client";
import { saveExport } from "../features/profile/export-download";
import { identityRequest } from "./identity-client";
import { RequestError } from "./request-error";
import type { SessionInternals } from "./session-runtime";
import type { AccountStatus } from "./session-types";
type Dependencies = Pick<
  SessionInternals,
  | "inFlight"
  | "signingOut"
  | "auth"
  | "setBusy"
  | "setError"
  | "drain"
  | "forgetInactive"
  | "failure"
  | "finishWork"
  | "mounted"
  | "client"
  | "localRef"
>;
export function createSessionRequests({
  inFlight,
  signingOut,
  auth,
  setBusy,
  setError,
  drain,
  forgetInactive,
  failure,
  finishWork,
  mounted,
  client,
  localRef,
}: Dependencies) {
  const refresh = async () => {
    if (inFlight.current || signingOut.current || !auth.session) return;
    const epoch = auth.epoch;
    inFlight.current = true;
    setBusy(true);
    setError(null);
    try {
      await drain();
    } catch (value) {
      if (auth.epoch !== epoch) return;
      const token = auth.session?.token;
      const status =
        value instanceof RequestError && value.status === 401 && token
          ? await identityRequest<AccountStatus>(
              "/users/me/status",
              token,
            ).catch(() => null)
          : null;
      if (auth.epoch !== epoch) return;
      if (status && status.state !== "Active") await forgetInactive(status);
      else failure(value);
    } finally {
      finishWork();
      if (mounted.current) setBusy(false);
    }
  };
  const previewZone = async (zone: string) => {
    if (!auth.session) throw new Error("Sign in to preview changes.");
    return client("GET /api/planning/zone", undefined, { zone });
  };
  const previewClock = async (date: string, time: string, zone: string) => {
    if (!auth.session) throw new Error("Connect to preview this planned time.");
    return client("GET /api/planning/clock", undefined, { date, time, zone });
  };
  async function exportData(format: "json" | "csv") {
    const active = auth.session;
    if (
      !active ||
      inFlight.current ||
      signingOut.current ||
      localRef.current?.queue.length
    )
      return;
    inFlight.current = true;
    setBusy(true);
    setError(null);
    try {
      const response = await auth.run(async (token) => {
        const result = await fetch(`${apiUrl}/api/export/${format}`, {
          headers: { Authorization: `Bearer ${token}` },
          signal: AbortSignal.timeout(requestTimeoutMs.export),
        });
        if (!result.ok)
          throw new RequestError(
            result.status,
            "Export requires a verified online session.",
          );
        return result;
      });
      await saveExport(await response.arrayBuffer(), format);
    } catch (value) {
      setError(value instanceof Error ? value.message : "Export failed.");
    } finally {
      finishWork();
      setBusy(false);
    }
  }
  return { refresh, previewZone, previewClock, exportData };
}
