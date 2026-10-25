import { View } from "react-native";
import { Label, Progress, styles } from "../../shared/ui";
import { levelProgress, type ProgressionTrack } from "./level-progress";

export function LevelProgress({
  name,
  track,
  xp,
  level,
  compact = false,
}: Readonly<{
  name: string;
  track: ProgressionTrack;
  xp: number;
  level: number;
  compact?: boolean;
}>) {
  const progress = levelProgress(track, xp, level);
  if (!progress) return <Label>Level progress unavailable.</Label>;
  const progressName = track === "overall" ? name : `${name} ${track}`;
  const valueText = `${progress.earned}/${progress.required} XP toward level ${progress.nextLevel}`;
  return (
    <View style={{ gap: 4 }}>
      {compact ? (
        <View
          accessible
          accessibilityRole="progressbar"
          accessibilityLabel={`${progressName} level progress`}
          accessibilityValue={{
            min: 0,
            max: 100,
            now: Math.round((progress.earned / progress.required) * 100),
            text: valueText,
          }}
          aria-valuetext={valueText}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={Math.round(
            (progress.earned / progress.required) * 100,
          )}
          style={[styles.progressTrack, { height: 16 }]}
        >
          <View
            style={[
              styles.progressFill,
              { width: `${(progress.earned / progress.required) * 100}%` },
            ]}
          />
        </View>
      ) : (
        <Progress
          label={`${progressName} level progress`}
          value={progress.earned}
          max={progress.required}
          valueText={valueText}
        />
      )}
      <Label style={compact ? styles.muted : undefined}>
        {progress.remaining} XP to next level
      </Label>
    </View>
  );
}
