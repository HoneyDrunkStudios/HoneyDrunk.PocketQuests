import { useEffect, useRef, useState } from "react";
import { View } from "react-native";
import { useSession } from "../../session/session";
import { Button, Input, Label } from "../../shared/ui";
import { questActions } from "../quests/commands/quest-actions";

export function CharacterEditor({
  onContinue,
}: Readonly<{ onContinue(): void }>) {
  const { state, command, busy, pending, offline, queuedCount } = useSession();
  const [name, setName] = useState(state?.profile.characterName ?? "");
  const portrait = state?.profile.portraitId ?? "portrait-1";
  const [submitted, setSubmitted] = useState<{
    name: string;
    portrait: string;
  } | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const completed = useRef(false);
  const confirmed =
    !!submitted &&
    state?.profile.characterName === submitted.name &&
    state?.profile.portraitId === submitted.portrait &&
    !busy &&
    !pending &&
    queuedCount === 0;
  useEffect(() => {
    if (confirmed && !completed.current) {
      completed.current = true;
      onContinue();
    }
  }, [confirmed, onContinue]);
  async function save() {
    if (busy || pending || offline || saving || !state) return;
    const trimmed = name.trim();
    if (!trimmed || /[\u0000-\u001f\u007f]/.test(trimmed)) {
      setError("Choose a name with 1–40 readable characters.");
      return;
    }
    if (
      state.profile.characterName === trimmed &&
      state.profile.portraitId === portrait
    ) {
      onContinue();
      return;
    }
    completed.current = false;
    setError("");
    setSaving(true);
    setSubmitted({ name: trimmed, portrait });
    try {
      await command({
        action: questActions.saveCharacter,
        characterName: trimmed,
        portraitId: portrait,
      });
    } catch {
      setError("We couldn’t save your character. Your choices are still here.");
    } finally {
      setSaving(false);
    }
  }
  return (
    <View style={{ gap: 16 }}>
      <Label>
        Give your traveler a name. Next, choose their face, hair, build and
        wardrobe.
      </Label>
      <Label>Character name</Label>
      <Input
        accessibilityLabel="Character name"
        placeholder="What should we call you?"
        value={name}
        maxLength={40}
        autoCapitalize="words"
        autoCorrect={false}
        editable={!saving && !pending}
        onChangeText={(value) => {
          setName(value);
          setSubmitted(null);
        }}
      />
      {!!error && <Label>{error}</Label>}
      <Button
        title={saving ? "Saving your character…" : "Save name and continue"}
        loading={saving}
        disabled={!name.trim() || busy || pending || offline || saving}
        onPress={() => void save()}
      />
    </View>
  );
}
