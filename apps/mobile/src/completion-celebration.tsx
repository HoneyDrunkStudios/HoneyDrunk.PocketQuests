import { useEffect, useState } from "react";
import { Text, View } from "react-native";
import { Button, Card, Label, createStyles } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "./theme";
import type { CompletionFeedback } from "./completion-feedback";

const styles = createStyles(pocketQuestsTheme);
export function CompletionCelebration({
  feedback,
  canUndo,
  onUndo,
  onDismiss,
}: {
  feedback: CompletionFeedback & { until: number };
  canUndo: boolean;
  onUndo(): void;
  onDismiss(): void;
}) {
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    // Static celebration respects reduced motion without hiding the reward.
    const timer = setTimeout(
      () => setNow(Date.now()),
      Math.max(0, feedback.until - Date.now()),
    );
    return () => clearTimeout(timer);
  }, [feedback]);
  return (
    <Card
      testID="quest-celebration"
      style={{
        borderWidth: 2,
        borderColor: pocketQuestsTheme.colors.accent,
        gap: 12,
      }}
    >
      <Text
        style={[
          styles.muted,
          { color: pocketQuestsTheme.colors.accent, fontWeight: "700" },
        ]}
      >
        ✦ A MOMENT TO CELEBRATE ✦
      </Text>
      <Text accessibilityRole="header" style={styles.title}>
        Quest complete!
      </Text>
      <Label>{feedback.title}</Label>
      <View style={{ gap: 8 }}>
        {feedback.rewards.map((r) => (
          <Label key={`${r.track}:${r.trackId}`}>
            {r.name} · +{r.xp} {r.track.toLowerCase()} XP
          </Label>
        ))}
      </View>
      {feedback.levelUps.length > 0 && (
        <View style={{ gap: 8 }}>
          <Text accessibilityRole="header" style={styles.subtitle}>
            Level up!
          </Text>
          {feedback.levelUps.map((l) => (
            <Label key={`${l.track}:${l.trackId}`}>
              {l.name} · Level {l.from} → {l.to}
            </Label>
          ))}
        </View>
      )}
      {feedback.rankUp && (
        <Label>Global rank promoted to {feedback.rankUp}</Label>
      )}
      {feedback.unlocks.map((name) => (
        <Label key={name}>Unlocked · {name}</Label>
      ))}
      {now < feedback.until && (
        <Button
          title="Undo recent completion"
          disabled={!canUndo}
          onPress={onUndo}
        />
      )}
      {now >= feedback.until && (
        <Label>Undo remains in quest details while eligible.</Label>
      )}
      <Button title="Keep adventuring" secondary onPress={onDismiss} />
    </Card>
  );
}
