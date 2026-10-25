import { Stack } from "expo-router";
import { useFonts } from "expo-font";
import Ionicons from "@expo/vector-icons/Ionicons";
import { StatusBar } from "expo-status-bar";
import { SessionProvider, useSessionStatus } from "../session/session";
import { ThemeProvider } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "../shared/theme";
import { colors } from "../shared/ui";
import { CompletionOverlay } from "../features/progression/completion-overlay";
export { AppErrorBoundary as ErrorBoundary } from "../shared/error-boundary";
function SessionNavigator() {
  const { signedIn, busy } = useSessionStatus();
  // Do not reject a cold-launch deep link while saved credentials are restoring.
  if (!signedIn && busy) return null;
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: colors.paper },
        headerTintColor: colors.ink,
      }}
    >
      <Stack.Protected guard={!signedIn} redirectTo="/(tabs)">
        <Stack.Screen name="index" options={{ title: "Pocket Quests" }} />
        <Stack.Screen name="callback" options={{ headerShown: false }} />
      </Stack.Protected>
      <Stack.Protected guard={signedIn} redirectTo="/">
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="quest/[id]" options={{ title: "Quest" }} />
        <Stack.Screen
          name="quest/available/[id]"
          options={{ title: "Quest details" }}
        />
      </Stack.Protected>
    </Stack>
  );
}
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
        <SessionNavigator />
        <CompletionOverlay />
      </SessionProvider>
    </ThemeProvider>
  );
}
