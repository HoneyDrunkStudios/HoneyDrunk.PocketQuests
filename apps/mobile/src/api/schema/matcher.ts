export type Schema =
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

// Pure generated-schema matcher shared by the native and administrative clients.
export function createSchemaMatcher(contract: {
  schemas: Record<string, Schema>;
  responses: Record<string, Schema>;
}) {
  const schemas = contract.schemas;
  const responses = contract.responses;
  function isRecord(value: unknown): value is Record<string, unknown> {
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
    return matchesType(value, schema);
  }

  function matchesType(
    value: unknown,
    schema: Exclude<Schema, boolean>,
  ): boolean {
    if (schema.type === "null") return value === null;
    if (schema.type === "array")
      return (
        Array.isArray(value) &&
        value.every((item) => matches(item, schema.items ?? false))
      );
    if (schema.type === "object") return matchesObject(value, schema);
    if (schema.type === "integer") return Number.isInteger(value);
    if (schema.type === "number")
      return typeof value === "number" && Number.isFinite(value);
    return typeof value === schema.type;
  }

  function matchesObject(
    value: unknown,
    schema: Exclude<Schema, boolean>,
  ): boolean {
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
  }

  return {
    matchesSchema: (name: string, value: unknown) =>
      matches(value, schemas[name]),
    matchesResponse: (name: string, value: unknown) =>
      matches(value, responses[name]),
  };
}
