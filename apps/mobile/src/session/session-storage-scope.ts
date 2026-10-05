import { signInRequired } from "./auth-request";
import * as account from "./offline-store";
import { sessionStorage, type Pending, type Session } from "./storage";

// Private storage is process-wide even when React replaces the provider. The
// next owner must wait for an already-started native operation to finish before
// reading or clearing its result. Queued work from a disposed owner never starts.
let owner: { key: symbol; cancel(): void } | null = null;
let storageIdle: Promise<unknown> = Promise.resolve();

export function createSessionStorageScope() {
  let active: symbol | null = null;
  async function run<T>(
    operation: () => Promise<T>,
    expected = active,
  ): Promise<T> {
    const check = () => {
      if (!expected || active !== expected || owner?.key !== expected)
        throw signInRequired();
    };
    const next = storageIdle
      .catch(() => {})
      .then(async () => {
        check();
        const result = await operation();
        // A native write can complete after disposal. Its committed generation is
        // retained for the next owner, but the stale workflow cannot continue.
        check();
        return result;
      });
    storageIdle = next.catch(() => {});
    return next;
  }
  async function read<T>(operation: () => Promise<T>): Promise<T> {
    const expected = active;
    await storageIdle;
    if (!expected || active !== expected || owner?.key !== expected)
      throw signInRequired();
    const result = await operation();
    if (active !== expected || owner?.key !== expected) throw signInRequired();
    return result;
  }
  return {
    activate(cancelAuth: () => void) {
      owner?.cancel();
      const key = Symbol("session storage owner");
      active = key;
      const cancel = () => {
        if (active !== key) return;
        active = null;
        if (owner?.key === key) owner = null;
        cancelAuth();
      };
      owner = { key, cancel };
      return cancel;
    },
    session: {
      load: () => read(() => sessionStorage.load()),
      save: (value: Session | null) => run(() => sessionStorage.save(value)),
      loadCleanupOwner: () => read(() => sessionStorage.loadCleanupOwner()),
      saveCleanupOwner: (value: string | null) =>
        run(() => sessionStorage.saveCleanupOwner(value)),
      loadPending: (userId: string) =>
        read(() => sessionStorage.loadPending(userId)),
      savePending: (userId: string, value: Pending | null) =>
        run(() => sessionStorage.savePending(userId, value)),
    },
    loadAccount: (userId: string) => {
      const expected = active;
      return read(() =>
        account.loadAccount(userId, (recover) => run(recover, expected)),
      );
    },
    saveAccount: (value: account.LocalAccount) =>
      run(() => account.saveAccount(value)),
    clearAccount: (userId: string) => run(() => account.clearAccount(userId)),
    discardCacheRecovery: (userId: string) =>
      run(() => account.discardCacheRecovery(userId)),
    deviceId: () => run(() => account.deviceId()),
  };
}
