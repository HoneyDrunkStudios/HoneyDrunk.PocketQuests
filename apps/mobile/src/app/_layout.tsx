import { Stack } from "expo-router";
import { SessionProvider } from "../session/session";
import { ThemeProvider } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "../shared/theme";
import { colors } from "../shared/ui";
import { CompletionOverlay } from "../features/progression/completion-overlay";
export default function Layout() {
  return (
    <ThemeProvider theme={pocketQuestsTheme}>
      <SessionProvider>
        <Stack
          screenOptions={{
            headerStyle: { backgroundColor: colors.paper },
            headerTintColor: colors.ink,
          }}
        >
          <Stack.Screen name="index" options={{ title: "Pocket Quests" }} />
          <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
          <Stack.Screen name="quest/[id]" options={{ title: "Quest" }} />
        </Stack>
        <CompletionOverlay />
      </SessionProvider>
    </ThemeProvider>
  );
}
