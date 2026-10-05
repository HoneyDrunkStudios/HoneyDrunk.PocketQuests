import type { DiscoveryDocument, TokenResponse } from "expo-auth-session";
import { identityUrl, requestTimeoutMs } from "../config/client";
import type { Credentials } from "./auth-session";
import type { Session } from "./storage";
import { RequestError } from "./request-error";
import { waitForAuth } from "./auth-request";

export type ProviderConfiguration = {
  authority: string;
  clientId: string;
  scope: string;
};

export function validateProviderConfiguration(config: ProviderConfiguration) {
  const authority = new URL(config.authority);
  if (
    authority.protocol !== "https:" ||
    authority.username ||
    authority.password ||
    authority.search ||
    authority.hash ||
    typeof config.clientId !== "string" ||
    !config.clientId.trim() ||
    typeof config.scope !== "string" ||
    !config.scope.trim()
  )
    throw new Error("Secure provider sign-in is not configured.");
  return config;
}

export function providerScopes(
  config: ProviderConfiguration,
  discovery: DiscoveryDocument | null,
) {
  const scopes = new Set([
    "openid",
    "profile",
    ...config.scope.split(/\s+/).filter(Boolean),
  ]);
  // The configured broker is authoritative. Do not assume every provider grants
  // offline access or add provider-specific consent parameters implicitly.
  if (
    discovery?.discoveryDocument?.scopes_supported?.includes("offline_access")
  )
    scopes.add("offline_access");
  return [...scopes];
}

export function providerCredentials(
  tokens: Pick<
    TokenResponse,
    "accessToken" | "refreshToken" | "expiresIn" | "issuedAt"
  >,
  config: ProviderConfiguration,
): Credentials {
  return {
    token: tokens.accessToken,
    refreshToken: tokens.refreshToken,
    authority: config.authority,
    clientId: config.clientId,
    expiresAt:
      tokens.expiresIn === undefined
        ? undefined
        : (tokens.issuedAt + tokens.expiresIn) * 1000,
  };
}

async function configurationFor(session: Session) {
  const configuration = await fetch(identityUrl + "/client-configuration", {
    signal: AbortSignal.timeout(requestTimeoutMs.signInConfiguration),
  });
  if (!configuration.ok)
    throw new RequestError(
      configuration.status,
      "Sign-in renewal is unavailable. Try again when connected.",
    );
  const config = validateProviderConfiguration(
    (await configuration.json()) as ProviderConfiguration,
  );
  if (
    config.authority !== session.authority ||
    config.clientId !== session.clientId
  )
    throw new RequestError(
      401,
      "Sign-in configuration changed. Sign in again as the same account.",
    );
  return config;
}

export async function renewProviderSession(
  session: Session,
  signal = new AbortController().signal,
): Promise<Credentials> {
  const { fetchDiscoveryAsync, refreshAsync } =
    await import("expo-auth-session");
  const config = await configurationFor(session);
  if (!session.refreshToken)
    throw new RequestError(401, "Sign in again as the same account.");
  const discovery = await waitForAuth(
    fetchDiscoveryAsync(config.authority),
    signal,
    requestTimeoutMs.signInConfiguration,
  );
  if (
    !discovery.tokenEndpoint ||
    new URL(discovery.tokenEndpoint).protocol !== "https:"
  )
    throw new Error("Secure provider renewal is not configured.");
  let tokens: TokenResponse;
  try {
    tokens = await refreshAsync(
      { clientId: config.clientId, refreshToken: session.refreshToken },
      discovery,
    );
  } catch (error) {
    const code = (error as { code?: string }).code;
    if (
      [
        "invalid_grant",
        "invalid_client",
        "unauthorized_client",
        "interaction_required",
      ].includes(code ?? "")
    )
      throw new RequestError(
        401,
        "Sign in again as the same account. Your recorded actions remain on this device.",
      );
    throw new Error("Sign-in renewal was interrupted. Retry when connected.");
  }
  return {
    ...providerCredentials(tokens, config),
    refreshToken: tokens.refreshToken ?? session.refreshToken,
  };
}

export async function verifyProviderSession(session: Session): Promise<void> {
  await configurationFor(session);
  // The coordinator retained the candidate securely before this network call.
  // Verification never releases another account's queue, including on restart.
  const response = await fetch(identityUrl + "/users/me", {
    headers: { Authorization: `Bearer ${session.token}` },
    signal: AbortSignal.timeout(requestTimeoutMs.api),
  });
  if (!response.ok)
    throw new RequestError(
      response.status,
      "The renewed session could not be verified. Retry when connected, or sign in again as the same account.",
    );
  const owner = (await response.json()) as { userId: string; state: string };
  if (owner.userId !== session.userId || owner.state !== "Active")
    throw new RequestError(
      401,
      "The renewed session does not belong to this active account.",
    );
}
