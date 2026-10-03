import React, { useId, useState, useSyncExternalStore } from "react";
import {
  Pressable,
  Text,
  TextInput,
  View,
  ActivityIndicator,
  Platform,
  AccessibilityInfo,
} from "react-native";
import type {
  PressableProps,
  TextInputProps,
  TextProps,
  ViewProps,
} from "react-native";
import { createStyles, buttonAppearance, focusAppearance } from "./styles";
import { useTheme } from "./theme";
import { createReducedMotionStore } from "./reducedMotion";

const reducedMotion = createReducedMotionStore(AccessibilityInfo);
export { ThemeProvider, useTheme } from "./theme";
export { createStyles, buttonAppearance, focusAppearance } from "./styles";
export { Badge, Progress } from "./status";
export type { BadgeProps, ProgressProps } from "./status";
export function Label({ style, ...props }: TextProps) {
  const theme = useTheme();
  return (
    <Text selectable {...props} style={[createStyles(theme).text, style]} />
  );
}
export interface ButtonProps extends Pick<
  PressableProps,
  "accessibilityLabel" | "accessibilityHint" | "testID" | "onFocus" | "onBlur"
> {
  title: string;
  onPress(): void;
  disabled?: boolean;
  secondary?: boolean;
  /** Busy actions remain named, expose busy state and cannot be activated. */
  loading?: boolean;
}

export function Button({
  title,
  onPress,
  disabled = false,
  secondary = false,
  loading = false,
  accessibilityLabel = title,
  onFocus,
  onBlur,
  ...props
}: ButtonProps) {
  const theme = useTheme();
  const styles = createStyles(theme);
  const [focused, setFocused] = useState(false);
  const unavailable = disabled || loading;
  return (
    <Pressable
      {...props}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityState={{ disabled: unavailable, busy: loading }}
      aria-disabled={unavailable}
      aria-busy={loading}
      disabled={unavailable}
      onPress={() => {
        if (!unavailable) onPress();
      }}
      onFocus={(event) => {
        setFocused(true);
        onFocus?.(event);
      }}
      onBlur={(event) => {
        setFocused(false);
        onBlur?.(event);
      }}
      style={({ pressed }) => {
        const appearance = buttonAppearance(
          theme,
          secondary,
          unavailable,
          pressed,
        );
        return [
          styles.button,
          {
            backgroundColor: appearance.backgroundColor,
            opacity: appearance.opacity,
          },
          focusAppearance(theme, focused && !unavailable),
        ];
      }}
    >
      <Text
        style={[
          styles.buttonText,
          {
            color: secondary ? theme.colors.primary : theme.colors.onPrimary,
          },
        ]}
      >
        {title}
      </Text>
    </Pressable>
  );
}

export interface InputProps extends TextInputProps {
  /** App-localized, visible error text; also appended to the native accessibility hint. */
  error?: string;
}
export function Card({ style, ...props }: ViewProps) {
  const theme = useTheme();
  return <View {...props} style={[createStyles(theme).card, style]} />;
}
export function Input({
  style,
  placeholderTextColor,
  error,
  accessibilityHint,
  onFocus,
  onBlur,
  ...props
}: InputProps) {
  const theme = useTheme();
  const styles = createStyles(theme);
  const [focused, setFocused] = useState(false);
  const errorID = useId();
  // React Native Web supports these DOM semantics; native readers receive the error hint.
  const webErrorProps =
    Platform.OS === "web" && error
      ? { "aria-invalid": true, "aria-describedby": errorID }
      : {};
  const input = (
    <TextInput
      {...props}
      {...webErrorProps}
      accessibilityHint={
        [accessibilityHint, error].filter(Boolean).join(". ") || undefined
      }
      placeholderTextColor={placeholderTextColor ?? theme.colors.muted}
      onFocus={(event) => {
        setFocused(true);
        onFocus?.(event);
      }}
      onBlur={(event) => {
        setFocused(false);
        onBlur?.(event);
      }}
      style={[
        styles.input,
        error ? { borderColor: theme.colors.danger } : undefined,
        focusAppearance(theme, focused),
        style,
      ]}
    />
  );
  // A stable fragment preserves focus and uncontrolled values when errors appear or clear.
  return (
    <>
      {input}
      {error ? (
        <Text
          nativeID={errorID}
          accessibilityLiveRegion="polite"
          selectable
          style={[
            styles.muted,
            { color: theme.colors.danger, marginTop: theme.spacing.gap },
          ]}
        >
          {error}
        </Text>
      ) : null}
    </>
  );
}

export function Loading({ label }: { label: string }) {
  const theme = useTheme();
  const reduced = useSyncExternalStore(
    reducedMotion.subscribe,
    reducedMotion.getSnapshot,
    reducedMotion.getServerSnapshot,
  );
  if (reduced)
    return (
      <Text
        accessibilityRole="progressbar"
        accessibilityState={{ busy: true }}
        aria-busy
        accessibilityLabel={label}
        style={createStyles(theme).text}
      >
        {label}
      </Text>
    );
  return (
    <ActivityIndicator
      accessible
      accessibilityRole="progressbar"
      accessibilityState={{ busy: true }}
      aria-busy
      accessibilityLabel={label}
      color={theme.colors.primary}
    />
  );
}
export function Notice({ children }: { children: React.ReactNode }) {
  const theme = useTheme();
  return (
    <Card accessibilityLiveRegion="polite">
      <Text
        selectable
        style={[createStyles(theme).text, { color: theme.colors.danger }]}
      >
        {children}
      </Text>
    </Card>
  );
}
