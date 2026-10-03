import { questActions, offlineQuestActions } from "./commands/quest-actions";
import React, {
  createContext,
  use,
  useCallback,
  useEffect,
  useRef,
  useState,
} from "react";
import { AppState, Platform } from "react-native";
import * as Crypto from "expo-crypto";
import type {
  Anchor,
  Catalog,
  Command,
  State,
  PlannedMoment,
  ZonePreview,
} from "./contracts";
import { sessionStorage, type Session } from "./storage";
import {
  loadAccount,
  saveAccount,
  clearAccount,
  deviceId,
  type LocalAccount,
} from "./offline-store";
import { pendingProjection } from "./offline-projection";
import { saveExport } from "./export-download";
import { syncWarnings } from "./notifications";
import { RequestError } from "./request-error";
import {
  completionFeedback,
  survivingFeedback,
  type CompletionFeedback,
} from "./completion-feedback";
import {
  apiUrl,
  identityUrl,
  requestTimeoutMs,
  completionNoticeDurationMs,
} from "./config/client";
export { apiUrl, identityUrl } from "./config/client";
export type AccountStatus = {
  userId: string;
  state: string;
  requestedAt: string | null;
  recoveryDeadline: string | null;
  version: number;
};
async function identityRequest<T>(
  path: string,
  token: string,
  body?: unknown,
): Promise<T> {
  const response = await fetch(identityUrl + path, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(requestTimeoutMs.api),
  });
  if (!response.ok) {
    const detail = await response.json().catch(() => ({}));
    throw new RequestError(
      response.status,
      response.status === 401
        ? "Recent sign-in could not be verified. Sign in again using the same account; the provider must supply fresh authentication evidence."
        : (detail.detail ??
            "Account change could not be completed. Retry when connected."),
    );
  }
  return response.json();
}
type SessionContext = {
  inactiveAccount: AccountStatus | null;
  verifyOwner(token: string): Promise<void>;
  accountAction(
    action: "deletion" | "recovery" | "link" | "unlink",
    token: string,
    additionalToken?: string,
  ): Promise<void>;
  state: State | null;
  catalog: Catalog | null;
  signedIn: boolean;
  busy: boolean;
  error: string | null;
  pending: boolean;
  offline: boolean;
  queuedCount: number;
  recentCompletion: (CompletionFeedback & { until: number }) | null;
  dismissCompletion(): void;
  connect(token: string): Promise<void>;
  refresh(): Promise<void>;
  command(command: Omit<Command, "operationId">): Promise<void>;
  retry(): Promise<void>;
  signOut(discard?: boolean): Promise<void>;
  discardPending(): Promise<void>;
  exportData(format: "json" | "csv"): Promise<void>;
  notificationStatus: string;
  previewZone(zone: string): Promise<ZonePreview>;
  previewClock(
    date: string,
    time: string,
    zone: string,
  ): Promise<PlannedMoment>;
};
const Context = createContext<SessionContext | null>(null);
export function useSession() {
  const value = use(Context);
  if (!value) throw new Error("Session provider missing");
  return value;
}
async function request<T>(
  path: string,
  token: string,
  body?: unknown,
): Promise<T> {
  const response = await fetch(apiUrl + path, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(requestTimeoutMs.api),
  });
  if (!response.ok) {
    const detail = await response.json().catch(() => ({}));
    throw new RequestError(
      response.status,
      response.status === 401
        ? "Sign in again as the same account to synchronize your recorded actions."
        : (detail.detail ?? `Could not save this action (${response.status}).`),
    );
  }
  return response.json();
}
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [inactiveAccount, setInactiveAccount] = useState<AccountStatus | null>(
    null,
  );
  const inactiveRef = useRef<AccountStatus | null>(null);
  const [session, setSession] = useState<Session | null>(null);
  const [local, setLocal] = useState<LocalAccount | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notificationStatus, setNotificationStatus] = useState(
    "Expiry warnings are off.",
  );
  const [offline, setOffline] = useState(false);
  const [needsSignIn, setNeedsSignIn] = useState(false);
  const [completionNotices, setCompletionNotices] = useState<
    (CompletionFeedback & { until: number })[]
  >([]);
  const awaitingConfirmation = useRef<CompletionFeedback[]>([]);
  const localRef = useRef<LocalAccount | null>(null);
  const sessionRef = useRef<Session | null>(null);
  const inFlight = useRef(false);
  const boot = useRef(Crypto.randomUUID());
  const mounted = useRef(true);
  const publish = useCallback(async (value: LocalAccount) => {
    await saveAccount(value);
    localRef.current = value;
    if (mounted.current) setLocal(value);
  }, []);
  const forgetInactive = useCallback(async (status: AccountStatus) => {
    if (
      localRef.current &&
      localRef.current.userId !== status.userId &&
      localRef.current.queue.length
    )
      throw new RequestError(
        409,
        "Pending changes belong to the previous account. Synchronize or explicitly discard them before switching.",
      );
    await syncWarnings(null);
    if (sessionRef.current && sessionRef.current.userId !== status.userId)
      await clearAccount(sessionRef.current.userId);
    await clearAccount(status.userId);
    await sessionStorage.savePending(status.userId, null);
    await sessionStorage.save(null);
    localRef.current = null;
    sessionRef.current = null;
    setLocal(null);
    setSession(null);
    setNeedsSignIn(false);
    setOffline(false);
    setCompletionNotices([]);
    awaitingConfirmation.current = [];
    inactiveRef.current = status;
    setInactiveAccount(status);
  }, []);
  const previewZone = useCallback(async (zone: string) => {
    if (!sessionRef.current) throw new Error("Sign in to preview changes.");
    return request<ZonePreview>(
      `/api/planning/zone?zone=${encodeURIComponent(zone)}`,
      sessionRef.current.token,
    );
  }, []);
  const previewClock = useCallback(
    async (date: string, time: string, zone: string) => {
      if (!sessionRef.current)
        throw new Error("Connect to preview this planned time.");
      return request<PlannedMoment>(
        `/api/planning/clock?date=${encodeURIComponent(date)}&time=${encodeURIComponent(time)}&zone=${encodeURIComponent(zone)}`,
        sessionRef.current.token,
      );
    },
    [],
  );
  const anchorFor = useCallback(async (token: string): Promise<Anchor> => {
    const monotonic = performance.now();
    const anchor = await request<Omit<Anchor, "monotonic" | "ordinal">>(
      "/api/sync-anchor",
      token,
      {
        deviceId: await deviceId(),
        bootId: boot.current,
        deviceUtc: new Date().toISOString(),
      },
    );
    return { ...anchor, monotonic, ordinal: 0 };
  }, []);
  const failure = useCallback((value: unknown) => {
    if (value instanceof RequestError) {
      setOffline(false);
      if (value.status === 401) setNeedsSignIn(true);
    } else setOffline(true);
    setError(
      value instanceof Error
        ? value.message
        : "Connection interrupted. Your recorded actions remain on this device.",
    );
  }, []);
  const drain = useCallback(async () => {
    const active = sessionRef.current;
    if (!active || !localRef.current) return;
    while (localRef.current.queue.length) {
      const current = localRef.current;
      if (current.userId !== active.userId)
        throw new Error("Pending actions belong to another account.");
      const result = await request<State>(
        "/api/commands",
        active.token,
        current.queue[0],
      );
      const action = current.queue[0];
      const feedback = completionFeedback(current.state, result, action);
      await publish({
        ...current,
        state: result,
        queue: current.queue.slice(1),
      });
      if (
        feedback &&
        !current.queue.some(
          (c) =>
            c.action === questActions.undo &&
            c.completionId === feedback.completionId,
        )
      ) {
        awaitingConfirmation.current.push(feedback);
      }
      if (action.action === questActions.undo)
        setCompletionNotices((notices) =>
          notices.filter(
            (notice) => notice.completionId !== action.completionId,
          ),
        );
    }
    const state = await request<State>("/api/state", active.token);
    const anchor = await anchorFor(active.token);
    await publish({ ...localRef.current, state, anchor });
    // A replayed receipt is historical. Confirm its completion still survives before displaying it.
    const acknowledged = awaitingConfirmation.current;
    awaitingConfirmation.current = [];
    setCompletionNotices((notices) =>
      survivingFeedback([...notices, ...acknowledged], state).map((notice) => ({
        ...notice,
        until:
          notices.find((prior) => prior.completionId === notice.completionId)
            ?.until ?? Date.now() + completionNoticeDurationMs,
      })),
    );
    setOffline(false);
    setNeedsSignIn(false);
  }, [anchorFor, publish]);
  const refresh = useCallback(async () => {
    if (inFlight.current || !sessionRef.current) return;
    inFlight.current = true;
    setBusy(true);
    setError(null);
    try {
      await drain();
    } catch (value) {
      const token = sessionRef.current?.token;
      const status =
        value instanceof RequestError && value.status === 401 && token
          ? await identityRequest<AccountStatus>(
              "/users/me/status",
              token,
            ).catch(() => null)
          : null;
      if (status && status.state !== "Active") await forgetInactive(status);
      else failure(value);
    } finally {
      inFlight.current = false;
      if (mounted.current) setBusy(false);
    }
  }, [drain, failure, forgetInactive]);
  const connect = useCallback(
    async (token: string) => {
      if (inFlight.current) return;
      inFlight.current = true;
      setBusy(true);
      setError(null);
      try {
        const response = await fetch(identityUrl + "/users/me", {
          headers: { Authorization: `Bearer ${token}` },
          signal: AbortSignal.timeout(requestTimeoutMs.api),
        });
        if (!response.ok) {
          if (response.status === 401) {
            const status = await identityRequest<AccountStatus>(
              "/users/me/status",
              token,
            ).catch(() => null);
            if (status && status.state !== "Active") {
              await forgetInactive(status);
              return;
            }
          }
          throw new RequestError(
            response.status,
            "Sign-in could not be verified. Recorded actions require the original account.",
          );
        }
        const user = (await response.json()) as {
          userId: string;
          state: string;
        };
        if (user.state !== "Active") {
          await syncWarnings(null);
          await clearAccount(user.userId);
          await sessionStorage.save(null);
          localRef.current = null;
          sessionRef.current = null;
          setLocal(null);
          setSession(null);
          setCompletionNotices([]);
          awaitingConfirmation.current = [];
          throw new RequestError(
            403,
            "This account is inactive; private cached data was cleared.",
          );
        }
        if (
          localRef.current?.queue.length &&
          localRef.current.userId !== user.userId
        )
          throw new RequestError(
            409,
            "Pending changes belong to the previous account. Sign in as that account or explicitly discard its pending changes before switching.",
          );
        const previous = await loadAccount(user.userId);
        const [state, catalog] = await Promise.all([
          request<State>("/api/profile", token, {
            zone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
          }),
          request<Catalog>("/api/catalog", token),
        ]);
        if (sessionRef.current && sessionRef.current.userId !== user.userId) {
          await syncWarnings(null);
          await clearAccount(sessionRef.current.userId);
          setCompletionNotices([]);
          awaitingConfirmation.current = [];
        }
        inactiveRef.current = null;
        setInactiveAccount(null);
        const next = { userId: user.userId, token };
        await sessionStorage.save(next);
        sessionRef.current = next;
        setSession(next);
        const legacy = await sessionStorage.loadPending(user.userId);
        const queue = previous?.queue ?? (legacy ? [legacy.command] : []);
        await publish({
          userId: user.userId,
          state,
          catalog,
          queue,
          anchor: previous?.anchor ?? null,
        });
        await sessionStorage.savePending(user.userId, null);
        setNeedsSignIn(false);
        setOffline(false);
        await drain();
      } catch (value) {
        failure(value);
        throw value;
      } finally {
        inFlight.current = false;
        if (mounted.current) setBusy(false);
      }
    },
    [drain, failure, publish, forgetInactive],
  );
  useEffect(() => {
    mounted.current = true;
    void (async () => {
      try {
        const saved = await sessionStorage.load();
        if (!saved || !mounted.current) return;
        const cached = await loadAccount(saved.userId);
        sessionRef.current = saved;
        setSession(saved);
        if (cached) {
          localRef.current = cached;
          setLocal(cached);
          setOffline(true);
        }
        await connect(saved.token).catch(() => {});
      } catch {
        setError(
          "Private storage could not be opened. Reconnect before changing quests.",
        );
      } finally {
        if (mounted.current) setBusy(false);
      }
    })();
    return () => {
      mounted.current = false;
    };
  }, [connect]);
  useEffect(() => {
    const projected =
      local && !needsSignIn
        ? pendingProjection(local.state, local.queue, local.catalog)
        : null;
    let active = true;
    void syncWarnings(projected)
      .then((message) => {
        if (active) setNotificationStatus(message);
      })
      .catch(() => {
        if (active)
          setNotificationStatus(
            "Warnings could not be scheduled. Quests remain usable; check phone notification settings.",
          );
      });
    return () => {
      active = false;
    };
  }, [local, needsSignIn]);
  useEffect(() => {
    const subscription = AppState.addEventListener("change", (status) => {
      if (status === "active") void refresh();
    });
    const online = () => void refresh();
    if (Platform.OS === "web") window.addEventListener("online", online);
    return () => {
      subscription.remove();
      if (Platform.OS === "web") window.removeEventListener("online", online);
    };
  }, [refresh]);
  async function command(input: Omit<Command, "operationId">) {
    const current = localRef.current;
    if (
      !sessionRef.current ||
      !current ||
      inFlight.current ||
      (!offline && current.queue.length)
    )
      return;
    if (offline && !offlineQuestActions.has(input.action)) {
      setError(
        "Reconnect to change schedules, profile settings or existing plans. Offline creation and completion remain available.",
      );
      return;
    }
    inFlight.current = true;
    setBusy(true);
    setError(null);
    try {
      const anchor = current.anchor;
      if (!anchor && offline)
        throw new Error(
          "Connect once to establish a trusted time baseline before recording offline actions.",
        );
      const elapsed = anchor
        ? anchor.bootId === boot.current
          ? performance.now() - anchor.monotonic
          : Date.now() - Date.parse(anchor.deviceUtc)
        : 0;
      const recordedTime = anchor
        ? {
            anchorId: anchor.id,
            bootId: boot.current,
            ordinal: anchor.ordinal + 1,
            elapsedMilliseconds: Math.max(0, elapsed),
            deviceUtc: new Date().toISOString(),
          }
        : undefined;
      const item: Command = {
        ...input,
        operationId: Crypto.randomUUID(),
        occurrenceId:
          input.action === questActions.accept
            ? (input.occurrenceId ?? Crypto.randomUUID())
            : input.occurrenceId,
        recordedTime,
      };
      await publish({
        ...current,
        queue: [...current.queue, item],
        anchor: anchor ? { ...anchor, ordinal: anchor.ordinal + 1 } : null,
      });
      if (item.action === questActions.undo)
        setCompletionNotices((notices) =>
          notices.filter((notice) => notice.completionId !== item.completionId),
        );
      if (!offline) await drain();
      else
        setError(
          "Recorded on this device; progression will be confirmed when synchronized. Clock changes may require reconciliation.",
        );
    } catch (value) {
      failure(value);
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }
  async function discardPending() {
    if (inFlight.current || !localRef.current) return;
    inFlight.current = true;
    try {
      await publish({ ...localRef.current, queue: [], anchor: null });
      setError(
        "Pending changes discarded. Refresh to establish a new clock baseline.",
      );
    } finally {
      inFlight.current = false;
    }
  }
  async function signOut(discard = false) {
    if (inFlight.current) return;
    if (localRef.current?.queue.length && !discard) {
      setError(
        "There are unsynced changes. Synchronize them, or choose Discard pending changes and sign out. Signing out clears this device's private cache.",
      );
      return;
    }
    inFlight.current = true;
    try {
      await syncWarnings(null);
      if (sessionRef.current) {
        await clearAccount(sessionRef.current.userId);
        await sessionStorage.savePending(sessionRef.current.userId, null);
      }
      await sessionStorage.save(null);
      sessionRef.current = null;
      localRef.current = null;
      setSession(null);
      setLocal(null);
      setError(null);
      setNeedsSignIn(false);
      setOffline(false);
      setCompletionNotices([]);
      awaitingConfirmation.current = [];
    } catch {
      setError(
        "Could not clear private storage. Please try signing out again.",
      );
    } finally {
      inFlight.current = false;
    }
  }
  async function verifyOwner(token: string) {
    const expected = sessionRef.current?.userId ?? inactiveRef.current?.userId;
    const status = await identityRequest<AccountStatus>(
      "/users/me/status",
      token,
    );
    if (!expected || status.userId !== expected)
      throw new Error(
        "Use a sign-in method belonging to this account. Accounts are never merged by email.",
      );
  }
  async function accountAction(
    action: "deletion" | "recovery" | "link" | "unlink",
    token: string,
    additionalToken?: string,
  ) {
    if (inFlight.current)
      throw new Error("Wait for the current action to finish.");
    if (localRef.current?.queue.length)
      throw new Error(
        "Synchronize or explicitly discard pending changes before changing this account.",
      );
    inFlight.current = true;
    setBusy(true);
    setError(null);
    let recovered = false;
    try {
      await verifyOwner(token);
      const status = await identityRequest<AccountStatus>(
        "/users/me/" + action,
        token,
        action === "link" || action === "unlink"
          ? { accessToken: additionalToken }
          : { confirmed: true },
      );
      if (status.state !== "Active") await forgetInactive(status);
      else if (action === "recovery") recovered = true;
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
    if (recovered) await connect(token);
  }
  async function exportData(format: "json" | "csv") {
    const active = sessionRef.current;
    if (!active || inFlight.current || localRef.current?.queue.length) return;
    inFlight.current = true;
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(`${apiUrl}/api/export/${format}`, {
        headers: { Authorization: `Bearer ${active.token}` },
        signal: AbortSignal.timeout(requestTimeoutMs.export),
      });
      if (!response.ok)
        throw new Error("Export requires a verified online session.");
      await saveExport(await response.arrayBuffer(), format);
    } catch (value) {
      setError(value instanceof Error ? value.message : "Export failed.");
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }
  return (
    <Context
      value={{
        inactiveAccount,
        verifyOwner,
        accountAction,
        state: local
          ? pendingProjection(local.state, local.queue, local.catalog)
          : null,
        catalog: local?.catalog ?? null,
        signedIn: !!session && !needsSignIn,
        busy,
        error,
        pending: !!local?.queue.length && !offline,
        offline,
        queuedCount: local?.queue.length ?? 0,
        recentCompletion: completionNotices[0] ?? null,
        dismissCompletion: () =>
          setCompletionNotices((notices) =>
            notices
              .slice(1)
              .map((notice, index) =>
                index === 0
                  ? {
                      ...notice,
                      until: Date.now() + completionNoticeDurationMs,
                    }
                  : notice,
              ),
          ),
        connect,
        refresh,
        command,
        retry: refresh,
        signOut,
        discardPending,
        exportData,
        notificationStatus,
        previewZone,
        previewClock,
      }}
    >
      {children}
    </Context>
  );
}
