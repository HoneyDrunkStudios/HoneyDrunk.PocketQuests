import { Stack } from "expo-router";
import { useFonts } from "expo-font";
import Ionicons from "@expo/vector-icons/Ionicons";
import { StatusBar } from "expo-status-bar";
import {
  SessionProvider,
  useSessionStatus,
  useAccountSnapshot,
} from "../session/session";
import { ThemeProvider } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "../shared/theme";
import { colors } from "../shared/ui";
import { CompletionOverlay } from "../features/progression/completion-overlay";
export { AppErrorBoundary as ErrorBoundary } from "../shared/error-boundary";
function SessionNavigator() {
  const { signedIn, restoring } = useSessionStatus();
  const { state } = useAccountSnapshot();
  const canEnterApp = signedIn && state?.profile.onboardingComplete === true;
  // Saved credentials can arrive before their profile. Preserve the requested route.
  if (restoring) return null;
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: colors.paper },
        headerTintColor: colors.ink,
      }}
    >
      <Stack.Protected
        guard={!signedIn}
        redirectTo={canEnterApp ? "/(tabs)" : "/onboarding"}
      >
        <Stack.Screen name="index" options={{ headerShown: false }} />
        <Stack.Screen name="callback" options={{ headerShown: false }} />
      </Stack.Protected>
      <Stack.Protected
        guard={canEnterApp}
        redirectTo={signedIn ? "/onboarding" : "/"}
      >
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="quest/[id]" options={{ title: "Quest" }} />
        <Stack.Screen
          name="quest/available/[id]"
          options={{ title: "Quest details" }}
        />
      </Stack.Protected>
      <Stack.Protected
        guard={signedIn && !canEnterApp}
        redirectTo={signedIn ? "/(tabs)/board" : "/"}
      >
        <Stack.Screen
          name="onboarding"
          options={{ headerShown: false, gestureEnabled: false }}
        />
      </Stack.Protected>
    </Stack>
  );
}
export default function Layout() {
  const [fontsLoaded, fontError] = useFonts({
    JournalSerifBold: require("../../assets/fonts/JournalSerifBold.ttf"),
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
