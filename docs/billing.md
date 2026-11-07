# Pocket Quests Plus

Free accounts can keep five active custom quests. An unarchived custom quest definition uses one slot, including a recurring definition. System quests and individual occurrences do not use slots. Archiving frees a slot. If a subscription ends while an account is over the limit, players can still edit, archive, and complete their existing quests.

Pocket Quests owns access rules, account links, and the lifetime grant. The API composes the Stripe adapter from `HoneyDrunk.Payments.Stripe`; business services use `HoneyDrunk.Payments.Abstractions`. Checkout returns never grant access. Paid access requires a matching active Stripe subscription and a paid invoice. A lifetime grant is local to a verified Pocket Quests account and needs no Stripe customer or payment.

## Sandbox

The HoneyDrunk Studios Stripe sandbox product is `prod_pocket_quests_plus`. Its USD prices are $4.99 monthly and $39.99 yearly. Pocket Quests uses HoneyDrunk.Payments 0.1.3. For sandbox payment tests without a Stripe tax origin, set `Billing:SandboxDisableAutomaticTax=true` alongside `Billing:SandboxEnabled=true`. The provider rejects the tax opt-out for live keys.

| Setting | Sandbox value |
| --- | --- |
| `Billing:MonthlyPriceId` | `price_1UZCMLAnroUlmKZ8TCF8ocJY` |
| `Billing:YearlyPriceId` | `price_1UZCMRAnroUlmKZ8U0mF4XXR` |
| `Billing:ReturnUrl` | A trusted web Settings URL served by Pocket Quests; use HTTPS outside loopback. |
| `Billing:StripeApiKeyName` | Vault secret name for a restricted Stripe sandbox API key. Allow Checkout Sessions creation, Subscription reads, and Invoice reads. |
| `Billing:StripeWebhookSecretName` | Vault secret name for this sandbox endpoint's signing secret. |
| `Billing:PortalUrl` | Optional Stripe-hosted sandbox Customer Portal login URL. |

For a local Development or Testing host, `Billing:StripeApiKey` and `Billing:StripeWebhookSecret` can come from the host's private configuration instead of Vault. Never put credentials in the client or in tracked settings. Set `Billing:SandboxEnabled=true` after the sandbox keys, prices and return URL are configured.

For a first sandbox Checkout, Pocket Quests supplies an account-specific address at `example.com` to satisfy the shared adapter's customer requirement. It does not send a sign-in email. Before enabling live billing, update HoneyDrunk.Payments to let hosted Checkout collect the email when creating a customer, or provide a verified billing contact through the product.

Use a dedicated Stripe sandbox endpoint or local Stripe CLI forwarding to `POST /api/billing/stripe/webhook`. Subscribe to `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.paid`, `invoice.payment_failed`, and `checkout.session.expired`. The API verifies signatures through HoneyDrunk.Payments and rejects live events and live API keys.

If using the Expo Go development host, forward to a disposable `PQExpoGo_<12 hex digits>` LocalDB instance with sandbox billing enabled. The retained paired-account host does not forward webhook signatures.

After a timeout, retry the same interval within 23 hours to reuse the original Checkout idempotency key. A signed expiry event releases an open reservation. If Stripe never created a session or the expiry event was lost, an operator must reconcile the sandbox reservation against Stripe before clearing it.

Verify both intervals with a sandbox card on a free test account. After the webhook, reload Settings and verify a sixth custom quest can be saved. Verify expired Checkout can be restarted and a canceled or unpaid subscription restores the Free limit without removing quests.

On November 7, 2026, two disposable accounts completed the monthly and yearly hosted sandbox checkouts with the published 0.1.3 packages and Stripe test cards. Pocket Quests handled the signed `invoice.paid` and `customer.subscription.created` events for each account. Each account went from Free and rejecting a sixth quest to Plus and retaining six quests.

Stripe Checkout is offered on web only. The native app shows plan and quest usage. Before release, choose the native purchase path and verify store requirements for the intended storefronts. Live billing also needs a dedicated catalog, tax and portal setup, and verification of cancellations and account erasure.

## Shared UI

Mobile Settings uses the pinned `@honeydrunk/ui-native` and `@honeydrunk/ui-tokens` packages.
