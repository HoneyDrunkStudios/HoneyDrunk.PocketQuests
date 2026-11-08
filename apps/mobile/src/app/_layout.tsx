import {
  AppearanceAutosaveProvider,
  AppearanceSaveNotice,
} from "../features/profile/appearance-autosave";
import { AppTour } from "../features/profile/app-tour";
import { Stack } from "expo-router";
import { useFonts } from "expo-font";
import Ionicons from "@expo/vector-icons/Ionicons";
import { StatusBar } from "expo-status-bar";
import {
  SessionProvider,
  useSessionStatus,
  useAccountSnapshot,
} from "../session/session";
import { DisplayModeProvider } from "../session/display-mode";

import { useAppTheme } from "../shared/ui";
import { CompletionOverlay } from "../features/progression/completion-overlay";
export { AppErrorBoundary as ErrorBoundary } from "../shared/error-boundary";
function SessionNavigator() {
  const { colors } = useAppTheme();
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
        headerBackButtonDisplayMode: "minimal",
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
        <Stack.Screen
          name="avatar"
          options={{
            title: "Create your traveler",
            headerTitleStyle: { fontFamily: "QuestSansBold", fontSize: 24 },
          }}
        />
        <Stack.Screen
          name="wardrobe"
          options={{
            title: "Your wardrobe",
            headerTitleStyle: { fontFamily: "QuestSansBold", fontSize: 24 },
          }}
        />
        <Stack.Screen name="rewards" options={{ title: "Rewards" }} />
        <Stack.Screen name="explore" options={{ title: "Explore" }} />
        <Stack.Screen name="settings" options={{ title: "Settings" }} />
        <Stack.Screen name="character-setup" options={{ headerShown: false }} />
        <Stack.Screen name="quest/[id]" options={{ title: "Quest" }} />
        <Stack.Screen
          name="quest/discover/[categoryId]"
          options={{ title: "Quest ideas" }}
        />
        <Stack.Screen
          name="quest/available/[id]"
          options={{ title: "Quest details" }}
        />
      </Stack.Protected>
      <Stack.Protected
        guard={signedIn && !canEnterApp}
        redirectTo={signedIn ? "/(tabs)" : "/"}
      >
        <Stack.Screen
          name="onboarding"
          options={{ headerShown: false, gestureEnabled: false }}
        />
      </Stack.Protected>
    </Stack>
  );
}
function JournalStatusBar() {
  const { isDark } = useAppTheme();
  return <StatusBar style={isDark ? "light" : "dark"} />;
}
export default function Layout() {
  const [fontsLoaded, fontError] = useFonts({
    JournalSerifBold: require("../../assets/fonts/JournalSerifBold.ttf"),
    QuestSans: require("../../assets/fonts/QuestSans.ttf"),
    QuestSansBold: require("../../assets/fonts/QuestSansBold.ttf"),
    ...Ionicons.font,
  });
  if (!fontsLoaded && !fontError) return null;
  return (
    <DisplayModeProvider>
      <JournalStatusBar />
      <SessionProvider>
        <AppearanceAutosaveProvider>
          <SessionNavigator />
          <CompletionOverlay />
          <AppTour />
          <AppearanceSaveNotice />
        </AppearanceAutosaveProvider>
      </SessionProvider>
    </DisplayModeProvider>
  );
}
