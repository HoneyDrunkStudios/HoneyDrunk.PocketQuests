import { View } from "react-native";
import { Label, Progress } from "../../shared/ui";
import { levelProgress, type ProgressionTrack } from "./level-progress";

export function LevelProgress({
  name,
  track,
  xp,
  level,
}: Readonly<{
  name: string;
  track: ProgressionTrack;
  xp: number;
  level: number;
}>) {
  const progress = levelProgress(track, xp, level);
  if (!progress) return <Label>Level progress unavailable.</Label>;
  const progressName = track === "overall" ? name : `${name} ${track}`;
  const valueText = `${progress.earned}/${progress.required} XP toward level ${progress.nextLevel}`;
  return (
    <View style={{ gap: 4 }}>
      <Progress
        label={`${progressName} level progress`}
        value={progress.earned}
        max={progress.required}
        valueText={valueText}
      />
      <Label>{progress.remaining} XP to next level</Label>
    </View>
  );
}
