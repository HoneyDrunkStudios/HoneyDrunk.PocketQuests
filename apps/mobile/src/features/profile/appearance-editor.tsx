import { useRef, useState } from "react";
import { Text, View } from "react-native";
import {
  useAccountSnapshot,
  useSessionActions,
  useSessionStatus,
} from "../../session/session";
import { Button, Label, styles } from "../../shared/ui";
import { questActions } from "../quests/commands/quest-actions";

function RewardChoice({ kind }: { kind: "Badge" | "Frame" }) {
  const { state } = useAccountSnapshot();
  const { busy, pending, offline, queuedCount } = useSessionStatus();
  const { command } = useSessionActions();
  const [draft, setDraft] = useState<string | null | undefined>();
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const submitting = useRef(false);
  if (!state) return null;
  const saved =
    kind === "Badge" ? state.profile.badgeId : state.profile.frameId;
  const choice = draft === undefined ? saved : draft;
  const dirty = choice !== saved;
  if (draft !== undefined && !dirty && queuedCount === 0 && !saving)
    setDraft(undefined);
  const noun = kind.toLowerCase();
  const fallback = kind === "Badge" ? "No badge" : "Default frame";
  const name =
    state.entitlements.find((reward) => reward.id === choice)?.name ?? fallback;
  async function save() {
    if (!dirty || saving || busy || pending || offline || submitting.current)
      return;
    submitting.current = true;
    setSaving(true);
    setMessage("");
    try {
      await command({
        action:
          kind === "Badge"
            ? questActions.selectBadge
            : questActions.selectFrame,
        rewardId: choice,
      });
    } catch {
      setMessage(`We couldn’t save your ${noun}. Please try again.`);
    } finally {
      submitting.current = false;
      setSaving(false);
    }
  }
  return (
    <View style={{ gap: 10 }}>
      <Text style={styles.subtitle}>{kind}</Text>
      <Label>
        {dirty ? "Preview" : "Selected"}: {name}
      </Label>
      <Button
        title={fallback}
        accessibilityLabel={`${choice === null ? "Selected: " : ""}${fallback}`}
        secondary={choice !== null}
        disabled={saving}
        onPress={() => setDraft(null)}
      />
      {state.entitlements
        .filter((reward) => reward.earned && reward.kind === kind)
        .map((reward) => (
          <Button
            key={reward.id}
            title={`${choice === reward.id ? "Selected" : "Choose"} ${noun}: ${reward.name}`}
            secondary={choice !== reward.id}
            disabled={saving}
            onPress={() => setDraft(reward.id)}
          />
        ))}
      {dirty && <Label>You have an unsaved {noun} choice.</Label>}
      <Button
        title={`Save ${noun}`}
        loading={saving}
        disabled={!dirty || busy || pending || offline}
        onPress={() => void save()}
      />
      {dirty && (
        <Button
          title={`Reset ${noun} choice`}
          secondary
          disabled={saving || queuedCount > 0}
          onPress={() => {
            setDraft(undefined);
            setMessage("");
          }}
        />
      )}
      {!!message && <Label>{message}</Label>}
    </View>
  );
}

export function AppearanceEditor() {
  return (
    <View style={styles.card}>
      <Text style={styles.subtitle}>Your look</Text>
      <Label>
        Try your earned badges and frames, then save each choice when you’re
        happy with it.
      </Label>
      <RewardChoice kind="Badge" />
      <RewardChoice kind="Frame" />
    </View>
  );
}
