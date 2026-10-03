import { Stack } from "expo-router";
import { SessionProvider } from "../session";
import { ThemeProvider } from "@honeydrunk/ui-native";
import { pocketQuestsTheme } from "../theme";
import { colors } from "../ui";
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
        </Stack>
      </SessionProvider>
    </ThemeProvider>
  );
}
