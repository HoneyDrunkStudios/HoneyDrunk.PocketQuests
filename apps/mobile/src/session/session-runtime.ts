import { createQuestClient } from "../api/client";
import { completionNoticeDurationMs } from "../config/client";
import type { CompletionFeedback } from "../features/progression/completion-feedback";
import type { Anchor } from "../shared/contracts";
import { createAccountLifecycle } from "./account-lifecycle";
import { createAuthSession } from "./auth-session";
import type { ClockSource } from "./clock-source";
import type { LocalAccount } from "./offline-store";
import { createPrivateAccount } from "./private-account";
import {
  renewProviderSession,
  verifyProviderSession,
} from "./provider-session";
import { createQuestSync } from "./quest-sync";
import { RequestError } from "./request-error";
import { restoreSession } from "./restore-session";
import { createSessionRequests } from "./session-requests";
import type { AccountStatus, SessionContext } from "./session-types";
import { sessionStorage, type Session } from "./storage";
type Notice = CompletionFeedback & { until: number };
type Cell<T> = { current: T };
type Setter<T> = (value: T | ((previous: T) => T)) => void;
export type SessionSnapshot = {
  inactiveAccount: AccountStatus | null;
  session: Session | null;
  cleanupOwner: string | null;
  local: LocalAccount | null;
  busy: boolean;
  error: string | null;
  offline: boolean;
  needsSignIn: boolean;
  completionNotices: Notice[];
};
export type SessionInternals = {
  auth: ReturnType<typeof createAuthSession>;
  client: ReturnType<typeof createQuestClient>;
  clockSource: ClockSource;
  localRef: Cell<LocalAccount | null>;
  cleanupOwnerRef: Cell<string | null>;
  inactiveRef: Cell<AccountStatus | null>;
  awaitingConfirmation: Cell<CompletionFeedback[]>;
  inFlight: Cell<boolean>;
  signingOut: Cell<boolean>;
  idleWaiters: Cell<(() => void)[]>;
  storageReady: Cell<boolean>;
  mounted: Cell<boolean>;
  setLocal: Setter<LocalAccount | null>;
  setSession: Setter<Session | null>;
  setCleanupOwner: Setter<string | null>;
  setInactiveAccount: Setter<AccountStatus | null>;
  setBusy: Setter<boolean>;
  setError: Setter<string | null>;
  setOffline: Setter<boolean>;
  setNeedsSignIn: Setter<boolean>;
  setCompletionNotices: Setter<Notice[]>;
  getOffline(): boolean;
  finishWork(): void;
  failure(value: unknown): void;
  rememberCleanupOwner(owner: string | null): void;
  privateOwner(): string | null;
  releaseCleanupOwner(epoch: number): Promise<void>;
  assertOwnerChange(owner: string): void;
  publish(value: LocalAccount): Promise<void>;
  forgetInactive(status: AccountStatus): Promise<void>;
  anchorFor(): Promise<Anchor>;
  drain(): Promise<void>;
} & Pick<
  SessionContext,
  | "connect"
  | "refresh"
  | "command"
  | "discardUnverified"
  | "discardRejected"
  | "discardPending"
  | "signOut"
  | "verifyOwner"
  | "accountAction"
  | "exportData"
  | "previewZone"
  | "previewClock"
>;

// A session-local external store for React's useSyncExternalStore. Workflows are
// ordinary functions; durable cache publication is the only queue commit path.
export function createSessionRuntime(clockSource: ClockSource) {
  let snapshot: SessionSnapshot = {
    inactiveAccount: null,
    session: null,
    cleanupOwner: null,
    local: null,
    busy: true,
    error: null,
    offline: false,
    needsSignIn: false,
    completionNotices: [],
  };
  const listeners = new Set<() => void>();
  const setter =
    <K extends keyof SessionSnapshot>(key: K): Setter<SessionSnapshot[K]> =>
    (value) => {
      const next =
        typeof value === "function"
          ? (value as (previous: SessionSnapshot[K]) => SessionSnapshot[K])(
              snapshot[key],
            )
          : value;
      if (Object.is(next, snapshot[key])) return;
      snapshot = { ...snapshot, [key]: next };
      for (const notify of listeners) notify();
    };
  const setSession = setter("session"),
    setBusy = setter("busy"),
    setError = setter("error"),
    setOffline = setter("offline"),
    setNeedsSignIn = setter("needsSignIn");
  const inFlight = { current: false },
    signingOut = { current: false },
    idleWaiters: Cell<(() => void)[]> = { current: [] };
  const base = {
    clockSource,
    setSession,
    setBusy,
    setError,
    setOffline,
    setNeedsSignIn,
    setLocal: setter("local"),
    setCleanupOwner: setter("cleanupOwner"),
    setInactiveAccount: setter("inactiveAccount"),
    setCompletionNotices: setter("completionNotices"),
    localRef: { current: null } as Cell<LocalAccount | null>,
    cleanupOwnerRef: { current: null } as Cell<string | null>,
    inactiveRef: { current: null } as Cell<AccountStatus | null>,
    awaitingConfirmation: { current: [] } as Cell<CompletionFeedback[]>,
    inFlight,
    signingOut,
    idleWaiters,
    storageReady: { current: true },
    mounted: { current: true },
    getOffline: () => snapshot.offline,
    finishWork: () => {
      inFlight.current = false;
      for (const done of idleWaiters.current.splice(0)) done();
    },
    failure: (value: unknown) => {
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
    },
  };
  const auth = createAuthSession({
    save: sessionStorage.save,
    changed: setSession,
    renew: renewProviderSession,
    verify: verifyProviderSession,
  });
  const authenticated = { ...base, auth, client: createQuestClient(auth.run) };
  const privateAccount = createPrivateAccount(authenticated);
  const sync = createQuestSync({ ...authenticated, ...privateAccount });
  const account = createAccountLifecycle({
    ...authenticated,
    ...privateAccount,
    ...sync,
  });
  const requests = createSessionRequests({
    ...authenticated,
    ...privateAccount,
    ...sync,
    ...account,
  });
  const dependencies = {
    ...authenticated,
    ...privateAccount,
    ...sync,
    ...account,
    ...requests,
  };
  const actions = {
    connect: account.connect,
    signOut: account.signOut,
    verifyOwner: account.verifyOwner,
    accountAction: account.accountAction,
    discardPending: account.discardPending,
    command: sync.command,
    discardRejected: sync.discardRejected,
    discardUnverified: sync.discardUnverified,
    refresh: requests.refresh,
    retry: requests.refresh,
    exportData: requests.exportData,
    previewZone: requests.previewZone,
    previewClock: requests.previewClock,
    dismissCompletion: () =>
      base.setCompletionNotices((notices) =>
        notices
          .slice(1)
          .map((notice, index) =>
            index === 0
              ? { ...notice, until: Date.now() + completionNoticeDurationMs }
              : notice,
          ),
      ),
  };
  return {
    actions,
    getSnapshot: () => snapshot,
    subscribe: (notify: () => void) => {
      listeners.add(notify);
      return () => {
        listeners.delete(notify);
      };
    },
    restore: (isCurrent: () => boolean) =>
      restoreSession(dependencies, isCurrent),
    setMounted: (value: boolean) => {
      base.mounted.current = value;
    },
  };
}
