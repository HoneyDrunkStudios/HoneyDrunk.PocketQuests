import type { Session } from "./storage";
import { RequestError } from "./request-error";
import { signInRequired, waitForAuth } from "./auth-request";
import { requestTimeoutMs } from "../config/client";

export type Credentials = {
  token: string;
  refreshToken?: string;
  authority?: string;
  clientId?: string;
  expiresAt?: number;
};

// One coordinator per mounted provider. Candidates live in the device-only
// secure session, and never authorize requests before owner verification.
export function createAuthSession(options: {
  save(session: Session | null): Promise<void>;
  changed(session: Session | null): void;
  renew(session: Session, signal: AbortSignal): Promise<Credentials>;
  verify(session: Session): Promise<void>;
  now?: () => number;
}) {
  let current: Session | null = null;
  let generation = 0;
  let cancellation = new AbortController();
  let writes: Promise<void> = Promise.resolve();
  let flight: { generation: number; promise: Promise<Session> } | undefined;
  let grant: { generation: number; promise: Promise<Session> } | undefined;
  const check = (expected: number) => {
    if (generation !== expected) throw signInRequired();
  };
  const persist = (
    value: Session | null,
    expected: number,
    beforeSave?: () => Promise<void>,
  ) => {
    const next = writes
      .catch(() => {})
      .then(async () => {
        check(expected);
        await beforeSave?.();
        check(expected);
        await options.save(value);
        check(expected);
        current = value;
        options.changed(value);
      });
    writes = next;
    return next;
  };
  function candidate(expected: Session, epoch: number): Promise<Session> {
    if (current?.renewal?.credentials) return Promise.resolve(current);
    if (grant?.generation === epoch) return grant.promise;
    // A restarted, unresolved exchange may already have consumed the old token.
    if (expected.renewal || !expected.refreshToken)
      return Promise.reject(signInRequired());
    const signal = cancellation.signal;
    const promise = (async () => {
      await persist({ ...expected, renewal: {} }, epoch);
      let credentials: Credentials;
      try {
        check(epoch);
        credentials = await options.renew(expected, signal);
      } catch (error) {
        if (generation === epoch) await persist(expected, epoch);
        throw error;
      }
      check(epoch);
      const retained = { ...expected, renewal: { credentials } };
      // A failed candidate write must not restore a possibly consumed token.
      await persist(retained, epoch);
      return retained;
    })();
    grant = { generation: epoch, promise };
    const clear = () => {
      if (grant?.promise === promise) grant = undefined;
    };
    void promise.then(clear, clear);
    return promise;
  }
  async function renew(expected: Session): Promise<Session> {
    if (flight?.generation === generation) return flight.promise;
    if (current !== expected && !current?.renewal) {
      if (current?.userId === expected.userId) return current;
      throw signInRequired();
    }
    const epoch = generation;
    const signal = cancellation.signal;
    const promise = (async () => {
      const retained = await waitForAuth(
        candidate(current ?? expected, epoch),
        signal,
        requestTimeoutMs.renewal,
      );
      check(epoch);
      const { renewal, ...owner } = retained;
      const next = { ...owner, ...renewal!.credentials! };
      await waitForAuth(options.verify(next), signal, requestTimeoutMs.api);
      check(epoch);
      await persist(next, epoch);
      return next;
    })();
    flight = { generation: epoch, promise };
    try {
      return await promise;
    } finally {
      if (flight?.promise === promise) flight = undefined;
    }
  }
  function invalidate() {
    generation++;
    cancellation.abort();
    cancellation = new AbortController();
  }
  return {
    get session() {
      return current;
    },
    get epoch() {
      return generation;
    },
    // Effect cleanup cancels this coordinator without signing out or deleting
    // durable credentials/pending work. A replacement provider restores them.
    cancel() {
      invalidate();
      current = null;
    },
    restore(value: Session) {
      invalidate();
      current = value;
      options.changed(value);
    },
    async install(value: Session | null, beforeSave?: () => Promise<void>) {
      invalidate();
      const epoch = generation;
      current = null;
      options.changed(null);
      // Logout may first retain the private-cache owner. This shares the same
      // serialized writer and runs after immediate request invalidation.
      await persist(value, epoch, beforeSave);
    },
    async run<T>(send: (token: string) => Promise<T>): Promise<T> {
      let active = current;
      if (!active) throw signInRequired();
      const epoch = generation;
      const signal = cancellation.signal;
      if (
        active.renewal ||
        (active.refreshToken &&
          active.expiresAt !== undefined &&
          active.expiresAt <= (options.now?.() ?? Date.now()) + 30_000)
      )
        active = await renew(active);
      check(epoch);
      try {
        const result = await waitForAuth(send(active.token), signal);
        check(epoch);
        return result;
      } catch (error) {
        check(epoch);
        if (!(error instanceof RequestError) || error.status !== 401)
          throw error;
        const next = await renew(active);
        const result = await waitForAuth(send(next.token), signal);
        check(epoch);
        return result;
      }
    },
  };
}
