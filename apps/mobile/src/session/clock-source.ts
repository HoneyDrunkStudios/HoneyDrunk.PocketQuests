import { Platform } from "react-native";
import { requireOptionalNativeModule } from "expo";
import * as Crypto from "expo-crypto";
import type { ClockSample } from "../shared/contracts";

export type ClockReading = {
  observedUtc: string;
  sample: ClockSample | null;
  reason?: string;
};
export type ClockSource = { read(): ClockReading };

type NativeClock = {
  sample(): {
    epoch: string;
    elapsedMilliseconds: number;
    utcMilliseconds: number;
  };
};

// A process token proves only continuity within this JS runtime. It is never
// treated as a native boot epoch or reused by a new runtime after restart.
export function createClockSource(): ClockSource {
  let processEpoch: string | undefined;
  return {
    read() {
      const observedUtc = new Date().toISOString();
      if (Platform.OS === "android") {
        try {
          const native =
            requireOptionalNativeModule<NativeClock>("PocketQuestsClock");
          if (!native) throw new Error("Native clock module unavailable");
          const value = native.sample();
          if (
            typeof value.epoch !== "string" ||
            !/^\d+$/.test(value.epoch) ||
            !Number.isSafeInteger(value.elapsedMilliseconds) ||
            value.elapsedMilliseconds < 0 ||
            !Number.isSafeInteger(value.utcMilliseconds)
          )
            throw new Error("Invalid native clock sample");
          const deviceUtc = new Date(value.utcMilliseconds).toISOString();
          return {
            observedUtc: deviceUtc,
            sample: {
              kind: "android-elapsed-realtime-v1",
              epoch: value.epoch,
              elapsedMilliseconds: value.elapsedMilliseconds,
              deviceUtc,
            },
          };
        } catch {
          // A different kind cannot continue an existing native anchor. A new
          // server-issued anchor may establish process-local timing instead.
        }
      }
      try {
        const elapsedMilliseconds = Math.floor(performance.now());
        if (
          !Number.isSafeInteger(elapsedMilliseconds) ||
          elapsedMilliseconds < 0
        )
          throw new Error("Process clock unavailable");
        processEpoch ??= Crypto.randomUUID();
        return {
          observedUtc,
          sample: {
            kind: "js-process-monotonic-v1",
            epoch: processEpoch,
            elapsedMilliseconds,
            deviceUtc: observedUtc,
          },
        };
      } catch {
        return {
          observedUtc,
          sample: null,
          reason:
            "Device timing could not be read. The action remains pending timing verification.",
        };
      }
    },
  };
}

// Shared by provider remounts, replaced with every new JS process/page load.
export const deviceClockSource = createClockSource();
