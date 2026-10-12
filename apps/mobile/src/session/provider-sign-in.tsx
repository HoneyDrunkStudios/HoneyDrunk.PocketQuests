import { useProviderSetup, type ProviderSetup } from "./use-provider-setup";
import { signInRedirect } from "../config/client";
import { useEffect, useRef, useState } from "react";
import * as AuthSession from "expo-auth-session";
import * as WebBrowser from "expo-web-browser";
import { Button, Label } from "../shared/ui";
WebBrowser.maybeCompleteAuthSession();
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
  const { setup, error, retry } = useProviderSetup();
  if (!setup)
    return (
      <>
        <Label>{error || "Preparing secure sign-in…"}</Label>
        {error && (
          <Button
            title="Retry sign-in setup"
            onPress={retry}
            disabled={disabled}
            secondary
          />
        )}
      </>
    );
  return (
    <LoadedProof
      {...setup}
      title={title}
      disabled={disabled}
      recent={recent}
      onToken={onToken}
    />
  );
}
function LoadedProof({
  config,
  discovery,
  title,
  disabled,
  recent,
  onToken,
}: {
  title: string;
  disabled: boolean;
  recent: boolean;
  onToken(token: string): Promise<void>;
} & ProviderSetup) {
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
