import { useState } from "react";
import { Text, View } from "react-native";
import { questActions } from "./commands/quest-actions";
import { QuestEditor } from "./quest-editor";
import { SeriesEditor } from "./series-editor";
import { useSession } from "./session";
import { Button, Label, styles } from "./ui";

/** Definition management remains reachable after an occurrence leaves Available. */
export function DefinitionManagement({ questId }: { questId: string }) {
  const { state, command, busy, pending } = useSession();
  const [mode, setMode] = useState<"edit" | "recurrence" | null>(null);
  const definition = state?.definitions.find((d) => d.quest.id === questId);
  if (!definition || definition.archived) return null;
  if (mode === "edit")
    return (
      <QuestEditor
        initial={definition}
        closeLabel="Back to quest details"
        onClose={() => setMode(null)}
      />
    );
  if (mode === "recurrence")
    return (
      <SeriesEditor
        quest={definition.quest}
        closeLabel="Back to quest details"
        onClose={() => setMode(null)}
      />
    );
  return (
    <View style={{ gap: 12 }}>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Manage custom quest
      </Text>
      <Label>
        Edits apply to eligible unfinished and future occurrences. Completed
        snapshots and accepted penalty terms stay preserved.
      </Label>
      <Button
        title={`Edit: ${definition.quest.title}`}
        secondary
        disabled={busy || pending}
        onPress={() => setMode("edit")}
      />
      <Button
        title={`Set recurrence: ${definition.quest.title}`}
        secondary
        disabled={busy || pending}
        onPress={() => setMode("recurrence")}
      />
      <Label>
        Archiving stops new use and recurrence. Unfinished recurring commitments
        are frozen; existing history remains.
      </Label>
      <Button
        title={`Archive definition: ${definition.quest.title}`}
        secondary
        disabled={busy || pending}
        onPress={() =>
          void command({
            action: questActions.archiveDefinition,
            questId,
            expectedRevision: definition.revision,
          })
        }
      />
    </View>
  );
}
