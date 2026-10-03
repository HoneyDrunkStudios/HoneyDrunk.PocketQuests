import { questActions } from "../quests/commands/quest-actions";
import { useState } from "react";
import { Text, View } from "react-native";
import { useSession } from "../../session/session";
import { Button, Input, Label, styles } from "../../shared/ui";
import { requestWarningPermission } from "../quests/notifications";
import type { ZonePreview } from "../../shared/contracts";
export function SettingsEditor() {
  const {
    state,
    command,
    previewZone,
    notificationStatus,
    busy,
    pending,
    offline,
  } = useSession();
  const [zone, setZone] = useState(state?.zone ?? "UTC");
  const [preview, setPreview] = useState<ZonePreview | null>(null);
  const [message, setMessage] = useState("");
  if (!state) return null;
  const deviceZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  return (
    <View style={styles.card}>
      <Text style={styles.subtitle}>Time and expiry warnings</Text>
      <Label>
        Selected timezone: {state.zone}. Planned times organize your calendar;
        deadlines remain the end of the due day.
      </Label>
      {deviceZone && deviceZone !== state.zone && (
        <Label>
          Your device uses {deviceZone}. Your selected zone stays {state.zone}{" "}
          until you explicitly change it.
        </Label>
      )}
      <Input
        accessibilityLabel="Selected IANA timezone"
        value={zone}
        onChangeText={(value) => {
          setZone(value);
          setPreview(null);
        }}
        style={styles.input}
      />
      <Button
        title="Preview timezone change"
        disabled={busy || pending || offline}
        onPress={() => {
          setMessage("");
          void previewZone(zone)
            .then(setPreview)
            .catch((error) => setMessage(error.message));
        }}
      />
      {preview && (
        <>
          <Label>
            {preview.deadlines.length} unfinished deadlines will use{" "}
            {preview.zone}.{" "}
            {preview.deadlines.filter((d) => d.becomesMissed).length} become
            missed immediately, with any previously accepted category loss.
            Frozen commitments stay frozen. Finalized history remains unchanged;
            crossing the date line adds no streak or freeze day.
          </Label>
          {preview.deadlines.map((d) => (
            <Label key={d.occurrenceId}>
              {d.title}: {d.deadline}
              {d.becomesMissed ? " — becomes missed" : ""}
            </Label>
          ))}
          <Button
            title="Confirm timezone and deadline changes"
            disabled={busy || pending || offline}
            onPress={() => {
              void command({
                action: questActions.zone,
                newZone: preview.zone,
                expectedZone: preview.previousZone,
                confirmZoneChange: true,
              });
              setPreview(null);
            }}
          />
        </>
      )}
      <Label>
        Expiry warnings: {state.profile.expiryWarnings ? "on" : "off"}.
        Optional, generic wording only. One grouped warning 60 minutes before
        due-day midnight, including daily quests. No separate routine reminders.
      </Label>
      <Button
        title={
          state.profile.expiryWarnings
            ? "Turn expiry warnings off"
            : "Enable phone expiry warnings"
        }
        disabled={busy || pending || offline}
        onPress={() => {
          void (async () => {
            try {
              if (
                !state.profile.expiryWarnings &&
                !(await requestWarningPermission())
              )
                setMessage(
                  "Permission was not granted. Quests remain usable; enable phone notification permission when ready.",
                );
              await command({
                action: questActions.expiryWarnings,
                expiryWarnings: !state.profile.expiryWarnings,
              });
            } catch {
              setMessage(
                "Could not update phone notification settings. Quests remain usable.",
              );
            }
          })();
        }}
      />
      <Label>{notificationStatus}</Label>
      <Label>
        A phone that is offline cannot receive remote cancellation. A previously
        scheduled generic warning may still appear until it reconnects. Open the
        app to refresh future warnings.
      </Label>
      {!!message && <Label>{message}</Label>}
    </View>
  );
}
