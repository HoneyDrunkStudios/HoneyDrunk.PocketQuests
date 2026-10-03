import { requestTimeoutMs, signInRedirect } from "../config/client";
import { useEffect, useRef, useState } from "react";
import * as AuthSession from "expo-auth-session";
import * as WebBrowser from "expo-web-browser";
import { identityUrl } from "./session";
import { Button, Label } from "../shared/ui";
WebBrowser.maybeCompleteAuthSession();
type Configuration = { authority: string; clientId: string; scope: string };
export function ProviderSignIn({
  title,
  onToken,
  disabled = false,
  recent = false,
}: {
  title: string;
  onToken(token: string): Promise<void>;
  disabled?: boolean;
  recent?: boolean;
}) {
  const [config, setConfig] = useState<Configuration | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    fetch(identityUrl + "/client-configuration", {
      signal: AbortSignal.timeout(requestTimeoutMs.signInConfiguration),
    })
      .then(async (response) => {
        if (!response.ok)
          throw new Error("Provider sign-in is not configured yet.");
        const value = await response.json();
        if (active) setConfig(value);
      })
      .catch(() => {
        if (active)
          setError(
            "Provider sign-in is unavailable. Try again when connected.",
          );
      });
    return () => {
      active = false;
    };
  }, []);
  if (!config) return <Label>{error || "Preparing secure sign-in…"}</Label>;
  return (
    <LoadedProof
      config={config}
      title={title}
      disabled={disabled}
      recent={recent}
      onToken={onToken}
    />
  );
}
function LoadedProof({
  config,
  title,
  disabled,
  recent,
  onToken,
}: {
  config: Configuration;
  title: string;
  disabled: boolean;
  recent: boolean;
  onToken(token: string): Promise<void>;
}) {
  const discovery = AuthSession.useAutoDiscovery(config.authority);
  const [error, setError] = useState("");
  const [working, setWorking] = useState(false);
  const exchanged = useRef<string | null>(null);
  const redirectUri = AuthSession.makeRedirectUri(signInRedirect);
  const [request, response, prompt] = AuthSession.useAuthRequest(
    {
      clientId: config.clientId,
      scopes: ["openid", "profile", config.scope],
      responseType: AuthSession.ResponseType.Code,
      usePKCE: true,
      redirectUri,
      extraParams: recent ? { max_age: "0", prompt: "login" } : {},
    },
    discovery,
  );
  useEffect(() => {
    if (
      response?.type !== "success" ||
      !discovery ||
      !request?.codeVerifier ||
      exchanged.current === response.params.code
    )
      return;
    exchanged.current = response.params.code;
    void AuthSession.exchangeCodeAsync(
      {
        clientId: config.clientId,
        code: response.params.code,
        redirectUri,
        extraParams: { code_verifier: request.codeVerifier },
      },
      discovery,
    )
      .then((tokens) => onToken(tokens.accessToken))
      .catch((value) =>
        setError(
          value instanceof Error
            ? value.message
            : "Sign-in did not finish. Try again.",
        ),
      )
      .finally(() => setWorking(false));
  }, [
    response,
    discovery,
    request?.codeVerifier,
    config.clientId,
    redirectUri,
    onToken,
  ]);
  return (
    <>
      <Button
        title={title}
        disabled={disabled || working || !request}
        onPress={() => {
          setError("");
          setWorking(true);
          void prompt()
            .then((result) => {
              if (result.type !== "success") setWorking(false);
            })
            .catch(() => {
              setWorking(false);
              setError("Sign-in was interrupted. Try again.");
            });
        }}
      />
      {!!error && <Label>{error}</Label>}
    </>
  );
}
