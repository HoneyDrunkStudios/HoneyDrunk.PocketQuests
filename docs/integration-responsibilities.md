# Shared capability responsibilities

Reviewed the complete 30-node Architecture catalog and the September 26 repository/package integration inventory before selecting dependencies. The following accounts for every catalog node; presence in a catalog is not treated as proof of a usable runtime.

| Node or group | Decision and evidence |
|---|---|
| Kernel | Adopted 0.8.1 in both hosts; node/environment identity, initialized request/operation context and scoped activity factory. Public ownership headers are stripped. |
| Standards | Adopted 0.3.0 with analyzers, documentation generation and warnings as errors. Expo has separate TypeScript/format/bundle checks. |
| Auth | Identity composes the actual `IAuthenticationProvider`/`BearerTokenAuthenticationProvider` and `ISigningKeyProvider` contract. No app-owned password or token issuer. Published Auth does not provide the proposed ADR's `IJwtBearerValidator` seam or automatic external OIDC discovery. |
| Data | Adopted EntityFramework 0.7.1 and its HoneyDrunkDbContext foundation, SQL Server constraints/transactions and real EF migrations. Identity's Audit marker maps to its scoped EF unit of work. Product code owns account predicates and transaction locking. |
| Audit | Adopted canonical Abstractions/Data 0.2.1. Identity Auth writes use DataAuditLog. User creation and quest mutations append canonical AuditRecord inside their existing SQL transactions. No public audit query endpoint; no claim of tamper evidence or production retention enforcement. |
| Pulse | Adopted Telemetry.OpenTelemetry 0.4.1. Traces, structured logs and metrics reached Aspire's local OTLP sink. No duplicate standalone OTel registration. Hosted collector/sinks, retention/alerts and mobile telemetry remain to configure. |
| Vault | Required when server-held provider/config secrets exist. Current public OIDC discovery/client IDs/scopes and integrated LocalDB authentication do not require invented secrets. Future secret access must use Vault/managed identity; no provider secret belongs in Expo. |
| Vault.Rotation | Deferred. Existing provider rotators do not establish working Apple/Google secret renewal; document and verify renewal before release. |
| Infrastructure | Owns tenant Bicep; build and what-if completed, deployment deferred. Azure SQL, app hosting, backup/network and observability resources are not provisioned. |
| Actions | Owns reusable CI/release workflows. Local checks implemented; workflow callers and remote permissions are release work, not duplicated workflow engines. |
| Architecture | Owns approved product rules, boundaries, ADR/catalog and implementation records. This milestone does not accept proposed ADRs or claim deployed-node status. |
| Web.Rest | Deferred while the audit's error/serialization/pagination integration defects remain unresolved. Minimal HTTP endpoints currently provide the narrow app contract. |
| Transport, NovOutbox | No current broker or durable external side effect. SQL transactions solve local quest atomicity; add outbox/transport when an actual cross-service effect requires it. |
| Notify, Communications | Deferred until reminders or account-change notification delivery is implemented. Recurrence date arithmetic is product logic, not a claim of a durable scheduling service. |
| Payments | No billing feature in this milestone. |
| Observe | No inbound observation ingestion requirement; it is not the API telemetry exporter. |
| AI, Agents, Capabilities, Memory, Knowledge, Flow, Operator, Evals, Sim | No AI/agent runtime is required for a deterministic starter catalog and progression ledger. Do not add these dependencies merely because the product may later offer recommendations. |
| HoneyHub, Studios, Lore | No runtime dependency in this private personal-progress slice. Repository governance and public presentation remain separate concerns. |

The genuinely missing capability was a running shared user directory/provider boundary. The new **HoneyDrunk.Identity** repository supplies that narrow foundation and an Entra public-key adapter. It does not yet implement all proposed ADR-0060 contracts: internal token issuance, profile lifecycle, erasure fan-out, linking and recovery remain open. Do not freeze placeholder contracts for those capabilities.

Quest progression, durable recurrence semantics and mobile offline timestamp policy remain Pocket Quests responsibilities until there is demonstrated cross-product demand. No new Progression or generic Scheduler node was invented.
