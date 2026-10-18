import { Stack } from "expo-router";
import { useFonts } from "expo-font";
import Ionicons from "@expo/vector-icons/Ionicons";
import { StatusBar } from "expo-status-bar";
import { SessionProvider } from "../session/session";
import { ThemeProvider } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "../shared/theme";
import { colors } from "../shared/ui";
import { CompletionOverlay } from "../features/progression/completion-overlay";
export { AppErrorBoundary as ErrorBoundary } from "../shared/error-boundary";
export default function Layout() {
  const [fontsLoaded, fontError] = useFonts({
    JournalSerif: require("../../assets/fonts/LibreBaskerville.ttf"),
    ...Ionicons.font,
  });
  if (!fontsLoaded && !fontError) return null;
  return (
    <ThemeProvider theme={pocketQuestsTheme}>
      <StatusBar style="light" />
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
          <Stack.Screen
            name="quest/available/[id]"
            options={{ title: "Quest details" }}
          />
        </Stack>
        <CompletionOverlay />
      </SessionProvider>
    </ThemeProvider>
  );
}
