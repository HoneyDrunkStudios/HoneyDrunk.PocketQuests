import React from "react";
import { Platform, Text, View } from "react-native";
import type { ViewProps } from "react-native";
import { useTheme } from "./theme";
import { createStyles } from "./styles";

export interface BadgeProps extends Pick<
  ViewProps,
  "testID" | "accessibilityLabel"
> {
  /** Required text makes status understandable without color. */
  label: string;
}

/** Noninteractive, text-sized status. App policy determines its label. */
export function Badge({
  label,
  accessibilityLabel = label,
  testID,
}: BadgeProps) {
  const styles = createStyles(useTheme());
  return (
    <View testID={testID} style={styles.badge}>
      <Text
        selectable
        accessibilityLabel={accessibilityLabel}
        style={styles.muted}
      >
        {label}
      </Text>
    </View>
  );
}

export interface ProgressProps extends Pick<ViewProps, "testID"> {
  /** App-localized accessible name, also shown above the track. */
  label: string;
  value: number;
  min?: number;
  max?: number;
  /** App-localized value description. Native readers otherwise receive a rounded percentage. */
  valueText?: string;
}

/** Determinate, nonanimated progress. Values clamp to a finite, increasing range. */
export function Progress({
  label,
  value,
  min = 0,
  max = 100,
  valueText,
  testID,
}: ProgressProps) {
  if (![min, max, value].every(Number.isFinite) || max <= min) {
    throw new RangeError(
      "Progress requires finite values and max greater than min.",
    );
  }
  const theme = useTheme();
  const styles = createStyles(theme);
  const now = Math.min(max, Math.max(min, value));
  const span = max - min;
  // Subtract first to preserve narrow ranges; halve only when the span overflows.
  const fraction = Number.isFinite(span)
    ? (now - min) / span
    : (now / 2 - min / 2) / (max / 2 - min / 2);
  // RN's Android delegate reads integer values. Normalize native ranges so
  // fractional or large app values cannot collapse or overflow at that boundary.
  const accessibilityValue =
    Platform.OS === "web"
      ? { min, max, now, text: valueText }
      : { min: 0, max: 100, now: Math.round(fraction * 100), text: valueText };
  return (
    <View
      testID={testID}
      accessible
      accessibilityRole="progressbar"
      accessibilityLabel={label}
      aria-valuemin={accessibilityValue.min}
      aria-valuemax={accessibilityValue.max}
      aria-valuenow={accessibilityValue.now}
      aria-valuetext={valueText}
      accessibilityValue={accessibilityValue}
      style={{ gap: theme.spacing.gap }}
    >
      <Text style={styles.text}>
        {label}
        {valueText ? `: ${valueText}` : ""}
      </Text>
      <View
        aria-hidden
        importantForAccessibility="no-hide-descendants"
        style={styles.progressTrack}
      >
        <View style={[styles.progressFill, { width: `${fraction * 100}%` }]} />
      </View>
    </View>
  );
}
