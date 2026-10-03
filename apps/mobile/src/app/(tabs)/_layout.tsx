import { Redirect, Tabs } from "expo-router";
import { useSession } from "../../session/session";
import { colors } from "../../shared/ui";
export default function Layout() {
  const { signedIn } = useSession();
  if (!signedIn) return <Redirect href="/" />;
  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.green,
        tabBarInactiveTintColor: colors.muted,
        tabBarStyle: { backgroundColor: colors.paper, minHeight: 64 },
        headerStyle: { backgroundColor: colors.paper },
        headerTintColor: colors.ink,
        tabBarIcon: () => null,
        tabBarIconStyle: { display: "none" },
        tabBarLabelPosition: "beside-icon",
        tabBarLabelStyle: { fontSize: 12 },
        tabBarItemStyle: { minHeight: 48 },
      }}
    >
      <Tabs.Screen name="index" options={{ title: "Home" }} />
      <Tabs.Screen name="board" options={{ title: "Quests" }} />
      <Tabs.Screen name="calendar" options={{ title: "Calendar" }} />
      <Tabs.Screen name="profile" options={{ title: "Progress" }} />
    </Tabs>
  );
}
