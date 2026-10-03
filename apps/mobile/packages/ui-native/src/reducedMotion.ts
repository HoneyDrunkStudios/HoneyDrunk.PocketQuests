interface ReducedMotionHost {
  addEventListener(
    event: "reduceMotionChanged",
    listener: (enabled: boolean) => void,
  ): { remove(): void } | undefined;
  isReduceMotionEnabled(): Promise<boolean>;
}

/** Internal host adapter; one subscription serves every mounted Loading. */
export function createReducedMotionStore(host: ReducedMotionHost) {
  let reduced = true;
  const listeners = new Set<() => void>();
  let disconnect: (() => void) | undefined;

  function update(value: boolean) {
    if (reduced === value) return;
    reduced = value;
    for (const listener of listeners) listener();
  }

  function subscribe(listener: () => void) {
    listeners.add(listener);
    if (listeners.size === 1) {
      let active = true;
      let changed = false;
      // RN Web 0.21 keys handlers by their string representation. A single
      // shared listener avoids collisions between identical per-loader closures.
      const subscription = host.addEventListener(
        "reduceMotionChanged",
        (value) => {
          if (!active) return;
          changed = true;
          update(value);
        },
      );
      void host
        .isReduceMotionEnabled()
        .then((value) => {
          if (active && !changed) update(value);
        })
        .catch(() => {
          /* Remain static if the initial host preference cannot be read. */
        });
      disconnect = () => {
        active = false;
        // RN Web may return no subscription when matchMedia is unavailable.
        subscription?.remove();
      };
    }

    let subscribed = true;
    return () => {
      if (!subscribed) return;
      subscribed = false;
      listeners.delete(listener);
      if (listeners.size === 0) {
        disconnect?.();
        disconnect = undefined;
        // Reconnecting queries the current host preference, with a static fallback.
        reduced = true;
      }
    };
  }

  return {
    subscribe,
    getSnapshot: () => reduced,
    getServerSnapshot: () => true,
  };
}
