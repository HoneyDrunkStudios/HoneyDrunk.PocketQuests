import { useCallback, useEffect, useState } from "react";
import { fetchDiscoveryAsync, type DiscoveryDocument } from "expo-auth-session";
import { identityUrl, requestTimeoutMs } from "../config/client";
import { waitForAuth } from "./auth-request";
import {
  validateProviderConfiguration,
  type ProviderConfiguration,
} from "./provider-session";

export type ProviderSetup = {
  config: ProviderConfiguration;
  discovery: DiscoveryDocument;
};

// Both entry points must recover from a transient configuration/discovery failure.
// Each attempt owns its cleanup so late results cannot replace a newer attempt.
export function useProviderSetup() {
  const [attempt, setAttempt] = useState(0);
  const [setup, setSetup] = useState<ProviderSetup | null>(null);
  const [error, setError] = useState<string | null>(null);
  const retry = useCallback(() => {
    setSetup(null);
    setError(null);
    setAttempt((value) => value + 1);
  }, []);

  useEffect(() => {
    let current = true;
    const controller = new AbortController();
    const load = async (): Promise<ProviderSetup> => {
      const response = await fetch(identityUrl + "/client-configuration", {
        signal: controller.signal,
      });
      if (!response.ok) throw new Error("Sign-in configuration unavailable");
      const config = validateProviderConfiguration(await response.json());
      if (controller.signal.aborted) throw new Error("Sign-in setup cancelled");
      const discovery = await fetchDiscoveryAsync(config.authority);
      for (const endpoint of [
        discovery.authorizationEndpoint,
        discovery.tokenEndpoint,
      ]) {
        if (!endpoint || new URL(endpoint).protocol !== "https:")
          throw new Error("Secure provider sign-in is not configured");
      }
      return { config, discovery };
    };
    void waitForAuth(
      load(),
      controller.signal,
      requestTimeoutMs.signInConfiguration,
    )
      .then((value) => {
        if (current) setSetup(value);
      })
      .catch(() => {
        if (current)
          setError(
            "Sign-in is unavailable. Check your connection and try again.",
          );
      })
      .finally(() => controller.abort());
    return () => {
      current = false;
      controller.abort();
    };
  }, [attempt]);

  return { setup, error, retry };
}
