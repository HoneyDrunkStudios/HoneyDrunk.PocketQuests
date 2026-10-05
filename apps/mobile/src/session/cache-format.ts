import { isCatalog, isQuestState, isRecord } from "../api/decode";
import type { LocalAccount } from "./offline-store";

export const cacheVersion = 1;
export const encodeAccount = (account: LocalAccount) => ({
  version: cacheVersion,
  account,
});

// A readable journal is kept separately from display readiness. Legacy flat
// generations remain readable; a future envelope is retained by quarantine.
export function decodeAccount(
  value: unknown,
  userId: string,
): LocalAccount | null {
  if (!isRecord(value)) return null;
  if (Object.hasOwn(value, "version")) {
    if (value.version !== cacheVersion || !isRecord(value.account)) return null;
    value = value.account;
  }
  if (
    !isRecord(value) ||
    value.userId !== userId ||
    !Array.isArray(value.queue) ||
    (value.rejected !== undefined && !Array.isArray(value.rejected)) ||
    (value.unverified !== undefined && !Array.isArray(value.unverified))
  )
    return null;
  const stringList = (items: unknown): items is string[] =>
    Array.isArray(items) && items.every((item) => typeof item === "string");
  // Recovery presentation also consumes persisted metadata. Keep malformed
  // records in quarantine rather than exposing objects as text or array fields.
  if (
    ((value.rejected ?? []) as unknown[]).some(
      (entry) =>
        !isRecord(entry) ||
        !["rejected", "blocked"].includes(String(entry.kind)) ||
        typeof entry.reason !== "string" ||
        (entry.blockedBy !== undefined && !stringList(entry.blockedBy)),
    )
  )
    return null;
  if (
    ((value.unverified ?? []) as unknown[]).some(
      (entry) =>
        !isRecord(entry) ||
        typeof entry.reason !== "string" ||
        typeof entry.observedUtc !== "string" ||
        !stringList(entry.blockedBy),
    )
  )
    return null;
  const commands: unknown[] = [
    ...value.queue,
    ...((value.rejected ?? []) as unknown[]).map((entry) =>
      isRecord(entry) ? entry.command : null,
    ),
    ...((value.unverified ?? []) as unknown[]).map((entry) =>
      isRecord(entry) ? entry.command : null,
    ),
  ];
  if (
    commands.some(
      (command) =>
        !isRecord(command) ||
        typeof command.operationId !== "string" ||
        typeof command.action !== "string",
    )
  )
    return null;
  // Journal records are retained byte-for-byte. Their existing recovery flow
  // handles rejection/uncertain timing; decoding never promotes them to proof.
  const journal = value as unknown as LocalAccount;
  if (!isQuestState(value.state) || !isCatalog(value.catalog))
    return { ...journal, state: null, catalog: null, requiresReload: true };
  return {
    ...journal,
    state: value.state,
    catalog: value.catalog,
    requiresReload: false,
  };
}
