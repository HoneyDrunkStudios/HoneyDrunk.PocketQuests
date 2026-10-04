# API boundary and public contract

The existing `/api/...` URLs, successful JSON fields, enum strings, export filenames and problem JSON remain compatible. `contracts/pocketquests-v1.openapi.json` describes that existing surface; `v1` is the document name, not a new route prefix. A future versioned-route rollout needs an explicit client migration decision.

Public records live under `PocketQuests.Api/Contracts` and map explicitly to internal records. Persistence and domain behavior are not serialized directly by the HTTP handlers. Unexpected `ArgumentException`, `InvalidOperationException`, `KeyNotFoundException` and other unclassified failures return a generic 500 with a generated diagnostic ID. The error log contains only the exception type and that ID, without the exception object, message, stack, request body, headers, URL or claims. Explicit quest validation/conflict/missing-resource exceptions retain 400/409/404. Existing authentication challenges and clock/reconciliation 503 semantics remain in place. Reconciliation retains `Retry-After: 1`.

JSON binding rejects missing/null required constructor values and invalid JSON with 400. Optional fields retain their defaults. Nested quest allocations reject null entries explicitly. This does not change valid command payloads.

Request records retain the Web JSON convention that accepts numbers or numeric strings. `QuestInput` and `ShareInput` keep that input behavior separate from the corresponding response records. Response records use strict numeric metadata and required constructor properties: numeric outputs are JSON numbers, and response properties are always present, including explicit null/default values. Optional request defaults remain optional. The actual Program HTTP tests assert these values and presence rules; endpoint-derived schema tests enforce the matching directional contracts. Incoming quest `baseXp` remains ignored because domain rules compute it.

## HTTP configuration

Configuration section `Api:Http`:

```json
{
  "Api": {
    "Http": {
      "AllowedOrigins": ["https://quests.example.test"],
      "KnownProxies": ["192.0.2.10"],
      "HttpsPort": 443,
      "CommandPermitLimit": 120,
      "ExportPermitLimit": 5,
      "RateWindowSeconds": 60
    }
  }
}
```

The addresses above are documentation examples. Supply the actual immediate proxy addresses and browser origins during environment configuration; this patch provisions nothing.

- Commands and exports have separate fixed-window budgets, partitioned by the verified issuer/subject pair, with no queue. Rejection returns 429 problem JSON and `Retry-After`. These limits are per API process; horizontal scaling requires a separately reviewed shared/gateway policy.
- HTTPS redirection to the configured external TLS port and 30-day HSTS apply outside Development/Testing. Native clients must start with an HTTPS base URL; redirection cannot protect an initial plaintext request.
- Forwarding processes only `X-Forwarded-For` and `X-Forwarded-Proto`, from explicitly configured immediate proxy IPs, with a one-hop limit and matching header counts. No forwarded host is accepted. Empty trust configuration disables forwarding, including the framework option used by automatic forwarding. The immediate proxy must set or sanitize these headers. Configure the normal ASP.NET `AllowedHosts` setting for the environment too.
- Development/Testing default browser origins are `http://localhost:8081` and `http://127.0.0.1:8081` when no origin configuration exists. Other environments have no allowed browser origins by default and accept only explicit HTTPS origins. CORS controls browser access to responses; it does not authenticate native clients. Authentication still applies.
- Invalid limits, ports, proxy addresses or origins fail startup. No APIM, paid gateway, cloud resource or deployment is activated.

## Contract generation and checks

The first-party `Microsoft.AspNetCore.OpenApi` 10.0.12 generator reads endpoint metadata and JSON contracts. See [Microsoft's OpenAPI documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0). `MapOpenApi` serves the document only in Development/Testing. Export metadata describes both JSON and binary ZIP downloads. Authentication requirements and error statuses are in the document.

`ApiContractTests.EndpointOpenApiMatchesCommittedContract` starts the actual API `Program` through `WebApplicationFactory` with Testing settings, no broker, and no calls to SQL or Identity. It compares generated operations with the running endpoint data source and checks the committed document. Separate HTTP fixtures exercise errors, policies and wire equivalence using synthetic storage/authentication.

```powershell
./scripts/Test-ApiContract.ps1              # compare against committed OpenAPI
./scripts/Test-ApiContract.ps1 -Update      # deliberately update it after reviewing the API change
./scripts/Test-ApiContract.ps1 -NoBuild -Configuration Release
```

The existing backend CI invokes the check after building. The fixture stores no provider credentials. The mobile owner must generate TypeScript from this document and retain its generated-client drift check in the combined change.
