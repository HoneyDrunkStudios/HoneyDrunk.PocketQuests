import type { Anchor, Command } from "../shared/contracts";
import type { ClockReading } from "./clock-source";

type ProofResult =
  | { proof: NonNullable<Command["recordedTime"]>; reason?: never }
  | { proof: null; reason: string };

export function recordedProof(
  anchor: Anchor | null,
  reading: ClockReading,
  currentDeviceId: string,
): ProofResult {
  const sample = reading.sample;
  if (!sample)
    return {
      proof: null,
      reason: reading.reason ?? "Device timing could not be verified.",
    };
  if (!anchor?.clock)
    return {
      proof: null,
      reason:
        "This action has no compatible trusted time baseline. Connecting can establish timing for future actions; this action remains pending verification.",
    };
  if (
    anchor.deviceId !== currentDeviceId ||
    sample.kind !== anchor.clock.kind ||
    sample.epoch !== anchor.clock.epoch
  )
    return {
      proof: null,
      reason:
        "The app or device timing baseline changed. This action remains pending timing verification.",
    };
  const elapsed = sample.elapsedMilliseconds - anchor.clock.elapsedMilliseconds;
  const wallElapsed =
    Date.parse(sample.deviceUtc) - Date.parse(anchor.deviceUtc);
  if (
    !Number.isSafeInteger(sample.elapsedMilliseconds) ||
    !Number.isSafeInteger(anchor.clock.elapsedMilliseconds) ||
    anchor.clock.elapsedMilliseconds < 0 ||
    !Number.isSafeInteger(elapsed) ||
    elapsed < 0 ||
    !Number.isFinite(wallElapsed) ||
    Math.abs(wallElapsed - elapsed) > 120_000 ||
    !Number.isSafeInteger(anchor.ordinal) ||
    anchor.ordinal < 0 ||
    anchor.ordinal >= Number.MAX_SAFE_INTEGER ||
    !Number.isSafeInteger(anchor.lastElapsedMilliseconds ?? 0) ||
    elapsed < (anchor.lastElapsedMilliseconds ?? 0)
  )
    return {
      proof: null,
      reason:
        "Device time or recorded ordering changed. The original action is preserved pending timing verification.",
    };
  return {
    proof: {
      anchorId: anchor.id,
      bootId: anchor.bootId,
      ordinal: anchor.ordinal + 1,
      elapsedMilliseconds: elapsed,
      deviceUtc: sample.deviceUtc,
    },
  };
}
