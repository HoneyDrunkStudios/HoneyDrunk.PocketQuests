# Studio administration

The administrative website is a React + TypeScript app in `apps/admin`, built with Vite and served by the existing ASP.NET Core API at `/admin`. It needs no separate Node server, database or hosting service. The API uses the same Contracts → Services → Data → SQL boundaries as the mobile product.

The API build restores the locked admin dependencies and builds the frontend before static-asset discovery. Generated HTML and hashed JavaScript/CSS in `src/PocketQuests.Api/wwwroot/admin` are ignored build output and participate in normal .NET publish. A clean checkout requires a supported Node installation as well as .NET. Do not edit generated assets. Authored React sources remain in Sonar analysis; bundled build output is excluded like the existing mobile export, and React interaction tests provide measured LCOV coverage.

For frontend-only development, run `npm ci` and `npm run dev` in `apps/admin`. The Vite development server proxies `/api` and the sign-in/callback routes to the local API on port 5217; update that development-only proxy if using a different local API binding. Ordinary API hosting serves the production bundle from the same origin. Run `npm run build` for strict TypeScript checking and a production build, and `npm run test:coverage` for the React interaction regressions. The API adapter receives JSON as unknown and validates successful responses before publishing state. Its DTO types reuse the existing endpoint-derived TypeScript artifact; there is no mobile runtime dependency or browser token store.

## Access and sign-in

Access is denied until `Admin:UserId` is explicitly set to the owner's canonical `usr_` Identity account ID. An email address, provider subject or role claim cannot grant access. No production account is configured in source. Both the endpoint policy and Services independently enforce the configured account.

For browser sign-in, configure these server environment settings through the existing host configuration:

| Setting | Value |
| --- | --- |
| `Admin__UserId` | The single explicitly authorized canonical Identity account ID. |
| `Admin__Authority` | The provider's HTTPS OIDC authority. |
| `Admin__ClientId` | A registered **public web client** supporting authorization code with PKCE. |
| `Admin__Scope` | Space-separated delegated API scopes required by HoneyDrunk Identity, in addition to `openid profile`. |
| `Identity__BaseUrl` | The existing trusted HoneyDrunk Identity API, HTTPS outside local development. |

Register the exact callback `<API origin>/admin/signin-oidc` with the provider. The current integration supports public-client PKCE, without a client secret or refresh-token flow. Provider registration, real delegated scopes and live provider acceptance still need verification against the selected identity tenant; they are not provisioned by this feature. Missing authority/client settings produce an explicit unavailable sign-in response and do not enable a development bypass.

The server resolves the provider access token through shared Identity, then issues an encrypted HttpOnly session cookie only for the active configured account. Tokens never enter JavaScript or local storage. Sessions expire after 30 minutes without sliding renewal, and every cookie request rechecks the active Identity account. Every cookie-authenticated POST, including directory search and logout, requires anti-forgery proof obtained from `/api/admin/session`. Supplying an extra bearer header cannot bypass that proof. Server-to-server bearer requests still require the same configured Identity account.

The console and its API responses use `no-store`, a restrictive same-origin content security policy, no framing, and no referrer transmission. Production requires HTTPS and normal host-managed persistent/protected ASP.NET Data Protection keys, shared across replicas when applicable. Those deployment settings are not created here. The static shell is public; every private read and write is authorized on the server.

## Player care

Search and inspect character name, canonical user ID, time zone, join date, setup state and current access restriction. Name matching ignores case and accents. Searches are POST bodies, not URL query strings. Each SQL query selects at most 51 rows, returns 50, and provides a stable user-ID cursor without a total-count query. Characters in search input are literal values, not SQL syntax or wildcard instructions. The directory covers initialized Pocket Quests profiles; it is not a shared Identity user directory.

Suspending or reactivating requires a private reason and an explicit review/confirmation step. The server takes the target account's existing command lock, checks the expected moderation revision, changes only access state and writes the shared audit plus account-erasure association in the same transaction. A concurrent stale edit gets 409. The browser preserves its reason but requires a status reload before retry; it never automatically resends an uncertain mutation. The configured operator cannot change their own access.

Suspension blocks connected Pocket Quests authentication and is rechecked at the product service boundary under the command lock. Recurring maintenance excludes suspended profiles. It preserves profile, quests, rewards and history and leaves the shared Identity/provider account untouched. It cannot remotely clear a device's already cached offline data. Suspension does not pause deadlines or cancel commitments: after reactivation, the existing clock/reconciliation rules still apply.

`AccountAccessRestriction` retains only the current private reason and monotonic revision, rather than copying reasons into immutable audit history. Shared audit records contain action, actor, time and target account, without the private reason. Erasure deletes the current restriction and its linked audit evidence with the target account, while preserving unrelated shared records. No new global Identity suspension API, Audit schema or Outbox schema is introduced.

## Verification

`RelationalApiTests.Admin.cs` covers real API policy enforcement, encrypted-cookie CSRF (including an invalid extra bearer header), inactive Identity rejection, versioned moderation, concurrent edits, bounded paging, literal search, progress preservation, SQL bounds and account erasure. It also checks that the API serves its generated React script and stylesheet. The same cases run in package and pinned-source modes, with measured source-lane C# coverage. They use controlled Identity HTTP replies and disposable deployed SQL, rather than a live provider.

Run `node scripts/tests/Test-AdminConsole.mjs` after building the admin bundle, running `npm ci` in `apps/mobile` and installing Playwright Chromium. It exercises the compiled React app against a controlled API: review/cancel, suspension/reactivation, stale-edit recovery, self protection, literal rendering/search, logout/session-expiry clearing and 320px/large-text layout. Hosted Browser journey runs it too. React component tests additionally cover malformed successes, erased profiles and directory failure recovery. These checks do not replace real API/SQL tests or live OIDC sign-in acceptance.

System-quest draft and publishing tools are a separate feature slice, using the approved immutable-revision design.
