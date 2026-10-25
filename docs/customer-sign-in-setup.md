# Customer sign-in setup

## October 22, 2026 inspection

Pocket Quests base source: `e18daa1c0c846cff8360bb9da5a80c03a22b94dd`, plus local sign-in changes. On October 22 the native redirect, application-specific branding and development Google federation were applied and verified. The actual emulator shows the Pocket Quests wordmark, custom sign-in text and Google button; that button reaches Google. Completed customer authentication and the full signed-in walkthrough remain pending owner sign-in. These source/documentation changes have not been published.

Read-only inspection of the deployed development Identity service and customer Microsoft Graph configuration found:

| Item | Observed value |
| --- | --- |
| Identity service | `https://app-hd-identity-dev.azurewebsites.net` |
| Customer tenant | `28ad6dfb-254d-41c3-a840-9b2135ed11a6` / `HoneyDrunk Customers` |
| Initial domain | `honeydrunkcustomers.onmicrosoft.com` |
| Authority | `https://honeydrunkcustomers.ciamlogin.com/28ad6dfb-254d-41c3-a840-9b2135ed11a6/v2.0` |
| Client application | `92a9912e-3ff7-491a-8f9c-c220bb8d4434`, currently named `Pocket Quests Web - Dev` |
| Client object | `4f718d64-ee19-4d88-9ab1-ba59dfd425ca` |
| SPA redirect | `http://localhost:8081/callback` |
| Native public-client redirects | Initially empty; now `pocketquests://callback` after the correction below |
| User flow | `HoneyDrunkSignUpSignIn`, ID `3c679ecf-a488-4759-8011-c86f3cc0f921`; associated with the client above |
| Enabled flow providers | Initially email OTP only; now email OTP and Google, confirmed by Graph readback |
| Application branding themes | Initially empty; now `Pocket Quests Dev`, ID `04bc26c6-de5e-4e22-ab7b-bd3e93bc14ef`, assigned only to the client above |

The initial provider-inventory API returned 403 to the existing CLI session. That did not establish that no providers existed elsewhere in the tenant. The initial flow was readable and listed only email OTP. The founder reported no known Google or Apple sign-in registrations; the initial Google console inspection below found an existing project but no OAuth clients. Default organizational branding returned 404, explaining the original tenant-name sign-in label. Direct Entra admin browser authentication timed out. Azure portal access subsequently succeeded and the customer directory was verified; no admin consent or broader CLI permissions were granted. Later applied changes and readbacks are recorded below.

## Native return fix: exact development change

The app uses authorization code + PKCE and constructs `pocketquests://callback` from its registered `pocketquests` scheme and `callback` path. The development client registration lacked that return URI. The correction added it under `publicClient.redirectUris` and preserved the existing SPA redirect, empty Web redirect list and account audience. No mobile client secret, implicit grant or API permission was added.

Applied Graph v1.0 PATCH target:

```text
/applications/4f718d64-ee19-4d88-9ab1-ba59dfd425ca
```

Body: [native-redirect.patch.json](sign-in/native-redirect.patch.json). Actual impact: **0 creates, 1 application update, 0 deletes**. Readback confirmed the new native URI and preserved values. The patch records the empty-list correction; do not blindly replay it over a later list. Future updates must re-read and merge legitimate entries. An actual authenticated native return remains to be tested. Rollback removes only the newly added URI, retaining intervening changes. No production registration or service deployment was included.

## Pocket Quests branding

Applied one application-specific theme named `Pocket Quests Dev`, ID `04bc26c6-de5e-4e22-ab7b-bd3e93bc14ef`, assigned only to the client above. The customer tenant name and tenant-wide default were not changed. Update this theme rather than creating a duplicate.

- Page title: **Sign in to Pocket Quests**.
- Description: **Your everyday adventure continues here.**
- Background: `#091A29`. The uploaded [wordmark](sign-in/pocket-quests-wordmark.png) is a transparent 245 x 36 PNG (1,530 bytes), rendered from the app's existing Libre Baskerville font in `#091A29` for the light sign-in panel.
- Centered sign-in panel with provider controls kept legible. Preserve provider-owned password/MFA pages and existing policy links; do not invent legal URLs or hide consent/security information.
- Preview desktop and narrow mobile layouts, then verify the actual hosted sign-in page; the admin preview does not prove text overrides loaded.

Applied impact: **1 theme creation, 1 application assignment and 1 logo upload**. No paid resource or custom domain was created. Current provider/domain identity remains visible in the browser address bar.

The application picker worked on the later attempt. The portal's banner Browse control still did not open a file chooser, so the logo was uploaded through the documented Graph beta `PUT /organization/{tenant}/branding/themes/{theme}/localizations/0/bannerLogo` endpoint with `image/png`. Readback confirmed the color, logo URL and `SignIn_Title`/`SignIn_Description` values. The portal confirmed one application assignment. The actual Android hosted sign-in page displayed the wordmark and both custom strings with a secure connection. Its narrow layout uses a full white sign-in panel; desktop color/appearance acceptance remains separate from that mobile check.

## New provider registrations

Use studio-owned development registrations and native provider branding. Keep secrets in the provider/Entra configuration and the approved secret store, never in Pocket Quests, chat, screenshots, source, test artifacts or public Expo variables. Account-owner login, legal terms, credentials, consent and paid membership must be handled at their actual setup gates.

### Google

Public support and developer contact address: **`contact@honeydrunkstudios.com`**. The founder created its Google account, and the signed-in account and support-email selection were verified. Ongoing monitoring of the inbox remains an operational responsibility.

Live console inspection subsequently found an existing **Pocket Quests** project, ID `pocket-quests-403417`, number `833138113853`, accessible in the founder's signed-in Google account. Its Credentials page lists no OAuth clients and prompts for consent-screen configuration. Reuse this project for preparation; no new project is needed. Apple Developer membership remains unknown.

The founder declined a personal public support address and subsequently completed the existing-email Google account registration. No Workspace subscription or mailbox migration was created. With explicit action-time approval, the studio account received only `roles/oauthconfig.editor`. This was sufficient to create the consent app and select the studio support email. Google documentation describes Editor/Owner, but broad project access was not needed for those actions. The console's overview, client and scope pages required additional read permissions; the existing owner session performed those approved configuration steps without broadening the studio account's role.

The founder separately approved Google Cloud terms, the API Services User Data Policy, OAuth client creation and Google-to-Entra federation. Applied configuration:

| Item | Applied value |
| --- | --- |
| Consent app | `Pocket Quests Dev` |
| Audience/status | External / Testing; not published to production |
| Initial test user | `contact@honeydrunkstudios.com` |
| Web client name | `HoneyDrunk Customers - Pocket Quests Dev` |
| Web client ID | `833138113853-pv3pdcab1cmofgs1v2n8ppff92qto2am.apps.googleusercontent.com` |
| Google provider ID in Entra | `851a3837-7f5c-4830-b9a8-144c8d683b05` |
| Declared scopes | `openid`, `https://www.googleapis.com/auth/userinfo.email`, `https://www.googleapis.com/auth/userinfo.profile` |
| Secret custody | Transferred directly from Google's creation dialog to Entra's protected provider configuration; not saved in source, chat, screenshots or local reports |

The test-user entry is not a restriction to the studio email. Google documents an exception to the Testing allowlist and seven-day authorization expiry for requests limited to `openid`, `userinfo.email` and `userinfo.profile`, including Sign in with Google. Customers authenticate with their own Google accounts; the studio address is the application's support/developer contact. Our declared scopes match that exception, but a completed live exchange remains to be verified. Workspace administrator policies can still restrict individual accounts. See [Google audience and Testing rules](https://support.google.com/cloud/answer/15549945).

Federation goes through Entra, so this is a Web application client, not an Android client. No Gmail, Drive or cloud-resource scopes were selected. No JavaScript origins were added.

Registered the tenant-specific callbacks required by [Microsoft's Google setup guide](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-google-federation-customers):

```text
https://login.microsoftonline.com
https://login.microsoftonline.com/te/28ad6dfb-254d-41c3-a840-9b2135ed11a6/oauth2/authresp
https://login.microsoftonline.com/te/honeydrunkcustomers.onmicrosoft.com/oauth2/authresp
https://28ad6dfb-254d-41c3-a840-9b2135ed11a6.ciamlogin.com/28ad6dfb-254d-41c3-a840-9b2135ed11a6/federation/oidc/accounts.google.com
https://28ad6dfb-254d-41c3-a840-9b2135ed11a6.ciamlogin.com/honeydrunkcustomers.onmicrosoft.com/federation/oidc/accounts.google.com
https://honeydrunkcustomers.ciamlogin.com/28ad6dfb-254d-41c3-a840-9b2135ed11a6/federation/oauth2
https://honeydrunkcustomers.ciamlogin.com/honeydrunkcustomers.onmicrosoft.com/federation/oauth2
```

All seven callbacks were checked against the current Microsoft guide and saved on the new client. Authorized domains are `ciamlogin.com` and `microsoftonline.com`. Google is configured in Entra and enabled in `HoneyDrunkSignUpSignIn`; Graph readback confirmed the Google client ID, retained email OTP and the single Pocket Quests app association. The native app callback is a separate Entra client setting and must not replace these federation callbacks.

Created objects: one consent application, one OAuth client with its credential and one Google provider, plus the existing-flow association and one approved studio-account IAM role binding; zero Google projects or paid resources. Re-read these inventories before modifying them.

The real native Google handoff currently says **continue to ciamlogin.com**. Google's app-name/logo display requires brand verification; development configuration is not production brand verification. Public launch still needs product homepage/privacy/terms URLs on a verified owned domain, Google's branding review/publishing requirements and a customer-availability decision. No legal URLs were invented and no production publishing or verification submission was made. See [Google's branding requirements](https://support.google.com/cloud/answer/15549049).

### Apple

Prepare the app identifier `com.honeydrunk.pocketquests` (reuse if already registered), Services ID `com.honeydrunk.pocketquests.signin.dev`, and a Sign in with Apple key under the owner's Apple Developer team. Confirm team and membership before creating anything; no enrollment purchase is authorized here.

Use `honeydrunkcustomers.ciamlogin.com` and `28ad6dfb-254d-41c3-a840-9b2135ed11a6.ciamlogin.com` as the federation domains and the tenant-specific return URLs specified by [Microsoft's Apple guide](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-apple-federation-customers). One applicable return URL is:

```text
https://honeydrunkcustomers.ciamlogin.com/28ad6dfb-254d-41c3-a840-9b2135ed11a6/federation/oauth2
```

Configure Entra with the Services ID, Team ID, Key ID and private key through its protected admin form, then associate Apple with the flow. Track key custody and the documented six-month client-secret renewal requirement with the Identity owner. Test first and repeat consent, including Apple's private relay email. Do not silently merge accounts by email.

Expected objects, if absent: one App ID, one Services ID, one signing key, one Apple provider configuration and its flow association.

### Microsoft accounts

Treat personal Microsoft accounts and an organization's Entra accounts as different acceptance cases. The documented [Entra-tenant federation](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-entra-id-federation-customers) covers the configured organization's accounts; it does not by itself establish Outlook/Hotmail consumer support.

Prepare a personal-account federation registration only after validating the [custom OIDC route](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-custom-oidc-federation-customers), consumer issuer, supported audience and required claims against the provider. Keep code flow and issuer validation enabled. Record the exact supported account types and test both success and rejection; do not label a single-organization button as universal Microsoft sign-in. This provider's final registration payload remains pending that validation.

## Acceptance and walkthrough

Keep the existing email method available during staged setup. Removing it is a separate policy decision. Only advertise providers after configuration and a successful live return; do not add decorative Google/Apple/Microsoft buttons backed by the same unconfigured email flow.

For each required provider: authenticate using an explicitly selected test identity, return to the app, verify Identity and the local API agree on the account, cancel/retry, restart, renew the session and sign out/re-enter. Preserve existing account-bound queues and never use a token injection or mock provider as live-provider evidence. The browser fixture remains useful regression evidence, not proof of real federation.

Then run the [agent-driven walkthrough](guided-walkthrough.md). Source changes, exported bundles and a booted emulator alone do not establish provider or device acceptance.

## Configuration sources

- [Expo SDK 57 AuthSession](https://docs.expo.dev/versions/v57.0.0/sdk/auth-session/)
- [Microsoft redirect URI configuration](https://learn.microsoft.com/en-us/entra/identity-platform/reply-url)
- [Graph application update](https://learn.microsoft.com/en-us/graph/api/application-update?view=graph-rest-1.0)
- [Application-specific branding](https://learn.microsoft.com/en-us/entra/fundamentals/how-to-customize-branding-themes-apps)
- [External-tenant provider support](https://learn.microsoft.com/en-us/entra/external-id/customers/concept-authentication-methods-customers)
