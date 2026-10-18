import { Redirect, Tabs } from "expo-router";
import Ionicons from "@expo/vector-icons/Ionicons";
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
        tabBarStyle: {
          backgroundColor: colors.paper,
          minHeight: 72,
          borderTopColor: colors.line,
          paddingTop: 6,
        },
        headerStyle: { backgroundColor: colors.paper },
        headerTintColor: colors.ink,
        tabBarLabelPosition: "below-icon",
        tabBarLabelStyle: { fontSize: 12 },
        tabBarItemStyle: { minHeight: 48 },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: "Home",
          headerShown: false,
          tabBarIcon: ({ color, focused }) => (
            <Ionicons
              name={focused ? "home" : "home-outline"}
              color={color}
              size={26}
            />
          ),
        }}
      />
      <Tabs.Screen
        name="board"
        options={{
          title: "Quests",
          headerShown: false,
          tabBarIcon: ({ color }) => (
            <Ionicons name="compass-outline" color={color} size={28} />
          ),
        }}
      />
      <Tabs.Screen
        name="calendar"
        options={{
          title: "Calendar",
          tabBarIcon: ({ color }) => (
            <Ionicons name="calendar-outline" color={color} size={26} />
          ),
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: "Progress",
          tabBarIcon: ({ color }) => (
            <Ionicons name="stats-chart-outline" color={color} size={26} />
          ),
        }}
      />
    </Tabs>
  );
}
