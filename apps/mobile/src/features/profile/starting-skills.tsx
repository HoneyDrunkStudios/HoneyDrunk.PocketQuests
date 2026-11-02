import { useEffect, useRef, useState } from "react";
import { Pressable, Text, View } from "react-native";
import { useSession } from "../../session/session";
import type { Experience } from "../../shared/contracts";
import { Button, Label, colors, styles } from "../../shared/ui";
import { questActions } from "../quests/commands/quest-actions";

const experiences = ["New", "Practiced", "Experienced", "Expert"] as const;
const levels = { New: 1, Practiced: 10, Experienced: 35, Expert: 50 };

export function StartingSkills({
  onContinue,
}: Readonly<{ onContinue(): void }>) {
  const {
    state,
    catalog,
    command,
    busy,
    pending,
    offline,
    error,
    queuedCount,
  } = useSession();
  const [draft, setDraft] = useState<Record<string, Experience>>({});
  const [targets, setTargets] = useState<Record<string, Experience> | null>(
    null,
  );
  const [saveError, setSaveError] = useState("");
  const sent = useRef<string | null>(null);
  const completed = useRef(false);
  const next = Object.entries(targets ?? {}).find(
    ([id, experience]) =>
      (state?.profile.assessments[id] ?? "New") !== experience,
  );
  useEffect(() => {
    if (
      !targets ||
      !state ||
      busy ||
      pending ||
      offline ||
      error ||
      saveError ||
      queuedCount > 0
    )
      return;
    if (!next) {
      if (!completed.current) {
        completed.current = true;
        onContinue();
      }
      return;
    }
    const [skillId, experience] = next;
    const key = `${skillId}:${experience}`;
    if (sent.current === key) return;
    sent.current = key;
    void command({
      action: questActions.assessSkill,
      skillId,
      experience,
    }).catch(() =>
      setSaveError(
        "We couldn’t save that skill. Review your choices or use the recovery controls to retry.",
      ),
    );
  }, [
    targets,
    state,
    busy,
    pending,
    offline,
    error,
    saveError,
    queuedCount,
    next,
    command,
    onContinue,
  ]);
  if (!state || !catalog) return <Label>Loading your skills…</Label>;
  const saving = targets !== null;
  return (
    <View style={{ gap: 16 }}>
      <Label>
        Bring your experience with you. Choose a starting point for skills you
        already practice; the rest can stay New.
      </Label>
      <Label style={styles.muted}>
        New · level 1. Practiced · 10. Experienced · 35. Expert · 50. These
        choices set skill XP; they do not add quest-earned overall XP.
      </Label>
      {catalog.skills.map((skill) => {
        const selected =
          draft[skill.id] ?? state.profile.assessments[skill.id] ?? "New";
        return (
          <View key={skill.id} style={[styles.card, { gap: 10 }]}>
            <Text style={[styles.text, { fontWeight: "600" }]}>
              {skill.name}
            </Text>
            <View
              accessibilityRole="radiogroup"
              accessibilityLabel={`${skill.name} starting experience`}
              style={{ flexDirection: "row", flexWrap: "wrap", gap: 6 }}
            >
              {experiences.map((experience) => (
                <Pressable
                  key={experience}
                  accessibilityRole="radio"
                  accessibilityLabel={`${skill.name}: ${experience}`}
                  accessibilityState={{
                    checked: selected === experience,
                    disabled: saving,
                  }}
                  disabled={saving}
                  onPress={() =>
                    setDraft((current) => ({
                      ...current,
                      [skill.id]: experience,
                    }))
                  }
                  style={({ pressed }) => ({
                    minHeight: 48,
                    paddingHorizontal: 12,
                    paddingVertical: 10,
                    borderWidth: 1,
                    borderRadius: 10,
                    borderColor:
                      selected === experience ? colors.gold : colors.line,
                    backgroundColor:
                      selected === experience ? colors.gold : colors.paper,
                    opacity: pressed ? 0.8 : 1,
                  })}
                >
                  <Text
                    style={[
                      styles.text,
                      {
                        fontSize: 13,
                        color:
                          selected === experience ? colors.onGold : colors.ink,
                      },
                    ]}
                  >
                    {experience} · {levels[experience]}
                  </Text>
                </Pressable>
              ))}
            </View>
          </View>
        );
      })}
      {!!saveError && <Label>{saveError}</Label>}
      {saving &&
        !busy &&
        !pending &&
        queuedCount === 0 &&
        !!(error || saveError) && (
          <Button
            title="Review skill choices"
            secondary
            onPress={() => {
              setTargets(null);
              setSaveError("");
            }}
          />
        )}
      <Button
        title={
          saving ? "Saving starting skills…" : "Continue with these skills"
        }
        loading={saving && !error && !saveError}
        disabled={busy || pending || offline || saving}
        onPress={() => {
          sent.current = null;
          completed.current = false;
          setTargets({ ...draft });
        }}
      />
      {!saving && (
        <Label style={styles.muted}>
          Optional. Continue with everything at New, and change assessments
          later in Settings.
        </Label>
      )}
    </View>
  );
}
