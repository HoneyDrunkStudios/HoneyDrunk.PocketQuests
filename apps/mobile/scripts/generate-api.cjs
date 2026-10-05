const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname, "../../..");
function type(schema) {
  if (schema === true) return "unknown";
  if (schema === false) return "never";
  if (schema.nullable) {
    const { nullable: _nullable, ...rest } = schema;
    return [...new Set([type(rest), "null"])].join(" | ");
  }
  if (schema.$ref) {
    if (!schema.$ref.startsWith("#/components/schemas/"))
      throw new Error(`Unsupported API reference: ${schema.$ref}`);
    return schema.$ref.split("/").at(-1);
  }
  if (schema.anyOf) return schema.anyOf.map(type).join(" | ");
  if (schema.oneOf) return schema.oneOf.map(type).join(" | ");
  if (schema.allOf)
    return schema.allOf.map((part) => `(${type(part)})`).join(" & ");
  if (schema.enum)
    return schema.enum.map((value) => JSON.stringify(value)).join(" | ");
  if (schema.type === "array") return `(${type(schema.items)})[]`;
  if (schema.type === "object") {
    if (
      schema.additionalProperties &&
      !Object.keys(schema.properties ?? {}).length
    )
      return `Record<string, ${type(schema.additionalProperties)}>`;
    if (
      schema.additionalProperties &&
      Object.keys(schema.properties ?? {}).length
    )
      throw new Error(
        "Mixed named properties and index signatures need an explicit client mapping.",
      );
    return `{\n${Object.entries(schema.properties ?? {})
      .map(
        ([name, value]) =>
          `  ${JSON.stringify(name)}${schema.required?.includes(name) ? "" : "?"}: ${type(value)};`,
      )
      .join("\n")}\n}`;
  }
  if (schema.type === "integer" || schema.type === "number") return "number";
  if (["string", "boolean", "null"].includes(schema.type)) return schema.type;
  throw new Error(`Unsupported API schema: ${JSON.stringify(schema)}`);
}
const content = (schema) => schema?.content?.["application/json"]?.schema;
function generate(document) {
  let generated =
    "// Generated from contracts/pocketquests-v1.openapi.json. Run npm run generate:api.\n";
  for (const [name, schema] of Object.entries(document.components.schemas).sort(
    ([a], [b]) => a.localeCompare(b),
  ))
    generated += `export type ${name} = ${type(schema)};\n\n`;
  generated += "export type ApiOperations = {\n";
  for (const [route, methods] of Object.entries(document.paths))
    for (const [method, operation] of Object.entries(methods)) {
      // Downloads use the dedicated binary export path, not the JSON request client.
      if (operation.responses?.["200"]?.content?.["application/zip"]) continue;
      if (operation.parameters?.some((parameter) => parameter.in !== "query"))
        throw new Error(
          `Unsupported JSON operation parameters: ${method} ${route}`,
        );
      const request = content(operation.requestBody);
      const parameters =
        operation.parameters?.filter((p) => p.in === "query") ?? [];
      const query = parameters.length
        ? `{ ${parameters.map((p) => `${JSON.stringify(p.name)}${p.required ? "" : "?"}: ${type(p.schema)}`).join("; ")} }`
        : "undefined";
      const body = request
        ? type(request) + (operation.requestBody.required ? "" : " | undefined")
        : "undefined";
      generated += `  ${JSON.stringify(method.toUpperCase() + " " + route)}: { body: ${body}; query: ${query}; response: ${type(content(operation.responses["200"]))} };\n`;
    }
  generated += "};\n";
  return generated;
}
function generateRuntime(document) {
  const responses = {};
  for (const [route, methods] of Object.entries(document.paths))
    for (const [method, operation] of Object.entries(methods)) {
      const schema = content(operation.responses?.["200"]);
      if (schema) responses[`${method.toUpperCase()} ${route}`] = schema;
    }
  // Preserve property names such as Quest.description as well as metadata.
  return (
    JSON.stringify(
      { schemas: document.components.schemas, responses },
      null,
      2,
    ) + "\n"
  );
}
module.exports = { type, generate, generateRuntime };
if (require.main === module) {
  const document = JSON.parse(
    fs.readFileSync(
      path.join(root, "contracts/pocketquests-v1.openapi.json"),
      "utf8",
    ),
  );
  for (const [name, generated] of [
    ["generated.ts", generate(document)],
    ["runtime-schemas.json", generateRuntime(document)],
  ]) {
    const target = path.join(root, "apps/mobile/src/api", name);
    if (process.argv.includes("--check")) {
      if (
        !fs.existsSync(target) ||
        fs.readFileSync(target, "utf8").replace(/\r\n/g, "\n") !== generated
      )
        throw new Error(
          "Generated API types drifted. Run scripts/Test-ApiContract.ps1 -Update to regenerate from actual endpoints.",
        );
      console.log("Generated API types match the committed OpenAPI contract.");
    } else {
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, generated);
    }
  }
}
