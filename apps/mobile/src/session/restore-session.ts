import { identityUrl, requestTimeoutMs } from "../config/client";
import { loadAccount } from "./offline-store";
import { RequestError } from "./request-error";
import type { SessionInternals } from "./session-runtime";
import { sessionStorage } from "./storage";
export async function restoreSession(
  {
    auth,
    rememberCleanupOwner,
    localRef,
    setLocal,
    setNeedsSignIn,
    setOffline,
    connect,
    failure,
    storageReady,
    setError,
    setBusy,
  }: Pick<
    SessionInternals,
    | "auth"
    | "rememberCleanupOwner"
    | "localRef"
    | "setLocal"
    | "setNeedsSignIn"
    | "setOffline"
    | "connect"
    | "failure"
    | "storageReady"
    | "setError"
    | "setBusy"
  >,
  isCurrent: () => boolean,
) {
  let openingOwner: string | null = null;
  let openingEpoch = auth.epoch;
  try {
    const epoch = auth.epoch;
    const retained = await sessionStorage.loadCleanupOwner();
    const saved = await sessionStorage.load();
    if (!isCurrent() || auth.epoch !== epoch) return;
    rememberCleanupOwner(retained);
    const owner = retained ?? saved?.userId;
    if (!owner) return;
    openingOwner = owner;
    if (saved && (!retained || saved.userId === retained)) auth.restore(saved);
    openingEpoch = auth.epoch;
    const cached = await loadAccount(owner);
    if (!isCurrent() || auth.epoch !== openingEpoch) return;
    if (cached) {
      localRef.current = cached;
      setLocal(cached);
      if (cached.requiresReload) setNeedsSignIn(true);
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
      .then(() =>
        isCurrent() && auth.session ? connect(auth.session) : undefined,
      )
      .catch((value) => {
        if (isCurrent()) failure(value);
      });
  } catch {
    if (!isCurrent() || auth.epoch !== openingEpoch) return;
    if (openingOwner) rememberCleanupOwner(openingOwner);
    storageReady.current = false;
    setError(
      "Private storage could not be opened. Reconnect before changing quests.",
    );
  } finally {
    if (isCurrent()) setBusy(false);
  }
}
