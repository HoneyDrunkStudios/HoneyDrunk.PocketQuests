import { questActions } from "./commands/quest-actions";
import { useState } from "react";
import * as Crypto from "expo-crypto";
import { Text, View } from "react-native";
import { useSession } from "./session";
import { Button, Input, Label, styles } from "./ui";
import type { Definition, Named, Quest, Share } from "./contracts";
import {
  baseXp,
  eligible,
  ranks,
  fullPoolBasisPoints,
  basisPointsPerPercent,
  questLimits,
} from "./quest-rules";
function Allocations({
  title,
  items,
  shares,
  onChange,
}: {
  title: string;
  items: Named[];
  shares: Share[];
  onChange(value: Share[]): void;
}) {
  const [inputs, setInputs] = useState<Record<string, string>>({});
  function toggle(id: string) {
    const ids = shares.some((s) => s.id === id)
      ? shares.filter((s) => s.id !== id).map((s) => s.id)
      : [...shares.map((s) => s.id), id];
    setInputs({});
    onChange(
      ids.map((id, i) => ({
        id,
        basisPoints:
          i === ids.length - 1
            ? fullPoolBasisPoints -
              Math.floor(fullPoolBasisPoints / ids.length) * i
            : Math.floor(fullPoolBasisPoints / ids.length),
      })),
    );
  }
  return (
    <View style={{ gap: 8 }}>
      <Text style={styles.subtitle}>{title}</Text>
      <Label>
        Optional. Selected shares must total 100%. Zero-weight skills still
        require the selected rank.
      </Label>
      {items.map((item) => {
        const share = shares.find((s) => s.id === item.id);
        return (
          <View key={item.id} style={{ gap: 8 }}>
            <Button
              title={`${share ? "Remove" : "Add"} ${item.name}`}
              secondary
              onPress={() => toggle(item.id)}
            />
            {share && (
              <Input
                accessibilityLabel={`${item.name} percentage`}
                keyboardType="decimal-pad"
                value={
                  inputs[item.id] ??
                  String(share.basisPoints / basisPointsPerPercent)
                }
                onChangeText={(value) => {
                  setInputs({ ...inputs, [item.id]: value });
                  const valid = /^\d{1,3}(\.\d{0,2})?$/.test(value);
                  onChange(
                    shares.map((s) =>
                      s.id === item.id
                        ? {
                            ...s,
                            basisPoints: valid
                              ? Math.round(
                                  Number(value) * basisPointsPerPercent,
                                )
                              : -1,
                          }
                        : s,
                    ),
                  );
                }}
                style={styles.input}
              />
            )}
          </View>
        );
      })}
      <Label>
        Total:{" "}
        {shares.reduce((total, s) => total + s.basisPoints, 0) /
          basisPointsPerPercent}
        %
      </Label>
    </View>
  );
}
export function QuestEditor({
  initial,
  onClose,
  closeLabel = "Back to available quests",
}: {
  initial?: Definition;
  onClose(): void;
  closeLabel?: string;
}) {
  const { state, catalog, command, busy, pending } = useSession();
  const [draft, setDraft] = useState<Quest>(
    () =>
      initial?.quest ?? {
        id: Crypto.randomUUID(),
        title: "",
        criterion: "",
        description: "",
        categoryId: "c01",
        rank: "F",
        effort: "Small",
        attributes: [],
        skills: [],
        baseXp: 10,
        isCustom: true,
      },
  );
  const [revision, setRevision] = useState(initial?.revision ?? 0);
  if (!state || !catalog) return null;
  const current = state.definitions.find((d) => d.quest.id === draft.id);
  const quest = { ...draft, baseXp: baseXp(draft.rank, draft.effort) };
  const validPool = (shares: Share[]) =>
    !shares.length ||
    (shares.every(
      (s) => s.basisPoints >= 0 && s.basisPoints <= fullPoolBasisPoints,
    ) &&
      shares.reduce((n, s) => n + s.basisPoints, 0) === fullPoolBasisPoints);
  const valid =
    draft.title.trim().length > 0 &&
    draft.criterion.trim().length > 0 &&
    validPool(draft.attributes) &&
    validPool(draft.skills) &&
    eligible(quest, state);
  return (
    <View style={{ gap: 16 }}>
      <Text style={styles.subtitle}>
        {initial ? "Edit custom quest" : "Create your own quest"}
      </Text>
      {current && (
        <Label>
          {current.pendingSave ? "Recorded revision" : "Saved revision"}{" "}
          {current.revision}
          {current.pendingSave ? ", awaiting confirmation" : ""}. Completed
          history keeps its original terms. Active occurrences use your saved
          changes.
        </Label>
      )}
      {current && current.revision !== revision && (
        <Button
          title="Load saved revision"
          onPress={() => {
            setDraft(current.quest);
            setRevision(current.revision);
          }}
          secondary
        />
      )}
      <Label>
        Title (required, up to {questLimits.titleLength} characters)
      </Label>
      <Input
        accessibilityLabel="Quest title"
        value={draft.title}
        maxLength={questLimits.titleLength}
        onChangeText={(title) => setDraft({ ...draft, title })}
        style={styles.input}
      />
      <Label>What achieved outcome means done (required)</Label>
      <Input
        accessibilityLabel="Completion criterion"
        value={draft.criterion}
        maxLength={questLimits.criterionLength}
        multiline
        onChangeText={(criterion) => setDraft({ ...draft, criterion })}
        style={styles.input}
      />
      <Label>Description (optional)</Label>
      <Input
        accessibilityLabel="Quest description"
        value={draft.description ?? ""}
        maxLength={questLimits.descriptionLength}
        multiline
        onChangeText={(description) => setDraft({ ...draft, description })}
        style={styles.input}
      />
      <Label>Primary category</Label>
      {catalog.categories.map((c) => (
        <Button
          key={c.id}
          title={c.name}
          secondary={draft.categoryId !== c.id}
          onPress={() => setDraft({ ...draft, categoryId: c.id })}
        />
      ))}
      <Label>
        Effort describes the amount of work, not minutes. Large goals need a
        separately achieved outcome.
      </Label>
      {["Small", "Medium", "Large"].map((effort) => (
        <Button
          key={effort}
          title={effort}
          secondary={draft.effort !== effort}
          onPress={() => setDraft({ ...draft, effort })}
        />
      ))}
      <Allocations
        title="Attribute allocation"
        items={catalog.attributes}
        shares={draft.attributes}
        onChange={(attributes) => setDraft({ ...draft, attributes })}
      />
      <Allocations
        title="Skill allocation"
        items={[
          ...catalog.skills,
          ...(state.profile.customSkills ?? []).filter((s) => !s.archived),
        ]}
        shares={draft.skills}
        onChange={(skills) => setDraft({ ...draft, skills })}
      />
      <Label>
        Rank: every selected skill must qualify. With no skills, your category
        level decides access.
      </Label>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {ranks.map((rank) => (
          <Button
            key={rank}
            title={`Rank ${rank}`}
            disabled={!eligible({ ...quest, rank }, state)}
            secondary={draft.rank !== rank}
            onPress={() => setDraft({ ...draft, rank })}
          />
        ))}
      </View>
      <Label>
        {quest.baseXp} overall XP and {quest.baseXp} primary category XP. Each
        selected attribute/skill pool splits {quest.baseXp} XP.
      </Label>
      {!eligible(quest, state) && (
        <Label>
          This rank requires more experience in every selected skill, or the
          category if none. Choose an eligible rank.
        </Label>
      )}
      <Label>
        Optional penalty, off by default. A future acceptance requires separate
        confirmation of the exact category loss.
      </Label>
      {[0, 10, 25, 50].map((percent) => (
        <Button
          key={percent}
          title={percent ? `Penalty ${percent}%` : "No penalty"}
          secondary={(draft.penaltyPercent ?? 0) !== percent}
          onPress={() => setDraft({ ...draft, penaltyPercent: percent })}
        />
      ))}
      {state.occurrences
        .filter(
          (o) =>
            o.occurrence.quest.id === draft.id &&
            o.occurrence.lifecycle?.lockedLoss != null &&
            (o.status === "Active" || o.status === "Frozen"),
        )
        .map((o) => (
          <Label key={o.occurrence.id}>
            Existing accepted loss remains {o.occurrence.lifecycle!.lockedLoss}{" "}
            category XP; revised reward is {quest.baseXp} XP. Its category and
            deadline stay locked. Future penalty auto-acceptance will stop until
            renewed.
          </Label>
        ))}
      <Button
        title="Save custom quest"
        disabled={
          busy ||
          pending ||
          !valid ||
          (current !== undefined && current.revision !== revision)
        }
        onPress={() =>
          void command({
            action: questActions.saveDefinition,
            definition: quest,
            expectedRevision: revision,
          })
        }
      />
      <Button title={closeLabel} secondary onPress={onClose} />
    </View>
  );
}
