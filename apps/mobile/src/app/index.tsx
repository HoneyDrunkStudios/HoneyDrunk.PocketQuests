import { Page } from "../session/session-page";
import {
  providerScopes,
  providerCredentials,
} from "../session/provider-session";
import {
  useProviderSetup,
  type ProviderSetup,
} from "../session/use-provider-setup";
import { Loading } from "@honeydrunk/ui-native";
import { signInRedirect } from "../config/client";
import { useEffect, useRef, useState } from "react";
import { Text, View } from "react-native";
import { Redirect } from "expo-router";
import * as AuthSession from "expo-auth-session";
import * as WebBrowser from "expo-web-browser";
import { useSession } from "../session/session";
import { RecoveryPanel } from "../features/profile/account-settings";
import { Button, Label, Card, colors, styles } from "../shared/ui";
WebBrowser.maybeCompleteAuthSession();
function SignIn({ config, discovery }: ProviderSetup) {
  const { connect, busy } = useSession();
  const [error, setError] = useState<string | null>(null);
  const [working, setWorking] = useState(false);
  const exchangedCode = useRef<string | null>(null);
  const redirectUri = AuthSession.makeRedirectUri(signInRedirect);
  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: config.clientId,
      scopes: providerScopes(config, discovery),
      responseType: AuthSession.ResponseType.Code,
      usePKCE: true,
      redirectUri,
    },
    discovery,
  );
  useEffect(() => {
    if (response?.type !== "success" || !discovery || !request?.codeVerifier)
      return;
    if (exchangedCode.current === response.params.code) return;
    exchangedCode.current = response.params.code;
    AuthSession.exchangeCodeAsync(
      {
        clientId: config.clientId,
        code: response.params.code,
        redirectUri,
        extraParams: { code_verifier: request.codeVerifier },
      },
      discovery,
    )
      .then((tokens) => connect(providerCredentials(tokens, config)))
      .catch(() => setError("Sign-in did not finish. Please try again."))
      .finally(() => setWorking(false));
  }, [
    response,
    discovery,
    request?.codeVerifier,
    config,
    redirectUri,
    connect,
  ]);
  return (
    <>
      <Button
        title="Sign in or create an account"
        disabled={!request || busy || working}
        onPress={() => {
          setError(null);
          setWorking(true);
          void promptAsync()
            .then((result) => {
              if (result.type !== "success") setWorking(false);
              if (result.type === "error")
                setError("Sign-in did not finish. Please try again.");
            })
            .catch(() => {
              setWorking(false);
              setError("Sign-in was interrupted. Please try again.");
            });
        }}
      />
      {error && <Label>{error}</Label>}
    </>
  );
}
export default function Welcome() {
  const { signedIn, busy } = useSession();
  const { setup, error, retry } = useProviderSetup();
  if (signedIn) return <Redirect href="/(tabs)" />;
  return (
    <Page>
      <View
        style={{
          gap: 14,
          borderLeftWidth: 4,
          borderLeftColor: colors.gold,
          paddingLeft: 18,
          paddingVertical: 12,
        }}
      >
        <Text
          style={{
            ...styles.muted,
            color: colors.gold,
            letterSpacing: 3,
            fontWeight: "700",
          }}
        >
          YOUR EVERYDAY ADVENTURE
        </Text>
        <Text selectable style={styles.title}>
          Small actions. A growing story.
        </Text>
        <Label>
          Choose what matters to you. Make room for work, discovery, rest, and
          everything in between.
        </Label>
      </View>
      <RecoveryPanel />
      <Card style={{ borderTopWidth: 3, borderTopColor: colors.green }}>
        <Text
          style={{
            ...styles.muted,
            color: colors.green,
            letterSpacing: 2,
            fontWeight: "700",
          }}
        >
          PLAYER JOURNAL / PRIVATE
        </Text>
        <Text selectable style={styles.subtitle}>
          Your progress stays yours
        </Text>
        <Label>
          Private quests, meaningful progress, and room to begin again.
        </Label>
        {busy && <Loading label="Restoring session" />}
        {setup ? (
          <SignIn {...setup} />
        ) : (
          <>
            <Text selectable style={styles.muted}>
              {error || "Preparing secure sign-in…"}
            </Text>
            {error && (
              <Button title="Retry sign-in setup" onPress={retry} secondary />
            )}
          </>
        )}
      </Card>
    </Page>
  );
}
