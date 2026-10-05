import type { ApiOperations } from "./generated";
import { apiUrl, requestTimeoutMs } from "../config/client";
import { RequestError } from "../session/request-error";
import { decodeResponse } from "./decode";

export function createQuestClient(
  authenticate: <T>(request: (token: string) => Promise<T>) => Promise<T>,
) {
  return async <K extends keyof ApiOperations>(
    operation: K,
    body: ApiOperations[K]["body"],
    query: ApiOperations[K]["query"],
  ): Promise<ApiOperations[K]["response"]> => {
    const [method, route] = operation.split(" ");
    const parameters = query ? "?" + new URLSearchParams(query).toString() : "";
    // Construct once so a token refresh retries precisely the same command/proof.
    const payload = body === undefined ? undefined : JSON.stringify(body);
    return authenticate(async (token) => {
      const response = await fetch(apiUrl + route + parameters, {
        method,
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: payload,
        signal: AbortSignal.timeout(requestTimeoutMs.api),
      });
      if (!response.ok) {
        const problem = (await response.json().catch(() => null)) as {
          detail?: unknown;
        } | null;
        throw new RequestError(
          response.status,
          response.status === 401
            ? "Sign in again as the same account to synchronize your recorded actions."
            : typeof problem?.detail === "string" && response.status < 500
              ? problem.detail
              : `The request could not be completed (${response.status}). Try again when connected.`,
        );
      }
      const value: unknown = await response.json();
      return decodeResponse(operation, value);
    });
  };
}
