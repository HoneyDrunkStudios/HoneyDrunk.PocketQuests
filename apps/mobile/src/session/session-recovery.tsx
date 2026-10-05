import { Text, View } from "react-native";
import { Notice } from "@honeydrunk/ui-native";
import { Button, Label, styles } from "../shared/ui";
import { useSessionActions, useSessionStatus } from "./session";
export function SessionRecovery() {
  const {
    error,
    pending,
    busy,
    retry,
    offline,
    queuedCount,
    recoveryRequired,
    rejected = [],
    discardRejected,
    unverified = [],
    discardUnverified,
    discardPending,
    signOut,
  } = { ...useSessionStatus(), ...useSessionActions() };
  return (
    <>
      {(offline ||
        recoveryRequired ||
        queuedCount > 0 ||
        rejected.length > 0 ||
        unverified.length > 0) && (
        <View style={styles.card} accessibilityLiveRegion="polite">
          <Label>
            {recoveryRequired
              ? "Private saved work needs attention. An unreadable recovery copy or unfinished cleanup may remain on this device. Some actions cannot be counted."
              : offline
                ? "Offline - showing your private device cache."
                : rejected.length > 0
                  ? "Recorded changes need review."
                  : "Recorded changes awaiting confirmation."}{" "}
            {queuedCount} pending. Displayed XP remains server-confirmed.
          </Label>
          {unverified.length > 0 && (
            <Notice>
              {unverified.length} action(s) pending timing verification. They
              are saved on this device. Reconnecting alone cannot verify their
              earlier timestamps or confirm their rewards.
            </Notice>
          )}
          <Button
            title="Synchronize recorded changes"
            disabled={busy}
            onPress={() => void retry()}
          />
          {rejected.map((entry) => (
            <View key={entry.command.operationId} style={{ gap: 8 }}>
              <Label>
                {entry.kind === "blocked" ? "Blocked" : "Rejected"}:{" "}
                {entry.label}
              </Label>
              <Notice>{entry.reason}</Notice>
              <Text selectable style={styles.muted}>
                Action ID: {entry.command.operationId}
              </Text>
              {entry.blockedBy?.length ? (
                <Text selectable style={styles.muted}>
                  Related action IDs: {entry.blockedBy.join(", ")}
                </Text>
              ) : null}
              <Button
                title={`Discard this action: ${entry.label}`}
                secondary
                disabled={busy}
                onPress={() => void discardRejected(entry.command.operationId)}
              />
            </View>
          ))}
          {unverified.map((entry) => (
            <View key={entry.command.operationId} style={{ gap: 8 }}>
              <Label>Pending timing verification: {entry.label}</Label>
              <Notice>{entry.reason}</Notice>
              <Text selectable style={styles.muted}>
                Action ID: {entry.command.operationId}
              </Text>
              {entry.blockedBy.length > 0 && (
                <Text selectable style={styles.muted}>
                  Related action IDs: {entry.blockedBy.join(", ")}. Discarding a
                  prerequisite does not release these dependent actions.
                </Text>
              )}
              <Button
                title={`Discard this unverified action: ${entry.label}`}
                secondary
                disabled={busy}
                onPress={() =>
                  void discardUnverified(entry.command.operationId)
                }
              />
            </View>
          ))}
          {(queuedCount > 0 ||
            recoveryRequired ||
            rejected.length > 0 ||
            unverified.length > 0) && (
            <>
              <Label>
                Discard pending changes removes all pending, unverified,
                rejected and blocked actions, including unreadable recovery
                copies, on this device.
              </Label>
              <Button
                title="Discard pending changes"
                secondary
                disabled={busy}
                onPress={() => void discardPending()}
              />
              <Button
                title="Discard pending changes and sign out"
                secondary
                onPress={() => void signOut(true)}
              />
            </>
          )}
        </View>
      )}
      {error && (
        <View accessibilityLiveRegion="polite" style={styles.card}>
          <Notice>{error}</Notice>
          {pending && (
            <Button
              title="Retry pending action"
              onPress={() => void retry()}
              disabled={busy}
            />
          )}
        </View>
      )}
    </>
  );
}
