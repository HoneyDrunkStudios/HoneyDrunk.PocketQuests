import React, { createContext, useContext } from "react";
import {
  Pressable,
  Text,
  TextInput,
  View,
  ActivityIndicator,
} from "react-native";
import type { TextInputProps, ViewProps } from "react-native";
import type { Theme } from "@honeydrunk/ui-tokens";
import { createStyles, buttonAppearance } from "./styles";
export { createStyles, buttonAppearance } from "./styles";
const ThemeContext = createContext<Theme | null>(null);
export function ThemeProvider({
  theme,
  children,
}: {
  theme: Theme;
  children: React.ReactNode;
}) {
  return (
    <ThemeContext.Provider value={theme}>{children}</ThemeContext.Provider>
  );
}
export function useTheme() {
  const theme = useContext(ThemeContext);
  if (!theme)
    throw new Error("HoneyDrunk UI requires an app-supplied ThemeProvider.");
  return theme;
}
export function Label({ children }: { children: React.ReactNode }) {
  const theme = useTheme();
  return (
    <Text selectable style={createStyles(theme).text}>
      {children}
    </Text>
  );
}
export function Button({
  title,
  onPress,
  disabled = false,
  secondary = false,
}: {
  title: string;
  onPress(): void;
  disabled?: boolean;
  secondary?: boolean;
}) {
  const theme = useTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => {
        const a = buttonAppearance(theme, secondary, disabled, pressed);
        return {
          minHeight: theme.controls.minHeight,
          paddingVertical: theme.spacing.control,
          paddingHorizontal: 18,
          borderRadius: theme.shape.controlRadius,
          justifyContent: "center",
          alignItems: "center",
          backgroundColor: a.backgroundColor,
          borderWidth: theme.shape.borderWidth,
          borderColor: theme.colors.primary,
          opacity: a.opacity,
        };
      }}
    >
      <Text
        style={{
          fontSize: theme.typography.bodySize,
          fontWeight: "600",
          fontFamily: theme.typography.fontFamily,
          color: secondary ? theme.colors.primary : theme.colors.onPrimary,
        }}
      >
        {title}
      </Text>
    </Pressable>
  );
}
export function Card({ style, ...props }: ViewProps) {
  const theme = useTheme();
  return <View {...props} style={[createStyles(theme).card, style]} />;
}
export function Input({
  style,
  placeholderTextColor,
  ...props
}: TextInputProps) {
  const theme = useTheme();
  return (
    <TextInput
      {...props}
      placeholderTextColor={placeholderTextColor ?? theme.colors.muted}
      style={[createStyles(theme).input, style]}
    />
  );
}
export function Loading({ label }: { label: string }) {
  const theme = useTheme();
  return (
    <ActivityIndicator
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
