import { signInRequired } from "./auth-request";
import { cacheRecoveryNotice, type LocalAccount } from "./offline-store";
import { RequestError } from "./request-error";
import type { SessionInternals } from "./session-runtime";
type Dependencies = Pick<
  SessionInternals,
  | "storage"
  | "cleanupOwnerRef"
  | "setCleanupOwner"
  | "auth"
  | "localRef"
  | "storageReady"
  | "mounted"
  | "setLocal"
>;
export function createPrivateAccount({
  storage,
  cleanupOwnerRef,
  setCleanupOwner,
  auth,
  localRef,
  storageReady,
  mounted,
  setLocal,
}: Dependencies) {
  const rememberCleanupOwner = (owner: string | null) => {
    cleanupOwnerRef.current = owner;
    setCleanupOwner(owner);
  };
  const privateOwner = () =>
    cleanupOwnerRef.current ??
    auth.session?.userId ??
    localRef.current?.userId ??
    null;
  const releaseCleanupOwner = async (epoch: number) => {
    if (auth.epoch !== epoch) throw signInRequired();
    await storage.session.saveCleanupOwner(null);
    if (auth.epoch !== epoch) throw signInRequired();
    rememberCleanupOwner(null);
  };
  const assertOwnerChange = (nextOwner: string) => {
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
  };
  const publish = async (value: LocalAccount) => {
    if (!storageReady.current)
      throw new Error(
        "Private storage must be reopened. Restart the app before changing recorded actions.",
      );
    try {
      await storage.saveAccount(value);
    } catch (error) {
      // A write may have committed before its response failed. Reload the actual
      // generation before allocating another ordinal or advancing the queue.
      try {
        const committed = await storage.loadAccount(value.userId);
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
  };
  return {
    rememberCleanupOwner,
    privateOwner,
    releaseCleanupOwner,
    assertOwnerChange,
    publish,
  };
}
