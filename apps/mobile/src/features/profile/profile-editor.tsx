import { questLimits } from "../quests/quest-rules";
import { questActions } from "../quests/commands/quest-actions";
import { useRef, useState } from "react";
import * as Crypto from "expo-crypto";
import { Text, View } from "react-native";
import { useSession } from "../../session/session";
import { Button, Input, Label, styles } from "../../shared/ui";
import type { Experience } from "../../shared/contracts";
import { ProfileInterests } from "./profile-interests";
export function ProfileEditor() {
  const { state, catalog, command, busy, pending, offline, queuedCount } =
    useSession();
  const [skillName, setSkillName] = useState("");
  const [editingSkill, setEditingSkill] = useState<string | null>(null);
  const [placement, setPlacement] = useState<{
    id: string;
    name: string;
    experience: Experience;
  } | null>(null);
  const [savingPlacement, setSavingPlacement] = useState(false);
  const [placementError, setPlacementError] = useState("");
  const submittingPlacement = useRef(false);
  if (!state || !catalog) return null;
  const skills = [
    ...catalog.skills,
    ...(state.profile.customSkills ?? []).filter((s) => !s.archived),
  ];
  const existing = state.profile.customSkills?.find(
    (s) => s.id === editingSkill,
  );
  const disabled = busy || pending || offline || savingPlacement;
  if (
    placement &&
    queuedCount === 0 &&
    !savingPlacement &&
    (state.profile.assessments[placement.id] ?? "New") === placement.experience
  )
    setPlacement(null);
  return (
    <View style={{ gap: 16 }}>
      <Text style={styles.subtitle}>Your interests and experience</Text>
      <ProfileInterests />
      <Label>
        Assess any relevant skill now or later. New: level 1, practiced: 10,
        experienced: 35, expert: 50. A correction replaces your starting XP and
        preserves earned XP. Overall and category XP stay unchanged.
      </Label>
      <Text style={styles.subtitle}>Your custom skills</Text>
      <Label>
        Use a skill across any category. Renaming keeps its XP and identity.
        Archived skills remain in your history.
      </Label>
      <Input
        accessibilityLabel="Custom skill name"
        style={styles.input}
        maxLength={questLimits.skillNameLength}
        value={skillName}
        onChangeText={setSkillName}
      />
      <Button
        title={existing ? "Save skill name" : "Create skill"}
        disabled={disabled || !skillName.trim()}
        onPress={() =>
          void command({
            action: questActions.saveSkill,
            skillId: existing?.id ?? Crypto.randomUUID(),
            skillName,
            expectedRevision: existing?.revision ?? 0,
          })
        }
      />
      {editingSkill && (
        <Button
          title="Create a different skill"
          secondary
          onPress={() => {
            setEditingSkill(null);
            setSkillName("");
          }}
        />
      )}
      {(state.profile.customSkills ?? [])
        .filter((s) => !s.archived)
        .map((skill) => (
          <View key={skill.id} style={styles.card}>
            <Label>{skill.name}</Label>
            <Button
              title={`Rename ${skill.name}`}
              secondary
              disabled={disabled}
              onPress={() => {
                setEditingSkill(skill.id);
                setSkillName(skill.name);
              }}
            />
            <Button
              title={`Archive ${skill.name}`}
              secondary
              disabled={disabled}
              onPress={() =>
                void command({
                  action: questActions.archiveSkill,
                  skillId: skill.id,
                  expectedRevision: skill.revision,
                })
              }
            />
          </View>
        ))}
      {placement && (
        <View style={styles.card}>
          <Label>
            {placement.name}: change to {placement.experience}. Starting skill
            XP becomes{" "}
            {
              { New: 0, Practiced: 405, Experienced: 5780, Expert: 12005 }[
                placement.experience
              ]
            }
            ; earned XP stays. Future rank access is recalculated. Accepted
            rewards stay unchanged.
          </Label>
          <Button
            title="Confirm experience correction"
            disabled={disabled}
            loading={savingPlacement}
            onPress={() => {
              if (submittingPlacement.current) return;
              submittingPlacement.current = true;
              setSavingPlacement(true);
              setPlacementError("");
              void command({
                action: questActions.assessSkill,
                skillId: placement.id,
                experience: placement.experience,
              })
                .catch(() =>
                  setPlacementError(
                    "We couldn’t save this experience change. Please try again.",
                  ),
                )
                .finally(() => {
                  submittingPlacement.current = false;
                  setSavingPlacement(false);
                });
            }}
          />
          <Button
            title="Keep current experience"
            secondary
            disabled={savingPlacement || queuedCount > 0}
            onPress={() => setPlacement(null)}
          />
          {!!placementError && <Label>{placementError}</Label>}
        </View>
      )}
      {skills.map((skill) => (
        <View key={skill.id} style={styles.card}>
          <Text style={styles.subtitle}>{skill.name}</Text>
          <Label>
            Current assessment: {state.profile.assessments[skill.id] ?? "New"}
          </Label>
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
            {(
              ["New", "Practiced", "Experienced", "Expert"] as Experience[]
            ).map((experience) => (
              <Button
                key={experience}
                title={`${skill.name}: ${experience}`}
                secondary={
                  (state.profile.assessments[skill.id] ?? "New") !== experience
                }
                disabled={savingPlacement}
                onPress={() => {
                  setPlacementError("");
                  setPlacement({
                    id: skill.id,
                    name: skill.name,
                    experience,
                  });
                }}
              />
            ))}
          </View>
        </View>
      ))}
    </View>
  );
}
