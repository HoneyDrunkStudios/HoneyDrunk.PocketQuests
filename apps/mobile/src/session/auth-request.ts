import { RequestError } from "./request-error";

export const signInRequired = () =>
  new RequestError(
    401,
    "Sign in again as the same account to synchronize your recorded actions.",
  );

// Expo discovery/refresh has no AbortSignal parameter. Bound the caller's wait;
// the coordinator owns late credentials and prevents duplicate token exchanges.
export function waitForAuth<T>(
  operation: Promise<T>,
  signal: AbortSignal,
  timeoutMs?: number,
): Promise<T> {
  return new Promise((resolve, reject) => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const cleanup = () => {
      clearTimeout(timer);
      signal.removeEventListener("abort", cancelled);
    };
    const cancelled = () => {
      cleanup();
      reject(signInRequired());
    };
    operation.then(
      (value) => {
        cleanup();
        resolve(value);
      },
      (error) => {
        cleanup();
        reject(error);
      },
    );
    if (signal.aborted) return cancelled();
    signal.addEventListener("abort", cancelled, { once: true });
    if (timeoutMs !== undefined)
      timer = setTimeout(() => {
        cleanup();
        reject(
          new RequestError(
            408,
            "Sign-in renewal timed out. Retry when connected, or sign in again. Your recorded actions remain on this device.",
          ),
        );
      }, timeoutMs);
  });
}
