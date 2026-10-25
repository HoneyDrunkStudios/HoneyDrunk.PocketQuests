import {
  apiUrl,
  developmentSignInEnabled,
  identityUrl,
  requestTimeoutMs,
} from "../config/client";
import { RequestError } from "./request-error";
export async function identityRequest<T>(
  path: string,
  token: string,
  body?: unknown,
): Promise<T> {
  const response = await fetch(identityUrl + path, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(requestTimeoutMs.api),
  });
  if (!response.ok) {
    const detail = await response.json().catch(() => ({}));
    throw new RequestError(
      response.status,
      response.status === 401
        ? "Recent sign-in could not be verified. Sign in again using the same account; the provider must supply fresh authentication evidence."
        : (detail.detail ??
            "Account change could not be completed. Retry when connected."),
    );
  }
  return response.json();
}

// Return the wire response so each lifecycle keeps its own status and owner policy.
export function requestIdentityUser(token: string) {
  return fetch(identityUrl + "/users/me", {
    headers: { Authorization: `Bearer ${token}` },
    signal: AbortSignal.timeout(requestTimeoutMs.api),
  });
}

export function requestProviderConfiguration(
  signal: AbortSignal = AbortSignal.timeout(
    requestTimeoutMs.signInConfiguration,
  ),
) {
  return fetch(identityUrl + "/client-configuration", { signal });
}

export async function requestDevelopmentSession(pairingCode: string) {
  if (!developmentSignInEnabled)
    throw new Error("Development sign-in is disabled.");
  const response = await fetch(apiUrl + "/development/sign-in", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ pairingCode: pairingCode.trim() }),
    signal: AbortSignal.timeout(requestTimeoutMs.api),
  });
  if (!response.ok)
    throw new Error(
      response.status === 401
        ? "The pairing code was not accepted. Check the code on your computer."
        : response.status === 429
          ? "Too many pairing attempts. Wait a minute and try again."
          : "The development backend is unavailable. Check its address and try again.",
    );
  const value: unknown = await response.json();
  if (
    typeof value !== "object" ||
    value === null ||
    !("token" in value) ||
    typeof value.token !== "string" ||
    !value.token.trim()
  )
    throw new Error("The development backend returned an invalid session.");
  return { token: value.token };
}
