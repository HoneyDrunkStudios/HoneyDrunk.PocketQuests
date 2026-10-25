import { useEffect, useRef, useState } from "react";
import { TextInput, View } from "react-native";
import { developmentSignInEnabled } from "../config/client";
import { Button, Label, styles } from "../shared/ui";
import { requestDevelopmentSession } from "./identity-client";
import { useSession } from "./session";

export function DevelopmentSignIn() {
  const { connect, busy } = useSession();
  const [pairingCode, setPairingCode] = useState("");
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const attempt = useRef(0);
  useEffect(
    () => () => {
      attempt.current++;
    },
    [],
  );
  if (!developmentSignInEnabled) return null;
  const signIn = async () => {
    const current = ++attempt.current;
    setWorking(true);
    setError(null);
    try {
      const credentials = await requestDevelopmentSession(pairingCode);
      if (attempt.current !== current) return;
      setPairingCode("");
      await connect(credentials);
    } catch (value) {
      if (attempt.current === current)
        setError(
          value instanceof Error
            ? value.message
            : "Development sign-in failed. Try again.",
        );
    } finally {
      if (attempt.current === current) setWorking(false);
    }
  };
  return (
    <View style={{ gap: 10 }}>
      <Label>Development test account</Label>
      <Label>
        Quest progress is saved to the development database on your computer.
      </Label>
      <TextInput
        accessibilityLabel="Development pairing code"
        placeholder="6-digit pairing code"
        value={pairingCode}
        onChangeText={(value) =>
          setPairingCode(value.replace(/[^0-9]/g, "").slice(0, 6))
        }
        keyboardType="number-pad"
        maxLength={6}
        autoCapitalize="none"
        autoCorrect={false}
        secureTextEntry
        editable={!busy && !working}
        style={styles.input}
      />
      <Button
        title={working ? "Signing in…" : "Use development account"}
        disabled={busy || working || pairingCode.length !== 6}
        onPress={() => void signIn()}
      />
      {error && <Label>{error}</Label>}
    </View>
  );
}
