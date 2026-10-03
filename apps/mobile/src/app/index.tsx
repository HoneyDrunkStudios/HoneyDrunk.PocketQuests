import { Loading } from "@honeydrunk/ui-native";
import { requestTimeoutMs, signInRedirect } from "../config/client";
import { useEffect, useRef, useState } from "react";
import { Text, View } from "react-native";
import { Redirect } from "expo-router";
import * as AuthSession from "expo-auth-session";
import * as WebBrowser from "expo-web-browser";
import { identityUrl, useSession } from "../session/session";
import { RecoveryPanel } from "../features/profile/account-settings";
import { Page, Button, Label, Card, colors, styles } from "../shared/ui";
WebBrowser.maybeCompleteAuthSession();
type Configuration = { authority: string; clientId: string; scope: string };
async function fetchConfiguration(): Promise<Configuration> {
  const response = await fetch(identityUrl + "/client-configuration", {
    signal: AbortSignal.timeout(requestTimeoutMs.signInConfiguration),
  });
  if (!response.ok) throw new Error("Sign-in configuration unavailable");
  return response.json();
}
function SignIn({ config }: { config: Configuration }) {
  const { connect, busy } = useSession();
  const [error, setError] = useState<string | null>(null);
  const exchangedCode = useRef<string | null>(null);
  const discovery = AuthSession.useAutoDiscovery(config.authority);
  const redirectUri = AuthSession.makeRedirectUri(signInRedirect);
  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: config.clientId,
      scopes: ["openid", "profile", config.scope],
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
      .then((tokens) => connect(tokens.accessToken))
      .catch(() => setError("Sign-in did not finish. Please try again."));
  }, [
    response,
    discovery,
    request?.codeVerifier,
    config.clientId,
    redirectUri,
    connect,
  ]);
  return (
    <>
      <Button
        title="Sign in or create an account"
        disabled={!request || busy}
        onPress={() => {
          setError(null);
          void promptAsync();
        }}
      />
      {error && <Label>{error}</Label>}
    </>
  );
}
export default function Welcome() {
  const { signedIn, busy } = useSession();
  const [config, setConfig] = useState<Configuration | null>(null);
  const [message, setMessage] = useState("Connecting to sign-in…");
  async function load() {
    try {
      setConfig(await fetchConfiguration());
    } catch {
      setMessage(
        "Sign-in setup is still in progress. Please try again shortly.",
      );
    }
  }
  useEffect(() => {
    let active = true;
    fetchConfiguration().then(
      (value) => {
        if (active) setConfig(value);
      },
      () => {
        if (active)
          setMessage(
            "Sign-in setup is still in progress. Please try again shortly.",
          );
      },
    );
    return () => {
      active = false;
    };
  }, []);
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
        {config ? (
          <SignIn config={config} />
        ) : (
          <>
            <Text selectable style={styles.muted}>
              {message}
            </Text>
            <Button
              title="Check sign-in setup"
              onPress={() => void load()}
              secondary
            />
          </>
        )}
      </Card>
    </Page>
  );
}
