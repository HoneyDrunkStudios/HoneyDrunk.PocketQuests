import { createQuestClient } from "../api/client";
import { identityUrl, requestTimeoutMs } from "../config/client";
import { syncWarnings } from "../features/quests/notifications";
import { signInRequired } from "./auth-request";
import { type Credentials } from "./auth-session";
import { identityRequest } from "./identity-client";
import { cacheRecoveryNotice } from "./offline-store";
import { RequestError } from "./request-error";
import type { SessionInternals } from "./session-runtime";
import type { AccountStatus } from "./session-types";
type Dependencies = Pick<
  SessionInternals,
  | "storage"
  | "auth"
  | "assertOwnerChange"
  | "privateOwner"
  | "releaseCleanupOwner"
  | "localRef"
  | "setLocal"
  | "setSession"
  | "setNeedsSignIn"
  | "setOffline"
  | "setCompletionNotices"
  | "awaitingConfirmation"
  | "inactiveRef"
  | "setInactiveAccount"
  | "inFlight"
  | "signingOut"
  | "storageReady"
  | "setBusy"
  | "setError"
  | "publish"
  | "drain"
  | "failure"
  | "finishWork"
  | "mounted"
  | "cleanupOwnerRef"
  | "rememberCleanupOwner"
  | "idleWaiters"
>;
export function createAccountLifecycle({
  storage,
  auth,
  assertOwnerChange,
  privateOwner,
  releaseCleanupOwner,
  localRef,
  setLocal,
  setSession,
  setNeedsSignIn,
  setOffline,
  setCompletionNotices,
  awaitingConfirmation,
  inactiveRef,
  setInactiveAccount,
  inFlight,
  signingOut,
  storageReady,
  setBusy,
  setError,
  publish,
  drain,
  failure,
  finishWork,
  mounted,
  cleanupOwnerRef,
  rememberCleanupOwner,
  idleWaiters,
}: Dependencies) {
  const forgetInactive = async (status: AccountStatus) => {
    const epoch = auth.epoch;
    assertOwnerChange(status.userId);
    const owner = privateOwner();
    await syncWarnings(null);
    if (auth.epoch !== epoch) throw signInRequired();
    if (owner && owner !== status.userId) await storage.clearAccount(owner);
    await storage.clearAccount(status.userId);
    await storage.session.savePending(status.userId, null);
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
  };
  const connect = async (credential: string | Credentials) => {
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
        await storage.clearAccount(user.userId);
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
      const previous = await storage.loadAccount(user.userId);
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
        await storage.clearAccount(oldOwner);
        setCompletionNotices([]);
        awaitingConfirmation.current = [];
      }
      inactiveRef.current = null;
      setInactiveAccount(null);
      if (auth.epoch !== epoch) throw signInRequired();
      const next = { ...credentials, userId: user.userId };
      await auth.install(next);
      const installedEpoch = epoch + 1;
      const legacy = await storage.session.loadPending(user.userId);
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
      await storage.session.savePending(user.userId, null);
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
  };
  async function discardPending() {
    const owner = privateOwner();
    const epoch = auth.epoch;
    if (inFlight.current || signingOut.current || !owner) return;
    inFlight.current = true;
    setBusy(true);
    try {
      await storage.discardCacheRecovery(owner);
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
        owner ? () => storage.session.saveCleanupOwner(owner) : undefined,
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
        await storage.clearAccount(owner);
        await storage.session.savePending(owner, null);
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
  return {
    forgetInactive,
    connect,
    discardPending,
    signOut,
    verifyOwner,
    accountAction,
  };
}
