import { requestQuestExport } from "../api/client";
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
        const result = await requestQuestExport(token, format);
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
  async function accountRequest<T>(
    mutation: boolean,
    request: () => Promise<T>,
  ): Promise<T> {
    if (!auth.session || signingOut.current)
      throw new Error("Connect to use this account feature.");
    if (mutation && inFlight.current)
      throw new Error("Wait for the current action to finish.");
    const epoch = auth.epoch;
    if (mutation) {
      inFlight.current = true;
      setBusy(true);
    }
    try {
      const result = await request();
      if (auth.epoch !== epoch || !mounted.current)
        throw new Error("This account session changed.");
      return result;
    } finally {
      if (mutation) {
        finishWork();
        if (mounted.current) setBusy(false);
      }
    }
  }
  const readDiscoveries = () =>
    accountRequest(false, () =>
      client("GET /api/discoveries", undefined, undefined),
    );
  const saveDiscovery = (
    request: import("../api/generated").SavePlaceRequest,
  ) =>
    accountRequest(true, () =>
      client("POST /api/discoveries/save", request, undefined),
    );
  const removeDiscovery = (
    request: import("../api/generated").RemovePlaceRequest,
  ) =>
    accountRequest(true, () =>
      client("POST /api/discoveries/remove", request, undefined),
    );
  return {
    refresh,
    previewZone,
    previewClock,
    exportData,
    readDiscoveries,
    saveDiscovery,
    removeDiscovery,
    readBilling: () =>
      accountRequest(false, () =>
        client("GET /api/billing", undefined, undefined),
      ),
    startBillingCheckout: (interval: "monthly" | "yearly") =>
      accountRequest(true, () =>
        client("POST /api/billing/checkout", { interval }, undefined),
      ),
  };
}
