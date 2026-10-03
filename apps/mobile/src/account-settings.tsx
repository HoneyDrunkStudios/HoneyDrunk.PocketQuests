import { useState } from "react";
import { Text, View } from "react-native";
import { useSession } from "./session";
import { ProviderSignIn } from "./provider-sign-in";
import { Button, Label, styles } from "./ui";
export function AccountSettings() {
  const { accountAction, verifyOwner, busy, pending, offline } = useSession();
  const [mode, setMode] = useState<"deletion" | "link" | "unlink" | null>(null);
  const [ownerProof, setOwnerProof] = useState<string | null>(null);
  const [message, setMessage] = useState("");
  const disabled = busy || pending || offline;
  function choose(value: typeof mode) {
    setMode(value);
    setOwnerProof(null);
    setMessage("");
  }
  return (
    <View style={styles.card}>
      <Text style={styles.subtitle}>Account and sign-in</Text>
      <Label>
        Use any explicitly linked sign-in method to recover access. Matching
        email addresses never merge accounts. If a provider account is
        unavailable, recover it with that provider or use another linked method.
      </Label>
      {!mode ? (
        <>
          <Button
            title="Link another sign-in method"
            secondary
            disabled={disabled}
            onPress={() => choose("link")}
          />
          <Button
            title="Remove a linked sign-in method"
            secondary
            disabled={disabled}
            onPress={() => choose("unlink")}
          />
          <Button
            title="Review account deletion"
            secondary
            disabled={disabled}
            onPress={() => choose("deletion")}
          />
        </>
      ) : mode === "deletion" ? (
        <>
          <Label>
            Requesting deletion immediately deactivates your account, stops this
            device’s expiry warnings, and clears its private cache. You can
            explicitly cancel after signing in again before the original 30-day
            deadline. After that, personal data and user-scoped audit events are
            erased. Only a minimal deletion marker remains for 35 days after
            verified erasure. An unreachable offline phone cannot be remotely
            cleared.
          </Label>
          <ProviderSignIn
            title="Reauthenticate and deactivate my account"
            recent
            disabled={disabled}
            onToken={(token) => accountAction("deletion", token)}
          />
        </>
      ) : (
        <>
          <Label>
            {mode === "link"
              ? "First verify this account, then sign in with the additional method. A method belonging to another existing account cannot be merged."
              : "First verify the sign-in you will keep, then verify the separate method to remove. Your last usable sign-in cannot be removed."}
          </Label>
          {!ownerProof ? (
            <ProviderSignIn
              key="owner"
              title="Verify this account"
              recent
              disabled={disabled}
              onToken={async (token) => {
                await verifyOwner(token);
                setOwnerProof(token);
              }}
            />
          ) : (
            <ProviderSignIn
              key="additional"
              title={
                mode === "link"
                  ? "Verify and link the additional sign-in"
                  : "Verify and remove the other sign-in"
              }
              recent
              disabled={disabled}
              onToken={async (token) => {
                await accountAction(mode, ownerProof, token);
                setOwnerProof(null);
                setMode(null);
                setMessage(
                  mode === "link"
                    ? "The additional sign-in was verified and linked to this account."
                    : "The other sign-in was removed. The current method remains available.",
                );
              }}
            />
          )}
        </>
      )}
      {mode && (
        <Button
          title="Cancel account change"
          secondary
          disabled={busy}
          onPress={() => choose(null)}
        />
      )}
      {!!message && <Label>{message}</Label>}
    </View>
  );
}
export function RecoveryPanel() {
  const { inactiveAccount, accountAction, busy } = useSession();
  if (!inactiveAccount) return null;
  const available =
    inactiveAccount.state === "Inactive" &&
    inactiveAccount.recoveryDeadline !== null;
  return (
    <View style={styles.card}>
      <Text style={styles.subtitle}>Account deletion requested</Text>
      <Label>
        Your account is inactive. This device’s private cache and scheduled
        warnings have been cleared. Signing in alone does not cancel deletion.
      </Label>
      {available ? (
        <>
          <Label>
            Recovery can be requested strictly before{" "}
            {inactiveAccount.recoveryDeadline}. Cancellation restores access but
            leaves commitments paused until you choose Resume.
          </Label>
          <ProviderSignIn
            title="Reauthenticate and explicitly cancel deletion"
            recent
            disabled={busy}
            onToken={(token) => accountAction("recovery", token)}
          />
        </>
      ) : (
        <Label>
          The recovery window has ended. Access stays blocked while erasure is
          verified; service failures are retried.
        </Label>
      )}
    </View>
  );
}
