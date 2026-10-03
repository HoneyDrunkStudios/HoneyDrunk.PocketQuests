import { Redirect, Tabs } from "expo-router";
import { useSession } from "../../session";
import { colors } from "../../ui";
export default function Layout() {
  const { signedIn } = useSession();
  if (!signedIn) return <Redirect href="/" />;
  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.green,
        tabBarInactiveTintColor: colors.muted,
        tabBarStyle: { backgroundColor: colors.paper },
        headerStyle: { backgroundColor: colors.paper },
        headerTintColor: colors.ink,
        tabBarIcon: () => null,
        tabBarLabelStyle: { fontSize: 12 },
        tabBarItemStyle: { minHeight: 48 },
      }}
    >
      <Tabs.Screen name="index" options={{ title: "Today" }} />
      <Tabs.Screen name="board" options={{ title: "Quest Board" }} />
      <Tabs.Screen name="calendar" options={{ title: "Calendar" }} />
      <Tabs.Screen name="profile" options={{ title: "Progress" }} />
    </Tabs>
  );
}
