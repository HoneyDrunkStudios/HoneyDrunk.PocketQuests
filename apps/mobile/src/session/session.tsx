import React, {
  createContext,
  use,
  useMemo,
  useState,
  useSyncExternalStore,
} from "react";
import { deviceClockSource, type ClockSource } from "./clock-source";
import { pendingProjection } from "./offline-projection";
import { cacheRecoveryNotice } from "./offline-store";
import { createSessionRuntime } from "./session-runtime";
import type { SessionContext } from "./session-types";
import { rejectedActionLabel } from "./sync-recovery";
import { useExpiryWarnings } from "./use-expiry-warnings";
import { useSessionLifecycle } from "./use-session-lifecycle";
export { apiUrl, identityUrl } from "../config/client";
export type { AccountStatus } from "./session-types";
type Actions = ReturnType<typeof createSessionRuntime>["actions"];
type AccountSnapshot = Pick<
  SessionContext,
  "state" | "catalog" | "recentCompletion" | "inactiveAccount"
>;
type Status = Omit<SessionContext, keyof Actions | keyof AccountSnapshot>;
const ActionsContext = createContext<Actions | null>(null);
const AccountContext = createContext<AccountSnapshot | null>(null);
const StatusContext = createContext<Status | null>(null);
export function useSessionActions() {
  const value = use(ActionsContext);
  if (!value) throw new Error("Session provider missing");
  return value;
}
export function useAccountSnapshot() {
  const value = use(AccountContext);
  if (!value) throw new Error("Session provider missing");
  return value;
}
export function useSessionStatus() {
  const value = use(StatusContext);
  if (!value) throw new Error("Session provider missing");
  return value;
}
export function useSession(): SessionContext {
  return {
    ...useAccountSnapshot(),
    ...useSessionStatus(),
    ...useSessionActions(),
  };
}
export function SessionProvider({
  children,
  clockSource = deviceClockSource,
}: {
  children: React.ReactNode;
  clockSource?: ClockSource;
}) {
  const [runtime] = useState(() => createSessionRuntime(clockSource));
  const snapshot = useSyncExternalStore(
    runtime.subscribe,
    runtime.getSnapshot,
    runtime.getSnapshot,
  );
  useSessionLifecycle(runtime);
  const {
    local,
    session,
    cleanupOwner,
    inactiveAccount,
    completionNotices,
    needsSignIn,
    offline,
    busy,
    error,
  } = snapshot;
  const state = useMemo(
    () =>
      local && !local.requiresReload
        ? pendingProjection(
            local.state,
            local.queue,
            local.catalog,
            local.unverified,
          )
        : null,
    [local],
  );
  const notificationStatus = useExpiryWarnings(needsSignIn ? null : state);
  const account = useMemo(
    () => ({
      state,
      catalog: local?.catalog ?? null,
      recentCompletion: completionNotices[0] ?? null,
      inactiveAccount,
    }),
    [state, local?.catalog, completionNotices, inactiveAccount],
  );
  const owner = cleanupOwner ?? session?.userId ?? local?.userId;
  const recoveryNotice = owner ? cacheRecoveryNotice(owner) : null;
  const recovery = useMemo(() => {
    const label = (command: Parameters<typeof rejectedActionLabel>[0]) =>
      local && !local.requiresReload
        ? rejectedActionLabel(command, local.state, local.catalog)
        : command.action;
    return {
      rejected: (local?.rejected ?? []).map((entry) => ({
        ...entry,
        label: label(entry.command),
      })),
      unverified: (local?.unverified ?? []).map((entry) => ({
        ...entry,
        label: label(entry.command),
      })),
    };
  }, [local]);
  const status = useMemo(
    () => ({
      signedIn: !!session && !needsSignIn,
      busy,
      error: error ?? recoveryNotice,
      pending: !!local?.queue.length && !offline,
      offline,
      queuedCount: local?.queue.length ?? 0,
      recoveryRequired: !!cleanupOwner || !!recoveryNotice,
      ...recovery,
      notificationStatus,
    }),
    [
      session,
      needsSignIn,
      busy,
      error,
      recoveryNotice,
      local?.queue.length,
      offline,
      cleanupOwner,
      recovery,
      notificationStatus,
    ],
  );
  return (
    <ActionsContext value={runtime.actions}>
      <AccountContext value={account}>
        <StatusContext value={status}>{children}</StatusContext>
      </AccountContext>
    </ActionsContext>
  );
}
