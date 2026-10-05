import { identityUrl, requestTimeoutMs } from "../config/client";
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
