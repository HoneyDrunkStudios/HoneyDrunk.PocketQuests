import { Text, View } from "react-native";
import { useState } from "react";
import { Notice } from "@honeydrunk/ui-native";
import { Button, Label, styles } from "../shared/ui";
import { useSessionActions, useSessionStatus } from "./session";

function RecoveryDetails({
  reference,
  label,
  reason,
}: Readonly<{
  reference: string;
  label: string;
  reason?: string;
}>) {
  const [expanded, setExpanded] = useState(false);
  return (
    <>
      <Button
        title={expanded ? "Hide details" : "Show details"}
        accessibilityLabel={`${expanded ? "Hide" : "Show"} details: ${label}`}
        secondary
        onPress={() => setExpanded((value) => !value)}
      />
      {expanded && (
        <>
          {reason && <Label>{reason}</Label>}
          <Text selectable style={styles.muted}>
            Support reference: {reference}
          </Text>
        </>
      )}
    </>
  );
}

function recoveryMessage(
  recoveryRequired: boolean,
  offline: boolean,
  rejected: number,
  unverified: number,
) {
  if (recoveryRequired)
    return "Some changes on this device need attention. They may not have been saved. Try again, or discard them to continue.";
  if (offline) return "You’re offline. You can still see your saved progress.";
  if (rejected > 0) return "Some changes couldn’t be saved. Review them below.";
  if (unverified > 0)
    return "Some activities need your attention before they can count toward your progress.";
  return "Some changes couldn’t be saved. Try again, or discard them to continue.";
}

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
  const activityExplainsError = unverified.some(
    (entry) => entry.reason === error,
  );
  const failedQueuedSave = queuedCount > 0 && !!error;
  return (
    <>
      {(offline ||
        recoveryRequired ||
        failedQueuedSave ||
        rejected.length > 0 ||
        unverified.length > 0) && (
        <View style={styles.card} accessibilityLiveRegion="polite">
          <Label>
            {recoveryMessage(
              recoveryRequired,
              offline,
              rejected.length,
              unverified.length,
            )}
          </Label>
          {queuedCount > 0 && (
            <Label>
              {queuedCount === 1
                ? "1 change is waiting to save."
                : `${queuedCount} changes are waiting to save.`}
            </Label>
          )}
          {unverified.length > 0 && (
            <Notice>
              We couldn’t check when{" "}
              {unverified.length === 1
                ? "this activity happened"
                : "these activities happened"}
              .{" "}
              {unverified.length === 1
                ? "It’s kept on this device but hasn’t changed your XP."
                : "They’re kept on this device but haven’t changed your XP."}{" "}
              Reconnecting won’t resolve this. You can discard the affected
              activities and record them again.
            </Notice>
          )}
          <Button
            title="Try saving again"
            disabled={busy}
            onPress={() => void retry()}
          />
          {rejected.map((entry) => (
            <View key={entry.command.operationId} style={{ gap: 8 }}>
              <Label>
                {entry.kind === "blocked"
                  ? "Waiting on another change"
                  : "Couldn’t save"}
                : {entry.label}
              </Label>
              <Notice>
                {entry.kind === "blocked"
                  ? "This change depends on another change that couldn’t be saved. Discard it if you want to record a replacement."
                  : entry.reason}
              </Notice>
              {entry.blockedBy?.length ? (
                <Label>
                  Discarding the earlier change won’t save this one.
                </Label>
              ) : null}
              <RecoveryDetails
                reference={entry.command.operationId}
                label={entry.label}
              />
              <Button
                title={`Discard change: ${entry.label}`}
                secondary
                disabled={busy}
                onPress={() => void discardRejected(entry.command.operationId)}
              />
            </View>
          ))}
          {unverified.map((entry) => (
            <View key={entry.command.operationId} style={{ gap: 8 }}>
              <Label>Needs review: {entry.label}</Label>
              {entry.blockedBy.length > 0 && (
                <Label>
                  This activity depends on another activity that needs review.
                  Discarding the earlier activity won’t save this one.
                </Label>
              )}
              <RecoveryDetails
                reference={entry.command.operationId}
                label={entry.label}
                reason={entry.reason}
              />
              <Button
                title={`Discard activity: ${entry.label}`}
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
                Discarding removes all changes waiting to save or needing review
                from this device, including any that can’t be recovered. Those
                changes can’t be restored here. Progress already saved to your
                account won’t be undone.
              </Label>
              <Button
                title="Discard these changes"
                secondary
                disabled={busy}
                onPress={() => void discardPending()}
              />
              <Button
                title="Discard changes and sign out"
                secondary
                disabled={busy && !recoveryRequired}
                onPress={() => void signOut(true)}
              />
            </>
          )}
        </View>
      )}
      {error && !activityExplainsError && (
        <View accessibilityLiveRegion="polite" style={styles.card}>
          <Notice>{error}</Notice>
          {pending && (
            <Button
              title="Try again"
              onPress={() => void retry()}
              disabled={busy}
            />
          )}
        </View>
      )}
    </>
  );
}
