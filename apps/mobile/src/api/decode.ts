import contract from "./runtime-schemas.json";
import type { ApiOperations, CatalogResponse, QuestState } from "./generated";
import type { Command } from "../shared/contracts";

import { createSchemaMatcher } from "./schema/matcher";
const matcher = createSchemaMatcher(contract);
export function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}
export const isQuestState = (value: unknown): value is QuestState =>
  matcher.matchesSchema("QuestState", value);
export const isCatalog = (value: unknown): value is CatalogResponse =>
  matcher.matchesSchema("CatalogResponse", value);
// Cached commands feed pendingProjection before server validation. The local
// queue retains full preview terms and numeric clock evidence, unlike the wider
// request DTO accepted from other clients. Do not project malformed terms.
export function isStoredCommand(value: unknown): value is Command {
  if (!isRecord(value) || !matcher.matchesSchema("QuestCommand", value))
    return false;
  if (
    value.definition != null &&
    !matcher.matchesSchema("Quest", value.definition)
  )
    return false;
  if (
    value.acceptedQuest != null &&
    !matcher.matchesSchema("Quest", value.acceptedQuest)
  )
    return false;
  if (
    value.expectedRevision != null &&
    typeof value.expectedRevision !== "number"
  )
    return false;
  if (
    value.recordedTime != null &&
    (!isRecord(value.recordedTime) ||
      typeof value.recordedTime.ordinal !== "number" ||
      typeof value.recordedTime.elapsedMilliseconds !== "number")
  )
    return false;
  return true;
}
export function decodeResponse<K extends keyof ApiOperations>(
  operation: K,
  value: unknown,
): ApiOperations[K]["response"] {
  if (!matcher.matchesResponse(operation, value))
    // A malformed success is uncertain, not a definitive command rejection.
    throw new Error(
      "The server returned an unreadable response. Your pending actions are retained; retry when connected.",
    );
  return value as ApiOperations[K]["response"];
}
