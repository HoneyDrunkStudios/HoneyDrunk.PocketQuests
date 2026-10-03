import { useEffect, useState } from "react";
import { AccessibilityInfo, AppState, Text, View } from "react-native";
import { Button, Input, Label, styles } from "./ui";
import { pauseFocus, remainingFocus, type FocusClock } from "./focus-clock";

/** Optional foreground aid. It has no quest-command or notification capability. */
export function FocusTimer() {
  const [minutes, setMinutes] = useState("10");
  const [clock, setClock] = useState<FocusClock | null>(null);
  const [now, setNow] = useState(Date.now);
  const duration = Number(minutes);
  const valid = /^\d{1,3}$/.test(minutes) && duration >= 1 && duration <= 180;
  const remaining = clock ? remainingFocus(clock, now) : 0;
  const done = clock !== null && remaining === 0;
  useEffect(() => {
    if (clock?.startedAt === null || !clock) return;
    const tick = setInterval(() => {
      const time = Date.now();
      setNow(time);
      if (remainingFocus(clock, time) === 0)
        setClock({ remainingMs: 0, startedAt: null });
    }, 250);
    const subscription = AppState.addEventListener("change", (status) => {
      if (status !== "active") {
        const time = Date.now();
        setClock((current) => (current ? pauseFocus(current, time) : current));
        setNow(time);
      }
    });
    return () => {
      clearInterval(tick);
      subscription.remove();
    };
  }, [clock]);
  useEffect(() => {
    if (done) {
      AccessibilityInfo.announceForAccessibility(
        "Focus time finished. Complete the quest when its criterion is met.",
      );
    }
  }, [done]);
  return (
    <View style={{ gap: 12 }}>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Do Now · optional focus time
      </Text>
      <Label>
        Use this when a focused session helps. It pauses when the app leaves the
        foreground. Complete the quest yourself when its criterion is met.
      </Label>
      {!clock ? (
        <>
          <Input
            accessibilityLabel="Focus minutes, 1 to 180"
            keyboardType="number-pad"
            value={minutes}
            onChangeText={setMinutes}
          />
          <Button
            title="Start focus timer"
            disabled={!valid}
            onPress={() => {
              const time = Date.now();
              setNow(time);
              setClock({ remainingMs: duration * 60_000, startedAt: time });
            }}
          />
        </>
      ) : (
        <>
          <Text
            style={styles.title}
            accessibilityLabel={
              done
                ? "Focus time finished"
                : `${Math.ceil(remaining / 1000)} seconds of focus time remaining`
            }
          >
            {done
              ? "Time well spent"
              : `${Math.floor(Math.ceil(remaining / 1000) / 60)}:${String(Math.ceil(remaining / 1000) % 60).padStart(2, "0")}`}
          </Text>
          {!done && (
            <Button
              secondary
              title={
                clock.startedAt === null
                  ? "Resume focus timer"
                  : "Pause focus timer"
              }
              onPress={() => {
                const time = Date.now();
                setNow(time);
                setClock(
                  clock.startedAt === null
                    ? { ...clock, startedAt: time }
                    : pauseFocus(clock, time),
                );
              }}
            />
          )}
          {done && (
            <Label>
              Ready when you are. No XP has been awarded by the timer.
            </Label>
          )}
          <Button
            title="Reset focus timer"
            secondary
            onPress={() => setClock(null)}
          />
        </>
      )}
    </View>
  );
}
