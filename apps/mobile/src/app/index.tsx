import { JournalEntry } from "../shared/journal-entry";
import { DevelopmentSignIn } from "../session/development-sign-in";
import { SessionRecovery } from "../session/session-recovery";
import {
  providerScopes,
  providerCredentials,
} from "../session/provider-session";
import {
  useProviderSetup,
  type ProviderSetup,
} from "../session/use-provider-setup";
import { Loading } from "@honeydrunk/ui-native";
import { developmentSignInEnabled, signInRedirect } from "../config/client";
import { useEffect, useRef, useState } from "react";
import { Text, View } from "react-native";
import * as AuthSession from "expo-auth-session";
import * as WebBrowser from "expo-web-browser";
import { useSession } from "../session/session";
import { RecoveryPanel } from "../features/profile/account-settings";
import { Button, Label, styles } from "../shared/ui";
WebBrowser.maybeCompleteAuthSession();
function SignIn({ config, discovery }: Readonly<ProviderSetup>) {
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
function ProviderWelcome() {
  const { signedIn, busy } = useSession();
  const { setup, error, retry } = useProviderSetup();
  // The root protected stack selects onboarding or the app after sign-in.
  if (signedIn) return null;
  return (
    <JournalEntry>
      <View style={{ gap: 12 }}>
        <Text style={[styles.subtitle, { textAlign: "center", fontSize: 22 }]}>
          A little progress. Every day.
        </Text>
        <Label style={{ textAlign: "center" }}>
          Turn things you care about into quests. Watch your character grow with
          you.
        </Label>
        <SessionRecovery />
        <RecoveryPanel />
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
        <Text
          style={[
            styles.muted,
            { textAlign: "center", fontSize: 13, lineHeight: 19 },
          ]}
        >
          New here? The same button creates your account. Available sign-in
          methods are shown in your browser.
        </Text>
      </View>
    </JournalEntry>
  );
}

export default function Welcome() {
  const { signedIn, busy } = useSession();
  if (!developmentSignInEnabled) return <ProviderWelcome />;
  if (signedIn) return null;
  return (
    <JournalEntry>
      <SessionRecovery />
      <RecoveryPanel />
      {busy && <Loading label="Restoring session" />}
      <DevelopmentSignIn />
    </JournalEntry>
  );
}
