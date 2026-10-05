import {
  questActions,
  offlineQuestActions,
} from "../features/quests/commands/quest-actions";
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
} from "../shared/contracts";
import { sessionStorage, type Session } from "./storage";
import { createQuestClient } from "../api/client";
import { createAuthSession, type Credentials } from "./auth-session";
import {
  renewProviderSession,
  verifyProviderSession,
} from "./provider-session";
import { signInRequired } from "./auth-request";
import {
  loadAccount,
  cacheRecoveryNotice,
  discardCacheRecovery,
  saveAccount,
  clearAccount,
  deviceId,
  type LocalAccount,
} from "./offline-store";
import { pendingProjection } from "./offline-projection";
import { deviceClockSource, type ClockSource } from "./clock-source";
import { recordedProof } from "./clock-proof";
import {
  unverifiedBlockers,
  type UnverifiedAction,
} from "./unverified-actions";
import { saveExport } from "../features/profile/export-download";
import { syncWarnings } from "../features/quests/notifications";
import { RequestError, isDefinitiveRejection } from "./request-error";
import {
  isolateRejection,
  rejectionBlockers,
  rejectedActionLabel,
  type RejectedCommand,
} from "./sync-recovery";
import {
  completionFeedback,
  survivingFeedback,
  type CompletionFeedback,
} from "../features/progression/completion-feedback";
import {
  apiUrl,
  identityUrl,
  requestTimeoutMs,
  completionNoticeDurationMs,
} from "../config/client";
export { apiUrl, identityUrl } from "../config/client";
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
  recoveryRequired: boolean;
  rejected: (RejectedCommand & { label: string })[];
  discardRejected(operationId: string): Promise<void>;
  unverified: (UnverifiedAction & { label: string })[];
  discardUnverified(operationId: string): Promise<void>;
  recentCompletion: (CompletionFeedback & { until: number }) | null;
  dismissCompletion(): void;
  connect(token: string | Credentials): Promise<void>;
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
export function SessionProvider({
  children,
  clockSource = deviceClockSource,
}: {
  children: React.ReactNode;
  clockSource?: ClockSource;
}) {
  const [inactiveAccount, setInactiveAccount] = useState<AccountStatus | null>(
    null,
  );
  const inactiveRef = useRef<AccountStatus | null>(null);
  const [session, setSession] = useState<Session | null>(null);
  const [cleanupOwner, setCleanupOwner] = useState<string | null>(null);
  const cleanupOwnerRef = useRef<string | null>(null);
  const rememberCleanupOwner = useCallback((owner: string | null) => {
    cleanupOwnerRef.current = owner;
    setCleanupOwner(owner);
  }, []);
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
  const [auth] = useState(() =>
    createAuthSession({
      save: sessionStorage.save,
      changed: setSession,
      renew: renewProviderSession,
      verify: verifyProviderSession,
    }),
  );
  const [client] = useState(() => createQuestClient(auth.run));
  const inFlight = useRef(false);
  const signingOut = useRef(false);
  const idleWaiters = useRef<(() => void)[]>([]);
  const finishWork = useCallback(() => {
    inFlight.current = false;
    for (const done of idleWaiters.current.splice(0)) done();
  }, []);
  const storageReady = useRef(true);
  const mounted = useRef(true);
  const privateOwner = useCallback(
    () =>
      cleanupOwnerRef.current ??
      auth.session?.userId ??
      localRef.current?.userId ??
      null,
    [auth],
  );
  const releaseCleanupOwner = useCallback(
    async (epoch: number) => {
      if (auth.epoch !== epoch) throw signInRequired();
      await sessionStorage.saveCleanupOwner(null);
      if (auth.epoch !== epoch) throw signInRequired();
      rememberCleanupOwner(null);
    },
    [auth, rememberCleanupOwner],
  );
  const assertOwnerChange = useCallback(
    (nextOwner: string) => {
      const owner = privateOwner();
      if (
        owner &&
        owner !== nextOwner &&
        (cleanupOwnerRef.current === owner || cacheRecoveryNotice(owner))
      )
        throw new RequestError(
          409,
          "The previous account has a saved recovery copy or unfinished private storage cleanup. Sign in as that account or explicitly discard its pending changes before switching.",
        );
      const cached = localRef.current;
      if (
        cached &&
        cached.userId !== nextOwner &&
        (cached.queue.length ||
          cached.rejected?.length ||
          cached.unverified?.length)
      )
        throw new RequestError(
          409,
          "Pending changes belong to the previous account. Synchronize or explicitly discard them before switching.",
        );
    },
    [privateOwner],
  );
  const publish = useCallback(async (value: LocalAccount) => {
    if (!storageReady.current)
      throw new Error(
        "Private storage must be reopened. Restart the app before changing recorded actions.",
      );
    try {
      await saveAccount(value);
    } catch (error) {
      // A write may have committed before its response failed. Reload the actual
      // generation before allocating another ordinal or advancing the queue.
      try {
        const committed = await loadAccount(value.userId);
        if (!committed || committed.requiresReload)
          throw new Error("Committed cache unavailable");
        localRef.current = committed;
        if (mounted.current) setLocal(committed);
      } catch {
        storageReady.current = false;
      }
      throw error;
    }
    localRef.current = value;
    if (mounted.current) setLocal(value);
  }, []);
  const forgetInactive = useCallback(
    async (status: AccountStatus) => {
      const epoch = auth.epoch;
      assertOwnerChange(status.userId);
      const owner = privateOwner();
      await syncWarnings(null);
      if (auth.epoch !== epoch) throw signInRequired();
      if (owner && owner !== status.userId) await clearAccount(owner);
      await clearAccount(status.userId);
      await sessionStorage.savePending(status.userId, null);
      if (auth.epoch !== epoch) throw signInRequired();
      await auth.install(null);
      await releaseCleanupOwner(epoch + 1);
      localRef.current = null;
      setLocal(null);
      setSession(null);
      setNeedsSignIn(false);
      setOffline(false);
      setCompletionNotices([]);
      awaitingConfirmation.current = [];
      inactiveRef.current = status;
      setInactiveAccount(status);
    },
    [auth, assertOwnerChange, privateOwner, releaseCleanupOwner],
  );
  const previewZone = useCallback(
    async (zone: string) => {
      if (!auth.session) throw new Error("Sign in to preview changes.");
      return client("GET /api/planning/zone", undefined, { zone });
    },
    [client, auth],
  );
  const previewClock = useCallback(
    async (date: string, time: string, zone: string) => {
      if (!auth.session)
        throw new Error("Connect to preview this planned time.");
      return client("GET /api/planning/clock", undefined, { date, time, zone });
    },
    [client, auth],
  );
  const anchorFor = useCallback(async (): Promise<Anchor> => {
    const device = await deviceId();
    const reading = clockSource.read();
    const anchor = await client(
      "POST /api/sync-anchor",
      {
        deviceId: device,
        bootId: Crypto.randomUUID(),
        deviceUtc: reading.observedUtc,
      },
      undefined,
    );
    return {
      ...anchor,
      monotonic: reading.sample?.elapsedMilliseconds ?? 0,
      clock: reading.sample ?? undefined,
      ordinal: 0,
      lastElapsedMilliseconds: 0,
    };
  }, [clockSource, client]);
  const failure = useCallback((value: unknown) => {
    if (signingOut.current) return;
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
    if (!storageReady.current)
      throw new Error(
        "Private storage must be reopened. Restart the app before synchronizing.",
      );
    const active = auth.session;
    if (!active || !localRef.current) return;
    if (localRef.current.requiresReload) {
      const [state, catalog] = await Promise.all([
        client("GET /api/state", undefined, undefined),
        client("GET /api/catalog", undefined, undefined),
      ]);
      await publish({
        ...localRef.current,
        state,
        catalog,
        requiresReload: false,
      });
    }
    while (localRef.current.queue.length) {
      const current = localRef.current;
      if (current.userId !== active.userId)
        throw new Error("Pending actions belong to another account.");
      const action = current.queue[0];
      let result: State;
      try {
        result = await client("POST /api/commands", action, undefined);
      } catch (value) {
        if (!isDefinitiveRejection(value)) throw value;
        const rejection = value as RequestError;
        await publish({
          ...current,
          ...isolateRejection(
            current.queue.slice(1),
            current.rejected ?? [],
            action,
            rejection.status,
            rejection.message,
          ),
        });
        continue;
      }
      const feedback = completionFeedback(current.state, result, action);
      await publish({
        ...current,
        state: result,
        queue: current.queue.slice(1),
      });
      if (
        feedback &&
        ![
          ...current.queue,
          ...(current.unverified ?? []).map((entry) => entry.command),
        ].some(
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
    const state = await client("GET /api/state", undefined, undefined);
    const anchor = await anchorFor();
    await publish({ ...localRef.current, state, anchor });
    // An Undo can arrive after its completion was acknowledged but before a
    // failed refresh delivered the feedback. Check durable pending intent at
    // delivery too, including writes that committed before their response failed.
    const pendingUndos = new Set(
      [
        ...localRef.current.queue,
        ...(localRef.current.unverified ?? []).map((entry) => entry.command),
      ]
        .filter((command) => command.action === questActions.undo)
        .map((command) => command.completionId),
    );
    // A replayed receipt is historical. Confirm its completion still survives before displaying it.
    const acknowledged = awaitingConfirmation.current;
    awaitingConfirmation.current = [];
    setCompletionNotices((notices) =>
      survivingFeedback([...notices, ...acknowledged], state)
        .filter((notice) => !pendingUndos.has(notice.completionId))
        .map((notice) => ({
          ...notice,
          until:
            notices.find((prior) => prior.completionId === notice.completionId)
              ?.until ?? Date.now() + completionNoticeDurationMs,
        })),
    );
    setOffline(false);
    setNeedsSignIn(false);
  }, [anchorFor, publish, client, auth]);
  const refresh = useCallback(async () => {
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
  }, [drain, failure, forgetInactive, auth, finishWork]);
  const connect = useCallback(
    async (credential: string | Credentials) => {
      const credentials =
        typeof credential === "string" ? { token: credential } : credential;
      const token = credentials.token;
      const epoch = auth.epoch;
      if (inFlight.current || signingOut.current) return;
      if (!storageReady.current)
        throw new Error(
          "Private storage must be reopened. Restart the app before reconnecting.",
        );
      inFlight.current = true;
      setBusy(true);
      setError(null);
      try {
        const response = await fetch(identityUrl + "/users/me", {
          headers: { Authorization: `Bearer ${token}` },
          signal: AbortSignal.timeout(requestTimeoutMs.api),
        });
        if (auth.epoch !== epoch) throw signInRequired();
        if (!response.ok) {
          if (response.status === 401) {
            const status = await identityRequest<AccountStatus>(
              "/users/me/status",
              token,
            ).catch(() => null);
            if (auth.epoch !== epoch) throw signInRequired();
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
        if (auth.epoch !== epoch) throw signInRequired();
        const user = (await response.json()) as {
          userId: string;
          state: string;
        };
        if (auth.epoch !== epoch) throw signInRequired();
        assertOwnerChange(user.userId);
        if (user.state !== "Active") {
          await syncWarnings(null);
          if (auth.epoch !== epoch) throw signInRequired();
          await clearAccount(user.userId);
          if (auth.epoch !== epoch) throw signInRequired();
          await auth.install(null);
          await releaseCleanupOwner(epoch + 1);
          localRef.current = null;
          setLocal(null);
          setSession(null);
          setCompletionNotices([]);
          awaitingConfirmation.current = [];
          throw new RequestError(
            403,
            "This account is inactive; private cached data was cleared.",
          );
        }
        const previous = await loadAccount(user.userId);
        const signingIn = createQuestClient(async (send) => send(token));
        const [state, catalog] = await Promise.all([
          signingIn(
            "POST /api/profile",
            {
              zone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
            },
            undefined,
          ),
          signingIn("GET /api/catalog", undefined, undefined),
        ]);
        if (auth.epoch !== epoch) throw signInRequired();
        assertOwnerChange(user.userId);
        const oldOwner = privateOwner();
        if (oldOwner && oldOwner !== user.userId) {
          await syncWarnings(null);
          if (auth.epoch !== epoch) throw signInRequired();
          await clearAccount(oldOwner);
          setCompletionNotices([]);
          awaitingConfirmation.current = [];
        }
        inactiveRef.current = null;
        setInactiveAccount(null);
        if (auth.epoch !== epoch) throw signInRequired();
        const next = { ...credentials, userId: user.userId };
        await auth.install(next);
        const installedEpoch = epoch + 1;
        const legacy = await sessionStorage.loadPending(user.userId);
        if (auth.epoch !== installedEpoch) throw signInRequired();
        const queue = previous?.queue ?? (legacy ? [legacy.command] : []);
        await publish({
          userId: user.userId,
          state,
          catalog,
          queue,
          rejected: previous?.rejected ?? [],
          unverified: previous?.unverified ?? [],
          anchor: previous?.anchor ?? null,
        });
        await sessionStorage.savePending(user.userId, null);
        await releaseCleanupOwner(installedEpoch);
        setNeedsSignIn(false);
        setOffline(false);
        await drain();
      } catch (value) {
        failure(value);
        throw value;
      } finally {
        finishWork();
        if (mounted.current) setBusy(false);
      }
    },
    [
      drain,
      failure,
      publish,
      forgetInactive,
      auth,
      finishWork,
      assertOwnerChange,
      privateOwner,
      releaseCleanupOwner,
    ],
  );
  useEffect(() => {
    mounted.current = true;
    void (async () => {
      let openingOwner: string | null = null;
      let openingEpoch = auth.epoch;
      try {
        const epoch = auth.epoch;
        const retained = await sessionStorage.loadCleanupOwner();
        const saved = await sessionStorage.load();
        if (!mounted.current || auth.epoch !== epoch) return;
        rememberCleanupOwner(retained);
        const owner = retained ?? saved?.userId;
        if (!owner) return;
        openingOwner = owner;
        if (saved && (!retained || saved.userId === retained))
          auth.restore(saved);
        openingEpoch = auth.epoch;
        const cached = await loadAccount(owner);
        if (auth.epoch !== openingEpoch) return;
        if (cached) {
          localRef.current = cached;
          if (!cached.requiresReload) setLocal(cached);
          else setNeedsSignIn(true);
          setOffline(true);
        }
        if (!saved || (retained && saved.userId !== retained)) {
          setNeedsSignIn(true);
          return;
        }
        // Restore and verify using the same refresh coordinator as requests.
        await auth
          .run(async (token) => {
            const response = await fetch(identityUrl + "/users/me", {
              headers: { Authorization: `Bearer ${token}` },
              signal: AbortSignal.timeout(requestTimeoutMs.api),
            });
            if (!response.ok)
              throw new RequestError(
                response.status,
                "Sign in again as the same account.",
              );
            const owner = (await response.json()) as {
              userId: string;
              state: string;
            };
            if (owner.userId !== saved.userId || owner.state !== "Active")
              throw new RequestError(401, "Sign in again as the same account.");
          })
          .then(() => connect(auth.session!))
          .catch(failure);
      } catch {
        if (auth.epoch !== openingEpoch) return;
        if (openingOwner) rememberCleanupOwner(openingOwner);
        storageReady.current = false;
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
  }, [connect, auth, failure, rememberCleanupOwner]);
  useEffect(() => {
    const projected =
      local && !needsSignIn
        ? pendingProjection(
            local.state,
            local.queue,
            local.catalog,
            local.unverified,
          )
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
    if (!auth.session || !current || inFlight.current) return;
    if (current.requiresReload) {
      setError(
        "Reconnect to reload your history before recording another action. Existing pending actions are retained.",
      );
      return;
    }
    if (!storageReady.current) {
      setError(
        "Private storage must be reopened. Restart the app before recording another action.",
      );
      return;
    }
    if (!offline && current.queue.length) {
      setError(
        "Synchronize or review the recorded actions before adding another change.",
      );
      return;
    }
    if (
      rejectionBlockers({ ...input, operationId: "" }, current.rejected ?? [])
        .length
    ) {
      setError(
        "Review the rejected actions for this quest before recording another related change. Each retained action can be discarded individually.",
      );
      return;
    }
    if (offline && !offlineQuestActions.has(input.action)) {
      setError(
        "Reconnect to change schedules, profile settings or existing plans.",
      );
      return;
    }
    inFlight.current = true;
    setBusy(true);
    setError(null);
    try {
      const anchor = current.anchor;
      const device = await deviceId();
      const reading = clockSource.read();
      const timing = recordedProof(anchor, reading, device);
      const item: Omit<Command, "recordedTime"> = {
        ...input,
        operationId: Crypto.randomUUID(),
        occurrenceId:
          input.action === questActions.accept
            ? (input.occurrenceId ?? Crypto.randomUUID())
            : input.occurrenceId,
      };
      // Input is not a trusted source of recordedTime, including for unverified
      // intents. Only recordedProof may create a wire proof for a fresh action.
      delete (item as Command).recordedTime;
      const blockers = unverifiedBlockers(item, current.unverified ?? []);
      if (!timing.proof || blockers.length) {
        const reason = blockers.length
          ? "An earlier related action is still awaiting timing verification. This dependent action is also retained pending verification."
          : timing.reason!;
        await publish({
          ...current,
          unverified: [
            ...(current.unverified ?? []),
            {
              command: item,
              observedUtc: reading.observedUtc,
              clock: reading.sample,
              reason,
              blockedBy: blockers.map((entry) => entry.command.operationId),
            },
          ],
        });
        if (item.action === questActions.undo)
          setCompletionNotices((notices) =>
            notices.filter(
              (notice) => notice.completionId !== item.completionId,
            ),
          );
        setError(reason);
        return;
      }
      const proven: Command = { ...item, recordedTime: timing.proof };
      await publish({
        ...current,
        queue: [...current.queue, proven],
        anchor: {
          ...anchor!,
          ordinal: timing.proof.ordinal,
          lastElapsedMilliseconds: timing.proof.elapsedMilliseconds,
        },
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
      finishWork();
      setBusy(false);
    }
  }
  async function discardUnverified(operationId: string) {
    if (inFlight.current || signingOut.current || !localRef.current) return;
    inFlight.current = true;
    setBusy(true);
    try {
      await publish({
        ...localRef.current,
        unverified: (localRef.current.unverified ?? []).filter(
          (entry) => entry.command.operationId !== operationId,
        ),
      });
      setError(
        "Selected action discarded. Dependent actions remain pending verification; unrelated work is preserved.",
      );
    } catch (value) {
      failure(value);
    } finally {
      finishWork();
      setBusy(false);
    }
  }
  async function discardRejected(operationId: string) {
    if (inFlight.current || signingOut.current || !localRef.current) return;
    inFlight.current = true;
    setBusy(true);
    try {
      await publish({
        ...localRef.current,
        rejected: (localRef.current.rejected ?? []).filter(
          (entry) => entry.command.operationId !== operationId,
        ),
      });
      setError(
        "Selected action discarded. Other recorded and blocked actions remain on this device.",
      );
    } catch (value) {
      failure(value);
    } finally {
      finishWork();
      setBusy(false);
    }
  }
  async function discardPending() {
    const owner = privateOwner();
    const epoch = auth.epoch;
    if (inFlight.current || signingOut.current || !owner) return;
    inFlight.current = true;
    setBusy(true);
    try {
      await discardCacheRecovery(owner);
      if (localRef.current)
        await publish({
          ...localRef.current,
          queue: [],
          rejected: [],
          unverified: [],
          anchor: null,
        });
      await releaseCleanupOwner(epoch);
      setError(
        "Pending changes discarded. Refresh to establish a new clock baseline.",
      );
    } catch (value) {
      failure(value);
    } finally {
      finishWork();
      setBusy(false);
    }
  }
  async function signOut(discard = false) {
    if (signingOut.current) return;
    const owner = privateOwner();
    if (
      (localRef.current?.queue.length ||
        localRef.current?.rejected?.length ||
        localRef.current?.unverified?.length ||
        !storageReady.current ||
        cleanupOwnerRef.current ||
        (owner && cacheRecoveryNotice(owner))) &&
      !discard
    ) {
      setError(
        "There are unsynced changes. Synchronize them, or choose Discard pending changes and sign out. Signing out clears this device's private cache.",
      );
      return;
    }
    signingOut.current = true;
    setBusy(true);
    rememberCleanupOwner(owner);
    // Cancellation must happen before waiting for an outstanding provider call.
    // Drain existing local writes before clearing storage so none can recreate it.
    const idle = inFlight.current
      ? new Promise<void>((resolve) => idleWaiters.current.push(resolve))
      : Promise.resolve();
    const logoutEpoch = auth.epoch + 1;
    try {
      await auth.install(
        null,
        owner ? () => sessionStorage.saveCleanupOwner(owner) : undefined,
      );
      await idle;
      inFlight.current = true;
      if (
        !discard &&
        (localRef.current?.queue.length ||
          localRef.current?.rejected?.length ||
          localRef.current?.unverified?.length ||
          !storageReady.current ||
          (owner && cacheRecoveryNotice(owner)))
      ) {
        setNeedsSignIn(true);
        setError(
          "Sign-in was cleared. Pending actions or a recovery copy remain on this device. Sign in as the same account, or explicitly discard them.",
        );
        return;
      }
      await syncWarnings(null);
      if (owner) {
        await clearAccount(owner);
        await sessionStorage.savePending(owner, null);
      }
      localRef.current = null;
      setSession(null);
      setLocal(null);
      setError(null);
      setNeedsSignIn(false);
      setOffline(false);
      setCompletionNotices([]);
      awaitingConfirmation.current = [];
      await releaseCleanupOwner(logoutEpoch);
    } catch {
      setError(
        "Could not clear private storage. Please try signing out again.",
      );
    } finally {
      signingOut.current = false;
      finishWork();
      setBusy(false);
    }
  }
  async function verifyOwner(token: string) {
    const expected = privateOwner() ?? inactiveRef.current?.userId;
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
    const epoch = auth.epoch;
    if (inFlight.current || signingOut.current)
      throw new Error("Wait for the current action to finish.");
    if (
      localRef.current?.queue.length ||
      localRef.current?.rejected?.length ||
      localRef.current?.unverified?.length ||
      !storageReady.current ||
      cleanupOwnerRef.current ||
      (privateOwner() && cacheRecoveryNotice(privateOwner()!))
    )
      throw new Error(
        "Synchronize or explicitly discard pending changes before changing this account.",
      );
    inFlight.current = true;
    setBusy(true);
    setError(null);
    let recovered = false;
    try {
      await verifyOwner(token);
      if (auth.epoch !== epoch) throw signInRequired();
      const status = await identityRequest<AccountStatus>(
        "/users/me/" + action,
        token,
        action === "link" || action === "unlink"
          ? { accessToken: additionalToken }
          : { confirmed: true },
      );
      if (auth.epoch !== epoch) throw signInRequired();
      if (status.state !== "Active") await forgetInactive(status);
      else if (action === "recovery") recovered = true;
    } finally {
      finishWork();
      setBusy(false);
    }
    if (recovered) await connect(token);
  }
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
  const visibleOwner = cleanupOwner ?? session?.userId ?? local?.userId;
  const recoveryNotice = visibleOwner
    ? cacheRecoveryNotice(visibleOwner)
    : null;
  return (
    <Context
      value={{
        inactiveAccount,
        verifyOwner,
        accountAction,
        state: local
          ? pendingProjection(
              local.state,
              local.queue,
              local.catalog,
              local.unverified,
            )
          : null,
        catalog: local?.catalog ?? null,
        signedIn: !!session && !needsSignIn,
        busy,
        error: error ?? recoveryNotice,
        pending: !!local?.queue.length && !offline,
        offline,
        queuedCount: local?.queue.length ?? 0,
        recoveryRequired: !!cleanupOwner || !!recoveryNotice,
        rejected: (local?.rejected ?? []).map((entry) => ({
          ...entry,
          label: rejectedActionLabel(
            entry.command,
            local!.state,
            local!.catalog,
          ),
        })),
        discardRejected,
        unverified: (local?.unverified ?? []).map((entry) => ({
          ...entry,
          label: rejectedActionLabel(
            entry.command,
            local!.state,
            local!.catalog,
          ),
        })),
        discardUnverified,
        recentCompletion: completionNotices[0] ?? null,
        dismissCompletion: () =>
          setCompletionNotices((notices) =>
            notices.slice(1).map((notice, index) =>
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
