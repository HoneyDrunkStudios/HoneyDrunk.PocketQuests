import contract from "./runtime-schemas.json";
import type { ApiOperations, CatalogResponse, QuestState } from "./generated";
import type { Command } from "../shared/contracts";

type Schema =
  | boolean
  | {
      $ref?: string;
      nullable?: boolean;
      pattern?: string;
      enum?: unknown[];
      anyOf?: Schema[];
      oneOf?: Schema[];
      allOf?: Schema[];
      type?: string;
      items?: Schema;
      required?: string[];
      properties?: Record<string, Schema>;
      additionalProperties?: Schema;
    };
const schemas: Record<string, Schema> = contract.schemas;
const responses: Record<string, Schema> = contract.responses;
export function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}
// The supported OpenAPI subset is emitted alongside the TypeScript types. No
// coercion, defaults or field removal: successful decoding preserves wire data.
function matches(value: unknown, schema: Schema): boolean {
  if (typeof schema === "boolean") return schema;
  if (schema.nullable && value === null) return true;
  if (
    typeof value === "string" &&
    schema.pattern &&
    !new RegExp(schema.pattern).test(value)
  )
    return false;
  if (schema.$ref) {
    const target = schemas[schema.$ref.split("/").at(-1)!];
    return target !== undefined && matches(value, target);
  }
  if (schema.anyOf) return schema.anyOf.some((part) => matches(value, part));
  if (schema.oneOf)
    return schema.oneOf.filter((part) => matches(value, part)).length === 1;
  if (schema.allOf) return schema.allOf.every((part) => matches(value, part));
  if (schema.enum) return schema.enum.includes(value);
  if (schema.type === "null") return value === null;
  if (schema.type === "array")
    return (
      Array.isArray(value) &&
      value.every((item) => matches(item, schema.items ?? false))
    );
  if (schema.type === "object")
    return (
      isRecord(value) &&
      (schema.required ?? []).every((name) => Object.hasOwn(value, name)) &&
      Object.entries(value).every(([name, child]) =>
        matches(
          child,
          schema.properties && Object.hasOwn(schema.properties, name)
            ? schema.properties[name]
            : (schema.additionalProperties ?? true),
        ),
      )
    );
  if (schema.type === "integer") return Number.isInteger(value);
  if (schema.type === "number")
    return typeof value === "number" && Number.isFinite(value);
  return typeof value === schema.type;
}
export const isQuestState = (value: unknown): value is QuestState =>
  matches(value, schemas.QuestState);
export const isCatalog = (value: unknown): value is CatalogResponse =>
  matches(value, schemas.CatalogResponse);
// Cached commands feed pendingProjection before server validation. The local
// queue retains full preview terms and numeric clock evidence, unlike the wider
// request DTO accepted from other clients. Do not project malformed terms.
export function isStoredCommand(value: unknown): value is Command {
  if (!isRecord(value) || !matches(value, schemas.QuestCommand)) return false;
  if (value.definition != null && !matches(value.definition, schemas.Quest))
    return false;
  if (
    value.acceptedQuest != null &&
    !matches(value.acceptedQuest, schemas.Quest)
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
  if (!matches(value, responses[operation]))
    // A malformed success is uncertain, not a definitive command rejection.
    throw new Error(
      "The server returned an unreadable response. Your pending actions are retained; retry when connected.",
    );
  return value as ApiOperations[K]["response"];
}
