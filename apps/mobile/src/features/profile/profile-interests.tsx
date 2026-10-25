import { useRef, useState } from "react";
import { View } from "react-native";
import {
  useAccountSnapshot,
  useSessionActions,
  useSessionStatus,
} from "../../session/session";
import { Button, Label } from "../../shared/ui";
import { questActions } from "../quests/commands/quest-actions";

export function ProfileInterests() {
  const { state, catalog } = useAccountSnapshot();
  const { busy, pending, offline, queuedCount } = useSessionStatus();
  const { command } = useSessionActions();
  const [draft, setDraft] = useState<string[] | null>(null);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const submitting = useRef(false);
  if (!state || !catalog) return null;
  const saved = state.profile.interests;
  const choices = draft ?? saved;
  const dirty =
    choices.length !== saved.length ||
    choices.some((id) => !saved.includes(id));
  // Confirmed interests are not an optimistic queue projection.
  if (draft && !dirty && queuedCount === 0 && !saving) setDraft(null);
  async function save() {
    if (!dirty || saving || busy || pending || offline || submitting.current)
      return;
    submitting.current = true;
    setSaving(true);
    setMessage("");
    try {
      await command({ action: questActions.interests, interests: choices });
    } catch {
      setMessage("We couldn’t save your interests. Please try again.");
    } finally {
      submitting.current = false;
      setSaving(false);
    }
  }
  return (
    <View style={{ gap: 10 }}>
      <Label>
        Interests put suggestions first. Every category stays available, and
        nothing repeats automatically. Choose what matters to you, then save.
      </Label>
      {catalog.categories.map((category) => (
        <Button
          key={category.id}
          title={`${choices.includes(category.id) ? "Selected: " : "Choose: "}${category.name}`}
          secondary={!choices.includes(category.id)}
          disabled={saving}
          onPress={() =>
            setDraft((current) => {
              const value = current ?? saved;
              return value.includes(category.id)
                ? value.filter((id) => id !== category.id)
                : [...value, category.id];
            })
          }
        />
      ))}
      {dirty && <Label>You have unsaved interest choices.</Label>}
      <Button
        title="Save interests"
        loading={saving}
        disabled={!dirty || busy || pending || offline}
        onPress={() => void save()}
      />
      {dirty && (
        <Button
          title="Reset interest choices"
          secondary
          disabled={saving || queuedCount > 0}
          onPress={() => {
            setDraft(null);
            setMessage("");
          }}
        />
      )}
      {!!message && <Label>{message}</Label>}
    </View>
  );
}
