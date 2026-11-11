import { useEffect, useState } from "react";
import { Animated, Text, View } from "react-native";
import { useAppTheme } from "../../shared/ui";
import {
  levelProgress,
  rewardProgressStart,
  type ProgressionTrack,
} from "./level-progress";

export function RewardProgress({
  name,
  xp,
  track,
  totalXp,
  level,
  reduceMotion,
}: Readonly<{
  name: string;
  xp: number;
  track: string;
  totalXp?: number;
  level?: number;
  reduceMotion: boolean | null;
}>) {
  const { styles, colors } = useAppTheme();
  const current =
    totalXp !== undefined && level !== undefined
      ? levelProgress(track.toLowerCase() as ProgressionTrack, totalXp, level)
      : null;
  const to = current ? current.earned / current.required : 1;
  const [value] = useState(() => new Animated.Value(to));
  const previous =
    totalXp === undefined
      ? null
      : rewardProgressStart(
          track.toLowerCase() as ProgressionTrack,
          totalXp,
          xp,
        );
  const from = previous ? previous.earned / previous.required : 0;
  const crossesLevel = !!current && xp > current.earned;
  useEffect(() => {
    value.stopAnimation();
    if (reduceMotion === null || reduceMotion) {
      value.setValue(to);
      return;
    }
    value.setValue(from);
    const animation = crossesLevel
      ? Animated.sequence([
          Animated.timing(value, {
            toValue: 1,
            duration: 650,
            useNativeDriver: false,
          }),
          Animated.timing(value, {
            toValue: 0,
            duration: 0,
            useNativeDriver: false,
          }),
          Animated.timing(value, {
            toValue: to,
            duration: 500,
            useNativeDriver: false,
          }),
        ])
      : Animated.timing(value, {
          toValue: to,
          duration: 1000,
          delay: 120,
          useNativeDriver: false,
        });
    animation.start();
    return () => animation.stop();
  }, [value, from, to, reduceMotion, crossesLevel]);
  return (
    <View style={{ gap: 8 }}>
      <View
        style={{
          flexDirection: "row",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 12,
        }}
      >
        <Text style={[styles.text, { flexShrink: 1 }]}>{name}</Text>
        <Text style={[styles.text, { color: colors.gold, fontWeight: "700" }]}>
          +{xp} XP
        </Text>
      </View>
      <View
        accessible
        accessibilityRole="progressbar"
        accessibilityLabel={`${name} reward progress`}
        accessibilityValue={{
          min: 0,
          max: 100,
          now: Math.round(to * 100),
          text: current
            ? `${current.earned} of ${current.required} XP toward level ${current.nextLevel}`
            : `${xp} XP earned`,
        }}
        style={[styles.progressTrack, { height: 12 }]}
      >
        <Animated.View
          style={[
            styles.progressFill,
            {
              width: value.interpolate({
                inputRange: [0, 1],
                outputRange: ["0%", "100%"],
              }),
            },
          ]}
        />
      </View>
      {current && (
        <Text style={[styles.muted, { fontSize: 12 }]}>
          {current.remaining} XP to level {current.nextLevel}
        </Text>
      )}
    </View>
  );
}
