import type { ClockSample, Command } from "../shared/contracts";
import { dependsOn } from "./sync-recovery";

export type UnverifiedAction = {
  // Deliberately not an executable command proof. Reconnection cannot promote it.
  command: Omit<Command, "recordedTime">;
  observedUtc: string;
  clock: ClockSample | null;
  reason: string;
  blockedBy: string[];
};

export function unverifiedBlockers(
  command: Command,
  pending: UnverifiedAction[],
) {
  return pending.filter((entry) => dependsOn(command, entry.command));
}
